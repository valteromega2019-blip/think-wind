using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class TutorialWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { internal int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out NativeRect bounds);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);

    private static Rect BoundsOf(FrameworkElement element)
    {
        var first = element.PointToScreen(new Point());
        var last = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new Rect(first, last);
    }
    private void PlaceBeside(FrameworkElement? target)
    {
        // Use the entire newly opened panel, not the small button that opened it.
        Rect? hintBounds = target is not null ? BoundsOf(target) : lastGuideBounds;
        string? device = hintBounds is Rect hint
            ? System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(hint.Left + hint.Width / 2), (int)(hint.Top + hint.Height / 2))).DeviceName : null;
        var visible = Application.Current.Windows.Cast<Window>().Where(window => window.IsVisible && window != this)
            .OrderByDescending(window => device is not null && System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(window).Handle).DeviceName == device).ToArray();
        Window? panel = null;
        if (progress.Step is 1 or 2) panel = visible.OfType<LauncherPanelWindow>().FirstOrDefault();
        string? targetKey = progress.Step >= 0 && progress.Step < Steps.Length ? Steps[progress.Step].Target : null;
        if (targetKey is "island" or "expand" or "widget-editor" or "widget-resize" or "widget-edit-finish" or "monitor" or "optimizer" or "optimizer-analyze" or "customize")
        {
            panel = visible.OfType<IslandWindow>().FirstOrDefault(window => window.Stage != IslandStage.Compact);
            if (targetKey is "monitor" or "optimizer" or "optimizer-analyze") panel = visible.OfType<EmptyIslandAreaWindow>().FirstOrDefault() ?? panel;
        }
        if (targetKey is "appearance" or "fixed-bar" || targetKey == "customize" && success) panel = visible.OfType<SettingsWindow>().FirstOrDefault() ?? panel;
        if (panel is not null && PresentationSource.FromVisual(panel) is not null) lastGuideBounds = BoundsOf(panel);
        else if (target is not null) lastGuideBounds = BoundsOf(target);

        var screen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
        if (lastGuideBounds is Rect pixels)
            screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)(pixels.Left + pixels.Width / 2), (int)(pixels.Top + pixels.Height / 2)));
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var work = new Rect(transform.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top)), transform.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom)));
        work.Inflate(-12, -12);
        Width = Math.Min(350, Math.Max(200, work.Width)); MaxHeight = Math.Max(120, work.Height);
        // Measure new text before placing it; the previous step's height can differ.
        card.Measure(new Size(Width, MaxHeight));
        double height = Math.Min(MaxHeight, Math.Max(1, card.DesiredSize.Height));
        Point Clamp(Point point) => new(Math.Clamp(point.X, work.Left, Math.Max(work.Left, work.Right - Width)), Math.Clamp(point.Y, work.Top, Math.Max(work.Top, work.Bottom - height)));
        Point chosen;
        if (lastGuideBounds is Rect bounds)
        {
            var area = new Rect(transform.Transform(bounds.TopLeft), transform.Transform(bounds.BottomRight));
            const double gap = 16;
            var candidates = new[]
            {
                new Point(area.Right + gap, area.Top), new Point(area.Left - Width - gap, area.Top),
                new Point(area.Left, area.Bottom + gap), new Point(area.Left, area.Top - height - gap),
                new Point(work.Left, work.Top), new Point(work.Right - Width, work.Top),
                new Point(work.Left, work.Bottom - height), new Point(work.Right - Width, work.Bottom - height)
            };
            // Prefer the sides, then above/below. On very small screens choose
            // the available position with the least overlap rather than going off-screen.
            chosen = candidates.Select(Clamp).Select((point, index) =>
            {
                var overlap = Rect.Intersect(new Rect(point, new Size(Width, height)), area);
                return new { Point = point, Index = index, Overlap = overlap.IsEmpty ? 0 : overlap.Width * overlap.Height };
            }).OrderBy(candidate => candidate.Overlap).ThenBy(candidate => candidate.Index).First().Point;
        }
        else chosen = Clamp(new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - height) / 2));
        if (!double.IsFinite(Left) || Math.Abs(Left - chosen.X) > .5) Left = chosen.X;
        if (!double.IsFinite(Top) || Math.Abs(Top - chosen.Y) > .5) Top = chosen.Y;
    }
    private void EnsureAbovePanels()
    {
        if (!IsVisible || blocked()) return;
        nint handle = new WindowInteropHelper(this).Handle;
        if (handle == 0 || !GetWindowRect(handle, out var own)) return;
        var ownBounds = new Rect(own.Left, own.Top, Math.Max(0, own.Right - own.Left), Math.Max(0, own.Bottom - own.Top));
        // Topmost alone does not order us above other topmost windows. Raise only
        // when something above actually overlaps, without activating or taking focus.
        nint above = GetWindow(handle, 3); // GW_HWNDPREV
        for (int count = 0; above != 0 && count < 128; count++, above = GetWindow(above, 3))
        {
            if (!IsWindowVisible(above) || !GetWindowRect(above, out var other)) continue;
            var otherBounds = new Rect(other.Left, other.Top, Math.Max(0, other.Right - other.Left), Math.Max(0, other.Bottom - other.Top));
            if (!ownBounds.IntersectsWith(otherBounds)) continue;
            SetWindowPos(handle, new nint(-1), 0, 0, 0, 0, 0x0213); // TOPMOST, NOACTIVATE, NOMOVE, NOSIZE, NOOWNERZORDER
            break;
        }
    }
}
