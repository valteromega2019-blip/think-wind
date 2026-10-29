using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ThinkWind.Native;

internal static class OverlayChrome
{
    [StructLayout(LayoutKind.Sequential)] internal struct PixelRect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { internal int Left, Right, Top, Bottom; }
    internal sealed record Display(System.Drawing.Rectangle Bounds, System.Drawing.Rectangle Work, double Scale)
    { internal double Width => Work.Width / Scale; internal double Height => Work.Height / Scale; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(nint monitor, int kind, out uint x, out uint y);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out PixelRect rectangle);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);
    [DllImport("gdi32.dll")] private static extern nint CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern int CombineRgn(nint result, nint first, nint second, int mode);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint value);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(nint window, nint region, bool redraw);

    internal static Display GetDisplay(int index, string? deviceName = null)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = screens.FirstOrDefault(s => s.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase)) ?? screens[Math.Clamp(index, 0, screens.Length - 1)];
        var bounds = screen.Bounds;
        var monitor = MonitorFromPoint(new Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 }, 2);
        var scale = GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? Math.Max(96, dpi) / 96d : 1;
        return new(bounds, screen.WorkingArea, scale);
    }
    internal static bool Material(Window window, string material, bool light)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return false;
        HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
        int dark = light ? 0 : 1, noBorder = unchecked((int)0xFFFFFFFE), square = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
        DwmSetWindowAttribute(hwnd, 34, ref noBorder, 4);
        DwmSetWindowAttribute(hwnd, 33, ref square, 4);
        int backdrop = material switch { "Acrylic" => 3, "Mica" => 2, _ => 1 };
        return DwmSetWindowAttribute(hwnd, 38, ref backdrop, 4) == 0 && backdrop != 1;
    }
    internal static void Move(nint hwnd, PixelRect rect)
    {
        SetWindowPos(hwnd, 0, rect.Left, rect.Top, Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top), 0x0014);
    }
    internal static void Clip(nint hwnd, int width, int height, double radius, bool attachedTop)
    {
        int corner = (int)Math.Clamp(radius * 2, 0, Math.Min(width, height));
        var region = corner == 0 ? CreateRectRgn(0, 0, width, height) : CreateRoundRectRgn(0, 0, width + 1, height + 1, corner, corner);
        if (region == 0) return;
        if (attachedTop && corner > 0)
        {
            var top = CreateRectRgn(0, 0, width, Math.Min(height / 2 + 1, corner / 2 + 1));
            if (top != 0) { CombineRgn(region, region, top, 2); DeleteObject(top); }
        }
        // SetWindowRgn transfers ownership of the HRGN on success.
        if (SetWindowRgn(hwnd, region, true) == 0) DeleteObject(region);
    }
}
