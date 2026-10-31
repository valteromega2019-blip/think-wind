using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed partial class IslandWindow
{
    private bool WeatherEnabled => !Store.Value.DisabledQuickControls.Contains("weather");
    private void BuildSecondaryQuickControls()
    {
        hoverQuick.Children.Clear();
        if (!Store.Value.ShowWeatherInHeader && WeatherEnabled) hoverQuick.Children.Add(WeatherButton(false));
        foreach (string id in Store.Value.QuickOrder.Where(id => id != "settings"
            && Store.Value.HiddenQuickControls.Contains(id) && !Store.Value.DisabledQuickControls.Contains(id)))
        {
            if (id == "tools") { hoverQuick.Children.Add(SystemToolsButton(false)); continue; }
            if (id == "monitor")
            {
                var monitor = ActionButton("Desempenho do PC", () =>
                {
                    if (SecondAreaAvailable) { secondAreaSelected = true; OpenFull(); }
                    else ToggleLeftArea();
                });
                monitor.Content = Glyph("\uE9D9", 17); StyleSecondaryControl(monitor); hoverQuick.Children.Add(monitor); continue;
            }
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            var button = ActionButton(control.Name, () => { });
            button.Content = Glyph(control.Glyph, 17); StyleSecondaryControl(button);
            button.Click += (_, _) =>
            {
                switch (id)
                {
                    case "clock": case "calendar": ShowMini(MiniIslandKind.Calendar, button); break;
                    case "wifi": ShowMini(MiniIslandKind.Network, button); break;
                    case "audio": ShowMini(MiniIslandKind.Volume, button); break;
                    case "bluetooth": ShowMini(MiniIslandKind.Bluetooth, button); break;
                    case "tray": ShowMini(MiniIslandKind.Tray, button); break;
                    case "notifications": Desktop.Chord(0x5B, 0x4E); break;
                    default: if (control.Target is string target) SystemActions.Run(target); break;
                }
            };
            hoverQuick.Children.Add(button);
        }
        hoverQuick.Visibility = hoverQuick.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void StyleSecondaryControl(Button button)
    {
        button.Style = (Style)FindResource("Think.IconButton");
        button.Width = button.Height = 32; button.Padding = new Thickness(3); button.Margin = new Thickness(2);
    }
}
