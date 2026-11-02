using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Keep existing CheckBox state/events/accessibility and replace only its visual.
internal static class SelectionCircleStyle
{
    internal static Style Create(string? glyph = null)
    {
        var root = new FrameworkElementFactory(typeof(DockPanel));
        root.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
        var indicator = new FrameworkElementFactory(typeof(LiquidToggle), "Indicator");
        indicator.SetValue(DockPanel.DockProperty, Dock.Left);
        indicator.SetValue(FrameworkElement.WidthProperty, 48d); indicator.SetValue(FrameworkElement.HeightProperty, 24d);
        indicator.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        indicator.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 10, 0));
        indicator.SetValue(UIElement.FocusableProperty, false); indicator.SetValue(UIElement.IsHitTestVisibleProperty, false);
        indicator.SetValue(LiquidToggle.GlyphProperty, glyph);
        indicator.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsChecked") { RelativeSource = RelativeSource.TemplatedParent, Mode = BindingMode.OneWay });
        root.AppendChild(indicator);
        var label = new FrameworkElementFactory(typeof(ContentPresenter));
        label.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        label.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(ContentControl.ContentTemplateProperty));
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); root.AppendChild(label);
        var template = new ControlTemplate(typeof(CheckBox)) { VisualTree = root };
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(LiquidToggle.FocusIndicatorProperty, true, "Indicator")); template.Triggers.Add(focus);
        var disabled = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabled.Setters.Add(new Setter(UIElement.OpacityProperty, .4)); template.Triggers.Add(disabled);
        var style = new Style(typeof(CheckBox));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 32d));
        style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));
        style.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Think.Text")));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        return style;
    }
}
