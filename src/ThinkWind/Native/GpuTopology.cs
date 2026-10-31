using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ThinkWind.Native;

internal sealed record GpuAdapterInfo(string Name, bool Integrated, ulong DedicatedMemory);

// DXCore's IsIntegrated property describes the adapter, not its vendor.
// Queried when the sensor inventory opens; never per frame or on the UI thread.
internal static class GpuTopology
{
    [ComImport, Guid("78ee5945-c36e-4b13-a669-005dd11c0f06"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface Factory
    {
        [PreserveSig] int CreateAdapterList(uint count, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] Guid[] attributes, in Guid iid, out AdapterList list);
    }
    [ComImport, Guid("526c7776-40e9-459b-b711-f32ad76dfc28"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface AdapterList
    {
        [PreserveSig] int GetAdapter(uint index, in Guid iid, out Adapter adapter);
        [PreserveSig] uint GetAdapterCount();
    }
    [ComImport, Guid("f0db4c7f-fe5a-42a2-bd62-f2a6cf6fc83e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface Adapter
    {
        [PreserveSig] [return: MarshalAs(UnmanagedType.I1)] bool IsValid();
        [PreserveSig] [return: MarshalAs(UnmanagedType.I1)] bool IsAttributeSupported(in Guid attribute);
        [PreserveSig] [return: MarshalAs(UnmanagedType.I1)] bool IsPropertySupported(uint property);
        [PreserveSig] int GetProperty(uint property, nuint size, nint buffer);
        [PreserveSig] int GetPropertySize(uint property, out nuint size);
    }
    [DllImport("dxcore.dll", ExactSpelling = true)]
    private static extern int DXCoreCreateAdapterFactory(in Guid iid, out Factory factory);
    internal static IReadOnlyList<GpuAdapterInfo> Read()
    {
        var result = new List<GpuAdapterInfo>(); Factory? factory = null; AdapterList? list = null;
        try
        {
            if (DXCoreCreateAdapterFactory(typeof(Factory).GUID, out factory) < 0) return result;
            var graphics = new[] { new Guid("8c47866b-7583-450d-f0f0-6bada895af4b") };
            if (factory.CreateAdapterList(1, graphics, typeof(AdapterList).GUID, out list) < 0) return result;
            for (uint i = 0; i < Math.Min(list.GetAdapterCount(), 64u); i++)
            {
                Adapter? adapter = null;
                try
                {
                    if (list.GetAdapter(i, typeof(Adapter).GUID, out adapter) < 0 || !adapter.IsValid()) continue;
                    var hardware = Property(adapter, 11); var integrated = Property(adapter, 12);
                    if (hardware is not { Length: > 0 } || hardware[0] == 0 || integrated is not { Length: > 0 }) continue;
                    var name = Property(adapter, 2); var memory = Property(adapter, 7);
                    if (name is null) continue;
                    result.Add(new(Encoding.UTF8.GetString(name).TrimEnd('\0'), integrated[0] != 0,
                        memory is { Length: >= 8 } ? BitConverter.ToUInt64(memory) : 0));
                }
                finally { Release(adapter); }
            }
        }
        catch (Exception error) when (error is COMException or DllNotFoundException or EntryPointNotFoundException or InvalidCastException) { }
        finally { Release(list); Release(factory); }
        return result;
    }
    private static byte[]? Property(Adapter adapter, uint property)
    {
        if (!adapter.IsPropertySupported(property) || adapter.GetPropertySize(property, out nuint size) < 0 || size is 0 or > 4096) return null;
        nint buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (adapter.GetProperty(property, size, buffer) < 0) return null;
            var bytes = new byte[(int)size]; Marshal.Copy(buffer, bytes, 0, bytes.Length); return bytes;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}
