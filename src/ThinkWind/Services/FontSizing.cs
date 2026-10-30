using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ThinkWind.Services;

internal static class FontSizing
{
    private sealed class Metrics
    {
        internal double Font = double.NaN, Line = double.NaN, Height = double.NaN;
    }
    private static readonly ConditionalWeakTable<DependencyObject, Metrics> originals = new();
    private static readonly DependencyProperty ScaleProperty = DependencyProperty.RegisterAttached(
        "Scale", typeof(double), typeof(FontSizing), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.Inherits));
    static FontSizing()
    {
        // Newly created cards and template text also receive the current scale.
        EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => ApplyElement((DependencyObject)sender)));
    }
    internal static void Apply(Window window, double scale)
    {
        if (window.ReadLocalValue(ScaleProperty) is double previous && previous == scale) return;
        window.SetValue(ScaleProperty, scale);
        window.FontSize = SystemFonts.MessageFontSize * scale;
        Walk(window);
    }
    private static void Walk(DependencyObject node)
    {
        ApplyElement(node);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) Walk(VisualTreeHelper.GetChild(node, i));
    }
    private static void ApplyElement(DependencyObject element)
    {
        if (element is Window) return;
        double scale = (double)element.GetValue(ScaleProperty);
        DependencyProperty font;
        FontFamily family;
        if (element is TextBlock text) { font = TextBlock.FontSizeProperty; family = text.FontFamily; }
        else if (element is Control control) { font = Control.FontSizeProperty; family = control.FontFamily; }
        else return;
        // Symbol fonts are icons, not user text. Inherited font sizes already
        // follow the window; scale only explicit numeric sizes once.
        if (family.Source.Contains("Icons", StringComparison.OrdinalIgnoreCase)
            || family.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase)) return;
        var metrics = originals.GetValue(element, _ => new Metrics());
        if (double.IsNaN(metrics.Font) && element.ReadLocalValue(font) is double size) metrics.Font = size;
        if (!double.IsNaN(metrics.Font)) element.SetCurrentValue(font, metrics.Font * scale);
        if (element is TextBlock block)
        {
            if (double.IsNaN(metrics.Line) && block.ReadLocalValue(TextBlock.LineHeightProperty) is double line && double.IsFinite(line)) metrics.Line = line;
            if (double.IsNaN(metrics.Height) && block.ReadLocalValue(FrameworkElement.HeightProperty) is double height && double.IsFinite(height)) metrics.Height = height;
            if (!double.IsNaN(metrics.Line)) block.SetCurrentValue(TextBlock.LineHeightProperty, metrics.Line * scale);
            if (!double.IsNaN(metrics.Height)) block.SetCurrentValue(FrameworkElement.HeightProperty, metrics.Height * scale);
        }
    }
}
