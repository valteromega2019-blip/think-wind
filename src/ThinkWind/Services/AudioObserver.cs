using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using ThinkWind.Native;
using static ThinkWind.Native.CoreAudio;

namespace ThinkWind.Services;
internal sealed class AudioObserver : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Flow flow;
    private readonly Func<string?> target;
    private readonly Action<bool> changed;
    private readonly Callbacks callbacks;
    private object? enumeratorObject, endpointObject;
    private IDeviceEnumerator? enumerator;
    private IEndpointVolume? volume;
    private readonly object gate = new();
    private bool pending, devicesDirty;
    private volatile bool disposed;
    internal AudioObserver(Dispatcher ui, Flow direction, Func<string?> selected, Action<bool> update)
    {
        dispatcher = ui; flow = direction; target = selected; changed = update; callbacks = new Callbacks(this);
        try
        {
            enumeratorObject = Activator.CreateInstance(Type.GetTypeFromCLSID(EnumeratorClass, true)!);
            enumerator = (IDeviceEnumerator)enumeratorObject!;
            Check(enumerator.RegisterEndpointNotificationCallback(callbacks)); Bind();
        }
        catch (Exception error) when (error is COMException or InvalidCastException or UnauthorizedAccessException) { Dispose(); }
    }
    internal void Rebind() => Queue(true);
    private void Bind()
    {
        DetachVolume(); if (disposed || enumerator is null) return;
        IDevice? device = null;
        try
        {
            string? id = target();
            Check(id is null ? enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia, out device) : enumerator.GetDevice(id, out device));
            var iid = EndpointVolumeId; Check(device.Activate(ref iid, AllContexts, 0, out endpointObject));
            volume = (IEndpointVolume)endpointObject; Check(volume.RegisterControlChangeNotify(callbacks));
        }
        catch (Exception error) when (error is COMException or InvalidCastException) { DetachVolume(); }
        finally { Release(device); }
    }
    private void Queue(bool devices)
    {
        lock (gate)
        {
            if (disposed || dispatcher.HasShutdownStarted) return;
            devicesDirty |= devices; if (pending) return; pending = true;
        }
        try { dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            bool refresh; lock (gate) { pending = false; refresh = devicesDirty; devicesDirty = false; }
            if (disposed) return;
            if (refresh) Bind(); changed(refresh);
        })); }
        catch (InvalidOperationException) { lock (gate) pending = false; }
    }
    private void DetachVolume()
    {
        try { volume?.UnregisterControlChangeNotify(callbacks); } catch (COMException) { }
        volume = null; Release(endpointObject); endpointObject = null;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; DetachVolume();
        try { enumerator?.UnregisterEndpointNotificationCallback(callbacks); } catch (COMException) { }
        enumerator = null; Release(enumeratorObject); enumeratorObject = null;
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class Callbacks : IDeviceNotifications, IVolumeNotifications
    {
        private readonly AudioObserver owner;
        internal Callbacks(AudioObserver observer) { owner = observer; }
        public int OnNotify(nint data) { owner.Queue(false); return 0; }
        public int OnDeviceStateChanged(string id, uint state) { owner.Queue(true); return 0; }
        public int OnDeviceAdded(string id) { owner.Queue(true); return 0; }
        public int OnDeviceRemoved(string id) { owner.Queue(true); return 0; }
        public int OnDefaultDeviceChanged(int direction, int role, string? id) { if ((Flow)direction == owner.flow) owner.Queue(true); return 0; }
        public int OnPropertyValueChanged(string id, PropertyKey key) { owner.Queue(true); return 0; }
    }
}
