using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class PeripheralBatteryStrip : WrapPanel
{
    private string signature = "";
    internal PeripheralBatteryStrip()
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Loaded += (_, _) => Refresh();
    }
    internal void Refresh()
    {
        if (!IsVisible) return;
        PeripheralBatteries.Refresh();
        string next = string.Join("|", System.Linq.Enumerable.Select(PeripheralBatteries.Current, device => device.Name + device.Percent));
        if (next == signature) return;
        signature = next; Children.Clear();
        foreach (var device in PeripheralBatteries.Current)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 2, 5, 2), ToolTip = device.Name + " · " + device.Percent + "%" };
            var icon = new TextBlock { Text = device.Glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
            row.Children.Add(icon);
            var value = new TextBlock { Text = device.Percent + "%", FontSize = 11, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            value.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); row.Children.Add(value); Children.Add(row);
        }
    }
}
