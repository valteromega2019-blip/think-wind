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
    private Size arrangedSize = new(760, 1);
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
        if (Child is not FrameworkElement content) return new Size();
        double availableWidth = double.IsFinite(Width) ? Width : logicalSize.Width * preferredScale;
        double availableHeight = double.IsFinite(Height) ? Height : logicalSize.Height * preferredScale;
        double width = logicalSize.Width, height = logicalSize.Height, fit = preferredScale;
        // Height fitting must also redistribute the logical width. Merely
        // shrinking the transform leaves an empty strip at the right edge.
        for (int pass = 0; pass < 6; pass++)
        {
            content.Width = width;
            content.Measure(new Size(width, double.PositiveInfinity));
            height = Math.Max(logicalSize.Height, Math.Ceiling(content.DesiredSize.Height));
            fit = Math.Min(preferredScale, Math.Min(availableWidth / width, availableHeight / height));
            double neededWidth = fit > 0 ? availableWidth / fit : width;
            if (neededWidth <= width + .1 || pass == 5) break;
            width = neededWidth;
        }
        var next = new Size(width, height);
        if (next != arrangedSize) InvalidateArrange();
        arrangedSize = next;
        return new Size(Math.Min(availableWidth, width * fit), Math.Min(availableHeight, height * fit));
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is FrameworkElement content)
        {
            double width = double.IsFinite(Width) ? Math.Min(finalSize.Width, Width) : finalSize.Width;
            double height = double.IsFinite(Height) ? Math.Min(finalSize.Height, Height) : finalSize.Height;
            double fit = Math.Max(0, Math.Min(preferredScale,
                Math.Min(width / arrangedSize.Width, height / arrangedSize.Height)));
            scale.ScaleX = scale.ScaleY = fit;
            content.RenderTransformOrigin = new Point(0, 0);
            if (!ReferenceEquals(content.RenderTransform, scale)) content.RenderTransform = scale;
            content.Arrange(new Rect(new Point(), arrangedSize));
        }
        return finalSize;
    }
}