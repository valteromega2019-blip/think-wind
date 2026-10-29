using System.Text.Json;

namespace ThinkWind.Services;
internal sealed record PreferenceStamp(string Dock, string Island, bool Editing)
{
    internal static PreferenceStamp Capture(SettingsStore store)
    {
        var p = store.Value;
        return new(JsonSerializer.Serialize(new { p.DockScale, p.IconSize, p.DockSpacing, p.DockBottomOffset, p.DockHorizontalOffset, p.MonitorIndex, p.MonitorDeviceName, p.AutoHide, p.DockSearchMode, p.DockOrder, p.Shortcuts, p.DockMagnification, p.DockMagnificationAmount, p.DockMagnificationNeighbors }),
            JsonSerializer.Serialize(new { p.IslandScale, p.IslandExpandedWidth, p.IslandSpacing, p.IslandHorizontalOffset, p.MonitorIndex, p.MonitorDeviceName, p.QuickOrder, p.HiddenQuickControls, p.ModuleOrder, p.HiddenModules, p.ShowAudio, p.ShowMemory, p.ShowBattery }), store.IsEditing);
    }
}
