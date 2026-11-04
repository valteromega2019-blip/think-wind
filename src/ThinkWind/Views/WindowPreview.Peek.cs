using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;

internal sealed partial class WindowPreview
{
    private readonly DesktopWindowPeek desktopPeek = new();
    private readonly DispatcherTimer peekDelay = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private nint peekTarget;
    private FrameworkElement? peekArea;
    private void InitializePeek()
    {
        peekDelay.Tick += (_, _) =>
        {
            peekDelay.Stop();
            // Native DWM thumbnail HWNDs can make WPF's IsMouseOver false.
            // Use the physical popup bounds instead of rejecting their hover.
            if (IsVisible && peekTarget != 0 && !WindowActionInteracting && PeekAreaContainsPointer())
            {
                var color = (TryFindResource("Think.Accent") as SolidColorBrush)?.Color ?? Colors.White;
                OverlayChrome.PixelRect? protectedDock = null;
                if (Owner is DockWindow dock && dock.IsVisible && OverlayChrome.GetWindowRect(dock.BackdropHandle, out var dockBounds))
                {
                    // Include a small sampling margin around Acrylic's surface.
                    dockBounds.Left -= 24; dockBounds.Right += 24;
                    dockBounds.Top -= 24; dockBounds.Bottom += 24;
                    protectedDock = dockBounds;
                }
                desktopPeek.Show(peekTarget, color.R | color.G << 8 | color.B << 16, protectedDock);
            }
            else EndWindowPeek();
        };
        PreviewMouseDown += (_, _) => EndWindowPeek();
        PreviewKeyDown += (_, _) => EndWindowPeek();
        MouseLeave += (_, _) => { if (!PointerInside) EndWindowPeek(); };
        Closed += (_, _) => EndWindowPeek();
    }
    private bool PeekAreaContainsPointer()
    {
        if (peekArea is not { IsLoaded: true, IsVisible: true } || !PointerInside) return false;
        var cursor = System.Windows.Forms.Cursor.Position;
        var hit = InputHitTest(PointFromScreen(new Point(cursor.X, cursor.Y))) as DependencyObject;
        while (hit is not null)
        {
            if (ReferenceEquals(hit, peekArea)) return true;
            hit = hit is Visual ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit);
        }
        return false;
    }
    private void BeginWindowPeek(nint window, FrameworkElement area)
    {
        if (peekTarget == window && ReferenceEquals(peekArea, area)) return;
        EndWindowPeek(); peekTarget = window; peekArea = area; peekDelay.Start();
    }
    private void EndWindowPeek()
    {
        peekDelay.Stop(); peekTarget = 0; peekArea = null; desktopPeek.Dispose();
    }
    private void AttachWindowPeek(FrameworkElement area, nint window)
    {
        area.MouseEnter += (_, _) => BeginWindowPeek(window, area);
        area.MouseLeave += (_, _) => EndWindowPeek();
        area.ContextMenuOpening += (_, _) => EndWindowPeek();
    }
}
