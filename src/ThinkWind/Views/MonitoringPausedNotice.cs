using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Views;

internal sealed class MonitoringPausedNotice : Border
{
    internal MonitoringPausedNotice(string instruction)
    {
        CornerRadius = new CornerRadius(12); Padding = new Thickness(12, 10, 12, 10);
        Margin = new Thickness(4, 4, 4, 10); BorderThickness = new Thickness(1);
        Visibility = Visibility.Collapsed;
        SetResourceReference(BackgroundProperty, "Think.AccentSurface");
        SetResourceReference(BorderBrushProperty, "Think.Accent");
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        var icon = new TextBlock { Text = "\uE769", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 20, Margin = new Thickness(0, 2, 10, 0), VerticalAlignment = VerticalAlignment.Top };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); layout.Children.Add(icon);
        var content = new StackPanel(); Grid.SetColumn(content, 1);
        var title = new TextBlock { Text = "Monitoramento desativado", FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        var details = new TextBlock { Text = instruction, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
        details.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        content.Children.Add(title); content.Children.Add(details); layout.Children.Add(content); Child = layout;
        System.Windows.Automation.AutomationProperties.SetName(this, "Monitoramento desativado. " + instruction);
    }
}
