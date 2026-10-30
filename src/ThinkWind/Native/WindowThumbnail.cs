using System;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ThinkWind.Native;
// A non-layered top-level destination lets DWM render live content independently
// of WPF's per-pixel-alpha foreground. It never activates or receives input.
internal sealed class WindowThumbnail : Forms.NativeWindow, IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Properties { internal uint Flags; internal Rect Destination, Source; internal byte Opacity; [MarshalAs(UnmanagedType.Bool)] internal bool Visible; [MarshalAs(UnmanagedType.Bool)] internal bool ClientOnly; }
    [StructLayout(LayoutKind.Sequential)] private struct SourceSize { internal int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct Paint { internal nint Dc; internal int Erase; internal Rect Area; internal int Restore, Update; internal int Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7, Reserved8; }
    [DllImport("user32.dll")] private static extern nint BeginPaint(nint window, out Paint paint);
    [DllImport("user32.dll")] private static extern bool EndPaint(nint window, ref Paint paint);
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] private static extern int FillRect(nint dc, ref Rect rect, nint brush);
    [DllImport("gdi32.dll")] private static extern nint GetStockObject(int index);
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(nint thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref Properties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out SourceSize size);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    private nint thumbnail;
    private Rect previous;
    private bool placed;
    internal static int ActiveCount { get; private set; }
    internal bool Available => thumbnail != 0;
    internal WindowThumbnail(nint owner, nint source)
    {
        // DWM thumbnails need a redirected destination. NOREDIRECTIONBITMAP is
        // appropriate for our Composition backdrop, but not for this HWND.
        CreateHandle(new Forms.CreateParams { Caption = "", Parent = owner, Style = unchecked((int)0x80000000), ExStyle = 0x08000088, X = -10000, Y = -10000, Width = 1, Height = 1 });
        if (DwmRegisterThumbnail(Handle, source, out thumbnail) < 0) { thumbnail = 0; DestroyHandle(); return; }
        ActiveCount++;
    }
    internal bool Place(Point topLeft, Size size)
    {
        if (!Available) return false;
        var next = new Rect { Left = (int)Math.Round(topLeft.X), Top = (int)Math.Round(topLeft.Y), Right = (int)Math.Round(topLeft.X + size.Width), Bottom = (int)Math.Round(topLeft.Y + size.Height) };
        int width = Math.Max(1, next.Right - next.Left), height = Math.Max(1, next.Bottom - next.Top);
        if (DwmQueryThumbnailSourceSize(thumbnail, out var source) >= 0 && source.Width > 0 && source.Height > 0)
        {
            double scale = Math.Min(width / (double)source.Width, height / (double)source.Height);
            int w = Math.Max(1, (int)(source.Width * scale)), h = Math.Max(1, (int)(source.Height * scale));
            // Fit the native destination HWND itself, not just its DWM image.
            // Unused space belongs to the transparent WPF card, never a black HWND.
            next.Left += (width - w) / 2; next.Top += (height - h) / 2;
            width = w; height = h; next.Right = next.Left + w; next.Bottom = next.Top + h;
        }
        if (placed && next.Left == previous.Left && next.Top == previous.Top && next.Right == previous.Right && next.Bottom == previous.Bottom) return true;
        var destination = new Rect { Right = width, Bottom = height };
        var properties = new Properties { Flags = 1 | 4 | 8 | 16, Destination = destination, Opacity = 255, Visible = true, ClientOnly = false };
        SetWindowPos(Handle, 0, next.Left, next.Top, width, height, 0x0010);
        if (DwmUpdateThumbnailProperties(thumbnail, ref properties) < 0) { SetWindowPos(Handle, 0, 0, 0, 0, 0, 0x0013 | 0x0080); return false; }
        SetWindowPos(Handle, 0, next.Left, next.Top, width, height, 0x0010 | 0x0040);
        previous = next; placed = true;
        return true;
    }
    internal static bool CloseWindow(nint window) => IsWindow(window) && PostMessage(window, 0x10, 0, 0);
    protected override void WndProc(ref Forms.Message message)
    {
        if (message.Msg == 0x84) { message.Result = -1; return; }
        if (message.Msg == 0x21) { message.Result = 3; return; }
        if (message.Msg == 0x83) { message.Result = 0; return; }
        if (message.Msg == 0x14) { GetClientRect(Handle, out var area); FillRect(message.WParam, ref area, GetStockObject(4)); message.Result = 1; return; }
        if (message.Msg == 0x0F)
        {
            var dc = BeginPaint(Handle, out var paint);
            try { GetClientRect(Handle, out var area); FillRect(dc, ref area, GetStockObject(4)); }
            finally { EndPaint(Handle, ref paint); }
            message.Result = 0; return;
        }
        base.WndProc(ref message);
    }
    public void Dispose() { if (thumbnail != 0) { DwmUnregisterThumbnail(thumbnail); thumbnail = 0; ActiveCount--; } if (Handle != 0) DestroyHandle(); }
}
