using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace ThinkWind.Services;
internal sealed record ForegroundGame(string Process, bool Fullscreen, bool Shell, nint Handle = 0, bool InstalledGame = false);
internal sealed class GameModeService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Monitor { internal uint Size; internal Rect Bounds, Work; internal uint Flags; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint window);
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
    private bool enabled = true, protectAll;
    private string[] include = Array.Empty<string>(), exclude = Array.Empty<string>();
    internal bool Active { get; private set; }
    internal event Action? ForegroundChanged;
    internal string CurrentProcess { get; private set; } = "";
    internal GameModeService(Dispatcher ui, Action<bool> onChange, bool initiallyActive = false)
    {
        dispatcher = ui; changed = onChange; Active = initiallyActive;
        foregroundEvent = (_, _, _, _, _, _, _) => Read();
        foregroundHook = SetWinEventHook(3, 3, 0, foregroundEvent, 0, 0, 2);
        timer = new Timer(_ => Read(), null, 1500, 1500);
    }
    internal static bool Probe(Preferences p) => Eligible(Snapshot(), p.GameModeEnabled, p.GameModeInclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), p.GameModeExclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries), p.GameModeProtectAllForeground);
    internal void Configure(Preferences p)
    { enabled = p.GameModeEnabled; protectAll = p.GameModeProtectAllForeground; include = p.GameModeInclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries); exclude = p.GameModeExclude.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries); Read(); }
    internal static bool Eligible(ForegroundGame game, bool enabled, string[] include, string[] exclude, bool protectAll = false)
    {
        string Key(string value) => Path.GetFileNameWithoutExtension(value);
        if (!enabled || game.Shell || exclude.Any(value => Key(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase))) return false;
        return protectAll || game.Fullscreen || game.InstalledGame || include.Any(value => Key(value).Equals(game.Process, StringComparison.OrdinalIgnoreCase));
    }
    private void Read()
    {
        if (disposed || Interlocked.Exchange(ref reading, 1) != 0) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            bool posted = false;
            try
            {
                var game = Snapshot(); bool active = Eligible(game, enabled, include, exclude, protectAll);
                if (disposed || dispatcher.HasShutdownStarted) return;
                dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() =>
                {
                    bool stale = false;
                    try
                    {
                        if (disposed) return;
                        stale = game.Handle != 0 && game.Handle != GetForegroundWindow(); if (stale) return;
                        ForegroundChanged?.Invoke();
                        CurrentProcess = game.Process; if (Active == active) return; Active = active; changed(active);
                    }
                    finally { Interlocked.Exchange(ref reading, 0); if (stale && !disposed) Read(); }
                }));
                posted = true;
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception) { }
            finally { if (!posted) Interlocked.Exchange(ref reading, 0); }
        });
    }
    private static ForegroundGame Snapshot()
    {
        nint window = GetForegroundWindow(); GetWindowThreadProcessId(window, out uint pid);
        if (window == 0 || pid == 0) return new("", false, true);
        using var process = Process.GetProcessById((int)pid); string name = process.ProcessName;
        bool shell = pid == Environment.ProcessId || name.Equals("explorer", StringComparison.OrdinalIgnoreCase) || name.Equals("dwm", StringComparison.OrdinalIgnoreCase) || name.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) || name.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase);
        var monitor = new Monitor { Size = (uint)Marshal.SizeOf<Monitor>() };
        bool full = !IsZoomed(window) && GetWindowRect(window, out var rect) && GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)
            && rect.Left <= monitor.Bounds.Left + 2 && rect.Top <= monitor.Bounds.Top + 2 && rect.Right >= monitor.Bounds.Right - 2 && rect.Bottom >= monitor.Bounds.Bottom - 2;
        bool installed = false;
        if (!shell && !full)
        {
            nint handle = OpenProcess(0x1000, false, pid);
            if (handle != 0) try
            {
                var path = new System.Text.StringBuilder(32768); uint length = (uint)path.Capacity;
                installed = QueryFullProcessImageName(handle, 0, path, ref length) && InstalledGameCatalog.Contains(path.ToString());
            }
            finally { CloseHandle(handle); }
        }
        return new(name, full, shell, window, installed);
    }
    public void Dispose() { disposed = true; timer.Dispose(); if (foregroundHook != 0) UnhookWinEvent(foregroundHook); }
}
