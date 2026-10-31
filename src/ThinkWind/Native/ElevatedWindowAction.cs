using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;

namespace ThinkWind.Native;

// One user-requested operation per UAC prompt. No persistent privileged server,
// arbitrary commands, process termination, or weakened Windows policies.
internal static class ElevatedWindowAction
{
    private const string Switch = "--elevated-window-action";
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint process);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(nint token, int kind, out int value, int length, out int needed);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    internal static bool NeedsAdminFallback(nint window)
    {
        if (!Desktop.WindowExists(window)) return false;
        GetWindowThreadProcessId(window, out uint id);
        if (id == 0) return false;
        try
        {
            nint process = OpenProcess(0x1000, false, id);
            // A normal-integrity UI may be unable to inspect a high-integrity
            // process/token. Unknown is not evidence of a non-elevated target.
            // Callers ask only after a user-requested native action has failed.
            if (process == 0) return Desktop.WindowExists(window);
            try
            {
                if (!OpenProcessToken(process, 8, out var token)) return Desktop.WindowExists(window);
                try { return !GetTokenInformation(token, 20, out int elevated, 4, out _) || elevated != 0; }
                finally { CloseHandle(token); }
            }
            finally { CloseHandle(process); }
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return Desktop.WindowExists(window); }
    }
    internal static async Task<bool> Request(nint window, bool close, bool minimize = false, Window? owner = null)
    {
        if (!Desktop.WindowExists(window)) return false;
        GetWindowThreadProcessId(window, out uint id);
        try
        {
            using var target = Process.GetProcessById((int)id);
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(Switch);
            info.ArgumentList.Add(close ? "close" : minimize ? "minimize" : "activate");
            info.ArgumentList.Add(window.ToInt64().ToString(CultureInfo.InvariantCulture));
            info.ArgumentList.Add(id.ToString(CultureInfo.InvariantCulture));
            info.ArgumentList.Add(target.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
            // This follows an explicit click, not a hover. Activating our own
            // requester lets it grant foreground permission to the UAC helper.
            if (owner?.IsVisible == true) owner.Activate();
            using var helper = Process.Start(info);
            if (helper is null) return false;
            AllowSetForegroundWindow((uint)helper.Id);
            await helper.WaitForExitAsync();
            if (helper.ExitCode == 0) return true;
            MessageBox.Show("O Windows não permitiu concluir a operação nesta janela, mesmo após a autorização.", "Think Wind");
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223) { }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { MessageBox.Show("Não foi possível solicitar autorização: " + error.Message, "Think Wind"); }
        return false;
    }
    internal static bool Run(string[] args, out int exitCode)
    {
        exitCode = 1;
        if (args.Length == 0 || args[0] != Switch) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)
                || args.Length != 5 || args[1] is not ("activate" or "close" or "minimize")
                || !long.TryParse(args[2], out long handle) || !int.TryParse(args[3], out int id)
                || !long.TryParse(args[4], out long started)) return true;
            nint window = (nint)handle;
            using var target = Process.GetProcessById(id);
            GetWindowThreadProcessId(window, out uint actual);
            if (actual != id || target.StartTime.ToUniversalTime().Ticks != started
                || target.SessionId != Process.GetCurrentProcess().SessionId || !Desktop.WindowExists(window)) return true;
            exitCode = (args[1] == "close" ? PostMessage(window, 0x0010, 0, 0) : args[1] == "minimize" ? Desktop.MinimizeWindow(window)
                : Desktop.ActivateFromDock(window, false).GetAwaiter().GetResult()) ? 0 : 2;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        return true;
    }
}
