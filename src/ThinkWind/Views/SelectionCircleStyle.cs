using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Preserve CheckBox semantics, bindings and UI Automation TogglePattern while
// replacing only the visual indicator with the launcher's ON/OFF language.
internal static class SelectionCircleStyle
{
    internal static Style Create(string? glyph = null)
    {
        var root = new FrameworkElementFactory(typeof(DockPanel));
        root.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
        var circle = new FrameworkElementFactory(typeof(Border), "Indicator");
        circle.SetValue(DockPanel.DockProperty, Dock.Left);
        circle.SetValue(FrameworkElement.WidthProperty, 36d); circle.SetValue(FrameworkElement.HeightProperty, 36d);
        circle.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        circle.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(18)); circle.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
        circle.SetResourceReference(Border.BackgroundProperty, "Think.Control"); circle.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
        var fit = new FrameworkElementFactory(typeof(Viewbox)); fit.SetValue(FrameworkElement.MarginProperty, new Thickness(5)); fit.SetValue(Viewbox.StretchDirectionProperty, StretchDirection.DownOnly);
        var state = new FrameworkElementFactory(typeof(TextBlock), "State"); state.SetValue(TextBlock.TextProperty, glyph ?? "OFF");
        state.SetValue(TextBlock.FontSizeProperty, glyph is null ? 10d : 18d); state.SetValue(TextBlock.FontWeightProperty, glyph is null ? FontWeights.SemiBold : FontWeights.Normal);
        if (glyph is not null) state.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"));
        state.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); fit.AppendChild(state); circle.AppendChild(fit); root.AppendChild(circle);
        var label = new FrameworkElementFactory(typeof(ContentPresenter));
        label.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        label.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); root.AppendChild(label);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = root };
        var on = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Think.Accent"), "Indicator"));
        on.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"), "Indicator"));
        if (glyph is null) on.Setters.Add(new Setter(TextBlock.TextProperty, "ON", "State"));
        on.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension(glyph is null ? "Think.Background" : "Think.AccentInk"), "State")); template.Triggers.Add(on);
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"), "Indicator")); template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"), "Indicator"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(3), "Indicator")); template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .4)); template.Triggers.Add(disabled);
        var style = new Style(typeof(CheckBox));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 36d));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Think.Text")));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return style;
    }
}
