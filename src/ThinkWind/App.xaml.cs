using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using OrbyLauncher.Native;
using OrbyLauncher.Services;
using OrbyLauncher.Views;
using Forms = System.Windows.Forms;

namespace OrbyLauncher;
public partial class App : Application
{
    private void SelectionControlLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ComboBox combo) return;
        combo.DropDownOpened -= PopupBackdrop.ComboOpened;
        combo.DropDownOpened += PopupBackdrop.ComboOpened;
    }
    private Mutex? instance;
    private Forms.NotifyIcon? tray;
    private DockWindow? dock;
    private IslandWindow? island;
    private readonly List<(DockWindow Dock, IslandWindow? Island)> extraMonitors = new();
    private string monitorSignature = "";
    private SettingsWindow? settings;
    internal GameOptimizer Optimizer { get; } = new();
    private VirtualKeyboardWindow? virtualKeyboard;
    internal UpdateController Updates { get; private set; } = null!;
    private UpdateNotice? updateNotice;
    private TutorialWindow? tutorial;
    internal void RepeatTutorial()
    {
        ClosePersonalization();
        tutorial ??= new TutorialWindow(store, () => gameMode?.Active == true || IsExiting);
        tutorial.Repeat();
    }
    internal void ContinueTutorial()
    {
        ClosePersonalization();
        tutorial ??= new TutorialWindow(store, () => gameMode?.Active == true || IsExiting);
        tutorial.Continue();
    }
    internal void RepeatUpdateTour()
    {
        ClosePersonalization();
        tutorial ??= new TutorialWindow(store, () => gameMode?.Active == true || IsExiting);
        tutorial.RepeatUpdateTour();
    }
    private WindowsTaskbar? windowsTaskbar;
    private WindowsStartKey? startKey;
    private LauncherWindowsKey? launcherWindowsKey;
    private ShellInputBridge? shellInput;
    private GameModeService? gameMode;
    private FullscreenHandleWindow? fullscreenHandle;
    internal HardwareTelemetry Telemetry { get; private set; } = null!;
    internal AlarmService Alarms { get; private set; } = null!;
    private AlarmNotice? alarmNotice;
    private readonly Queue<ClockAlarm> alarmQueue = new();
    private void ShowAlarm(ClockAlarm alarm)
    {
        alarmQueue.Enqueue(alarm); PresentNextAlarm();
    }
    private void PresentNextAlarm()
    {
        if (IsExiting || gameMode?.Active == true || alarmNotice is not null || alarmQueue.Count == 0) return;
        alarmNotice = new AlarmNotice(store, alarmQueue.Dequeue(), Alarms);
        alarmNotice.Closed += (_, _) => { alarmNotice = null; Dispatcher.BeginInvoke(new Action(PresentNextAlarm)); };
        alarmNotice.Show();
    }
    private HardwareOverlayWindow? hardwareOverlay;
    private OverlaySettingsWindow? overlaySettings;
    private OverlayHotkey? overlayHotkey;
    private readonly HashSet<Window> gameHidden = new();
    private bool diagnosing;
    private string? diagnosticReport;
    private int gameTransitions;
    private SettingsStore store = null!;
    private PreferenceStamp? preferenceStamp;
    internal ApplicationCatalog Catalog { get; private set; } = null!;
    private bool startupServicesReady;
    private readonly object startupSensorConsumer = new();
    internal bool IsExiting { get; private set; }
    internal double AnimationDuration => store?.Value.AnimationMilliseconds ?? 220;
    protected override void OnStartup(StartupEventArgs e)
    {
        AttachDiagnostics();
        base.OnStartup(e);
        if (StartupRegistration.Run(e.Args, out int startupRegistrationCode)) { Shutdown(startupRegistrationCode); return; }
        if (ShellInputBridge.Run(e.Args)) { Shutdown(); return; }
        AppBrand.InitializeWindowsIdentity();
        LaunchEnvironment.RemoveDevelopmentBootstrap();
        if (StartupPermissions.RunSetup(e.Args, out int startupSetupCode)) { Shutdown(startupSetupCode); return; }
        if (WindowActionSetup.RunSetup(e.Args, out int windowSetupExitCode)) { Shutdown(windowSetupExitCode); return; }
        if (AuthorizedWindowAction.Run(e.Args, out int authorizedWindowExitCode)) { Shutdown(authorizedWindowExitCode); return; }
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
        // Administrative roles above remain isolated. The visible launcher
        // must match Explorer's token to receive its OLE drag/drop operations.
        if (!configuringInstallation)
        {
            try { if (DesktopUserLauncher.RelaunchIfElevated(e.Args)) { Shutdown(); return; } }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                DiagnosticLog.Record("Inicialização da interface", error);
                try
                {
                    string log = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher");
                    System.IO.Directory.CreateDirectory(log);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(log, "desktop-ui-startup.log"), DateTime.UtcNow.ToString("O") + Environment.NewLine + error);
                }
                catch (Exception logError) when (logError is System.IO.IOException or UnauthorizedAccessException) { }
                MessageBox.Show("Não foi possível abrir a interface com permissões compatíveis com o arrasto do Explorador.\n\n" + error.Message,
                    "Orby Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(1); return;
            }
        }
        instance = new Mutex(true, @"Local\OrbyLauncher.Launcher", out var first);
        if (configuringInstallation)
        {
            Shutdown(first ? InstallationSetup.Run() : 2);
            return;
        }
        if (!first)
        {
            if (e.Args.Contains("--startup")) { Shutdown(0); return; }
            MessageBox.Show("Outra cópia do Orby Launcher já está aberta, possivelmente em outra pasta. Feche essa cópia pelo ícone na bandeja antes de abrir a versão instalada.", "Orby Launcher", MessageBoxButton.OK, MessageBoxImage.Information); Shutdown(2); return;
        }
        diagnosing = e.Args.Contains("--diagnostic");
        bool batteryPreview = e.Args.Contains("--simulate-battery");
        int reportIndex = Array.IndexOf(e.Args, "--diagnostic-report");
        diagnosticReport = reportIndex >= 0 && reportIndex + 1 < e.Args.Length ? e.Args[reportIndex + 1] : null;
        store = diagnosing ? new SettingsStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OrbyLauncherDiagnostic", Guid.NewGuid() + ".json")) : new SettingsStore();
        WindowActionSetup.AutomaticAuthorizationEnabled = store.Value.AutoAuthorizeWindowActions;
        bool initialGame = false;
        try { initialGame = GameModeService.Probe(store.Value); } catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        if (!diagnosing && !batteryPreview) StartupPriority.EnsureNormal();
        if (diagnosing) { store.Value.AutoHide = false; store.Value.GameModeEnabled = !e.Args.Contains("--diagnostic-normal"); }
        ThemeService.Apply(store.Value);
        Alarms = new AlarmService(store.AlarmStatePath);
        Alarms.Ring += ShowAlarm;
        Telemetry = new HardwareTelemetry(Dispatcher);
        Telemetry.SetMonitoringEnabled(store.Value.HardwareMonitoringEnabled);
        Telemetry.SelectGpu(store.Value.MonitoringGpuId ?? "");
        Telemetry.SelectDisk(store.Value.MonitoringDiskId);
        hardwareOverlay = new HardwareOverlayWindow(store, Telemetry);
        Catalog = new ApplicationCatalog(Dispatcher);
        Catalog.SetSuspended(initialGame);
        preferenceStamp = PreferenceStamp.Capture(store);
        store.Changed += PreferencesChanged;
        SystemEvents.UserPreferenceChanged += WindowsPreferencesChanged;
        SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Personalizar", null, (_, _) => Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Editar / concluir layout", null, (_, _) => Dispatcher.Invoke(() => store.SetEditing(!store.IsEditing)));
        menu.Items.Add("Mostrar barra de aplicativos e central de controle", null, (_, _) => Dispatcher.Invoke(() => { if (gameMode?.Active == true) return; dock?.Show(); island?.Show(); dock?.RefreshSettings(); island?.RefreshSettings(); }));
        menu.Items.Add("Sair do Orby Launcher", null, (_, _) => Dispatcher.Invoke(() => { IsExiting = true; Shutdown(); }));
        tray = new Forms.NotifyIcon { Text = "Orby Launcher", Icon = AppBrand.TrayIcon(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(OpenSettings);
        var localTrayImage = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(tray.Icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        localTrayImage.Freeze();
        TrayService.RegisterLocal(new TrayEntry("Orby Launcher", localTrayImage, null, 0, "local:orby-launcher", LocalAction: context =>
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (IsExiting || gameMode?.Active == true) return;
                if (context) menu.Show(Forms.Cursor.Position); else OpenSettings();
            }))));
        windowsTaskbar = new WindowsTaskbar(message =>
        {
            if (IsExiting) return;
            Dispatcher.BeginInvoke(new Action(() => { store.Update(p => p.HideWindowsTaskbar = false); MessageBox.Show(message, "Orby Launcher"); }));
        });
        windowsTaskbar.Apply(store.Value.HideWindowsTaskbar);
        gameMode = new GameModeService(Dispatcher, ChangeGameMode, initialGame);
        gameMode.ForegroundChanged += RefreshForegroundLayouts;
        VirtualKeyboardInput.RememberTarget();
        gameMode.ForegroundChanged += VirtualKeyboardInput.RememberTarget;
        if (initialGame) gameTransitions++;
        gameMode.Configure(store.Value); ConfigureStartHotkey();
        ConfigureMonitors();
        // ConfigureMonitors owns the initial windows as well as later rebuilds.
        if (e.Args.Contains("--inspect"))
            foreach (var window in Windows.OfType<FloatingWindow>()) window.ShowInTaskbar = true;
        InitializeUpdates();
        ApplyHardwareOverlay();
        // Window families are built once by ConfigureMonitors. Let their first
        // layout/render complete before warming nonvisual services. The cache
        // still starts at launch, without waiting for the monitoring panel.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (IsExiting) return;
            if (!diagnosing && !batteryPreview) WindowsUpdatePower.StartMonitoring();
            Optimizer.Recover();
            startupServicesReady = true;
            ApplyHardwareOverlay();
            if (!diagnosing && !batteryPreview)
            {
                try { SystemActions.ApplyStartupDefault(store); }
                catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
                { MessageBox.Show("Não foi possível configurar a inicialização automática: " + error.Message, "Orby Launcher"); }
            }
            if (!diagnosing && gameMode?.Active != true) _ = WifiService.PrimeAsync();
        }));
        if (!diagnosing && !initialGame && !batteryPreview && ((store.Value.HardwareMonitoringEnabled && store.Value.HardwareSensorsAutoSetup && !AdvancedSensorSetup.IsConfigured)
            || (store.Value.AutoAuthorizeWindowActions && !WindowActionSetup.IsConfigured)))
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (IsExiting || gameMode?.Active == true) return;
                try
                {
                    if (store.Value.HardwareMonitoringEnabled && store.Value.HardwareSensorsAutoSetup) store.Update(p => p.HardwareSensorsSetupAttempted = true);
                    if (await StartupPermissions.Prepare(store.Value.HardwareMonitoringEnabled && store.Value.HardwareSensorsAutoSetup, store.Value.AutoAuthorizeWindowActions))
                    { Telemetry.RefreshAuthorizedSensors(); ConfigureStartHotkey(); }
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
                { MessageBox.Show("Não foi possível preparar os auxiliares: " + error.Message, "Orby Launcher"); }
            }), System.Windows.Threading.DispatcherPriority.ContextIdle);
        if (!diagnosing)
        {
            tutorial = new TutorialWindow(store, () => gameMode?.Active == true || IsExiting);
            Dispatcher.BeginInvoke(new Action(() => { tutorial.OfferFirstRun(); tutorial.Suspend(gameMode?.Active == true); }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        if (diagnosing)
        {
            var exit = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            exit.Tick += (_, _) => { exit.Stop(); WriteDiagnosticReport(); IsExiting = true; Shutdown(); }; exit.Start();
        }
        if (e.Args.Contains("--settings")) OpenSettings();
        int readyIndex = Array.IndexOf(e.Args, "--update-ready-event");
        if (readyIndex >= 0 && readyIndex + 1 < e.Args.Length
            && System.Text.RegularExpressions.Regex.IsMatch(e.Args[readyIndex + 1], @"^Local\\OrbyLauncher\.UpdateReady\.[a-f0-9]{32}$"))
        {
            try { using var ready = EventWaitHandle.OpenExisting(e.Args[readyIndex + 1]); ready.Set(); }
            catch (Exception error) when (error is WaitHandleCannotBeOpenedException or UnauthorizedAccessException) { }
        }
        if (store.LoadWarning is not null) MessageBox.Show(store.LoadWarning, "Orby Launcher");
    }
    internal void OpenWeatherSettings()
    {
        OpenSettings();
        settings?.ShowWeatherSection();
    }
    internal void ToggleVirtualKeyboard(bool forceCustom = false)
    {
        if (IsExiting || gameMode?.Active == true) return;
        if (!forceCustom && store.Value.KeyboardUseNative) { SystemActions.Run("osk.exe"); return; }
        virtualKeyboard ??= new VirtualKeyboardWindow(store);
        if (virtualKeyboard.IsVisible) { virtualKeyboard.Hide(); return; }
        VirtualKeyboardInput.RememberTarget(); VirtualKeyboardInput.RestoreTarget();
        virtualKeyboard.Show(); virtualKeyboard.Position();
    }
    internal void ClosePersonalization()
    {
        if (store.IsEditing) store.SetEditing(false);
        settings?.Close();
    }
    internal void OpenWidgetSettings()
    {
        OpenSettings(); settings?.ShowWidgetsSection();
    }
    internal (string Label, Action Action)[] TrayActions() => new (string, Action)[]
    {
        ("Personalizar", OpenSettings),
        ("Editar / concluir layout", () => store.SetEditing(!store.IsEditing)),
        ("Mostrar barra e central de controle", () =>
        {
            if (gameMode?.Active == true) return;
            dock?.Show(); island?.Show(); dock?.RefreshSettings(); island?.RefreshSettings();
        }),
        ("Sair do Orby Launcher", () => { IsExiting = true; Shutdown(); })
    };
    private void OpenSettings()
    {
        if (gameMode?.Active == true) return;
        TutorialGuide.Notify("settings-open");
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
    private readonly HashSet<object> overlayEditors = new();
    internal void SetOverlayEditorPreview(object editor, bool visible)
    {
        if (visible) overlayEditors.Add(editor); else overlayEditors.Remove(editor);
        if (!IsExiting && Telemetry is not null) ApplyHardwareOverlay();
    }
    private async void RunSensorWorker(bool captureFrames)
    {
        int result = 1;
        try { result = await AdvancedSensorWorker.Run(Dispatcher, captureFrames); }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        {
            DiagnosticLog.Record(captureFrames ? "Coletor de FPS" : "Coletor de sensores", error);
            try { System.IO.File.WriteAllText(System.IO.Path.Combine(AdvancedSensorSetup.DirectoryPath, "sensor-worker-error.log"), DateTime.UtcNow.ToString("O") + Environment.NewLine + error); }
            catch (Exception logError) when (logError is System.IO.IOException or UnauthorizedAccessException) { }
        }
        Shutdown(result);
    }
    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        WindowsUpdatePower.StopMonitoring();
        if (Alarms is not null) { Alarms.Ring -= ShowAlarm; Alarms.Dispose(); }
        alarmQueue.Clear(); alarmNotice?.Close();
        Optimizer.Restore();
        virtualKeyboard?.Retire();
        updateNotice?.Close(); Updates?.Dispose();
        tutorial?.Close();
        windowsTaskbar?.Dispose();
        startKey?.Dispose();
        launcherWindowsKey?.Dispose();
        shellInput?.Dispose();
        if (diagnosticReport is not null && !System.IO.File.Exists(diagnosticReport)) WriteDiagnosticReport();
        gameMode?.Dispose();
        overlayHotkey?.Dispose(); hardwareOverlay?.Close();
        Telemetry?.Dispose();
        Catalog?.Dispose();
        fullscreenHandle?.Close();
        TrayService.RegisterLocal(null);
        SystemEvents.UserPreferenceChanged -= WindowsPreferencesChanged;
        SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;
        if (store is not null) { store.Changed -= PreferencesChanged; store.Flush(); }
        if (tray is not null) { tray.Visible = false; tray.Icon?.Dispose(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        BackdropComposition.Shutdown();
        instance?.Dispose();
        DetachDiagnostics();
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
            if (editing || preferenceStamp?.Island != next.Island) pair.Island?.RefreshSettings(); else pair.Island?.RefreshAppearance();
        }
        preferenceStamp = next;
    }
    private void ConfigureStartHotkey()
    {
        WindowActionSetup.AutomaticAuthorizationEnabled = store.Value.AutoAuthorizeWindowActions;
        // The isolated Windows key remains ours during games as requested.
        // The dedicated hook only posts the action; it does no UI/system work.
        bool interceptWindows = store.Value.WindowsKeyOpensLauncher;
        shellInput ??= new ShellInputBridge(Dispatcher, OpenStartFromKeyboard, ConfigureStartHotkey);
        shellInput.Configure(interceptWindows && store.Value.AutoAuthorizeWindowActions);
        if (!interceptWindows || (shellInput.Active && store.Value.AutoAuthorizeWindowActions)) { launcherWindowsKey?.Dispose(); launcherWindowsKey = null; }
        else launcherWindowsKey ??= new LauncherWindowsKey(Dispatcher, OpenStartFromKeyboard);
        bool enabled = store.Value.StartHotkeyEnabled;
        if (!enabled) { startKey?.Dispose(); startKey = null; return; }
        if (startKey is not null) return;
        try { startKey = new WindowsStartKey(OpenStartFromKeyboard); }
        catch (System.ComponentModel.Win32Exception) { /* Shortcut remains unavailable; normal Start button works. */ }
    }
    private void OpenStartFromKeyboard()
    {
        if (IsExiting) return;
        PrimaryDock?.ToggleStartFromKeyboard(gameMode?.Active == true);
    }
    private void ApplyHardwareOverlay()
    {
        Telemetry.SetMonitoringEnabled(store.Value.HardwareMonitoringEnabled);
        if (startupServicesReady) Optimizer.Apply(gameMode?.RecognizedGame == true, store.Value);
        Telemetry.SelectGpu(store.Value.MonitoringGpuId ?? "");
        Telemetry.SelectDisk(store.Value.MonitoringDiskId);
        if (!diagnosing) Telemetry.SetActive(startupSensorConsumer, startupServicesReady && !IsExiting && gameMode?.Active != true);
        Telemetry.FrameProcessId = gameMode?.OverlayProcessId ?? 0;
        hardwareOverlay?.Apply(store.Value.HardwareMonitoringEnabled && gameMode?.OverlayGameActive == true,
            store.Value.HardwareMonitoringEnabled && overlayEditors.Count > 0);
        if (!store.Value.OverlayHotkeyEnabled) { overlayHotkey?.Dispose(); overlayHotkey = null; }
        else if (overlayHotkey is null)
        {
            try { overlayHotkey = new OverlayHotkey(() => store.Update(p => p.HardwareOverlayEnabled = !p.HardwareOverlayEnabled)); }
            catch (System.ComponentModel.Win32Exception) { /* Hotkey may belong to another app; panel remains usable. */ }
        }
    }
    private void ChangeGameMode(bool active)
    {
        if (active) { updateNotice?.Close(); updateNotice = null; }
        tutorial?.Suspend(active);
        if (IsExiting) return; gameTransitions++;
        ConfigureStartHotkey();
        Catalog?.SetSuspended(active);
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
            foreach (var pair in extraMonitors) pair.Island?.EndFullscreenReveal();
            foreach (var window in gameHidden.Where(window => Windows.OfType<Window>().Contains(window)).ToArray())
            { if (ReferenceEquals(window, island) && !window.IsLoaded) island?.PrepareInitialLayout(); bool activate = window.ShowActivated; window.ShowActivated = false; window.Show(); window.ShowActivated = activate; }
            gameHidden.Clear(); windowsTaskbar?.Apply(store.Value.HideWindowsTaskbar);
            PresentNextAlarm();
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
