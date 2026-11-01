using System;
using System.Windows;
using System.Windows.Controls;

namespace OrbyLauncher.Views;
// The native window reveals a destination-sized layout. Intermediate HWND
// dimensions must never become constraints for Viewbox/DockPanel children.
internal sealed class IslandLayoutHost : Decorator
{
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is not FrameworkElement content) return new Size();
        double width = double.IsFinite(content.Width) ? content.Width : Math.Max(1, constraint.Width);
        double height = double.IsFinite(content.Height) ? content.Height : double.PositiveInfinity;
        content.Measure(new Size(width, height));
        return new Size(width, double.IsFinite(height) ? height : content.DesiredSize.Height);
    }
    protected override Size ArrangeOverride(Size size)
    {
        if (Child is FrameworkElement content)
        {
            double width = double.IsFinite(content.Width) ? content.Width : content.DesiredSize.Width;
            double height = double.IsFinite(content.Height) ? content.Height : content.DesiredSize.Height;
            content.Arrange(new Rect((size.Width - width) / 2, 0, Math.Max(1, width), Math.Max(1, height)));
        }
        return size;
    }
}
