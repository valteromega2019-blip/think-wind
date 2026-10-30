using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed partial class IslandWindow
{
    private TextBlock? headerWeather, stripWeather;
    private Button? stripWeatherButton;
    private ContextMenu? systemTools;
    private bool SystemToolsOpen => systemTools?.IsOpen == true;

    private Button WeatherButton(bool strip)
    {
        var reading = new TextBlock { FontSize = strip ? 11 : 12, VerticalAlignment = VerticalAlignment.Center };
        var button = ActionButton("Clima · personalizar cidade e exibição", openSettings);
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = reading; button.Padding = new Thickness(5, 0, 5, 0);
        button.Height = strip ? 24 : 32; button.Width = strip ? 72 : 78;
        button.Margin = new Thickness(4, 0, 4, 0);
        if (strip) { stripWeather = reading; stripWeatherButton = button; }
        else headerWeather = reading;
        RefreshHeaderWeather();
        return button;
    }
    private void RefreshHeaderWeather()
    {
        var value = weather.Reading;
        string icon = value?.Code switch
        {
            0 => "☀", 1 or 2 => "⛅", 3 => "☁", 45 or 48 => "≋",
            >= 71 and <= 77 or 85 or 86 => "❄", >= 95 => "⚡", null => "☁", _ => "☂"
        };
        string text = value is null ? icon + " —" : $"{icon} {value.Temperature:0}°C";
        string tip = value is null ? "Escolha uma cidade em Personalizar > Clima" : $"{Store.Value.WeatherCity} · {WeatherService.Description(value.Code)} · atualizado {value.Updated:HH:mm}";
        foreach (var label in new[] { headerWeather, stripWeather })
            if (label is not null) { label.Text = text; label.ToolTip = tip; }
        if (stripWeatherButton is not null) stripWeatherButton.Visibility = Store.Value.ShowWeatherInHeader ? Visibility.Visible : Visibility.Collapsed;
    }
    private Button SystemToolsButton(bool strip)
    {
        var button = ActionButton("Ferramentas do Windows", () => { });
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = Glyph("\uE712", strip ? 13 : 17 * Store.Value.IslandScale);
        button.Width = strip ? 28 : 32 * Store.Value.IslandScale;
        button.Height = strip ? 24 : 32 * Store.Value.IslandScale;
        button.Padding = new Thickness(3);
        button.Click += (_, _) =>
        {
            if (systemTools is not null) systemTools.IsOpen = false;
            systemTools = SystemToolsMenu.Create(button);
            systemTools.Opened += (_, _) => CancelCollapse();
            systemTools.Closed += (_, _) => ScheduleCollapse();
            systemTools.IsOpen = true;
        };
        return button;
    }
}
