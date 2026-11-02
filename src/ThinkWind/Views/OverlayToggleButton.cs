using System;
using System.Windows;
using System.Windows.Automation;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class OverlayToggleButton : LiquidToggle
{
    private readonly SettingsStore store;
    private bool syncing;
    internal OverlayToggleButton(SettingsStore settings)
    {
        store = settings; Glyph = "\uE9D9";
        Width = 56; Height = 28; Margin = new Thickness(0, 0, 10, 0);
        Checked += (_, _) => Save(true); Unchecked += (_, _) => Save(false);
        Loaded += (_, _) => { store.Changed += Refresh; Refresh(null, EventArgs.Empty); };
        Unloaded += (_, _) => store.Changed -= Refresh;
        Refresh(null, EventArgs.Empty);
    }
    private void Save(bool enabled)
    { if (!syncing && store.Value.HardwareOverlayEnabled != enabled) store.Update(p => p.HardwareOverlayEnabled = enabled); }
    private void Refresh(object? sender, EventArgs e)
    {
        syncing = true;
        bool enabled = store.Value.HardwareOverlayEnabled;
        IsChecked = enabled;
        string label = enabled ? "Overlay habilitado · aparece somente em jogos reconhecidos" : "Overlay desabilitado";
        ToolTip = label; AutomationProperties.SetName(this, label);
        syncing = false;
    }
}
