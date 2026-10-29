using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using WinRT;

namespace ThinkWind.Native;

internal sealed class BackdropComposition : IDisposable
{
    [ComImport, Guid("29E691FA-4567-4DCA-B319-D0F207EB6807"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICompositorDesktopInterop
    {
        void CreateDesktopWindowTarget(nint window, [MarshalAs(UnmanagedType.Bool)] bool topmost, out nint target);
        void EnsureOnThread(uint thread);
    }
    [StructLayout(LayoutKind.Sequential)] private struct QueueOptions { internal uint Size, ThreadType, ApartmentType; }
    [DllImport("CoreMessaging.dll")] private static extern int CreateDispatcherQueueController(QueueOptions options, out nint controller);
    private static DispatcherQueueController? queue;
    private static Compositor? compositor;
    private readonly DesktopWindowTarget target;
    private readonly SpriteVisual visual;
    private readonly CompositionBackdropBrush brush;
    internal BackdropComposition(nint window)
    {
        if (DispatcherQueue.GetForCurrentThread() is null)
        {
            Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(new QueueOptions { Size = 12, ThreadType = 2, ApartmentType = 2 }, out var pointer));
            try { queue = MarshalInterface<DispatcherQueueController>.FromAbi(pointer); }
            finally { Marshal.Release(pointer); }
        }
        compositor ??= new Compositor();
        compositor.As<ICompositorDesktopInterop>().CreateDesktopWindowTarget(window, true, out var raw);
        try { target = MarshalInterface<DesktopWindowTarget>.FromAbi(raw); }
        finally { Marshal.Release(raw); }
        brush = compositor.CreateHostBackdropBrush();
        visual = compositor.CreateSpriteVisual(); visual.Brush = brush;
        target.Root = visual;
    }
    internal void Resize(int width, int height) => visual.Size = new Vector2(width, height);
    internal void Enable(bool enabled) => visual.IsVisible = enabled;
    public void Dispose() { target.Root = null; visual.Dispose(); brush.Dispose(); target.Dispose(); }
    internal static void Shutdown()
    {
        compositor?.Dispose(); compositor = null;
        var controller = queue; queue = null;
        if (controller is null) return;
        var action = controller.ShutdownQueueAsync();
        if (action.Status != AsyncStatus.Started) return;
        var frame = new DispatcherFrame();
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timeout.Tick += (_, _) => frame.Continue = false;
        action.Completed = (_, _) => frame.Continue = false;
        try { timeout.Start(); Dispatcher.PushFrame(frame); }
        finally { timeout.Stop(); }
    }
}
