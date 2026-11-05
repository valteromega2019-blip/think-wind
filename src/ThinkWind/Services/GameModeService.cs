using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace OrbyLauncher.Services;
internal sealed record ForegroundGame(string Process, bool Fullscreen, bool Shell, nint Handle = 0, bool InstalledGame = false);
internal sealed class GameModeService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Monitor { internal uint Size; internal Rect Bounds, Work; internal uint Flags; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, System.Text.StringBuilder name, int capacity);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, System.Text.StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref Monitor info);
    private delegate void ForegroundEvent(nint hook, uint kind, nint window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, ForegroundEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    private readonly Dispatcher dispatcher;
    private readonly Action<bool> changed;
    private readonly Timer timer;
    private readonly ForegroundEvent foregroundEvent;
    private readonly nint foregroundHook;
    private int reading;
    private volatile bool disposed;
    private bool enabled = true;
    private bool hideFullscreen = true;
    private string[] include = Array.Empty<string>(), exclude = Array.Empty<string>();
    private (bool Enabled, bool HideFullscreen, string Include, string Exclude)? configuration;
    private nint presentationWindow;
    private uint presentationProcess;
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    internal bool Active { get; private set; }
    internal bool FullscreenPresentation { get; private set; }
    internal nint PresentationWindow => FullscreenPresentation ? presentationWindow : 0;
    internal bool RecognizedGame { get; private set; }
    private readonly OverlayGameSession overlaySession = new();
    internal bool OverlayGameActive => overlaySession.ProcessId != 0;
    internal int OverlayProcessId => overlaySession.ProcessId;
    internal event Action? ForegroundChanged;
    internal string CurrentProcess { get; private set; } = "";
    internal GameModeService(Dispatcher ui, Action<bool> onChange, bool initiallyActive = false)
    {
        dispatcher = ui; changed = onChange; Active = initiallyActive;
        foregroundEvent = (_, _, _, _, _, _, _) => Read();
        foregroundHook = SetWinEventHook(3, 3, 0, foregroundEvent, 0, 0, 2);
        timer = new Timer(_ => Read(), null, 1500, 1500);
    }
    internal static bool Probe(Preferences p) => Eligible(Snapshot(), p.GameModeEnabled, p.GameModeInclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), p.GameModeExclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), p.HideOverFullscreen);
    internal void Configure(Preferences p)
    {
        var next = (p.GameModeEnabled, p.HideOverFullscreen, p.GameModeInclude, p.GameModeExclude);
        if (configuration == next) return;
        configuration = next;
        enabled = p.GameModeEnabled; hideFullscreen = p.HideOverFullscreen;
        include = p.GameModeInclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        exclude = p.GameModeExclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        Read();
    }
    internal static bool Eligible(ForegroundGame game, bool enabled, string[] include, string[] exclude, bool hideFullscreen = true)
    {
        string Key(string value) => Path.GetFileNameWithoutExtension(value);
        // Browser exclusions apply to game identification, never to genuine
        // fullscreen video. This protection works even with Game Mode disabled.
        if (hideFullscreen && game.Fullscreen && !game.Shell) return true;
        // Companion desktop tools must not toggle shell visibility just by
        // gaining focus, even on existing profiles with broad protection on.
        if (game.Process.Equals("ThinkExplode", StringComparison.OrdinalIgnoreCase)
            || game.Process.Equals("ThinkMonitor", StringComparison.OrdinalIgnoreCase)) return false;
        if (!enabled || game.Shell || exclude.Any(value => Key(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase))) return false;
        return game.Fullscreen || game.InstalledGame || include.Any(value => Key(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase));
    }
    private void Read()
    {
        if (disposed || Interlocked.Exchange(ref reading, 1) != 0) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            bool posted = false;
            try
            {
                nint observedForeground = GetForegroundWindow();
                GetWindowThreadProcessId(observedForeground, out uint foregroundProcess);
                // Foreground is briefly null during activation/minimize. This is
                // not a switch to the desktop; wait for the next event/sample.
                if (observedForeground == 0 || foregroundProcess == 0) return;
                var game = Snapshot(observedForeground);
                // Opening our Start menu is not Alt+Tab away from a game/video.
                // Recheck its real HWND instead of restoring all launcher UI.
                if ((foregroundProcess == Environment.ProcessId || foregroundProcess == presentationProcess) && presentationWindow != 0
                    && IsWindowVisible(presentationWindow) && !IsIconic(presentationWindow)
                    && GetWindowThreadProcessId(presentationWindow, out uint owner) != 0 && owner == presentationProcess
                    && !(DwmGetWindowAttribute(presentationWindow, 14, out int cloaked, 4) >= 0 && cloaked != 0))
                {
                    game = Snapshot(presentationWindow);
                    // Class/path queries may temporarily fail during a game's
                    // window transitions. Preserve recognition for this live
                    // HWND/PID only, not when focus moves to another application.
                    if (RecognizedGame) game = game with { InstalledGame = true };
                }
                bool active = Eligible(game, enabled, include, exclude, hideFullscreen);
                if (disposed || dispatcher.HasShutdownStarted) return;
                dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                {
                    bool stale = false;
                    try
                    {
                        if (disposed) return;
                        stale = observedForeground != GetForegroundWindow(); if (stale) return;
                        CurrentProcess = game.Process;
                        bool recognized = Recognizes(game, include, exclude);
                        bool overlayChanged = overlaySession.Update(game, recognized);
                        bool recognizedChanged = RecognizedGame != recognized;
                        RecognizedGame = recognized;
                        bool presentation = active && game.Fullscreen && !game.InstalledGame
                            && !include.Any(value => Path.GetFileNameWithoutExtension(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase));
                        bool presentationChanged = FullscreenPresentation != presentation
                            || (presentation && presentationWindow != game.Handle);
                        presentationWindow = active ? game.Handle : 0;
                        if (active) GetWindowThreadProcessId(game.Handle, out presentationProcess);
                        else presentationProcess = 0;
                        FullscreenPresentation = presentation;
                        if (Active != active) { Active = active; changed(active); }
                        else if (presentationChanged || recognizedChanged || overlayChanged) changed(active);
                        if (!active) ForegroundChanged?.Invoke();
                    }
                    finally { Interlocked.Exchange(ref reading, 0); if (stale && !disposed) Read(); }
                }));
                posted = true;
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception) { }
            finally { if (!posted) Interlocked.Exchange(ref reading, 0); }
        });
    }
    internal static ForegroundGame Snapshot(nint selectedWindow = 0)
    {
        nint window = selectedWindow != 0 ? selectedWindow : GetForegroundWindow(); GetWindowThreadProcessId(window, out uint pid);
        if (window == 0 || pid == 0) return new("", false, true);
        using var process = Process.GetProcessById((int)pid); string name = process.ProcessName;
        bool shell = pid == Environment.ProcessId || name.Equals("explorer", StringComparison.OrdinalIgnoreCase) || name.Equals("dwm", StringComparison.OrdinalIgnoreCase) || name.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) || name.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase);
        var monitor = new Monitor { Size = (uint)Marshal.SizeOf<Monitor>() };
        bool full = (!IsZoomed(window) || (GetWindowLong(window, -16) & 0x00C00000) == 0)
            && GetWindowRect(window, out var rect) && GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)
            && rect.Left <= monitor.Bounds.Left + 2 && rect.Top <= monitor.Bounds.Top + 2 && rect.Right >= monitor.Bounds.Right - 2 && rect.Bottom >= monitor.Bounds.Bottom - 2;
        bool installed = false;
        if (!shell)
        {
            nint handle = OpenProcess(0x1000, false, pid);
            if (handle != 0) try
            {
                var path = new System.Text.StringBuilder(32768); uint length = (uint)path.Capacity;
                installed = QueryFullProcessImageName(handle, 0, path, ref length) && InstalledGameCatalog.Contains(path.ToString());
            }
            finally { CloseHandle(handle); }
            // Anti-cheat/elevated games can deny image-path access. Recognize
            // packaged game windows independently of fullscreen geometry.
            if (!installed)
            {
                var windowClass = new System.Text.StringBuilder(256);
                GetClassName(window, windowClass, windowClass.Capacity);
                installed = IsGameWindow(name, windowClass.ToString());
            }
        }
        return new(name, full, shell, window, installed);
    }
    internal static bool IsGameWindow(string process, string windowClass)
    {
        // Never classify generic Chromium/SDL windows or any fullscreen app
        // as a game: that would put the overlay over browsers and videos.
        bool shipping = process.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase)
            || process.EndsWith("-Win32-Shipping", StringComparison.OrdinalIgnoreCase);
        return (shipping && windowClass.Equals("UnrealWindow", StringComparison.Ordinal))
            || (windowClass.Equals("UnityWndClass", StringComparison.Ordinal)
                && !process.Equals("Unity", StringComparison.OrdinalIgnoreCase)
                && !process.Equals("Unity Hub", StringComparison.OrdinalIgnoreCase));
    }
    internal static bool Recognizes(ForegroundGame game, string[] include, string[] exclude) => !game.Shell
        && (game.InstalledGame || include.Any(value => Path.GetFileNameWithoutExtension(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase)))
        && !exclude.Any(value => Path.GetFileNameWithoutExtension(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase));
    public void Dispose() { disposed = true; timer.Dispose(); overlaySession.Dispose(); if (foregroundHook != 0) UnhookWinEvent(foregroundHook); }
}
