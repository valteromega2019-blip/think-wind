using System;
using System.Runtime.InteropServices;

namespace ThinkWind.Native;

// Only the documented prefix of each COM vtable is declared. Keep this order:
// the native headers, rather than the alphabetical documentation index, define it.
public static class CoreAudio
{
    internal const uint ActiveDevice = 1;
    internal const uint AllContexts = 23;
    internal static readonly Guid EnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    internal static readonly Guid EndpointVolumeId = new("5CDF2C82-841E-4546-9722-0CF74078229A");
    internal static readonly Guid EventContext = new("E6881783-14E1-4667-AEA3-4F2B19483C8A");

    internal enum Flow { Render, Capture }
    internal enum Role { Console, Multimedia, Communications }

    [StructLayout(LayoutKind.Sequential)]
    public struct PropertyKey
    {
        internal Guid Format;
        internal uint Id;
    }

    [DllImport("ole32.dll", ExactSpelling = true)]
    internal static extern int PropVariantClear(nint variant);

    internal static void Release(object? value)
    {
          // Release each local reference on the apartment that created it.
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    internal static void Check(int result)
    {
        // Keep native HRESULT failures uniform. ThrowExceptionForHR can map an
        // invalid/disconnected endpoint to ArgumentException instead of COMException.
        if (result < 0) throw new COMException("A operação de áudio do Windows falhou.", result);
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(Flow flow, uint mask, out IDeviceCollection collection);
        [PreserveSig] int GetDefaultAudioEndpoint(Flow flow, Role role, out IDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IDevice device);
        [PreserveSig] int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IDeviceNotifications callback);
        [PreserveSig] int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IDeviceNotifications callback);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, nint activation, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out uint state);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, nint value);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] IVolumeNotifications callback);
        [PreserveSig] int UnregisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] IVolumeNotifications callback);
        [PreserveSig] int GetChannelCount(out uint channels);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    [ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDeviceNotifications
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? id);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
    }
    [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IVolumeNotifications { [PreserveSig] int OnNotify(nint notification); }
}
