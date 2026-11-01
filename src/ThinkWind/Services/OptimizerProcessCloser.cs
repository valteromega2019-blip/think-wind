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
internal sealed record OptimizerCloseCandidate(int Id, long Started, string Name, string Path, bool BackgroundOnly = false, string Detail = "Aplicativo aberto");
internal static class OptimizerProcessCloser
{
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll")] private static extern bool IsProcessCritical(nint process, out bool critical);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint id);
    private static string? BackgroundDescription(string name, string path)
    {
        if (name.Equals("OneDrive", StringComparison.OrdinalIgnoreCase) && path.Contains("\\Microsoft\\OneDrive\\", StringComparison.OrdinalIgnoreCase))
            return "OneDrive · interrompe a sincronização até reabrir";
        if (name.Equals("GoogleDriveFS", StringComparison.OrdinalIgnoreCase) && path.Contains("\\Google\\Drive", StringComparison.OrdinalIgnoreCase))
            return "Google Drive · interrompe a sincronização até reabrir";
        if (name.Equals("Dropbox", StringComparison.OrdinalIgnoreCase) && path.Contains("\\Dropbox\\", StringComparison.OrdinalIgnoreCase))
            return "Dropbox · interrompe a sincronização até reabrir";
        if (new[] { "AdobeARM", "CCXProcess", "Creative Cloud", "AdobeIPCBroker" }.Contains(name, StringComparer.OrdinalIgnoreCase)
            && path.Contains("\\Adobe\\", StringComparison.OrdinalIgnoreCase))
            return "Componente Adobe · pode afetar sincronização e aplicativos Adobe abertos";
        return null;
    }
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
            || process.SessionId != self.SessionId) return false;
        string name = process.ProcessName;
        if (new[] { "orby", "think", "steam", "epic", "discord", "tretalk", "riot", "battle.net", "anticheat", "easyanticheat", "beservice", "security", "defender", "msmpeng", "nvcontainer", "radeon", "pawnio" }
            .Any(part => name.Contains(part, StringComparison.OrdinalIgnoreCase))) return false;
        if (new[] { "Code", "devenv", "dotnet", "cmd", "powershell", "pwsh", "WindowsTerminal", "OpenConsole" }.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
        string? path = process.MainModule?.FileName;
        string windows = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) + Path.DirectorySeparatorChar;
        if (path is null || path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)
            || path.Contains("steamapps", StringComparison.OrdinalIgnoreCase)
            || path.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)) return false;
        if (process.MainWindowHandle == 0 && BackgroundDescription(name, path) is null) return false;
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
                    if (Eligible(process, services))
                    {
                        string path = process.MainModule!.FileName!;
                        result.Add(new(id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName, path,
                            process.MainWindowHandle == 0, BackgroundDescription(process.ProcessName, path) ?? "Aplicativo aberto · salve seu trabalho antes de fechar"));
                    }
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException) { }
            }
            return result.ToArray();
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException) { return Array.Empty<OptimizerCloseCandidate>(); }
    }
    internal static Task<string> RequestClose(OptimizerCloseCandidate candidate) => Task.Run(async () =>
    {
        try
        {
            using var process = Process.GetProcessById(candidate.Id);
            if (process.StartTime.ToUniversalTime().Ticks != candidate.Started
                || !Eligible(process, ServiceProcesses())
                || !string.Equals(process.MainModule?.FileName, candidate.Path, StringComparison.OrdinalIgnoreCase))
                return candidate.Name + ": fechamento bloqueado após nova verificação.";
            bool background = process.MainWindowHandle == 0;
            if (background != candidate.BackgroundOnly)
                return candidate.Name + ": estado mudou; analise novamente para aprovar a ação correta.";
            if (background)
            {
                if (BackgroundDescription(process.ProcessName, candidate.Path) is null)
                    return candidate.Name + ": processo de segundo plano não autorizado.";
                // Only explicitly approved, known background applications, never a tree.
                process.Kill();
            }
            else if (!process.CloseMainWindow())
                return candidate.Name + ": não aceitou a solicitação; feche pelo próprio aplicativo.";
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return candidate.Name + (background ? ": encerramento enviado, mas a saída ainda não foi confirmada." : ": continua em execução; pode estar aguardando salvamento ou ter ficado na bandeja. Não foi forçado."); }
            return candidate.Name + ": processo encerrado.";
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException or ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { return candidate.Name + ": não foi possível solicitar o fechamento (" + e.Message + ")."; }
    });
}
