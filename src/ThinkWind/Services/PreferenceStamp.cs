using System.Text.Json;

namespace OrbyLauncher.Services;
internal sealed record PreferenceStamp(string Dock, string Island, bool Editing)
{
    internal static PreferenceStamp Capture(SettingsStore store)
    {
        var p = store.Value;
        return new(JsonSerializer.Serialize(new { p.FontFamily, p.FontScale, p.DockScale, p.DockFixedBar, p.IconSize, p.DockSpacing, p.DockBottomOffset, p.DockHorizontalOffset, p.MonitorIndex, p.MonitorDeviceName, p.AutoHide, p.DockVisibleOnDesktop, p.DockSearchMode, p.DockOrder, p.Shortcuts, p.DockMagnification, p.DockMagnificationAmount, p.DockMagnificationNeighbors }),
            JsonSerializer.Serialize(new { p.FontFamily, p.FontScale, p.IslandReserveTop, p.IslandReduceOverMaximized, p.IslandScale, p.IslandExpandedWidth, p.IslandPanelSide, p.IslandSpacing, p.IslandHorizontalOffset, p.MonitorIndex, p.MonitorDeviceName, p.QuickOrder, p.HiddenQuickControls, p.DisabledQuickControls, p.ModuleOrder, p.ModuleColumns, p.ModuleExtraHeight, p.HiddenModules, p.ShowAudio, p.ShowMemory, p.ShowBattery, p.ShowBatteryPercentage, p.WeatherCity, p.WeatherLatitude, p.WeatherLongitude, p.ShowWeatherInHeader, p.WidgetOrder, p.HiddenWidgets, p.WidgetFavorites }), store.IsEditing);
    }
}
