using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ThinkWind.Native;

// Adjust only native maximized application windows. Never changes SPI work area,
// registers an AppBar, restores a window, or takes foreground focus.
internal sealed class MaximizedWindowInset : IDisposable
{
    private sealed record Adjustment(uint Process, OverlayChrome.PixelRect Original, OverlayChrome.PixelRect Applied);
    private readonly Dictionary<nint, Adjustment> adjusted = new();
    private bool updating;
    private delegate bool EnumProc(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int count);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetFrameBounds(nint window, int attribute, out OverlayChrome.PixelRect value, int size);
    private static bool Same(OverlayChrome.PixelRect a, OverlayChrome.PixelRect b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    private static bool Move(nint window, OverlayChrome.PixelRect rectangle)
    {
        // DefWindowProc / browser WM_WINDOWPOSCHANGING handlers can replace a
        // maximized rectangle with the monitor's full work area. Suppress that
        // pre-change negotiation, while still delivering WM_WINDOWPOSCHANGED
        // so the browser lays out its tabs and contents at the requested size.
        const uint flags = 0x4000 /* ASYNCWINDOWPOS */ | 0x0400 /* NOSENDCHANGING */
            | 0x0200 /* NOOWNERZORDER */ | 0x0010 /* NOACTIVATE */ | 0x0004 /* NOZORDER */;
        return SetWindowPos(window, 0, rectangle.Left, rectangle.Top,
            rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, flags);
    }
    internal void Apply(OverlayChrome.Display display, int height)
    {
        if (updating) return;
        updating = true;
        try
        {
            if (height <= 0) { Restore(); return; }
            var seen = new HashSet<nint>();
            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window) || IsIconic(window) || !IsZoomed(window)
                    || GetWindow(window, 4) != 0 || (GetWindowLong(window, -20) & 0x80) != 0) return true;
                GetWindowThreadProcessId(window, out uint process);
                if (process == 0 || process == Environment.ProcessId) return true;
                if (DwmGetWindowAttribute(window, 14, out int cloaked, 4) >= 0 && cloaked != 0) return true;
                if (System.Windows.Forms.Screen.FromHandle(window).Bounds != display.Bounds) return true;
                var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
                if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
                if (!OverlayChrome.GetWindowRect(window, out var current)) return true;
                seen.Add(window);
                var original = adjusted.TryGetValue(window, out var previous) && previous.Process == process && Same(current, previous.Applied)
                    ? previous.Original : current;
                // Preserve invisible frame margins and all three other edges.
                int frameTop = DwmGetFrameBounds(window, 9, out var visibleFrame, Marshal.SizeOf<OverlayChrome.PixelRect>()) >= 0
                    ? Math.Min(0, current.Top - visibleFrame.Top) : Math.Min(0, original.Top - display.Work.Top);
                var target = original;
                target.Top = Math.Max(display.Work.Top, display.Bounds.Top + height) + frameTop;
                if (target.Bottom - target.Top < 80) return true;
                if (!Same(current, target) && Move(window, target)) adjusted[window] = new(process, original, target);
                return true;
            }, 0);
            // Respect a user's restore/minimize/move; do not re-maximize it.
            foreach (nint old in adjusted.Keys.Where(window => !seen.Contains(window)).ToArray()) adjusted.Remove(old);
        }
        finally { updating = false; }
    }
    private void Restore()
    {
        foreach (var pair in adjusted)
        {
            GetWindowThreadProcessId(pair.Key, out uint process);
            if (process == pair.Value.Process && IsWindowVisible(pair.Key) && IsZoomed(pair.Key) && !IsIconic(pair.Key)
                && OverlayChrome.GetWindowRect(pair.Key, out var current) && Same(current, pair.Value.Applied)) Move(pair.Key, pair.Value.Original);
        }
        adjusted.Clear();
    }
    public void Dispose() => Restore();
}
