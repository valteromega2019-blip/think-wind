using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ThinkWind.Native;

// A reversible shell appbar reservation keeps maximized title bars below the
// visible status strip. It owns no polling loop and is removed when hidden.
internal sealed class StatusBarReservation : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData
    {
        internal uint Size;
        internal nint Window;
        internal uint Callback, Edge;
        internal OverlayChrome.PixelRect Rect;
        internal nint Parameter;
    }
    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);
    private const int Callback = 0x8037;
    private readonly HwndSource source;
    private AppBarData data;
    private OverlayChrome.Display? display;
    private int height;
    private bool registered, updating, disposed;
    internal StatusBarReservation(HwndSource owner)
    { source = owner; data.Size = (uint)Marshal.SizeOf<AppBarData>(); data.Window = source.Handle; data.Callback = Callback; data.Edge = 1; source.AddHook(Message); }
    internal void Set(OverlayChrome.Display screen, int pixels)
    {
        if (disposed) return;
        bool same = display?.Bounds == screen.Bounds && height == pixels;
        display = screen; height = pixels;
        if (pixels <= 0)
        {
            if (registered) { registered = false; SHAppBarMessage(1, ref data); }
            return;
        }
        if (!registered) registered = SHAppBarMessage(0, ref data) != 0;
        else if (same) return;
        if (registered) Update();
    }
    private void Update()
    {
        if (!registered || updating || display is null || height <= 0) return;
        updating = true;
        try
        {
            var bounds = display.Bounds;
            data.Rect = new OverlayChrome.PixelRect { Left = bounds.Left, Right = bounds.Right, Top = bounds.Top, Bottom = bounds.Top + height };
            SHAppBarMessage(2, ref data);
            data.Rect.Bottom = data.Rect.Top + height;
            SHAppBarMessage(3, ref data);
        }
        finally { updating = false; }
    }
    private nint Message(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (disposed || !registered) return 0;
        if (message == Callback && wParam == 1) Update();
        else if (!updating && message == 0x47) SHAppBarMessage(9, ref data);
        else if (message == 0x06) SHAppBarMessage(6, ref data);
        return 0;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (registered) { registered = false; SHAppBarMessage(1, ref data); }
        source.RemoveHook(Message);
    }
}
