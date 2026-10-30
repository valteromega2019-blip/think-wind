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
    private SystemToolsWindow? systemTools;
    private bool SystemToolsOpen => systemTools?.IsVisible == true;

    private Button WeatherButton(bool strip)
    {
        var reading = new TextBlock { FontSize = strip ? 11 : 12, VerticalAlignment = VerticalAlignment.Center };
        var button = ActionButton("Clima · personalizar cidade e exibição", () => ((App)Application.Current).OpenWeatherSettings());
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = reading; button.Padding = new Thickness(5, 0, 5, 0);
        button.Height = strip ? 24 : 32; button.Width = strip ? 72 : 78;
        button.Margin = strip ? new Thickness(0, 0, 8, 0) : new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
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
        button.Margin = strip ? new Thickness(0, 0, 8, 0) : new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
        button.Click += (_, _) =>
        {
            if (systemTools is not null) { systemTools.Close(); return; }
            var popup = new SystemToolsWindow(Store, button) { Owner = this };
            systemTools = popup;
            popup.Closed += (_, _) => { if (ReferenceEquals(systemTools, popup)) systemTools = null; ScheduleCollapse(); };
            CancelCollapse(); popup.Show(); popup.Position(); popup.Activate();
        };
        return button;
    }
}
