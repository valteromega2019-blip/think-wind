using System;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace OrbyLauncher.Native;

// Only a DWM representation: never restore, activate, move or restack the source.
internal sealed class DesktopWindowPeek : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Placement
    {
        internal int Length, Flags, Show;
        internal NativePoint Minimum, Maximum;
        internal OverlayChrome.PixelRect Normal;
    }
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    private WindowThumbnail? image;
    internal void Show(nint source, int outlineColor)
    {
        Dispose();
        if (!Desktop.WindowExists(source) || !OverlayChrome.GetWindowRect(source, out var bounds)) return;
        if (IsIconic(source))
        {
            var placement = new Placement { Length = Marshal.SizeOf<Placement>() };
            if (!GetWindowPlacement(source, ref placement)) return;
            var screen = Forms.Screen.FromHandle(source);
            if ((placement.Flags & 2) != 0)
            {
                var work = screen.WorkingArea;
                bounds = new() { Left = work.Left, Top = work.Top, Right = work.Right, Bottom = work.Bottom };
            }
            else
            {
                bounds = placement.Normal;
                // Normal placements use workspace coordinates except tool windows.
                if ((GetWindowLong(source, -20) & 0x80) == 0)
                {
                    int x = screen.WorkingArea.Left - screen.Bounds.Left, y = screen.WorkingArea.Top - screen.Bounds.Top;
                    bounds.Left += x; bounds.Right += x; bounds.Top += y; bounds.Bottom += y;
                }
            }
        }
        int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
        if (width < 32 || height < 32 || width > 16384 || height > 16384) return;
        try
        {
            image = new WindowThumbnail(0, source, clickThrough: true, outlineColor: outlineColor);
            // HWND_NOTOPMOST: above ordinary apps but below every launcher
            // card/material/thumbnail, not between the alpha content and Acrylic.
            if (!image.Available || !image.Place(new Point(bounds.Left, bounds.Top), new Size(width, height), new nint(-2))) Dispose();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or ExternalException or InvalidOperationException)
        { Dispose(); }
    }
    public void Dispose() { image?.Dispose(); image = null; }
}
