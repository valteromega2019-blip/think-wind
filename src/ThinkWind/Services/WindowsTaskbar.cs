using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace ThinkWind.Services;

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
            string executable = Path.Combine(AppContext.BaseDirectory, "ThinkWind.exe");
            if (!File.Exists(executable)) throw new IOException("O executável do Think Wind não foi encontrado para proteger a restauração da barra.");
            string name = @"Local\ThinkWind.Taskbar." + Guid.NewGuid().ToString("N");
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
    private static bool IsTaskbar(nint window)
    { var name = new StringBuilder(128); GetClassName(window, name, name.Capacity); return name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd"; }
    internal static bool RunGuard(string[] args)
    {
        if (args.Length == 0 || args[0] != "--taskbar-guard") return false;
        var hidden = new Dictionary<nint, uint>();
        try
        {
            if (args.Length != 3 || !int.TryParse(args[1], out int pid) || !args[2].StartsWith(@"Local\ThinkWind.Taskbar.", StringComparison.Ordinal)) return true;
            using var owner = Process.GetProcessById(pid);
            using var done = EventWaitHandle.OpenExisting(args[2]);
            while (!owner.HasExited && !done.WaitOne(250))
            {
                EnumWindows((window, _) =>
                {
                    if (!IsTaskbar(window) || !IsWindowVisible(window)) return true;
                    GetWindowThreadProcessId(window, out uint process);
                    hidden[window] = process; ShowWindow(window, 0); return true;
                }, 0);
            }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or WaitHandleCannotBeOpenedException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
        finally
        {
            foreach (var entry in hidden)
            {
                GetWindowThreadProcessId(entry.Key, out uint process);
                if (process == entry.Value && IsTaskbar(entry.Key)) ShowWindow(entry.Key, 8);
            }
        }
        return true;
    }
}
