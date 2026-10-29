using System;
using System.Runtime.InteropServices;

namespace ThinkWind.Native;
// Windows has no documented MMDevice setter for the global default. Keep the
// desktop PolicyConfig interop isolated; callers validate the actual OS endpoint.
internal static class DefaultAudioPolicy
{
    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface Policy
    {
        [PreserveSig] int GetMixFormat(nint id, out nint format);
        [PreserveSig] int GetDeviceFormat(nint id, int formatKind, out nint format);
        [PreserveSig] int ResetDeviceFormat(nint id);
        [PreserveSig] int SetDeviceFormat(nint id, nint endpoint, nint mix);
        [PreserveSig] int GetProcessingPeriod(nint id, int kind, out long period, out long minimum);
        [PreserveSig] int SetProcessingPeriod(nint id, nint period);
        [PreserveSig] int GetShareMode(nint id, nint mode);
        [PreserveSig] int SetShareMode(nint id, nint mode);
        [PreserveSig] int GetPropertyValue(nint id, nint key, nint value);
        [PreserveSig] int SetPropertyValue(nint id, nint key, nint value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, CoreAudio.Role role);
    }
    internal static void Set(string deviceId, CoreAudio.Role role)
    {
        object? policy = null;
        try
        {
            policy = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"), true)!);
            CoreAudio.Check(((Policy)policy!).SetDefaultEndpoint(deviceId, role));
        }
        finally { CoreAudio.Release(policy); }
    }
}
