using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private TextBlock? headerWeather, stripWeather;
    private WeatherIcon? headerWeatherIcon, stripWeatherIcon;
    private Button? stripWeatherButton;
    private SystemToolsWindow? systemTools;
    private bool SystemToolsOpen => systemTools?.IsVisible == true;
    private bool IsQuickVisible(string id) => !Store.Value.HiddenQuickControls.Contains(id) && !Store.Value.DisabledQuickControls.Contains(id)
        && (id != "battery" || batteryAvailable);

    private Button CreateThemeToggle()
    {
        var button = ActionButton("Alternar tema do Windows e do launcher", async () =>
            await ThemeService.SelectTheme(Store, ThemeService.IsLight(Store.Value) ? "Dark" : "Light"));
        themeToggle = button;
        StyleSecondaryControl(button);
        RefreshThemeToggle();
        return button;
    }

    private Button WeatherButton(bool strip)
    {
        var reading = new TextBlock { FontSize = strip ? 11 : 12, VerticalAlignment = VerticalAlignment.Center };
        var button = ActionButton("Clima · escolher cidade", () => { });
        button.Click += (_, _) => { if (!Store.IsEditing) ShowMini(MiniIslandKind.Weather, button); };
        button.Style = (Style)FindResource("Think.IconButton");
        var icon = new WeatherIcon { FontSize = strip ? 14 : 17, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(icon); content.Children.Add(reading);
        button.Content = content; button.Padding = new Thickness(5, 0, 5, 0);
        button.Height = strip ? 24 : 32; button.Width = strip ? 72 : 78;
        button.Margin = strip ? new Thickness(0, 0, 8, 0) : new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
        if (strip) { stripWeather = reading; stripWeatherButton = button; stripWeatherIcon = icon; }
        else { headerWeather = reading; headerWeatherIcon = icon; }
        RefreshHeaderWeather();
        return MarkPanelIcon(button, "weather");
    }
    private void RefreshHeaderWeather()
    {
        var value = weather.Reading;
        string text = value is null ? "—" : $"{value.Temperature:0}°C";
        foreach (var icon in new[] { headerWeatherIcon, stripWeatherIcon }) if (icon is not null) icon.Code = value?.Code;
        string tip = value is null ? "Escolha uma cidade em Personalizar > Clima" : $"{Store.Value.WeatherCity} · {WeatherService.Description(value.Code)} · atualizado {value.Updated:HH:mm}";
        foreach (var label in new[] { headerWeather, stripWeather })
            if (label is not null) { label.Text = text; label.ToolTip = tip; }
        if (stripWeatherButton is not null) stripWeatherButton.Visibility = Store.Value.ShowWeatherInHeader && WeatherEnabled ? Visibility.Visible : Visibility.Collapsed;
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
            var popup = new SystemToolsWindow(Store, button) { Owner = this, MonitorDeviceOverride = MonitorDeviceOverride };
            systemTools = popup;
            popup.Closed += (_, _) => { if (ReferenceEquals(systemTools, popup)) systemTools = null; RefreshPanelIcons(); ScheduleCollapse(); };
            CancelCollapse(); popup.Show(); popup.Position(); popup.Activate(); RefreshPanelIcons();
        };
        return MarkPanelIcon(button, "tools");
    }
}
