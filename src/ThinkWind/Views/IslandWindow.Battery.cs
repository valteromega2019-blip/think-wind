using System.Windows;
using System.Collections.Generic;
using System.Linq;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private bool batteryAvailable = BatteryStatusService.Read().Available;
    private BatteryStatusButton? headerBattery, stripBattery, secondaryBattery;

    // Honor the saved order, including manually repositioned battery controls.
    private static IEnumerable<string> OrderBatteryBefore(IEnumerable<string> controls, string neighbour)
    {
        return controls.ToList();
    }

    private BatteryStatusButton BatteryButton(bool strip)
    {
        var button = new BatteryStatusButton(strip, Store.Value.IslandScale)
        {
            Margin = strip ? new Thickness(0) : new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0)
        };
        button.Click += (_, _) => { if (!Store.IsEditing && !LayoutDrag.SuppressClick) SystemActions.Run("ms-settings:powersleep"); };
        button.Present(BatteryStatusService.Read(), Store.Value.ShowBatteryPercentage);
        EnableQuickReorder(button, "battery", strip && Store.Value.IslandPanelSide == "Left");
        MarkPanelIcon(button, "battery");
        return button;
    }

    private void RefreshBattery()
    {
        var reading = BatteryStatusService.Read();
        if (batteryAvailable != reading.Available)
        {
            batteryAvailable = reading.Available;
            BuildHeader(); BuildStatusRow();
            if (IsVisible) Resize(false);
        }
        headerBattery?.Present(reading, Store.Value.ShowBatteryPercentage);
        stripBattery?.Present(reading, Store.Value.ShowBatteryPercentage);
        secondaryBattery?.Present(reading, Store.Value.ShowBatteryPercentage);
    }
}
