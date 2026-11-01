using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;
internal sealed record OptimizerCloseCandidate(int Id, long Started, string Name, string Path);
internal static class OptimizerProcessCloser
{
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll")] private static extern bool IsProcessCritical(nint process, out bool critical);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint id);
    private static HashSet<int> ServiceProcesses()
    {
        var ids = new HashSet<int>();
        using var query = new ManagementObjectSearcher("SELECT ProcessId FROM Win32_Service WHERE ProcessId > 0");
        query.Options.Timeout = TimeSpan.FromSeconds(3);
        using var services = query.Get();
        foreach (ManagementObject service in services)
            using (service) { ids.Add(Convert.ToInt32(service["ProcessId"])); }
        return ids;
    }
    private static bool Eligible(Process process, HashSet<int> services)
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
        using var self = Process.GetCurrentProcess();
        if (process.Id == self.Id || process.Id == foreground || services.Contains(process.Id)
            || process.SessionId != self.SessionId || process.MainWindowHandle == 0) return false;
        string name = process.ProcessName;
        if (new[] { "orby", "think", "steam", "epic", "discord", "tretalk", "riot", "battle.net", "anticheat", "easyanticheat", "beservice", "security", "defender", "msmpeng" }
            .Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))) return false;
        string? path = process.MainModule?.FileName;
        string windows = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) + Path.DirectorySeparatorChar;
        if (path is null || path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)
            || path.Contains("steamapps", StringComparison.OrdinalIgnoreCase)
            || path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)) return false;
        nint handle = OpenProcess(0x1000, false, process.Id);
        if (handle == 0) return false;
        try { return IsProcessCritical(handle, out bool critical) && !critical; }
        finally { CloseHandle(handle); }
    }
    internal static OptimizerCloseCandidate[] Candidates(IEnumerable<int> ids, CancellationToken token)
    {
        try
        {
            var services = ServiceProcesses(); var result = new List<OptimizerCloseCandidate>();
            foreach (int id in ids.Distinct())
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var process = Process.GetProcessById(id);
                    if (Eligible(process, services)) result.Add(new(id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName, process.MainModule!.FileName!));
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException) { }
            }
            return result.ToArray();
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { return Array.Empty<OptimizerCloseCandidate>(); }
    }
    internal static Task<string> RequestClose(OptimizerCloseCandidate candidate) => Task.Run(() =>
    {
        try
        {
            using var process = Process.GetProcessById(candidate.Id);
            if (process.StartTime.ToUniversalTime().Ticks != candidate.Started
                || !Eligible(process, ServiceProcesses())
                || !string.Equals(process.MainModule?.FileName, candidate.Path, StringComparison.OrdinalIgnoreCase))
                return candidate.Name + ": fechamento bloqueado após nova verificação.";
            return process.CloseMainWindow()
                ? candidate.Name + ": fechamento solicitado. O aplicativo pode pedir para salvar ou permanecer aberto."
                : candidate.Name + ": não aceitou a solicitação; feche pelo próprio aplicativo.";
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException or ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { return candidate.Name + ": não foi possível solicitar o fechamento (" + e.Message + ")."; }
    });
}
