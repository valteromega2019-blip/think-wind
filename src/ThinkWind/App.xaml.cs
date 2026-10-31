using System;
using System.Linq;
using System.Collections.Generic;
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
    private readonly List<(DockWindow Dock, IslandWindow Island)> extraMonitors = new();
    private string monitorSignature = "";
    private SettingsWindow? settings;
    private WindowsTaskbar? windowsTaskbar;
    private WindowsStartKey? startKey;
    private LauncherWindowsKey? launcherWindowsKey;
    private GameModeService? gameMode;
    private FullscreenHandleWindow? fullscreenHandle;
    internal HardwareTelemetry Telemetry { get; private set; } = null!;
    private HardwareOverlayWindow? hardwareOverlay;
    private OverlaySettingsWindow? overlaySettings;
    private OverlayHotkey? overlayHotkey;
    private readonly HashSet<Window> gameHidden = new();
    private bool diagnosing;
    private string? diagnosticReport;
    private int gameTransitions;
    private SettingsStore store = null!;
    private PreferenceStamp? preferenceStamp;
    internal bool IsExiting { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LaunchEnvironment.RemoveDevelopmentBootstrap();
        if (AdvancedSensorSetup.RunSetup(e.Args, out int sensorSetupExitCode)) { Shutdown(sensorSetupExitCode); return; }
        if (e.Args.Length > 0 && e.Args[0] == "--sensor-worker")
        {
            // Logon triggers have no runtime parameter: default to sensors.
            string mode = e.Args.Length == 1 ? "sensors" : e.Args[1];
            if (e.Args.Length > 2 || mode is not ("" or "$(Arg0)" or "sensors" or "frames")) { Shutdown(2); return; }
            ShutdownMode = ShutdownMode.OnExplicitShutdown; RunSensorWorker(mode == "frames"); return;
        }
        if (ElevatedWindowAction.Run(e.Args, out int actionExitCode)) { Shutdown(actionExitCode); return; }
        if (WindowsTaskbar.RunGuard(e.Args)) { Shutdown(); return; }
        bool configuringInstallation = e.Args.Contains("--configure-installation");
        instance = new Mutex(true, @"Local\ThinkWind.Launcher", out var first);
        if (configuringInstallation)
        {
            Shutdown(first ? InstallationSetup.Run() : 2);
            return;
        }
        if (!first) { MessageBox.Show("O Think Wind já está em execução. Use o ícone na bandeja do Windows.", "Think Wind"); Shutdown(); return; }
        diagnosing = e.Args.Contains("--diagnostic");
        int reportIndex = Array.IndexOf(e.Args, "--diagnostic-report");
        diagnosticReport = reportIndex >= 0 && reportIndex + 1 < e.Args.Length ? e.Args[reportIndex + 1] : null;
        store = diagnosing ? new SettingsStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ThinkWindDiagnostic", Guid.NewGuid() + ".json")) : new SettingsStore();
        if (diagnosing) { store.Value.AutoHide = false; store.Value.GameModeEnabled = !e.Args.Contains("--diagnostic-normal"); }
        if (!diagnosing)
        {
            try { SystemActions.ApplyStartupDefault(store); }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { MessageBox.Show("Não foi possível configurar a inicialização automática: " + error.Message, "Think Wind"); }
        }
        ThemeService.Apply(store.Value);
        Telemetry = new HardwareTelemetry(Dispatcher);
        hardwareOverlay = new HardwareOverlayWindow(store, Telemetry);
        dock = new DockWindow(store);
        island = new IslandWindow(store, OpenSettings);
        island.FullscreenRevealClosed += () =>
        {
            if (gameMode?.Active == true && gameMode.FullscreenPresentation)
            { fullscreenHandle?.Show(); fullscreenHandle?.Position(); }
        };
        preferenceStamp = PreferenceStamp.Capture(store);
        store.Changed += PreferencesChanged;
        SystemEvents.UserPreferenceChanged += WindowsPreferencesChanged;
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        // Expose both overlay windows to desktop inspection tools during manual QA.
        // Normal launches keep the launcher out of the native taskbar.
        if (e.Args.Contains("--inspect")) { dock.ShowInTaskbar = true; island.ShowInTaskbar = true; }
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Personalizar", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Editar / concluir layout", null, (_, _) => Dispatcher.Invoke(() => store.SetEditing(!store.IsEditing)));
        menu.Items.Add("Mostrar barra e ilha", null, (_, _) => Dispatcher.Invoke(() => { if (gameMode?.Active == true) return; dock.Show(); island.Show(); dock.RefreshSettings(); island.RefreshSettings(); }));
        menu.Items.Add("Sair do Think Wind", null, (_, _) => Dispatcher.Invoke(() => { IsExiting = true; Shutdown(); }));
        tray = new Forms.NotifyIcon { Text = "Think Wind", Icon = AppBrand.TrayIcon(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
        var localTrayImage = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(tray.Icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        localTrayImage.Freeze();
        TrayService.RegisterLocal(new TrayEntry("Think Wind", localTrayImage, null, 0, "local:think-wind", LocalAction: context =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsExiting || gameMode?.Active == true) return;
                if (context) menu.Show(Forms.Cursor.Position); else OpenSettings();
            }))));
        bool initialGame = false;
        try { initialGame = GameModeService.Probe(store.Value); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        if (!initialGame) { dock.Show(); island.PrepareInitialLayout(); island.Show(); }
        windowsTaskbar = new WindowsTaskbar(message =>
        {
            if (IsExiting) return;
            Dispatcher.BeginInvoke(new Action(() => { store.Update(p => p.HideWindowsTaskbar = false); MessageBox.Show(message, "Think Wind"); }));
        });
        windowsTaskbar.Apply(store.Value.HideWindowsTaskbar);
        gameMode = new GameModeService(Dispatcher, ChangeGameMode, initialGame);
        gameMode.ForegroundChanged += RefreshForegroundLayouts;
        island.RefreshForegroundLayout();
        if (initialGame) { gameHidden.Add(dock); gameHidden.Add(island); dock.SuspendForGame(true); gameTransitions++; }
        gameMode.Configure(store.Value); ConfigureStartHotkey();
        ConfigureMonitors();
        ApplyHardwareOverlay();
        if (!diagnosing && !initialGame && store.Value.HardwareSensorsAutoSetup && !AdvancedSensorSetup.IsConfigured)
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                store.Update(p => p.HardwareSensorsSetupAttempted = true);
                if (!AdvancedSensorSetup.IsConfigured) await AdvancedSensorSetup.Configure();
            }));
        if (diagnosing)
        {
            var exit = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            exit.Tick += (_, _) => { exit.Stop(); WriteDiagnosticReport(); IsExiting = true; Shutdown(); }; exit.Start();
        }
        if (e.Args.Contains("--settings")) OpenSettings();
        if (store.LoadWarning is not null) MessageBox.Show(store.LoadWarning, "Think Wind");
    }
    internal void OpenWeatherSettings()
    {
        OpenSettings();
        settings?.ShowWeatherSection();
    }
    private void OpenSettings()
    {
        if (gameMode?.Active == true) return;
        if (settings is not null) { settings.Activate(); return; }
        store.SetCustomizing(true);
        settings = new SettingsWindow(store);
        settings.Closed += (_, _) => { settings = null; store.SetCustomizing(overlaySettings?.IsVisible == true); };
        settings.Show(); settings.Activate();
    }
    internal void OpenOverlaySettings()
    {
        if (overlaySettings is not null) { overlaySettings.Activate(); return; }
        store.SetCustomizing(true);
        overlaySettings = new OverlaySettingsWindow(store);
        overlaySettings.Closed += (_, _) => { overlaySettings = null; store.SetCustomizing(settings?.IsVisible == true); };
        overlaySettings.Show(); overlaySettings.Position(); overlaySettings.Activate();
    }
    private async void RunSensorWorker(bool captureFrames)
    {
        int result = 1;
        try { result = await AdvancedSensorWorker.Run(Dispatcher, captureFrames); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(AdvancedSensorSetup.DirectoryPath, "sensor-worker-error.log"), DateTime.UtcNow.ToString("O") + Environment.NewLine + error); }
            catch (Exception logError) when (logError is System.IO.IOException or UnauthorizedAccessException) { }
        }
        Shutdown(result);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        windowsTaskbar?.Dispose();
        startKey?.Dispose();
        launcherWindowsKey?.Dispose();
        if (diagnosticReport is not null && !System.IO.File.Exists(diagnosticReport)) WriteDiagnosticReport();
        gameMode?.Dispose();
        overlayHotkey?.Dispose(); hardwareOverlay?.Close();
        Telemetry?.Dispose();
        fullscreenHandle?.Close();
        TrayService.RegisterLocal(null);
        SystemEvents.UserPreferenceChanged -= WindowsPreferencesChanged;
        SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        if (store is not null) { store.Changed -= PreferencesChanged; store.Flush(); }
        if (tray is not null) { tray.Visible = false; tray.Icon?.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        BackdropComposition.Shutdown();
        instance?.Dispose();
        base.OnExit(e);
    }
    private void WriteDiagnosticReport()
    { if (diagnosticReport is not null) System.IO.File.WriteAllText(diagnosticReport, System.Text.Json.JsonSerializer.Serialize(new { GameMode = gameMode?.Active, Foreground = gameMode?.CurrentProcess, Transitions = gameTransitions, VisibleLauncherWindows = Windows.OfType<Window>().Count(window => window.IsVisible), KeyboardHook = launcherWindowsKey?.IsActive == true, HotkeyRegistered = startKey is not null, NativeThumbnails = WindowThumbnail.ActiveCount, BluetoothWatchers = Windows.OfType<MiniIslandWindow>().Sum(mini => mini.ActiveBluetoothWatchers) })); }
    private void PreferencesChanged(object? sender, EventArgs e)
    {
        if (IsExiting) return;
        windowsTaskbar?.Apply(store.Value.HideWindowsTaskbar);
        gameMode?.Configure(store.Value); ConfigureStartHotkey();
        ThemeService.Apply(store.Value);
        ConfigureMonitors();
        var next = PreferenceStamp.Capture(store);
        ApplyHardwareOverlay();
        bool editing = preferenceStamp?.Editing != next.Editing;
        if (editing || preferenceStamp?.Dock != next.Dock) dock?.RefreshSettings(); else dock?.RefreshAppearance();
        if (editing || preferenceStamp?.Island != next.Island) island?.RefreshSettings(); else island?.RefreshAppearance();
        foreach (var pair in extraMonitors)
        {
            if (editing || preferenceStamp?.Dock != next.Dock) pair.Dock.RefreshSettings(); else pair.Dock.RefreshAppearance();
            if (editing || preferenceStamp?.Island != next.Island) pair.Island.RefreshSettings(); else pair.Island.RefreshAppearance();
        }
        preferenceStamp = next;
    }
    private void ConfigureStartHotkey()
    {
        bool interceptWindows = store.Value.WindowsKeyOpensLauncher && gameMode?.Active != true;
        if (!interceptWindows) { launcherWindowsKey?.Dispose(); launcherWindowsKey = null; }
        else launcherWindowsKey ??= new LauncherWindowsKey(Dispatcher, () => { if (!IsExiting && gameMode?.Active != true) PrimaryDock?.ToggleStartFromKeyboard(); });
        bool enabled = store.Value.StartHotkeyEnabled && gameMode?.Active != true;
        if (!enabled) { startKey?.Dispose(); startKey = null; return; }
        if (startKey is not null) return;
        try { startKey = new WindowsStartKey(() => { if (!IsExiting && gameMode?.Active != true) PrimaryDock?.ToggleStartFromKeyboard(); }); }
        catch (System.ComponentModel.Win32Exception) { /* Shortcut remains unavailable; normal Start button works. */ }
    }
    private void ApplyHardwareOverlay()
    {
        hardwareOverlay?.Apply(gameMode?.RecognizedGame == true);
        if (!store.Value.OverlayHotkeyEnabled) { overlayHotkey?.Dispose(); overlayHotkey = null; }
        else if (overlayHotkey is null)
        {
            try { overlayHotkey = new OverlayHotkey(() => store.Update(p => p.HardwareOverlayEnabled = !p.HardwareOverlayEnabled)); }
            catch (System.ComponentModel.Win32Exception) { /* Hotkey may belong to another app; panel remains usable. */ }
        }
    }
    private void ChangeGameMode(bool active)
    {
        if (IsExiting) return; gameTransitions++;
        ConfigureStartHotkey();
        dock?.SuspendForGame(active);
        foreach (var pair in extraMonitors) pair.Dock.SuspendForGame(active);
        if (active)
        {
            foreach (Window window in Windows.OfType<Window>().ToArray())
                if (!ReferenceEquals(window, fullscreenHandle) && !ReferenceEquals(window, hardwareOverlay) && window.IsVisible) { gameHidden.Add(window); window.Hide(); }
            windowsTaskbar?.Apply(store.Value.HideWindowsTaskbar);
            if (gameMode?.FullscreenPresentation == true)
            {
                ShowFullscreenAccess();
            }
            else fullscreenHandle?.Hide();
        }
        else
        {
            fullscreenHandle?.Hide();
            island?.EndFullscreenReveal();
            foreach (var pair in extraMonitors) pair.Island.EndFullscreenReveal();
            foreach (var window in gameHidden.Where(window => Windows.OfType<Window>().Contains(window)).ToArray())
            { if (ReferenceEquals(window, island) && !window.IsLoaded) island?.PrepareInitialLayout(); bool activate = window.ShowActivated; window.ShowActivated = false; window.Show(); window.ShowActivated = activate; }
            gameHidden.Clear(); windowsTaskbar?.Apply(store.Value.HideWindowsTaskbar);
        }
        ApplyHardwareOverlay();
    }
    private void WindowsPreferencesChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!IsExiting && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(() =>
        { ThemeService.Apply(store.Value, true); dock?.RefreshAppearance(); island?.RefreshAppearance(); }));
    }
    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    { IsExiting = true; store?.Flush(); base.OnSessionEnding(e); }
}
