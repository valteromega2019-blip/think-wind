using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace OrbyLauncher.Services;

internal static class WindowsUpdatePower
{
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