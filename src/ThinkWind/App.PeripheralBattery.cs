using OrbyLauncher.Services;
using OrbyLauncher.Views;

namespace OrbyLauncher;

public partial class App
{
    private PeripheralBatteryWindow? peripheralBatteryWindow;
    internal void OpenPeripheralBattery(PeripheralBattery device)
    {
        if (IsExiting) return;
        peripheralBatteryWindow?.Close();
        var window = new PeripheralBatteryWindow(store, device);
        peripheralBatteryWindow = window;
        window.Closed += (_, _) => { if (ReferenceEquals(peripheralBatteryWindow, window)) peripheralBatteryWindow = null; };
        window.Show(); window.Position(); window.Activate();
    }
}
