using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// The preferred scale comes from the island; the final slot is a hard bound.
// The island measures the board once and owns both the frame and content sizes.
internal sealed class IslandModuleViewport : Decorator
{
    private Size logicalSize = new(760, 1);
    private readonly ScaleTransform scale = new(1, 1);
    private double preferredScale = 1;
    private double measuredHeight = 1;
    internal void Configure(double width, double height, double factor)
    {
        var next = new Size(Math.Max(1, width), Math.Max(1, height));
        factor = Math.Clamp(factor, .01, 1);
        if (logicalSize == next && preferredScale == factor) return;
        logicalSize = next;
        preferredScale = factor;
        InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return new Size();
        // Keep natural content height unconstrained during measurement.
        Child.Measure(new Size(logicalSize.Width, double.PositiveInfinity));
        double nextHeight = Math.Max(logicalSize.Height, Math.Ceiling(Child.DesiredSize.Height));
        if (nextHeight != measuredHeight) InvalidateArrange();
        measuredHeight = nextHeight;
        double availableWidth = double.IsFinite(Width) ? Width : logicalSize.Width * preferredScale;
        double availableHeight = double.IsFinite(Height) ? Height : measuredHeight * preferredScale;
        return new Size(Math.Min(availableWidth, logicalSize.Width * preferredScale),
            Math.Min(availableHeight, measuredHeight * preferredScale));
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is FrameworkElement content)
        {
            double width = double.IsFinite(Width) ? Math.Min(finalSize.Width, Width) : finalSize.Width;
            double height = double.IsFinite(Height) ? Math.Min(finalSize.Height, Height) : finalSize.Height;
            double fit = Math.Max(0, Math.Min(preferredScale,
                Math.Min(width / logicalSize.Width, height / measuredHeight)));
            scale.ScaleX = scale.ScaleY = fit;
            content.RenderTransformOrigin = new Point(0, 0);
            if (!ReferenceEquals(content.RenderTransform, scale)) content.RenderTransform = scale;
            content.Arrange(new Rect(0, 0, logicalSize.Width, measuredHeight));
        }
        return finalSize;
    }
}