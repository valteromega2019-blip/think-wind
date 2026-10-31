using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkWind.Services;

internal sealed class StartupPriority : IDisposable
{
    private readonly object sync = new();
    private Timer? deadline;
    private bool restored;
    internal StartupPriority()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.AboveNormal;
            // Restore even if the UI dispatcher is busy constructing windows.
            deadline = new Timer(_ => Dispose(), null, 5000, Timeout.Infinite);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { Dispose(); }
    }
    public void Dispose()
    {
        lock (sync)
        {
            if (restored) return;
            restored = true;
            deadline?.Dispose(); deadline = null;
            try { using var process = Process.GetCurrentProcess(); process.PriorityClass = ProcessPriorityClass.Normal; }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
        }
    }
}

internal static class StartupPermissions
{
    private const string Switch = "--configure-startup-permissions";
    internal static async Task<bool> Prepare(bool sensors, bool windows)
    {
        sensors &= !AdvancedSensorSetup.IsConfigured;
        windows &= !WindowActionSetup.IsConfigured;
        if (!sensors && !windows) return true;
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(Switch); info.ArgumentList.Add(AdvancedSensorSetup.UserSid);
            info.ArgumentList.Add(sensors ? "1" : "0"); info.ArgumentList.Add(windows ? "1" : "0");
            using var process = Process.Start(info);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223) { return false; }
    }
    internal static bool RunSetup(string[] args, out int code)
    {
        code = 1;
        if (args.Length == 0 || args[0] != Switch) return false;
        using var identity = WindowsIdentity.GetCurrent();
        if (args.Length != 4 || args[1] != AdvancedSensorSetup.UserSid
            || args[2] is not ("0" or "1") || args[3] is not ("0" or "1")
            || !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return true;
        if (args[2] == "1")
        {
            AdvancedSensorSetup.RunSetup(new[] { "--configure-sensor-task", args[1] }, out code);
            if (code != 0) return true;
        }
        if (args[3] == "1")
        {
            WindowActionSetup.RunSetup(new[] { "--configure-window-action-task", args[1] }, out code);
            if (code != 0) return true;
        }
        code = 0; return true;
    }
}
