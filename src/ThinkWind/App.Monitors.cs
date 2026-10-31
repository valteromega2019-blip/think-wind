using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ThinkWind.Views;
using Forms = System.Windows.Forms;

namespace ThinkWind;

public partial class App
{
    private DockWindow? PrimaryDock => dock?.HasPrimaryControls == true ? dock
        : extraMonitors.Select(pair => pair.Dock).FirstOrDefault(bar => bar.HasPrimaryControls);
    private void RefreshForegroundLayouts()
    {
        island?.RefreshForegroundLayout();
        foreach (var pair in extraMonitors) pair.Island.RefreshForegroundLayout();
    }

    private void DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (!IsExiting && !Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(new Action(ConfigureMonitors));
    }

    private void ConfigureMonitors()
    {
        var screens = Forms.Screen.AllScreens;
        if (screens.Length == 0 || dock is null || island is null) return;
        var p = store.Value;
        var primary = screens.FirstOrDefault(s => s.DeviceName.Equals(p.MonitorDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? screens[Math.Clamp(p.MonitorIndex, 0, screens.Length - 1)];
        var selected = p.MultiMonitorEnabled
            ? screens.Where(s => p.AllMonitors || p.ActiveMonitors.Contains(s.DeviceName, StringComparer.OrdinalIgnoreCase)).ToArray()
            : new[] { primary };
        if (selected.Length == 0) selected = new[] { primary };
        selected = selected.OrderByDescending(s => s.Primary).ToArray();
        string signature = string.Join("|", selected.Select(s => $"{s.DeviceName}:{s.Bounds}:{s.Primary}"));
        if (signature == monitorSignature) return;
        monitorSignature = signature;
        // Retire the complete window families, including their native materials,
        // subscriptions, appbar reservations and transient panels.
        foreach (var window in Windows.OfType<FloatingWindow>().Where(w =>
            w is not SettingsWindow && w is not OverlaySettingsWindow).ToArray())
        {
            gameHidden.Remove(window);
            if (Windows.OfType<Window>().Contains(window)) window.Retire();
        }
        fullscreenHandle = null;
        extraMonitors.Clear();
        for (int index = 0; index < selected.Length; index++)
        {
            var bar = new DockWindow(store) { MonitorDeviceOverride = selected[index].DeviceName };
            var top = new IslandWindow(store, OpenSettings) { MonitorDeviceOverride = selected[index].DeviceName };
            if (index == 0)
            {
                dock = bar; island = top;
                top.FullscreenRevealClosed += () =>
                {
                    if (gameMode?.Active == true && gameMode.FullscreenPresentation)
                    { fullscreenHandle?.Show(); fullscreenHandle?.Position(); }
                };
            }
            else extraMonitors.Add((bar, top));
            if (gameMode?.Active == true)
            { bar.SuspendForGame(true); gameHidden.Add(bar); gameHidden.Add(top); }
            else { bar.Show(); top.PrepareInitialLayout(); top.Show(); top.RefreshForegroundLayout(); }
        }
    }
}
