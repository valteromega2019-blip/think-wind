using System;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using ThinkWind.Native;
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
    private WindowsTaskbar? windowsTaskbar;
    private SettingsStore store = null!;
    private PreferenceStamp? preferenceStamp;
    internal bool IsExiting { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (WindowsTaskbar.RunGuard(e.Args)) { Shutdown(); return; }
        instance = new Mutex(true, @"Local\ThinkWind.Launcher", out var first);
        if (!first) { MessageBox.Show("O Think Wind já está em execução. Use o ícone na bandeja do Windows.", "Think Wind"); Shutdown(); return; }
        store = new SettingsStore();
        ThemeService.Apply(store.Value);
        dock = new DockWindow(store);
        island = new IslandWindow(store, OpenSettings);
        preferenceStamp = PreferenceStamp.Capture(store);
        store.Changed += PreferencesChanged;
        SystemEvents.UserPreferenceChanged += WindowsPreferencesChanged;
        // Expose both overlay windows to desktop inspection tools during manual QA.
        // Normal launches keep the launcher out of the native taskbar.
        if (e.Args.Contains("--inspect")) { dock.ShowInTaskbar = true; island.ShowInTaskbar = true; }
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Personalizar", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Editar / concluir layout", null, (_, _) => Dispatcher.Invoke(() => store.SetEditing(!store.IsEditing)));
        menu.Items.Add("Mostrar barra e ilha", null, (_, _) => Dispatcher.Invoke(() => { dock.Show(); island.Show(); dock.RefreshSettings(); island.RefreshSettings(); }));
        menu.Items.Add("Sair do Think Wind", null, (_, _) => Dispatcher.Invoke(() => { IsExiting = true; Shutdown(); }));
        tray = new Forms.NotifyIcon { Text = "Think Wind", Icon = AppBrand.TrayIcon(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
        dock.Show(); island.Show();
        windowsTaskbar = new WindowsTaskbar(message =>
        {
            if (IsExiting) return;
            Dispatcher.BeginInvoke(new Action(() => { store.Update(p => p.HideWindowsTaskbar = false); MessageBox.Show(message, "Think Wind"); }));
        });
        windowsTaskbar.Apply(store.Value.HideWindowsTaskbar);
        if (e.Args.Contains("--settings")) OpenSettings();
        if (store.LoadWarning is not null) MessageBox.Show(store.LoadWarning, "Think Wind");
    }
    private void OpenSettings()
    {
        if (settings is not null) { settings.Activate(); return; }
        store.SetCustomizing(true);
        settings = new SettingsWindow(store);
        settings.Closed += (_, _) => { settings = null; store.SetCustomizing(false); };
        settings.Show(); settings.Activate();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        windowsTaskbar?.Dispose();
        SystemEvents.UserPreferenceChanged -= WindowsPreferencesChanged;
        if (store is not null) { store.Changed -= PreferencesChanged; store.Flush(); }
        if (tray is not null) { tray.Visible = false; tray.Icon?.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        BackdropComposition.Shutdown();
        instance?.Dispose();
        base.OnExit(e);
    }
    private void PreferencesChanged(object? sender, EventArgs e)
    {
        if (IsExiting) return;
        windowsTaskbar?.Apply(store.Value.HideWindowsTaskbar);
        ThemeService.Apply(store.Value);
        var next = PreferenceStamp.Capture(store);
        bool editing = preferenceStamp?.Editing != next.Editing;
        if (editing || preferenceStamp?.Dock != next.Dock) dock?.RefreshSettings(); else dock?.RefreshAppearance();
        if (editing || preferenceStamp?.Island != next.Island) island?.RefreshSettings(); else island?.RefreshAppearance();
        preferenceStamp = next;
    }
    private void WindowsPreferencesChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!IsExiting && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(() =>
        { ThemeService.Apply(store.Value, true); dock?.RefreshAppearance(); island?.RefreshAppearance(); }));
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    { IsExiting = true; store?.Flush(); base.OnSessionEnding(e); }
}
