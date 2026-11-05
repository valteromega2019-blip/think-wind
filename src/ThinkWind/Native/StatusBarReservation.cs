using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace OrbyLauncher.Native;

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
    private readonly bool bottom;
    private AppBarData data;
    private OverlayChrome.Display? display;
    private int height;
    private bool registered, updating, disposed;
    private OverlayChrome.PixelRect reservedRect;
    private bool hasReservedRect;
    internal StatusBarReservation(HwndSource owner, bool bottom = false)
    { source = owner; this.bottom = bottom; data.Size = (uint)Marshal.SizeOf<AppBarData>(); data.Window = source.Handle; data.Callback = Callback; data.Edge = bottom ? 3u : 1u; source.AddHook(Message); }
    internal void Set(OverlayChrome.Display screen, int pixels)
    {
        if (disposed) return;
        bool same = display?.Bounds == screen.Bounds && height == pixels;
        display = screen; height = pixels;
        if (pixels <= 0)
        {
            hasReservedRect = false;
            if (registered) { registered = false; SHAppBarMessage(1, ref data); }
            return;
        }
        if (!registered) { hasReservedRect = false; registered = SHAppBarMessage(0, ref data) != 0; }
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
            data.Rect = new OverlayChrome.PixelRect { Left = bounds.Left, Right = bounds.Right,
                Top = bottom ? bounds.Bottom - height : bounds.Top, Bottom = bottom ? bounds.Bottom : bounds.Top + height };
            SHAppBarMessage(2, ref data);
            if (bottom) data.Rect.Top = data.Rect.Bottom - height;
            else data.Rect.Bottom = data.Rect.Top + height;
            // ABN_POSCHANGED can be caused by another overlay reacting to our
            // reservation. Do not broadcast another work-area change when
            // the negotiated rectangle is already the one we reserved.
            if (hasReservedRect && reservedRect.Left == data.Rect.Left && reservedRect.Top == data.Rect.Top
                && reservedRect.Right == data.Rect.Right && reservedRect.Bottom == data.Rect.Bottom) return;
            SHAppBarMessage(3, ref data);
            reservedRect = data.Rect; hasReservedRect = true;
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
