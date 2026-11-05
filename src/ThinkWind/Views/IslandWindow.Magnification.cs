using System.Collections.Generic;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private readonly List<HeaderIconMagnifier> headerMagnifiers = new();
    private Button MarkPanelIcon(Button button, string id)
    { button.SetValue(ActivePanelIcon.IdProperty, id); ActivePanelIcon.Update(button, PanelIconActive(id), force: true); return button; }
    private bool PanelIconActive(string id)
    {
        MiniIslandKind? kind = id switch { "clock" => MiniIslandKind.Clock, "calendar" => MiniIslandKind.Calendar, "weather" => MiniIslandKind.Weather,
            "audio" => MiniIslandKind.Volume, "brightness" => MiniIslandKind.Brightness, "wifi" => MiniIslandKind.Network, "bluetooth" => MiniIslandKind.Bluetooth, "tray" => MiniIslandKind.Tray, _ => null };
        if (kind is { } mini) return minis.TryGetValue(mini, out var window) && window.IsVisible;
        return id switch
        {
            "tools" => SystemToolsOpen,
            "monitor" => leftArea?.IsVisible == true || Stage == OrbyLauncher.Services.IslandStage.Full && secondAreaSelected,
            "expand" => Stage == OrbyLauncher.Services.IslandStage.Full,
            "settings" => Application.Current.Windows.OfType<SettingsWindow>().Any(w => w.IsVisible),
            "keyboard" => Application.Current.Windows.OfType<VirtualKeyboardWindow>().Any(w => w.IsVisible),
            _ => false
        };
    }
    private void RefreshPanelIcons()
    {
        if (!IsVisible && statusWindow?.IsVisible != true) return;
        void Visit(Panel row)
        {
            foreach (UIElement child in row.Children)
                if (child is Button button && button.GetValue(ActivePanelIcon.IdProperty) is string id) ActivePanelIcon.Update(button, PanelIconActive(id));
                else if (child is Panel nested) Visit(nested);
        }
        foreach (var row in new Panel[] { header, hoverQuick, statusRow }) Visit(row);
        foreach (var effect in headerMagnifiers) effect.RefreshSelection();
    }
    private void InitializeHeaderMagnification()
    {
        foreach (Panel row in new Panel[] { header, hoverQuick, statusRow })
            headerMagnifiers.Add(new HeaderIconMagnifier(row,
                () => !Store.IsEditing && !LayoutDrag.Active && !IsPositionAnimating && !ControlsBusy));
        OrbyLauncher.Services.ThemeService.Changed += RefreshPanelIcons;
        Closed += (_, _) => { OrbyLauncher.Services.ThemeService.Changed -= RefreshPanelIcons; foreach (var effect in headerMagnifiers) effect.Dispose(); headerMagnifiers.Clear(); };
    }
}
