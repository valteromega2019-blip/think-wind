using System;
using System.Linq;
using System.Threading;
using System.Windows;
using ThinkWind.Services;
using ThinkWind.Views;
using Forms = System.Windows.Forms;

namespace ThinkWind;
public partial class App : Application
{
    private Mutex? instance;
    private Forms.NotifyIcon? tray;
    private DockWindow? dock;
    private IslandWindow? island;
    private SettingsWindow? settings;
    private SettingsStore store = null!;
    internal bool IsExiting { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\ThinkWind.Launcher", out var first);
        if (!first) { MessageBox.Show("O Think Wind já está em execução. Use o ícone na bandeja do Windows.", "Think Wind"); Shutdown(); return; }
        store = new SettingsStore();
        dock = new DockWindow(store, OpenSettings);
        island = new IslandWindow(store, OpenSettings);
        // Expose both overlay windows to desktop inspection tools during manual QA.
        // Normal launches keep the launcher out of the native taskbar.
        if (e.Args.Contains("--inspect")) { dock.ShowInTaskbar = true; island.ShowInTaskbar = true; }
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Personalizar", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Mostrar barra e ilha", null, (_, _) => Dispatcher.Invoke(() => { dock.Show(); island.Show(); dock.RefreshSettings(); island.RefreshSettings(); }));
        menu.Items.Add("Sair do Think Wind", null, (_, _) => Dispatcher.Invoke(() => { IsExiting = true; Shutdown(); }));
        tray = new Forms.NotifyIcon { Text = "Think Wind", Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
        dock.Show(); island.Show();
        if (e.Args.Contains("--settings")) OpenSettings();
        if (store.LoadWarning is not null) MessageBox.Show(store.LoadWarning, "Think Wind");
    }
    private void OpenSettings()
    {
        if (settings is not null) { settings.Activate(); return; }
        settings = new SettingsWindow(store, () => { dock?.RefreshSettings(); island?.RefreshSettings(); });
        settings.Closed += (_, _) => settings = null;
        settings.Show(); settings.Activate();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        instance?.Dispose();
        base.OnExit(e);
    }
}
