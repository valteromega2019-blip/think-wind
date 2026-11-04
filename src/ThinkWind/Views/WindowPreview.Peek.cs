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
    private void InitializePeek()
    {
        peekDelay.Tick += (_, _) =>
        {
            peekDelay.Stop();
            // Native DWM thumbnail HWNDs can make WPF's IsMouseOver false.
            // Use the physical popup bounds instead of rejecting their hover.
            if (IsVisible && peekTarget != 0 && !WindowActionInteracting && PointerInside)
            {
                var color = (TryFindResource("Think.Accent") as SolidColorBrush)?.Color ?? Colors.White;
                desktopPeek.Show(peekTarget, color.R | color.G << 8 | color.B << 16);
            }
        };
        PreviewMouseDown += (_, _) => EndWindowPeek();
        PreviewKeyDown += (_, _) => EndWindowPeek();
        MouseLeave += (_, _) => EndWindowPeek();
        Closed += (_, _) => EndWindowPeek();
    }
    private void BeginWindowPeek(nint window)
    {
        if (peekTarget == window) return;
        EndWindowPeek(); peekTarget = window; peekDelay.Start();
    }
    private void EndWindowPeek()
    {
        peekDelay.Stop(); peekTarget = 0; desktopPeek.Dispose();
    }
    private void AttachWindowPeek(FrameworkElement area, nint window)
    {
        area.MouseEnter += (_, _) => BeginWindowPeek(window);
        area.MouseLeave += (_, _) => EndWindowPeek();
        area.ContextMenuOpening += (_, _) => EndWindowPeek();
    }
}
