using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;
internal sealed record AudioSessionKey(string Device, string Instance);
internal sealed record AppAudioState(string Id, string Name, double Volume, bool Muted, bool Active, AudioSessionKey[] Sessions);
internal static class AppAudioMixer
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    internal static async Task<AppAudioState[]> ReadAsync(CancellationToken cancellation, bool includeInactive = false)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try { return await Task.Run(() => Read(cancellation, includeInactive), cancellation).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    internal static async Task<bool> SetAsync(AppAudioState target, double? volume, bool? mute, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                bool found = false, success = true;
                Visit((device, control, simple) =>
                {
                    CoreAudio.Check(control.GetSessionInstanceIdentifier(out string instance));
                    if (!target.Sessions.Any(key => key.Device == device && key.Instance == instance)) return;
                    found = true; Guid context = CoreAudio.EventContext;
                    if (volume is double value) success &= simple.SetMasterVolume((float)Math.Clamp(value / 100, 0, 1), ref context) >= 0;
                    if (mute is bool muted) success &= simple.SetMute(muted, ref context) >= 0;
                }, cancellation);
                return found && success;
            }, cancellation).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    private static AppAudioState[] Read(CancellationToken cancellation, bool includeInactive)
    {
        var sessions = new List<(string Group, string Name, double Volume, bool Muted, bool Active, AudioSessionKey Key)>();
        var names = new Dictionary<uint, (string Group, string Name)>();
        Visit((device, control, volume) =>
        {
            CoreAudio.Check(control.GetState(out int state)); if (state == 2) return;
            CoreAudio.Check(volume.GetMute(out bool muted));
            if (!includeInactive && state != 1 && !muted) return;
            CoreAudio.Check(control.GetProcessId(out uint process));
            CoreAudio.Check(control.GetSessionInstanceIdentifier(out string instance));
            CoreAudio.Check(volume.GetMasterVolume(out float level));
            if (!float.IsFinite(level)) return;
            bool system = control.IsSystemSoundsSession() == 0;
            if (!names.TryGetValue(process, out var name))
            {
                name = system ? ("system", "Sons do Windows") : ProcessName(process);
                names[process] = name;
            }
            sessions.Add((name.Group, name.Name, Math.Clamp(level * 100d, 0, 100), muted, state == 1, new(device, instance)));
        }, cancellation);
        return sessions.GroupBy(session => session.Group, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppAudioState(group.Key, group.First().Name, group.Max(item => item.Volume), group.All(item => item.Muted), group.Any(item => item.Active), group.Select(item => item.Key).ToArray()))
            .OrderByDescending(item => item.Active).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private static (string Group, string Name) ProcessName(uint pid)
    {
        try
        {
            using var process = Process.GetProcessById((int)pid);
            string name = process.ProcessName, path = "";
            try { path = process.MainModule?.FileName ?? ""; } catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException) { }
            if (path.Length > 0)
            {
                string? description = FileVersionInfo.GetVersionInfo(path).FileDescription;
                return (path, string.IsNullOrWhiteSpace(description) ? name : description);
            }
            return ("pid:" + pid, name);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.IO.IOException or UnauthorizedAccessException)
        { return ("pid:" + pid, "Aplicativo " + pid); }
    }
    private static void Visit(Action<string, AudioSessions.IControl, AudioSessions.IVolume> action, CancellationToken cancellation)
    {
        object? root = null; CoreAudio.IDeviceCollection? devices = null;
        try
        {
            root = Activator.CreateInstance(Type.GetTypeFromCLSID(CoreAudio.EnumeratorClass, true)!)!;
            var enumerator = (CoreAudio.IDeviceEnumerator)root;
            CoreAudio.Check(enumerator.EnumAudioEndpoints(CoreAudio.Flow.Render, CoreAudio.ActiveDevice, out devices));
            CoreAudio.Check(devices.GetCount(out uint count));
            for (uint d = 0; d < count; d++)
            {
                cancellation.ThrowIfCancellationRequested(); CoreAudio.IDevice? device = null; object? manager = null; AudioSessions.IEnumeration? sessions = null;
                try
                {
                    CoreAudio.Check(devices.Item(d, out device)); CoreAudio.Check(device.GetId(out string id)); Guid iid = AudioSessions.ManagerId;
                    CoreAudio.Check(device.Activate(ref iid, CoreAudio.AllContexts, 0, out manager));
                    CoreAudio.Check(((AudioSessions.IManager)manager).GetSessionEnumerator(out sessions)); CoreAudio.Check(sessions.GetCount(out int size));
                    for (int s = 0; s < size; s++)
                    {
                        cancellation.ThrowIfCancellationRequested(); AudioSessions.IControl? control = null;
                        try { CoreAudio.Check(sessions.GetSession(s, out control)); action(id, control, (AudioSessions.IVolume)control); }
                        catch (Exception e) when (e is COMException or InvalidCastException) { /* Session may close while enumerating. */ }
                        finally { CoreAudio.Release(control); }
                    }
                }
                catch (COMException) { /* A disconnected endpoint must not hide the others. */ }
                finally { CoreAudio.Release(sessions); CoreAudio.Release(manager); CoreAudio.Release(device); }
            }
        }
        catch (Exception e) when (e is COMException or InvalidCastException or UnauthorizedAccessException) { throw new AudioUnavailableException(e); }
        finally { CoreAudio.Release(devices); CoreAudio.Release(root); }
    }
}
