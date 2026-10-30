using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace ThinkWind.Native;

// Adjust only native maximized application windows. Never changes SPI work area,
// registers an AppBar, or takes foreground focus. Incompatible custom maximized
// frames can use a normal, monitor-sized compatibility rectangle.
internal sealed class MaximizedWindowInset : IDisposable
{
    private sealed record Adjustment(uint Process, OverlayChrome.PixelRect Original, OverlayChrome.PixelRect Applied, int FrameTop, bool Negotiated = false, bool Compatibility = false, int AlignmentPasses = 0)
    {
        internal DateTime RequestedAt { get; } = DateTime.UtcNow;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<nint, Adjustment> adjusted = new();
    private bool updating;
    private delegate bool EnumProc(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint window);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(nint window, int command);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int count);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int GetFrame(nint window, int attribute, out OverlayChrome.PixelRect value, int size);
    private static bool Same(OverlayChrome.PixelRect a, OverlayChrome.PixelRect b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    internal static bool IsAdjustedWindow(nint window) => adjusted.TryGetValue(window, out var adjustment)
        && !IsIconic(window) && GetWindowThreadProcessId(window, out uint process) != 0 && process == adjustment.Process
        && OverlayChrome.GetWindowRect(window, out var current) && MatchesAdjustment(window, current, adjustment);
    private static bool RetainMinimized(nint window)
    {
        if (!adjusted.TryGetValue(window, out var adjustment) || !IsIconic(window)
            || GetWindowThreadProcessId(window, out uint process) == 0 || process != adjustment.Process) return false;
        // Minimization hides the application, but does not cancel its logical
        // maximized/compatibility state. Re-measure its frame after restoring.
        adjusted.TryUpdate(window, adjustment with { AlignmentPasses = 0 }, adjustment);
        return true;
    }
    private static bool MatchesAdjustment(nint window, OverlayChrome.PixelRect current, Adjustment adjustment)
    {
        if (Same(current, adjustment.Applied)) return true;
        if (!adjustment.Compatibility) return false;
        // Normal custom frames can clamp the requested size to their monitor
        // and add DPI-sized nonclient margins. Keep logical maximization while
        // the queued restore/resize settles, then require matching geometry.
        if (DateTime.UtcNow - adjustment.RequestedAt < TimeSpan.FromMilliseconds(750)) return true;
        int tolerance = Math.Max(2, (int)Math.Ceiling(32 * Math.Max(96u, GetDpiForWindow(window)) / 96d));
        return Math.Abs((long)current.Left - adjustment.Applied.Left) <= tolerance
            && Math.Abs((long)current.Top - adjustment.Applied.Top) <= tolerance
            && Math.Abs((long)current.Right - adjustment.Applied.Right) <= tolerance
            && Math.Abs((long)current.Bottom - adjustment.Applied.Bottom) <= tolerance;
    }
    private static bool Move(nint window, OverlayChrome.PixelRect rectangle, bool negotiate = false)
    {
        // DefWindowProc / browser WM_WINDOWPOSCHANGING handlers can replace a
        // maximized rectangle with the monitor's full work area. Suppress that
        // pre-change negotiation, while still delivering WM_WINDOWPOSCHANGED
        // so the browser lays out its tabs and contents at the requested size.
        uint flags = 0x4000 /* ASYNCWINDOWPOS */
            | 0x0020 /* FRAMECHANGED: recalculate client bounds */
            | 0x0200 /* NOOWNERZORDER */ | 0x0010 /* NOACTIVATE */ | 0x0004 /* NOZORDER */;
        if (!negotiate) flags |= 0x0400 /* NOSENDCHANGING */;
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
                if (!IsWindowVisible(window) || IsIconic(window)) return true;
                int style = GetWindowLong(window, -20);
                bool appWindow = (style & 0x40000) != 0;
                if (!appWindow && (GetWindow(window, 4) != 0 || (style & 0x80) != 0)) return true;
                GetWindowThreadProcessId(window, out uint process);
                if (process == 0 || process == Environment.ProcessId) return true;
                if (DwmGetWindowAttribute(window, 14, out int cloaked, 4) >= 0 && cloaked != 0) return true;
                if (System.Windows.Forms.Screen.FromHandle(window).Bounds != display.Bounds) return true;
                var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
                if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
                if (!OverlayChrome.GetWindowRect(window, out var current)) return true;
                bool fillsWork = current.Left <= display.Work.Left + 2 && current.Right >= display.Work.Right - 2
                    && current.Top <= display.Work.Top + 2 && current.Bottom >= display.Work.Bottom - 2;
                if (!IsZoomed(window) && !fillsWork && !IsAdjustedWindow(window)) return true;
                seen.Add(window);
                bool tracked = adjusted.TryGetValue(window, out var previous) && previous.Process == process && MatchesAdjustment(window, current, previous);
                // Let asynchronous normalization finish before measuring the
                // new frame; otherwise a transient rectangle becomes the target.
                if (tracked && previous!.Compatibility && !Same(current, previous.Applied)
                    && DateTime.UtcNow - previous.RequestedAt < TimeSpan.FromMilliseconds(750)) return true;
                var original = tracked ? previous!.Original : current;
                // Measure the invisible frame once, before moving. Reusing it
                // avoids cumulative offsets and aligns the visible window edge.
                int frameTop = tracked ? previous!.FrameTop : 0;
                if (!tracked && GetFrame(window, 9, out var frame, Marshal.SizeOf<OverlayChrome.PixelRect>()) >= 0)
                    frameTop = Math.Clamp(frame.Top - current.Top, 0, (int)Math.Ceiling(16 * display.Scale));
                var target = original;
                if (tracked && previous!.Compatibility)
                {
                    // Accept the application's actual normal-frame width and
                    // bottom rather than repeatedly asking for maximized borders.
                    target.Left = current.Left; target.Right = current.Right; target.Bottom = current.Bottom;
                }
                target.Top = Math.Max(display.Work.Top, display.Bounds.Top + height) - frameTop;
                int alignmentPasses = tracked ? previous!.AlignmentPasses : 0;
                // DWM margins can change after the application recalculates its
                // frame. Correct measured visible bounds, with a strict cap.
                if (tracked && alignmentPasses < 2
                    && GetFrame(window, 9, out var visible, Marshal.SizeOf<OverlayChrome.PixelRect>()) >= 0)
                {
                    int gap = visible.Top - Math.Max(display.Work.Top, display.Bounds.Top + height);
                    if (Math.Abs(gap) > 1 && Math.Abs(gap) <= (int)Math.Ceiling(32 * display.Scale))
                    {
                        target.Top = current.Top - gap;
                        frameTop = Math.Max(display.Work.Top, display.Bounds.Top + height) - target.Top;
                        alignmentPasses++;
                    }
                }
                if (target.Bottom - target.Top < 80) return true;
                // Some custom frames (including Illustrator) keep a client
                // origin above the moved HWND. On the following update, allow
                // their normal sizing negotiation once, not on every tick.
                bool negotiated = previous is not null && previous.Process == process && previous.Negotiated;
                bool compatibility = previous is not null && previous.Process == process && previous.Compatibility;
                var clientOrigin = new Point();
                // Only an origin outside the HWND indicates a broken custom
                // frame; an ordinary invisible border must not trigger this.
                bool staleClient = tracked && ClientToScreen(window, ref clientOrigin)
                    && current.Top - clientOrigin.Y > Math.Max(16 * display.Scale, height / 2d);
                if (staleClient && !negotiated)
                {
                    if (Move(window, target, negotiate: true))
                        adjusted[window] = new(process, original, target, frameTop, true, compatibility, alignmentPasses);
                    return true;
                }
                if (staleClient && negotiated && !compatibility && IsZoomed(window))
                {
                    // Custom applications that insist on the full monitor while
                    // zoomed need a normal-size frame. Queue both operations in
                    // their UI thread without activation or global work-area edits.
                    if (ShowWindowAsync(window, 4 /* SHOWNOACTIVATE */) && Move(window, target, negotiate: true))
                        adjusted[window] = new(process, original, target, frameTop, true, true, alignmentPasses);
                    return true;
                }
                // First observation also refreshes a stale client area left by
                // an earlier instance, even when the HWND already has this size.
                if ((!tracked || !Same(current, target)) && Move(window, target))
                    adjusted[window] = new(process, original, target, frameTop, negotiated, compatibility, alignmentPasses);
                return true;
            }, 0);
            // Respect a user's restore/minimize/move; do not re-maximize it.
            foreach (nint old in adjusted.Keys.Where(window => !seen.Contains(window)).ToArray())
                if (!RetainMinimized(old) && adjusted.TryRemove(old, out var previous)) RestoreOne(old, previous);
        }
        finally { updating = false; }
    }
    private static void RestoreOne(nint window, Adjustment adjustment)
    {
        GetWindowThreadProcessId(window, out uint process);
        if (process == adjustment.Process && IsWindowVisible(window) && !IsIconic(window)
            && OverlayChrome.GetWindowRect(window, out var current) && MatchesAdjustment(window, current, adjustment)) Move(window, adjustment.Original);
    }
    private void Restore(bool retainMinimized = true)
    {
        foreach (nint window in adjusted.Keys.ToArray())
            if (!(retainMinimized && RetainMinimized(window)) && adjusted.TryRemove(window, out var previous)) RestoreOne(window, previous);
    }
    public void Dispose() => Restore(retainMinimized: false);
}
