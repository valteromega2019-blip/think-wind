using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OrbyLauncher.Services;

internal static class WindowsUpdatePower
{
    internal static void StartInstallAndShutdown()
    {
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executável do Orby indisponível.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = true, Verb = "runas" };
        start.ArgumentList.Add("--windows-update-shutdown");
        using var worker = Process.Start(start);
    }

    // Runs only in the explicitly elevated, visible update worker, never in the
    // hourly detector. Downloaded updates must be installed before shutdown.
    internal static void InstallDownloaded(Action<string> progress)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        if (!new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("O Windows não concedeu permissão para instalar atualizações.");
        object? session = null, searcher = null, result = null, available = null, collection = null, installer = null, installation = null;
        try
        {
            progress("Consultando as atualizações já baixadas…");
            session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session", true)!);
            searcher = ((dynamic)session!).CreateUpdateSearcher();
            ((dynamic)searcher).Online = false;
            result = ((dynamic)searcher).Search("IsInstalled=0 and IsHidden=0 and Type='Software'");
            if ((int)((dynamic)result).ResultCode != 2)
                throw new InvalidOperationException("O Windows não concluiu a consulta de atualizações. O desligamento foi cancelado.");
            available = ((dynamic)result).Updates;
            collection = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl", true)!);
            int count = ((dynamic)available).Count;
            bool skipped = false;
            for (int i = 0; i < count; i++)
            {
                object? update = null, behavior = null;
                try
                {
                    update = ((dynamic)available).Item(i);
                    if (!((dynamic)update).IsDownloaded) continue;
                    behavior = ((dynamic)update).InstallationBehavior;
                    // Do not accept licensing or interactive updates implicitly,
                    // or install packages that can force a reboot themselves.
                    if (!((dynamic)update).EulaAccepted || ((dynamic)behavior).CanRequestUserInput
                        || (int)((dynamic)behavior).RebootBehavior == 1) { skipped = true; continue; }
                    ((dynamic)collection!).Add(update);
                }
                finally { Release(behavior); Release(update); }
            }
            if (skipped) throw new InvalidOperationException("Uma atualização precisa de confirmação ou instalação pelo Windows Update. O PC não será desligado. Conclua essa etapa nas configurações do Windows.");
            int selected = ((dynamic)collection!).Count;
            if (selected == 0) throw new InvalidOperationException("Não há atualizações baixadas compatíveis com esta instalação. O PC não será desligado. Confira o Windows Update.");
            installer = ((dynamic)session!).CreateUpdateInstaller();
            if (((dynamic)installer).IsBusy || ((dynamic)installer).RebootRequiredBeforeInstallation)
                throw new InvalidOperationException("O Windows já está instalando ou exige reinicialização antes de continuar. O PC não será desligado automaticamente.");
            ((dynamic)installer).Updates = collection;
            ((dynamic)installer).AllowSourcePrompts = false;
            progress($"Instalando {selected} atualização(ões). Aguarde; o computador permanece ligado até concluir…");
            installation = ((dynamic)installer).Install();
            if ((int)((dynamic)installation).ResultCode != 2)
                throw new InvalidOperationException("O Windows não concluiu todas as atualizações com sucesso. O desligamento foi cancelado. Consulte o Windows Update.");
            for (int i = 0; i < selected; i++)
            {
                object? item = null;
                try
                {
                    item = ((dynamic)installation).GetUpdateResult(i);
                    if ((int)((dynamic)item).ResultCode != 2 || (int)((dynamic)item).HResult < 0)
                        throw new InvalidOperationException("Uma atualização não foi instalada com sucesso. O desligamento foi cancelado.");
                }
                finally { Release(item); }
            }
            if (((dynamic)installation).RebootRequired)
                throw new InvalidOperationException("As atualizações foram instaladas, mas precisam de reinicialização para terminar. O PC continuará ligado: salve seu trabalho e escolha Atualizar e reiniciar no Windows.");
            progress("Atualizações instaladas. Solicitando o desligamento…");
            UpdateAndShutdown();
        }
        finally { Release(installation); Release(installer); Release(collection); Release(available); Release(result); Release(searcher); Release(session); }
    }
    private static readonly object gate = new();
    private static Timer? refreshTimer;
    private static bool reading, stopped = true, updatePending;
    internal static bool UpdatePending { get { lock (gate) return updatePending; } }
    internal static void StartMonitoring()
    {
        lock (gate)
        {
            if (refreshTimer is not null) return;
            stopped = false;
            // Shared by all monitors. One background callback at startup, then hourly.
            refreshTimer = new Timer(_ => Refresh(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
        }
    }
    internal static void StopMonitoring()
    {
        lock (gate)
        {
            stopped = true;
            refreshTimer?.Dispose(); refreshTimer = null;
        }
    }
    private static void Refresh()
    {
        lock (gate) { if (stopped || reading) return; reading = true; }
        try
        {
            bool ready = ReadPending();
            lock (gate) { if (!stopped) updatePending = ready; }
        }
        finally { lock (gate) reading = false; }
    }
    private static bool ReadPending()
    {
        object? information = null, session = null, searcher = null, result = null, updates = null;
        try
        {
            information = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.SystemInfo", true)!);
            if (((dynamic)information!).RebootRequired) return true;
            session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session", true)!);
            searcher = ((dynamic)session!).CreateUpdateSearcher();
            ((dynamic)searcher).Online = false; // Local metadata only; never start a download or online scan.
            result = ((dynamic)searcher).Search("IsInstalled=0 and IsHidden=0");
            updates = ((dynamic)result).Updates;
            int count = ((dynamic)updates).Count;
            for (int i = 0; i < count; i++)
            {
                object? update = null;
                try { update = ((dynamic)updates).Item(i); if (((dynamic)update).IsDownloaded) return true; }
                finally { Release(update); }
            }
        }
        catch (Exception error) when (error is COMException or InvalidCastException or TypeLoadException
            or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or System.Security.SecurityException)
        { return false; }
        finally { Release(updates); Release(result); Release(searcher); Release(session); Release(information); }
        return false;
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct Privileges { public uint Count; public Luid Luid; public uint Attributes; }
    [DllImport("kernel32.dll")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValue(string? system, string name, out Luid luid);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(nint token, bool disableAll, ref Privileges state, uint length, out Privileges previous, out uint returned);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern uint InitiateShutdownW(string? machine, string? message, uint grace, uint flags, uint reason);
    internal static void UpdateAndShutdown()
    {
        if (!OpenProcessToken(GetCurrentProcess(), 0x28, out var token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        Privileges previous = default; bool enabled = false;
        try
        {
            if (!LookupPrivilegeValue(null, "SeShutdownPrivilege", out var luid)) throw new Win32Exception(Marshal.GetLastWin32Error());
            var state = new Privileges { Count = 1, Luid = luid, Attributes = 2 };
            if (!AdjustTokenPrivileges(token, false, ref state, (uint)Marshal.SizeOf<Privileges>(), out previous, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            int error = Marshal.GetLastWin32Error();
            if (error != 0) throw new Win32Exception(error);
            enabled = true;
            // INSTALL_UPDATES | POWEROFF, planned OS upgrade; no forced app termination.
            uint status = InitiateShutdownW(null, null, 0, 0x40 | 0x08, 0x80020003);
            if (status != 0) throw new Win32Exception((int)status);
        }
        finally
        {
            if (enabled) AdjustTokenPrivileges(token, false, ref previous, 0, out _, out _);
            CloseHandle(token);
        }
    }
}
