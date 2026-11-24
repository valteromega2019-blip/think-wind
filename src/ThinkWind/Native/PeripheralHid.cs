using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace OrbyLauncher.Native;

// Only callers with an exact VID/PID/usage whitelist may open a collection.
internal sealed class PeripheralHid : IDisposable
{
    private readonly FileStream stream;
    internal int InputLength { get; }
    internal int OutputLength { get; }
    private PeripheralHid(SafeFileHandle handle, Caps caps, bool write)
    { InputLength = caps.Input; OutputLength = caps.Output; stream = new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read, 1, true); }
    [StructLayout(LayoutKind.Sequential)] private struct Caps
    {
        internal ushort Usage, Page, Input, Output, Feature;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] internal ushort[] Reserved;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] internal ushort[] Counts;
    }
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_Interface_List_SizeW(out uint size, ref Guid guid, string? id, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern uint CM_Get_Device_Interface_ListW(ref Guid guid, string? id, [Out] char[] buffer, uint size, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("hid.dll")] private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out nint data);
    [DllImport("hid.dll")] private static extern bool HidD_FreePreparsedData(nint data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(nint data, out Caps caps);
    internal static bool Present(ushort vendor, ushort product)
    {
        var guid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        if (CM_Get_Device_Interface_List_SizeW(out uint size, ref guid, null, 0) != 0 || size > 1048576) return false;
        var buffer = new char[size];
        return CM_Get_Device_Interface_ListW(ref guid, null, buffer, size, 0) == 0
            && new string(buffer).Contains($"vid_{vendor:x4}&pid_{product:x4}", StringComparison.OrdinalIgnoreCase);
    }
    internal static PeripheralHid? Open(ushort vendor, ushort product, ushort page, ushort usage, bool write)
    {
        var guid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        if (CM_Get_Device_Interface_List_SizeW(out uint size, ref guid, null, 0) != 0 || size > 1048576) return null;
        var buffer = new char[size];
        if (CM_Get_Device_Interface_ListW(ref guid, null, buffer, size, 0) != 0) return null;
        foreach (string path in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => path.Contains($"vid_{vendor:x4}&pid_{product:x4}", StringComparison.OrdinalIgnoreCase)))
        {
            using var probe = CreateFileW(path, 0, 3, 0, 3, 0, 0);
            if (probe.IsInvalid || !HidD_GetPreparsedData(probe, out var data)) continue;
            Caps caps;
            try { if (HidP_GetCaps(data, out caps) != 0x00110000) continue; }
            finally { HidD_FreePreparsedData(data); }
            if (caps.Page != page || caps.Usage != usage || caps.Input < 1 || caps.Input > 1024 || caps.Output > 1024) continue;
            var handle = CreateFileW(path, write ? 0xC0000000u : 0x80000000u, 3, 0, 3, 0x40000000, 0);
            if (handle.IsInvalid) { handle.Dispose(); continue; }
            try { return new PeripheralHid(handle, caps, write); }
            catch { handle.Dispose(); throw; }
        }
        return null;
    }
    internal async Task<byte[]> Read(CancellationToken token)
    {
        var bytes = new byte[InputLength]; int count = await stream.ReadAsync(bytes, token).ConfigureAwait(false);
        return bytes.AsSpan(0, count).ToArray();
    }
    internal async Task Write(byte[] report, CancellationToken token)
    {
        if (report.Length > OutputLength) throw new InvalidOperationException("Relatório HID excede a coleção.");
        var buffer = new byte[OutputLength]; report.CopyTo(buffer, 0);
        await stream.WriteAsync(buffer, token).ConfigureAwait(false);
    }
    public void Dispose() => stream.Dispose();
}
