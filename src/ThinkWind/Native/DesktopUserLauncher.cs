using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace ThinkWind.Native;

// OLE drag/drop must stay at the desktop user's integrity level. Only the
// existing protected sensor/action helpers retain administrative permissions.
internal static class DesktopUserLauncher
{
    private const string RelaunchFlag = "--desktop-user-ui";
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        internal uint Size;
        internal string? Reserved, Desktop, Title;
        internal uint X, Y, Width, Height, Columns, Rows, Fill, Flags;
        internal ushort Show, ReservedLength;
        internal nint ReservedData, Input, Output, Error;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInfo { internal nint Process, Thread; internal uint ProcessId, ThreadId; }
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GetExitCodeProcess(nint process, out uint code);
    [DllImport("user32.dll")] private static extern uint WaitForInputIdle(nint process, uint milliseconds);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool DuplicateTokenEx(nint token, uint access, nint attributes, int level, int type, out nint duplicate);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(nint token, int kind, out int value, int length, out int needed);
    [DllImport("userenv.dll", SetLastError = true)] private static extern bool CreateEnvironmentBlock(out nint environment, nint token, bool inherit);
    [DllImport("userenv.dll")] private static extern bool DestroyEnvironmentBlock(nint environment);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern bool CreateProcessWithTokenW(nint token, uint logonFlags, string application, StringBuilder command,
        uint flags, nint environment, string directory, ref StartupInfo startup, out ProcessInfo process);

    internal static bool RelaunchIfElevated(string[] args)
    {
        using var current = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator)) return false;
        if (args.Contains(RelaunchFlag)) throw new InvalidOperationException("A interface ainda recebeu permissões administrativas. Abra o Think Wind pelo Explorador sem marcar Executar como administrador.");
        nint shell = GetShellWindow();
        if (shell == 0) throw new InvalidOperationException("O Explorador precisa estar aberto para iniciar a interface com suporte a arrastar e soltar.");
        GetWindowThreadProcessId(shell, out uint id);
        nint process = 0, token = 0, primary = 0, environment = 0;
        var created = new ProcessInfo();
        try
        {
            using var desktop = Process.GetProcessById((int)id);
            using var self = Process.GetCurrentProcess();
            if (desktop.SessionId != self.SessionId) throw new InvalidOperationException("O Explorador pertence a outra sessão.");
            process = OpenProcess(0x1000, false, id);
            if (process == 0 || !OpenProcessToken(process, 0xB, out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            using var desktopIdentity = new WindowsIdentity(token);
            if (desktopIdentity.User is null || current.User is null || !desktopIdentity.User.Equals(current.User)) throw new InvalidOperationException("Use a mesma conta do desktop para iniciar o Think Wind. A interface não será transferida para outro usuário.");
            if (!GetTokenInformation(token, 20, out int elevated, 4, out _)) throw new Win32Exception(Marshal.GetLastWin32Error());
            // With UAC disabled, Explorer itself can be elevated. The source and
            // target already share its level; there is no medium desktop token.
            if (elevated != 0) return false;
            if (!DuplicateTokenEx(token, 0x02000000, 0, 2, 1, out primary)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!CreateEnvironmentBlock(out environment, primary, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executável do Think Wind indisponível.");
            string command = string.Join(" ", new[] { executable }.Concat(args).Append(RelaunchFlag).Select(Quote));
            if (command.Length >= 1024) throw new InvalidOperationException("Argumentos de inicialização muito longos para reabrir a interface.");
            var startup = new StartupInfo { Size = (uint)Marshal.SizeOf<StartupInfo>(), Desktop = @"winsta0\default" };
            // A build tool may put its children in a kill-on-exit job. The UI
            // must outlive this short bootstrap; break away when permitted.
            bool launched = CreateProcessWithTokenW(primary, 0, executable, new StringBuilder(command), 0x1000400, environment,
                Path.GetDirectoryName(executable)!, ref startup, out created);
            if (!launched && Marshal.GetLastWin32Error() == 5)
                launched = CreateProcessWithTokenW(primary, 0, executable, new StringBuilder(command), 0x400, environment,
                    Path.GetDirectoryName(executable)!, ref startup, out created);
            if (!launched) throw new Win32Exception(Marshal.GetLastWin32Error());
            WaitForInputIdle(created.Process, 5000);
            if (GetExitCodeProcess(created.Process, out uint code) && code != 259)
                throw new InvalidOperationException("A interface normal encerrou ao iniciar. Código: " + code + ". Abra o executável pelo Explorador.");
            return true;
        }
        finally
        {
            if (created.Thread != 0) CloseHandle(created.Thread);
            if (created.Process != 0) CloseHandle(created.Process);
            if (environment != 0) DestroyEnvironmentBlock(environment);
            if (primary != 0) CloseHandle(primary);
            if (token != 0) CloseHandle(token);
            if (process != 0) CloseHandle(process);
        }
    }
    private static string Quote(string argument)
    {
        var quoted = new StringBuilder("\""); int slashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\') { slashes++; continue; }
            quoted.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            quoted.Append(character); slashes = 0;
        }
        return quoted.Append('\\', slashes * 2).Append('"').ToString();
    }
}
