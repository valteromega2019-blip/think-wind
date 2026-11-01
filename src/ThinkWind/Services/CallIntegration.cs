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

internal enum CallAction { Microphone, Headphones, Leave }
internal sealed record CallCommand(CallAction Action, string AccessibleName, bool? Muted = null);
internal sealed record CallSnapshot(nint Window, int ProcessId, string App, string Title, IReadOnlyList<CallCommand> Commands);

// Inspect only candidate windows on hover. All cross-process accessibility calls
// run off the dispatcher; actions rediscover controls rather than caching UIA nodes.
internal static class CallIntegration
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    internal static bool IsCandidate(Desktop.RunningWindow window) => AppName(window) is not null;
    private static string? AppName(Desktop.RunningWindow window)
    {
        string exe = Path.GetFileNameWithoutExtension(window.Executable ?? "").ToLowerInvariant();
        if (exe is "discord" or "discordcanary" or "discordptb") return "Discord";
        if (exe is "ms-teams" or "teams") return "Microsoft Teams";
        if (exe is "zoom" or "zoomworkplace") return "Zoom";
        // The active tab/title can change without replacing the HWND. Origin
        // verification in InspectControls decides whether this really is Meet.
        if (exe is "chrome" or "msedge" or "firefox" or "brave")
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
        var matches = new Dictionary<CallAction, List<(AutomationElement Element, string Name)>>();
        var walker = TreeWalker.ControlViewWalker;
        var pending = new Stack<AutomationElement>(); pending.Push(root);
        var clock = Stopwatch.StartNew(); int nodes = 0;
        bool meetOrigin = app != "Google Meet";
        while (pending.Count > 0 && nodes++ < 900 && clock.ElapsedMilliseconds < 600)
        {
            cancellation.ThrowIfCancellationRequested();
            var element = pending.Pop(); var current = element.Current;
            if (current.ControlType == ControlType.Edit && app == "Google Meet" && element.TryGetCurrentPattern(ValuePattern.Pattern, out var value))
            {
                string address = ((ValuePattern)value).Current.Value;
                if (Uri.TryCreate(address.Contains("://") ? address : "https://" + address, UriKind.Absolute, out var uri)
                    && uri.Host.Equals("meet.google.com", StringComparison.OrdinalIgnoreCase)) meetOrigin = true;
            }
            if (!current.IsOffscreen && current.IsEnabled && current.ControlType == ControlType.Button
                && Classify(current.Name, app) is CallAction action
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
        if (!meetOrigin) return false;
        foreach (var pair in matches.Where(pair => pair.Value.Count == 1))
        {
            string name = pair.Value[0].Name;
            commands.Add(new(pair.Key, name, MutedState(pair.Key, name)));
        }
        if (execute is null || !commands.Any(c => c.Action == CallAction.Leave) || !commands.Any(c => c.Action == CallAction.Microphone)) return false;
        var selected = commands.FirstOrDefault(c => c.Action == execute.Action && c.AccessibleName == execute.AccessibleName);
        if (selected is null) return false;
        cancellation.ThrowIfCancellationRequested();
        // Resolve the exact visible label uniquely over the full window before
        // executing; a bounded discovery must never choose an ambiguous control.
        var exact = root.FindAll(TreeScope.Descendants, new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty, selected.AccessibleName),
            new PropertyCondition(AutomationElement.IsEnabledProperty, true),
            new PropertyCondition(AutomationElement.IsOffscreenProperty, false)));
        if (exact.Count != 1) return false;
        var target = exact[0];
        cancellation.ThrowIfCancellationRequested();
        if (target.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) { ((InvokePattern)invoke).Invoke(); return true; }
        if (target.TryGetCurrentPattern(TogglePattern.Pattern, out var toggle)) { ((TogglePattern)toggle).Toggle(); return true; }
        return false;
    }
    private static CallAction? Classify(string name, string app)
    {
        string value = Fold(name);
        // Strip documented shortcut suffixes, retaining strict action labels.
        int suffix = value.IndexOf(" ("); if (suffix >= 0) value = value[..suffix];
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
    private static bool? MutedState(CallAction action, string name)
    {
        if (action == CallAction.Leave) return null;
        string value = Fold(name).Split('(')[0].Trim();
        if (value is "unmute" or "undeafen" or "ativar som" or "reativar som" or "ativar audio" or "desensurdecer"
            or "ativar microfone" or "ativar o microfone" or "turn on microphone" or "unmute microphone"
            or "unmute my audio" or "ativar meu audio" or "desativar mudo") return true;
        if (value is "mute" or "deafen" or "silenciar" or "desativar audio" or "ensurdecer"
            or "desativar microfone" or "desativar o microfone" or "turn off microphone" or "mute microphone"
            or "mute my audio" or "desativar meu audio" or "ativar mudo") return false;
        return null;
    }
    private static bool Unavailable(Exception e) => e is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException or COMException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or NotSupportedException or TimeoutException or System.Security.SecurityException;
}
