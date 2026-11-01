using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

internal enum CallAction { Microphone, Headphones, Leave, Camera }
internal sealed record CallCommand(CallAction Action, string AccessibleName, bool? Muted = null);
internal sealed record CallSnapshot(nint Window, int ProcessId, string App, string Title, IReadOnlyList<CallCommand> Commands);

// Inspect only candidate windows on hover. All cross-process accessibility calls
// run off the dispatcher; actions rediscover controls rather than caching UIA nodes.
internal static class CallIntegration
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(nint window, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible accessible);
    [DllImport("oleacc.dll")] private static extern int AccessibleChildren(Accessibility.IAccessible parent, int start, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] object[] children, out int obtained);
    internal static bool IsCandidate(Desktop.RunningWindow window) => AppName(window) is not null;
    private static string? AppName(Desktop.RunningWindow window)
    {
        string exe = Path.GetFileNameWithoutExtension(window.Executable ?? "").ToLowerInvariant();
        if (exe is "discord" or "discordcanary" or "discordptb") return "Discord";
        if (exe == "tretalk") return "Tretalk";
        if (exe is "ms-teams" or "teams") return "Microsoft Teams";
        if (exe is "zoom" or "zoomworkplace") return "Zoom";
        // The active tab/title can change without replacing the HWND. Origin
        // verification in InspectControls decides whether this really is Meet.
        if (exe is "chrome" or "msedge" or "firefox" or "brave" or "chromium" or "opera" or "vivaldi")
            return "Google Meet";
        return null;
    }
    internal static async Task<CallSnapshot?> FindAsync(IEnumerable<Desktop.RunningWindow> windows, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            return await Task.Run(() =>
            {
                foreach (var window in windows.Where(IsCandidate).Take(4))
                {
                    cancellation.ThrowIfCancellationRequested();
                    var snapshot = Inspect(window, cancellation, null);
                    if (snapshot is not null) return snapshot;
                }
                return null;
            }, cancellation);
        }
        finally { gate.Release(); }
    }
    internal static async Task<bool> ExecuteAsync(CallSnapshot snapshot, CallCommand command, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            return await Task.Run(() =>
            {
                if (!WindowThumbnail.IsWindow(snapshot.Window)) return false;
                var root = AutomationElement.FromHandle(snapshot.Window);
                if (root.Current.ProcessId != snapshot.ProcessId) return false;
                return InspectControls(root, snapshot.App, cancellation, command, out _) == true;
            }, cancellation);
        }
        catch (Exception e) when (Unavailable(e)) { return false; }
        finally { gate.Release(); }
    }
    private static CallSnapshot? Inspect(Desktop.RunningWindow window, CancellationToken cancellation, CallCommand? command)
    {
        try
        {
            var root = AutomationElement.FromHandle(window.Handle);
            string app = AppName(window)!;
            InspectControls(root, app, cancellation, command, out var commands);
            // The leave control is the evidence of a call; a mic button alone
            // can belong to a settings or pre-join screen.
            return commands.Any(c => c.Action == CallAction.Leave) && commands.Any(c => c.Action == CallAction.Microphone)
                ? new(window.Handle, root.Current.ProcessId, app, window.Title, commands) : null;
        }
        catch (Exception e) when (Unavailable(e)) { return null; }
    }
    private static bool InspectControls(AutomationElement root, string app, CancellationToken cancellation, CallCommand? execute, out List<CallCommand> commands)
    {
        commands = new();
        if (app == "Google Meet")
        {
            var document = MeetDocument(root, cancellation);
            if (document is null) return false;
            root = document;
        }
        var matches = new Dictionary<CallAction, List<(AutomationElement Element, string Name)>>();
        var walker = TreeWalker.ControlViewWalker;
        var pending = new Stack<AutomationElement>(); pending.Push(root);
        var clock = Stopwatch.StartNew(); int nodes = 0;
        while (pending.Count > 0 && nodes++ < 900 && clock.ElapsedMilliseconds < 600)
        {
            cancellation.ThrowIfCancellationRequested();
            var element = pending.Pop(); var current = element.Current;
            if (current.IsEnabled && current.ControlType == ControlType.Button
                && Classify(current.Name, app) is CallAction action
                && ExposedCallControl(element, app)
                && (element.TryGetCurrentPattern(InvokePattern.Pattern, out _) || element.TryGetCurrentPattern(TogglePattern.Pattern, out _)))
            {
                if (!matches.TryGetValue(action, out var list)) matches[action] = list = new();
                list.Add((element, current.Name));
            }
            // Bounded traversal avoids broad FindAll queries over browser documents.
            var children = new List<AutomationElement>();
            for (var child = walker.GetFirstChild(element); child is not null && children.Count < 150 && clock.ElapsedMilliseconds < 600; child = walker.GetNextSibling(child)) children.Add(child);
            for (int index = children.Count - 1; index >= 0; index--) pending.Push(children[index]);
        }
        foreach (var pair in matches.Where(pair => pair.Value.Count == 1))
        {
            string name = pair.Value[0].Name;
            commands.Add(new(pair.Key, name, MutedState(pair.Key, name, pair.Value[0].Element, app, (nint)root.Current.NativeWindowHandle)));
        }
        if (execute is null || !commands.Any(c => c.Action == CallAction.Leave) || !commands.Any(c => c.Action == CallAction.Microphone)) return false;
        var selected = commands.FirstOrDefault(c => c.Action == execute.Action && c.AccessibleName == execute.AccessibleName);
        if (selected is null) return false;
        cancellation.ThrowIfCancellationRequested();
        // Resolve the exact label uniquely in the verified document/window;
        // a bounded discovery must never choose an ambiguous control.
        var conditions = new List<Condition>
        {
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, selected.AccessibleName),
            new PropertyCondition(AutomationElement.IsEnabledProperty, true)
        };
        if (app != "Google Meet") conditions.Add(new PropertyCondition(AutomationElement.IsOffscreenProperty, false));
        var exact = root.FindAll(TreeScope.Descendants, new AndCondition(conditions.ToArray()));
        if (exact.Count != 1) return false;
        var target = exact[0];
        if (!ExposedCallControl(target, app)) return false;
        cancellation.ThrowIfCancellationRequested();
        if (target.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) { ((InvokePattern)invoke).Invoke(); return true; }
        if (target.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle)) { ((TogglePattern)toggle).Toggle(); return true; }
        return false;
    }
    private static AutomationElement? MeetDocument(AutomationElement window, CancellationToken cancellation)
    {
        var pending = new Stack<AutomationElement>(); pending.Push(window);
        var documents = new List<AutomationElement>(); var verified = new List<AutomationElement>();
        AutomationElement? documentWithoutAddress = null;
        bool addressBar = false; int nodes = 0; var clock = Stopwatch.StartNew();
        var walker = TreeWalker.ControlViewWalker;
        while (pending.Count > 0 && nodes++ < 350 && clock.ElapsedMilliseconds < 400)
        {
            cancellation.ThrowIfCancellationRequested();
            var element = pending.Pop(); var info = element.Current;
            if (info.ControlType == ControlType.Document)
            {
                documents.Add(element);
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var documentValue))
                {
                    string address = ((ValuePattern)documentValue).Current.Value;
                    if (MeetRoomAddress(address)) verified.Add(element);
                    else if (string.IsNullOrWhiteSpace(address)) documentWithoutAddress = element;
                }
                else documentWithoutAddress = element;
                continue; // Never accept a web page's own text fields as browser origin.
            }
            if (info.ControlType == ControlType.Edit && element.TryGetCurrentPattern(ValuePattern.Pattern, out var value)
                && MeetRoomAddress(((ValuePattern)value).Current.Value)) addressBar = true;
            var children = new List<AutomationElement>();
            for (var child = walker.GetFirstChild(element); child is not null && children.Count < 100 && clock.ElapsedMilliseconds < 400; child = walker.GetNextSibling(child)) children.Add(child);
            for (int i = children.Count - 1; i >= 0; i--) pending.Push(children[i]);
        }
        if (verified.Count == 1) return verified[0];
        // The native address bar is a fallback only for a unique exposed document.
        return verified.Count == 0 && addressBar && documents.Count == 1 ? documentWithoutAddress : null;
    }
    private static bool MeetRoomAddress(string address)
    {
        if (!Uri.TryCreate(address.Contains("://") ? address : "https://" + address, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || !uri.Host.Equals("meet.google.com", StringComparison.OrdinalIgnoreCase)) return false;
        var path = uri.AbsolutePath.Trim('/').Split('/');
        // A homepage (/ or /meet/) is not a room. A room URL is still only
        // a candidate: microphone + leave controls must prove the call below.
        if (path.Length == 2 && path[0].Equals("lookup", StringComparison.OrdinalIgnoreCase)) return !string.IsNullOrWhiteSpace(path[1]);
        if (path.Length != 1) return false;
        string code = path[0].Replace("-", "", StringComparison.Ordinal);
        return code.Length == 10 && code.All(char.IsAsciiLetter);
    }
    private static bool ExposedCallControl(AutomationElement element, string app)
    {
        var info = element.Current;
        if (app != "Google Meet") return !info.IsOffscreen;
        // Hidden, zero-size toolbar remnants must not keep a finished meeting
        // alive just because its room URL and old control labels still exist.
        var bounds = info.BoundingRectangle;
        return !bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0;
    }
    private static CallAction? Classify(string name, string app)
    {
        string value = Fold(name);
        // Strip shortcut suffixes and match actions, not a generic settings button.
        int suffix = value.IndexOf(" ("); if (suffix >= 0) value = value[..suffix];
        if (CameraDisabled(value).HasValue) return CallAction.Camera;
        if (app == "Tretalk")
        {
            if (value == "sair da voz") return CallAction.Leave;
            if (value is "microfone ligado" or "microfone desligado" or "microfone silenciado") return CallAction.Microphone;
            if (value is "som ligado" or "som desligado") return CallAction.Headphones;
            return null;
        }
        if (value is "disconnect" or "desconectar" or "leave call" or "leave meeting" or "sair da chamada" or "sair da reuniao" or "sair da ligacao" or "hang up" or "desligar") return CallAction.Leave;
        if (value is "mute" or "unmute" or "silenciar" or "reativar som" or "ativar som" or "ativar microfone" or "desativar microfone" or "turn off microphone" or "turn on microphone" or "mute microphone" or "unmute microphone" or "ativar o microfone" or "desativar o microfone") return CallAction.Microphone;
        if ((app is "Microsoft Teams" or "Zoom") && (value is "mute my audio" or "unmute my audio" or "desativar meu audio" or "ativar meu audio" or "ativar mudo" or "desativar mudo")) return CallAction.Microphone;
        if ((app is "Microsoft Teams" or "Zoom") && (value is "leave" or "sair")) return CallAction.Leave;
        if (app == "Discord" && (value is "deafen" or "undeafen" or "desativar audio" or "ativar audio" or "ensurdecer" or "desensurdecer")) return CallAction.Headphones;
        return null;
    }
    private static string Fold(string text)
    {
        var result = new StringBuilder();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) result.Append(char.ToLowerInvariant(c));
        return result.ToString().Trim();
    }
    private static bool? MutedState(CallAction action, string name, AutomationElement element, string app, nint window)
    {
        if (action == CallAction.Leave) return null;
        if (action == CallAction.Camera)
        {
            // "Turn on" describes the next action: the camera is currently off.
            // Camera toggles use the opposite meaning to Discord's mute toggle.
            string cameraLabel = Fold(name).Split('(')[0].Trim();
            if (CameraDisabled(cameraLabel) is bool disabled) return disabled;
            if (element.TryGetCurrentPattern(TogglePattern.Pattern, out var cameraToggle))
            {
                var state = ((TogglePattern)cameraToggle).Current.ToggleState;
                if (state != ToggleState.Indeterminate) return state == ToggleState.Off;
            }
            return null;
        }
        if (app == "Tretalk")
        {
            string label = Fold(name);
            if (label is "microfone desligado" or "microfone silenciado" or "som desligado") return true;
            if (label is "microfone ligado" or "som ligado") return false;
            return null;
        }
        // Discord can retain the label "Mute" even when aria-pressed is on.
        // Prefer the actual toggle/pressed state exposed by accessibility.
        if (app == "Discord" && element.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle))
        {
            var state = ((TogglePattern)toggle).Current.ToggleState;
            if (state != ToggleState.Indeterminate) return state == ToggleState.On;
        }
        if (app == "Discord" && ReadAccessibleMute(window, name) is bool accessibleState) return accessibleState;
        string value = Fold(name).Split('(')[0].Trim();
        if (value is "unmute" or "undeafen" or "ativar som" or "reativar som" or "ativar audio" or "desensurdecer"
            or "ativar microfone" or "ativar o microfone" or "turn on microphone" or "unmute microphone"
            or "unmute my audio" or "ativar meu audio" or "desativar mudo") return true;
        if (value is "mute" or "deafen" or "silenciar" or "desativar audio" or "ensurdecer"
            or "desativar microfone" or "desativar o microfone" or "turn off microphone" or "mute microphone"
            or "mute my audio" or "desativar meu audio" or "ativar mudo") return false;
        return null;
    }
    private static bool? CameraDisabled(string label) => label switch
    {
        "turn on camera" or "turn camera on" or "enable camera" or "turn on video" or "start video" or "start my video"
            or "ativar camera" or "ativar a camera" or "ligar camera" or "ligar a camera"
            or "iniciar video" or "iniciar meu video" or "ativar video" or "ativar o video" => true,
        "turn off camera" or "turn camera off" or "disable camera" or "turn off video" or "stop video" or "stop my video"
            or "desativar camera" or "desativar a camera" or "desligar camera" or "desligar a camera"
            or "parar video" or "parar meu video" or "desativar video" or "desativar o video" => false,
        _ => null
    };
    private static bool? ReadAccessibleMute(nint window, string name)
    {
        var iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
        var clock = Stopwatch.StartNew(); int visited = 0;
        bool? Visit(Accessibility.IAccessible accessible, int child, int depth)
        {
            if (depth > 18 || visited++ > 700 || clock.ElapsedMilliseconds > 200) return null;
            try
            {
                if (accessible.get_accName(child) == name)
                {
                    int state = Convert.ToInt32(accessible.get_accState(child));
                    if ((state & (8 | 16)) != 0) return true;
                    string command = Fold(accessible.get_accDefaultAction(child) ?? "");
                    if (command is "uncheck" or "desmarcar") return true;
                    if (command is "check" or "marcar") return false;
                }
                if (child != 0) return null;
                int count = Math.Clamp(accessible.accChildCount, 0, 128);
                if (count == 0) return null;
                var children = new object[count];
                if (AccessibleChildren(accessible, 0, count, children, out int obtained) < 0) return null;
                foreach (var item in children.Take(Math.Clamp(obtained, 0, count)))
                {
                    bool? result = item is Accessibility.IAccessible next ? Visit(next, 0, depth + 1) : item is int id && id > 0 ? Visit(accessible, id, depth + 1) : null;
                    if (result.HasValue) return result;
                }
            }
            catch (Exception e) when (Unavailable(e) || e is ArgumentException or NotImplementedException) { }
            return null;
        }
        try { return AccessibleObjectFromWindow(window, 0xFFFFFFFC, ref iid, out var root) >= 0 ? Visit(root, 0, 0) : null; }
        catch (Exception e) when (Unavailable(e)) { return null; }
    }
    private static bool Unavailable(Exception e) => e is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException or COMException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or NotSupportedException or TimeoutException or System.Security.SecurityException;
}
