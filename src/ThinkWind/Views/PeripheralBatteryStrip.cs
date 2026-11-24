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
        Loaded += (_, _) => { PeripheralBatteries.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => PeripheralBatteries.Changed -= Refresh;
    }
    internal void Refresh()
    {
        if (!IsVisible) return;
        PeripheralBatteries.Refresh();
        string next = string.Join("|", System.Linq.Enumerable.Select(PeripheralBatteries.Current, device => device.Name + device.Display + device.Charging));
        if (next == signature) return;
        signature = next; Children.Clear();
        foreach (var device in PeripheralBatteries.Current)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 2, 5, 2) };
            var icon = new TextBlock { Text = device.Glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
            row.Children.Add(icon);
            var battery = new BatteryIcon { Charge = device.VisualCharge, Charging = device.Charging, Width = 25, Height = 21, FontSize = 19, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            row.Children.Add(battery);
            var button = new Button { Content = row, Padding = new Thickness(0), Margin = new Thickness(1, 0, 1, 0), ToolTip = device.Name + " · " + device.Display + (device.Charging ? " · Carregando" : "") };
            button.SetResourceReference(StyleProperty, "Think.IconButton");
            System.Windows.Automation.AutomationProperties.SetName(button, button.ToolTip.ToString());
            button.Click += (_, _) => ((App)Application.Current).OpenPeripheralBattery(device);
            Children.Add(button);
        }
    }
}
