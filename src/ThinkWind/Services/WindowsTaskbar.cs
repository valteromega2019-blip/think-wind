using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace OrbyLauncher.Services;

// A separate copy of the app owns hiding/restoration. If the UI crashes, this
// process observes its exit and restores only the shell windows it hid.
internal sealed class WindowsTaskbar : IDisposable
{
    private Process? guard;
    private EventWaitHandle? stop;
    private bool preparing;
    private int revision;
    private readonly DispatcherTimer monitor = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Action<string> failed;
    internal WindowsTaskbar(Action<string> onFailure)
    {
        failed = onFailure;
        monitor.Tick += (_, _) => { if (guard?.HasExited == true) { Dispose(); failed("Não foi possível manter a barra do Windows oculta. A opção foi desativada."); } };
    }
    internal async void Apply(bool enabled)
    {
        if (!enabled) { Dispose(); return; }
        if (guard is not null || preparing) return;
        preparing = true; int request = ++revision;
        try
        {
            // Prime real icon pixels before hiding their source HWND. Do not
            // block the dispatcher or reveal the shell's overflow to do this.
            await TrayService.ReadAsync();
            if (request != revision) return;
            string executable = Path.Combine(AppContext.BaseDirectory, "OrbyLauncher.exe");
            if (!File.Exists(executable)) throw new IOException("O executável do Orby Launcher não foi encontrado para proteger a restauração da barra.");
            string name = @"Local\OrbyLauncher.Taskbar." + Guid.NewGuid().ToString("N");
            stop = new EventWaitHandle(false, EventResetMode.ManualReset, name);
            var launch = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            launch.ArgumentList.Add("--taskbar-guard"); launch.ArgumentList.Add(Environment.ProcessId.ToString()); launch.ArgumentList.Add(name);
            guard = Process.Start(launch) ?? throw new IOException("Não foi possível iniciar a proteção da barra.");
            monitor.Start();
        }
        catch (Exception error) when (error is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException or COMException or InvalidOperationException)
        { Dispose(); failed(error.Message); }
        finally { if (request == revision) preparing = false; }
    }
    public void Dispose()
    {
        revision++; preparing = false; monitor.Stop(); stop?.Set();
        // Do not kill the helper: its finally block must restore the native bars.
        guard?.Dispose(); guard = null; stop?.Dispose(); stop = null;
    }

    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int size);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(nint window);
    [DllImport("user32.dll")] private static extern bool EnableWindow(nint window, bool enabled);
    private delegate void ShellEvent(nint hook, uint kind, nint window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint minimum, uint maximum, nint module, ShellEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    [StructLayout(LayoutKind.Sequential)] private struct AppbarData
    { internal uint Size; internal nint Window; internal uint Callback, Edge; internal int Left, Top, Right, Bottom; internal nint Parameter; }
    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppbarData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    private static uint TaskbarState(uint? state = null)
    {
        var data = new AppbarData { Size = (uint)Marshal.SizeOf<AppbarData>(), Window = FindWindow("Shell_TrayWnd", null), Parameter = (nint)(state ?? 0) };
        if (data.Window == 0) return 0;
        return (uint)SHAppBarMessage(state.HasValue ? 10u : 4u, ref data);
    }
    private static bool IsTaskbar(nint window)
    { var name = new StringBuilder(128); GetClassName(window, name, name.Capacity); return name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"; }
    internal static bool RunGuard(string[] args)
    {
        if (args.Length == 0 || args[0] != "--taskbar-guard") return false;
        var hidden = new Dictionary<nint, (uint Process, bool Enabled, bool Visible)>();
        nint shellHook = 0, foregroundHook = 0, switchHook = 0, stateHook = 0, desktopHook = 0;
        uint? originalState = null;
        try
        {
            if (args.Length != 3 || !int.TryParse(args[1], out int pid) || !args[2].StartsWith(@"Local\OrbyLauncher.Taskbar.", StringComparison.Ordinal)) return true;
            using var owner = Process.GetProcessById(pid);
            using var done = EventWaitHandle.OpenExisting(args[2]);
            // Cooperate with the shell's own edge activation, instead of only
            // fighting its visible HWND. The recovery process restores the prior
            // preference on exit/crash; external user changes are left alone.
            originalState = TaskbarState();
            void Suppress(nint window)
            {
                if (!IsTaskbar(window)) return;
                GetWindowThreadProcessId(window, out uint process);
                if (!hidden.TryGetValue(window, out var previous) || previous.Process != process)
                    hidden[window] = (process, IsWindowEnabled(window), IsWindowVisible(window));
                if (IsWindowEnabled(window)) EnableWindow(window, false);
                if (IsWindowVisible(window))
                {
                    // Hide without activation, movement or a z-order change.
                    // Explorer can reveal/reposition an existing taskbar during
                    // Alt+Tab without producing a new object-show notification.
                    SetWindowPos(window, 0, 0, 0, 0, 0, 0x0080 | 0x0010 | 0x0001 | 0x0002 | 0x0004);
                    if (IsWindowVisible(window)) ShowWindow(window, 0);
                }
            }
            void Sweep() => EnumWindows((window, _) => { Suppress(window); return true; }, 0);
            // The helper owns its message pump: shell show events are handled
            // here, without keyboard/mouse hooks or work on the launcher UI.
            ShellEvent shown = (_, kind, window, objectId, childId, _, _) =>
            {
                if (window == 0 || objectId != 0 || childId != 0) return;
                // State/location events are frequent in games: reject unrelated
                // HWNDs before querying their class or invoking shell APIs.
                if (kind != 0x8002 && !hidden.ContainsKey(window)) return;
                Suppress(window);
            };
            shellHook = SetWinEventHook(0x8002, 0x8002, 0, shown, 0, 0, 2);
            ShellEvent changed = (_, _, _, _, _, _, _) => Sweep();
            foregroundHook = SetWinEventHook(0x0003, 0x0003, 0, changed, 0, 0, 2);
            switchHook = SetWinEventHook(0x0014, 0x0015, 0, changed, 0, 0, 2);
            stateHook = SetWinEventHook(0x800A, 0x800B, 0, shown, 0, 0, 2);
            // Return from UAC/lock/switch desktop: re-hide on the normal desktop.
            // Never open, draw on, or modify the protected consent desktop.
            desktopHook = SetWinEventHook(0x0020, 0x0020, 0, changed, 0, 0, 2);
            using var context = new System.Windows.Forms.ApplicationContext();
            using var recovery = new System.Windows.Forms.Timer { Interval = 250 };
            recovery.Tick += (_, _) =>
            {
                if (owner.HasExited || done.WaitOne(0)) { context.ExitThread(); return; }
                Sweep();
            };
            Sweep();
            if ((originalState.Value & 1) == 0) TaskbarState(originalState.Value | 1);
            recovery.Start();
            System.Windows.Forms.Application.Run(context);
            System.GC.KeepAlive(shown);
            System.GC.KeepAlive(changed);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or WaitHandleCannotBeOpenedException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        finally
        {
            if (shellHook != 0) UnhookWinEvent(shellHook);
            if (foregroundHook != 0) UnhookWinEvent(foregroundHook);
            if (switchHook != 0) UnhookWinEvent(switchHook);
            if (stateHook != 0) UnhookWinEvent(stateHook);
            if (desktopHook != 0) UnhookWinEvent(desktopHook);
            if (originalState is uint state && (state & 1) == 0 && TaskbarState() == (state | 1)) TaskbarState(state);
            foreach (var entry in hidden)
            {
                GetWindowThreadProcessId(entry.Key, out uint process);
                if (process == entry.Value.Process && IsTaskbar(entry.Key))
                {
                    EnableWindow(entry.Key, entry.Value.Enabled);
                    if (entry.Value.Visible) ShowWindow(entry.Key, 8);
                }
            }
        }
        return true;
    }
}
