using System;
using System.Linq;
using System.Windows.Threading;
using OrbyLauncher.Services;
using OrbyLauncher.Views;
namespace OrbyLauncher;
public partial class App
{
    private BatteryLowNotice? batteryNotice;
    private DispatcherTimer? batteryNoticeTimer;
    private void InitializePeripheralAlerts()
    {
        PeripheralPresentation.Preferences = store.Value;
        store.Changed += PeripheralPreferencesChanged;
        PeripheralBatteries.Changed += ReviewBatteryAlerts;
        batteryNoticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        batteryNoticeTimer.Tick += ReviewBatteryAlertsTick; batteryNoticeTimer.Start();
    }
    private void PeripheralPreferencesChanged(object? sender, EventArgs e) => PeripheralPresentation.Preferences = store.Value;
    private void ReviewBatteryAlertsTick(object? sender, EventArgs e) => ReviewBatteryAlerts();
    private void ReviewBatteryAlerts()
    {
        var current = PeripheralBatteries.Current;
        var recovered = current.Where(d => !d.Stale && (d.Charging || d.Percent >= 20)).Select(d => d.Name).ToArray();
        if (recovered.Any(name => store.Value.BatteryLowNotified.Contains(name)))
            store.Update(p => p.BatteryLowNotified.RemoveAll(name => recovered.Contains(name)));
        if (!store.Value.BatteryLowAlerts || batteryNotice is not null || UpdateBlocked()) return;
        var device = current.FirstOrDefault(d => !d.Stale && !d.Charging && d.Percent is >= 0 and < 15 && !store.Value.BatteryLowNotified.Contains(d.Name));
        if (device is null) return;
        batteryNotice = new BatteryLowNotice(store, device);
        batteryNotice.Closed += (_, _) => batteryNotice = null;
        batteryNotice.Show();
        store.Update(p => p.BatteryLowNotified.Add(device.Name));
    }
    private void StopPeripheralAlerts()
    {
        batteryNoticeTimer?.Stop(); batteryNotice?.Close();
        store.Changed -= PeripheralPreferencesChanged;
        PeripheralBatteries.Changed -= ReviewBatteryAlerts;
    }
}
