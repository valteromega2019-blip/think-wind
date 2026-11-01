using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private TextBox AddColorSetting(Panel parent, string label, string value, Action<string> changed)
    {
        var title = new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 6), TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); parent.Children.Add(title);
        var row = new Grid(); row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var hex = new TextBox { Text = value, MinWidth = 104, MaxLength = 7, BorderThickness = new Thickness(0), Background = Brushes.Transparent,
            Padding = new Thickness(8), VerticalContentAlignment = VerticalAlignment.Center };
        hex.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        System.Windows.Automation.AutomationProperties.SetName(hex, label + " em hexadecimal");
        Grid.SetRow(hex, 1); row.Children.Add(hex);
        var swatch = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)) };
        var caption = new TextBlock { Text = "Escolher cor", VerticalAlignment = VerticalAlignment.Center };
        caption.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        var buttonContent = new StackPanel { Orientation = Orientation.Horizontal }; buttonContent.Children.Add(swatch); buttonContent.Children.Add(caption);
        var choose = new Button { Content = buttonContent, Padding = new Thickness(8), HorizontalContentAlignment = HorizontalAlignment.Left, ToolTip = label };
        choose.SetResourceReference(StyleProperty, "Think.IconButton"); row.Children.Add(choose);
        choose.Click += (_, _) => ColorPickerPopup.Open(choose, OverlayAppearanceOptions.IsValidColor(hex.Text) ? hex.Text : value, selected => hex.Text = selected);
        hex.TextChanged += (_, _) =>
        {
            if (!OverlayAppearanceOptions.IsValidColor(hex.Text)) return;
            swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex.Text));
            if (!building) changed(hex.Text.ToUpperInvariant());
        };
        var frame = new Border { Child = row, CornerRadius = new CornerRadius(14), Padding = new Thickness(4), Margin = new Thickness(0, 0, 0, 10) };
        frame.SetResourceReference(Border.BackgroundProperty, "Think.Control"); parent.Children.Add(frame);
        var presets = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (string preset in new[] { "#60CDFF", "#89A5FF", "#B69CFF", "#5ED6B3", "#FFAE6D", "#FA819A" })
        {
            var pick = new Button { Width = 32, Height = 32, Padding = new Thickness(4), Margin = new Thickness(0, 0, 6, 0), ToolTip = preset,
                Content = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(preset)) } };
            pick.SetResourceReference(StyleProperty, "Think.IconButton");
            System.Windows.Automation.AutomationProperties.SetName(pick, label + " " + preset);
            pick.Click += (_, _) => hex.Text = preset; presets.Children.Add(pick);
        }
        parent.Children.Add(presets);
        return hex;
    }
}
