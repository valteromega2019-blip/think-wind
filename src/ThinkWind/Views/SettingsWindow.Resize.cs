using System;
using System.Windows;
using System.Windows.Interop;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private bool manualResize;
    private void EnableManualResize()
    {
        ResizeMode = ResizeMode.CanResize;
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(ResizeMessage);
    }
    private nint ResizeMessage(nint hwnd, int message, nint wp, nint lp, ref bool handled)
    {
        if (message == 0x231) manualResize = true;
        if (message == 0x232) { manualResize = false; UpdateClip(); }
        if (message != 0x84 || !OverlayChrome.GetWindowRect(hwnd, out var rectangle)) return 0;
        int x = unchecked((short)(lp.ToInt64() & 0xffff)), y = unchecked((short)((lp.ToInt64() >> 16) & 0xffff));
        double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? Display.Scale;
        int edge = (int)Math.Ceiling(9 * scale);
        bool left = x < rectangle.Left + edge, right = x >= rectangle.Right - edge;
        bool top = y < rectangle.Top + edge, bottom = y >= rectangle.Bottom - edge;
        int hit = top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 0;
        if (hit == 0) return 0;
        handled = true; return hit;
    }
}
