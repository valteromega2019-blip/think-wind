using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;

namespace ThinkWind.Services;

// Only explicit user actions may end ordinary applications. No service control,
// elevation, process-tree termination or changes to startup/system settings.
internal static class BackgroundAppExit
{
    internal sealed record Target(int Id, long Started, string Executable);
    internal sealed record Result(int Closed, string? Error);
    [StructLayout(LayoutKind.Sequential)] private struct ServiceStatus
    { internal uint Type, State, Accepted, Exit, SpecificExit, Checkpoint, Wait, Process, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct ServiceEntry
    { internal nint Name, DisplayName; internal ServiceStatus Status; }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool EnumServicesStatusEx(nint manager, int level, uint type, uint state, nint buffer, uint size, out uint needed, out uint returned, ref uint resume, string? group);
    [DllImport("advapi32.dll")] private static extern bool CloseServiceHandle(nint handle);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(nint token, int kind, out int value, int length, out int needed);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool IsProcessCritical(nint process, out bool critical);
    private static HashSet<int>? ServiceProcesses()
    {
        nint manager = OpenSCManager(null, null, 4);
        if (manager == 0) return null;
        nint buffer = Marshal.AllocHGlobal(262144);
        try
        {
            var result = new HashSet<int>(); uint resume = 0;
            for (int page = 0; page < 16; page++)
            {
                bool complete = EnumServicesStatusEx(manager, 0, 0x30, 3, buffer, 262144, out _, out uint count, ref resume, null);
                int error = complete ? 0 : Marshal.GetLastWin32Error();
                if (!complete && error != 234) return null;
                int stride = Marshal.SizeOf<ServiceEntry>();
                if (count > 262144 / stride) return null;
                for (int index = 0; index < count; index++)
                {
                    var entry = Marshal.PtrToStructure<ServiceEntry>(buffer + index * stride);
                    if (entry.Status.Process != 0) result.Add((int)entry.Status.Process);
                }
                if (complete) return result;
            }
            return null;
        }
        finally { Marshal.FreeHGlobal(buffer); CloseServiceHandle(manager); }
    }
    private static bool Eligible(Process process, string executable, HashSet<int> services)
    {
        if (process.Id == Environment.ProcessId || services.Contains(process.Id) || process.HasExited) return false;
        using var current = Process.GetCurrentProcess();
        if (process.SessionId != current.SessionId) return false;
        string? path = process.MainModule?.FileName;
        if (path is null || !path.Equals(executable, StringComparison.OrdinalIgnoreCase)) return false;
        string windows = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (Path.GetFullPath(path).StartsWith(windows, StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(path).Equals(Path.GetFileName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase)) return false;
        if (!IsProcessCritical(process.Handle, out bool critical) || critical) return false;
        if (!OpenProcessToken(process.Handle, 8, out nint token)) return false;
        try
        {
            if (!GetTokenInformation(token, 20, out int elevated, 4, out _) || elevated != 0) return false;
            using var identity = new WindowsIdentity(token);
            using var self = WindowsIdentity.GetCurrent();
            return identity.User is not null && identity.User == self.User;
        }
        finally { CloseHandle(token); }
    }
    internal static Task<Target[]> Find(string? executable) => Task.Run(() =>
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable)) return Array.Empty<Target>();
        var services = ServiceProcesses();
        if (services is null) return Array.Empty<Target>();
        var targets = new List<Target>();
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
        {
            using (process)
            {
                try { if (Eligible(process, executable, services)) targets.Add(new(process.Id, process.StartTime.ToUniversalTime().Ticks, executable)); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException) { }
            }
        }
        return targets.ToArray();
    });
    internal static Task<Result> End(Target[] targets) => Task.Run(() =>
    {
        var services = ServiceProcesses();
        if (services is null) return new Result(0, "Não foi possível verificar os serviços do sistema. Nenhum processo foi encerrado.");
        int closed = 0; bool denied = false;
        foreach (var target in targets)
        {
            try
            {
                using var process = Process.GetProcessById(target.Id);
                if (process.StartTime.ToUniversalTime().Ticks != target.Started || !Eligible(process, target.Executable, services)) { denied = true; continue; }
                // The user has confirmed the end-process operation. Revalidate
                // identity immediately before using this exact process handle.
                process.Kill(entireProcessTree: false);
                if (process.WaitForExit(1500)) closed++;
                else denied = true;
            }
            catch (ArgumentException) { /* Already exited. Never act on another PID. */ }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException or UnauthorizedAccessException or System.Security.SecurityException) { denied = true; }
        }
        return new Result(closed, denied ? "Alguns processos não puderam ser encerrados ou foram protegidos. Nenhuma permissão adicional foi solicitada." : null);
    });
}
