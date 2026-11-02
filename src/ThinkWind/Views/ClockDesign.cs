using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrbyLauncher.Views;

internal static class ClockDesign
{
    internal static TextBlock Text(string value, double size = 12, bool muted = false)
    {
        var text = new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, muted ? "Think.Muted" : "Think.Text"); return text;
    }
    internal static UIElement Label(string glyph, string title, bool vertical = false)
    {
        var row = new StackPanel { Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal };
        var icon = Text(glyph, 16); icon.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        icon.HorizontalAlignment = HorizontalAlignment.Center; icon.VerticalAlignment = VerticalAlignment.Center;
        icon.Margin = vertical ? new Thickness(0, 0, 0, 4) : new Thickness(0, 0, 6, 0);
        // Let active/hover foreground on the button reach its icon and label.
        icon.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ButtonBase), 1) });
        row.Children.Add(icon);
        if (title.Length > 0) { var text = Text(title, vertical ? 11 : 12); text.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ButtonBase), 1) }); text.HorizontalAlignment = HorizontalAlignment.Center; text.VerticalAlignment = VerticalAlignment.Center; row.Children.Add(text); }
        return row;
    }
    internal static Button Button(string glyph, string title, Action action, bool iconOnly = false)
    {
        var button = new Button { Content = Label(glyph, iconOnly ? "" : title), ToolTip = title, MinHeight = 32,
            Padding = new Thickness(iconOnly ? 8 : 10, 6, iconOnly ? 8 : 10, 6), Margin = new Thickness(2, 3, 2, 3) };
        System.Windows.Automation.AutomationProperties.SetName(button, title);
        button.Click += (_, _) => action(); return button;
    }
    internal static Border Card(UIElement child, double padding = 10)
    {
        var border = new Border { Child = child, CornerRadius = new CornerRadius(18), Padding = new Thickness(padding) };
        border.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard"); return border;
    }
    internal static ProgressBar Meter()
    {
        var meter = new ProgressBar { Minimum = 0, Maximum = 1, Height = 8, Margin = new Thickness(4, 8, 4, 14), BorderThickness = new Thickness(0) };
        meter.SetResourceReference(Control.BackgroundProperty, "Think.Control"); meter.SetResourceReference(Control.ForegroundProperty, "Think.Accent");
        var grid = new FrameworkElementFactory(typeof(Grid));
        var track = new FrameworkElementFactory(typeof(Border), "PART_Track"); track.SetValue(Border.CornerRadiusProperty, new CornerRadius(4)); track.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); grid.AppendChild(track);
        var fill = new FrameworkElementFactory(typeof(Border), "PART_Indicator"); fill.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(4)); fill.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.ForegroundProperty)); grid.AppendChild(fill);
        meter.Template = new ControlTemplate(typeof(ProgressBar)) { VisualTree = grid }; return meter;
    }
    internal static void Reveal(FrameworkElement element)
    {
        if (!element.IsVisible || !SystemParameters.ClientAreaAnimation) return;
        double ms = ((OrbyLauncher.App)Application.Current).AnimationDuration;
        if (ms <= 0) return;
        var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform(); element.RenderTransform = transform;
        var duration = TimeSpan.FromMilliseconds(Math.Min(260, ms));
        transform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.65, 1, duration) { FillBehavior = FillBehavior.Stop });
    }
}
