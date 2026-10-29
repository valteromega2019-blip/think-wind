using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ThinkWind.Native;
using static ThinkWind.Native.CoreAudio;

namespace ThinkWind.Services;

internal sealed record AudioDevice(string Id, string Name);
internal sealed record AudioState(string Id, string Name, double Volume, bool Muted);
internal sealed class AudioUnavailableException : Exception
{
    internal AudioUnavailableException(Exception cause)
        : base("Dispositivo indisponível. Confira a conexão e o serviço de áudio do Windows.", cause) { }
}

// No capture/render stream is opened. Reading a microphone endpoint never records it.
// Calls are synchronous and confined to the WPF dispatcher (STA).
internal sealed class AudioService
{
    internal IReadOnlyList<AudioDevice> Devices(Flow flow) => WithEnumerator(enumerator =>
    {
        IDeviceCollection? collection = null;
        try
        {
            Check(enumerator.EnumAudioEndpoints(flow, ActiveDevice, out collection));
            Check(collection.GetCount(out var count));
            var devices = new List<AudioDevice>();
            for (uint index = 0; index < count; index++)
            {
                IDevice? device = null;
                try
                {
                    Check(collection.Item(index, out device));
                    Check(device.GetId(out var id));
                    devices.Add(new(id, FriendlyName(device)));
                }
                // A device can disappear between enumeration and property access.
                catch (COMException) { }
                finally { Release(device); }
            }
            devices.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
            return (IReadOnlyList<AudioDevice>)devices;
        }
        finally { Release(collection); }
    });

    internal AudioState Read(Flow flow, string? deviceId) => WithEndpoint(flow, deviceId, (device, volume) =>
    {
        Check(device.GetId(out var id));
        Check(volume.GetMasterVolumeLevelScalar(out var level));
        Check(volume.GetMute(out var muted));
        return new AudioState(id, FriendlyName(device), Math.Clamp(level * 100d, 0, 100), muted);
    });

    internal void SetVolume(Flow flow, string deviceId, double percent) => WithEndpoint(flow, deviceId, (_, volume) =>
    {
        if (!double.IsFinite(percent)) throw new ArgumentOutOfRangeException(nameof(percent));
        var context = EventContext;
        Check(volume.SetMasterVolumeLevelScalar((float)Math.Clamp(percent / 100, 0, 1), ref context));
        return true;
    });

    internal void SetMuted(Flow flow, string deviceId, bool muted) => WithEndpoint(flow, deviceId, (_, volume) =>
    {
        var context = EventContext;
        Check(volume.SetMute(muted, ref context));
        return true;
    });

    private static T WithEndpoint<T>(Flow flow, string? id, Func<IDevice, IEndpointVolume, T> action) => WithEnumerator(enumerator =>
    {
        IDevice? device = null;
        object? endpoint = null;
        try
        {
            int result = id is null
                ? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia, out device)
                : enumerator.GetDevice(id, out device);
            Check(result);
            Check(device.GetState(out var state));
            if ((state & ActiveDevice) == 0) throw new COMException("Endpoint disconnected.");
            var iid = EndpointVolumeId;
            Check(device.Activate(ref iid, AllContexts, 0, out endpoint));
            return action(device, (IEndpointVolume)endpoint);
        }
        finally { Release(endpoint); Release(device); }
    });

    private static T WithEnumerator<T>(Func<IDeviceEnumerator, T> action)
    {
        object? instance = null;
        try
        {
            var type = Type.GetTypeFromCLSID(EnumeratorClass, throwOnError: true)!;
            instance = Activator.CreateInstance(type)!;
            return action((IDeviceEnumerator)instance);
        }
        catch (Exception e) when (e is COMException or InvalidCastException or UnauthorizedAccessException)
        { throw new AudioUnavailableException(e); }
        finally { Release(instance); }
    }

    private static string FriendlyName(IDevice device)
    {
        IPropertyStore? properties = null;
        // PROPVARIANT has an 8-byte header and an 8/16-byte union (x86/x64).
        var variant = Marshal.AllocCoTaskMem(IntPtr.Size == 8 ? 24 : 16);
        Marshal.Copy(new byte[IntPtr.Size == 8 ? 24 : 16], 0, variant, IntPtr.Size == 8 ? 24 : 16);
        try
        {
            Check(device.OpenPropertyStore(0, out properties));
            var key = new PropertyKey { Format = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
            Check(properties.GetValue(ref key, variant));
            return Marshal.ReadInt16(variant) == 31
                ? Marshal.PtrToStringUni(Marshal.ReadIntPtr(variant, 8)) ?? "Dispositivo de áudio"
                : "Dispositivo de áudio";
        }
        finally { PropVariantClear(variant); Marshal.FreeCoTaskMem(variant); Release(properties); }
    }
}
