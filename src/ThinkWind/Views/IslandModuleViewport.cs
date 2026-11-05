using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Unlike a Viewbox, this viewport never chooses another scale while arranging.
// The island measures the board once and owns both the frame and content sizes.
internal sealed class IslandModuleViewport : Decorator
{
    private Size logicalSize = new(760, 1);
    private readonly ScaleTransform scale = new(1, 1);
    internal void Configure(double width, double height, double factor)
    {
        var next = new Size(Math.Max(1, width), Math.Max(1, height));
        factor = Math.Clamp(factor, .01, 1);
        if (logicalSize == next && scale.ScaleX == factor) return;
        logicalSize = next;
        scale.ScaleX = scale.ScaleY = factor;
        InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size constraint)
    {
        if (Child is null) return new Size();
        // Keep natural content height unconstrained during measurement.
        Child.Measure(new Size(logicalSize.Width, double.PositiveInfinity));
        return new Size(logicalSize.Width * scale.ScaleX, logicalSize.Height * scale.ScaleY);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is FrameworkElement content)
        {
            content.RenderTransformOrigin = new Point(0, 0);
            if (!ReferenceEquals(content.RenderTransform, scale)) content.RenderTransform = scale;
            content.Arrange(new Rect(new Point(), logicalSize));
        }
        return finalSize;
    }
}