using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;

namespace OrbyLauncher.Services;
public sealed record Shortcut(string Name, string Target);
public sealed record TrayActivity(int Count, DateTime LastUsedUtc, DateTime FirstSeenUtc);
public sealed class Preferences
{
    public bool OptimizerEnabled { get; set; }
    public string OptimizerPowerPlan { get; set; } = "Keep";
    public bool KeyboardUseNative { get; set; }
    public bool KeyboardNumberPad { get; set; } = true;
    public double KeyboardScale { get; set; } = 1;
    public double? KeyboardWidth { get; set; }
    public double? KeyboardHeight { get; set; }
    public string KeyboardLayout { get; set; } = "PT-BR";
    public bool TutorialPending { get; set; }
    public double DockScale { get; set; } = 1;
    public double TintOpacity { get; set; } = .72;
    public double InnerSurfaceOpacity { get; set; } = .5;
    public bool GameModeEnabled { get; set; } = true;
    // Legacy preference retained for reading older profiles; no longer used
    // to classify ordinary foreground applications as games.
    public bool GameModeProtectAllForeground { get; set; }
    public bool StartHotkeyEnabled { get; set; }
    public bool WindowsKeyOpensLauncher { get; set; } = true;
    public bool WindowPreviewsEnabled { get; set; } = true;
    public bool CallPreviewsEnabled { get; set; } = true;
    public double WindowPreviewDelayMilliseconds { get; set; } = 400;
    public string GameModeInclude { get; set; } = "";
    public string GameModeExclude { get; set; } = "msedge;chrome;firefox;Code;steam;steamwebhelper";
    public bool AutoHide { get; set; } = true;
    public bool DockVisibleOnDesktop { get; set; } = false;
    public bool HideOverFullscreen { get; set; } = true;
    public bool IslandReserveTop { get; set; } = true;
    public bool HardwareOverlayEnabled { get; set; } = false;
    public bool HardwareMonitoringEnabled { get; set; } = true;
    public bool AutoAuthorizeWindowActions { get; set; } = true;
    public string MonitoringGpuId { get; set; } = "";
    public string MonitoringDiskId { get; set; } = "";
    public List<string> CompactMonitoringMetrics { get; set; } = new() { "ram" };
    public bool OverlayAutomatic { get; set; } = true;
    public bool OverlayHotkeyEnabled { get; set; } = true;
    public OverlayAppearanceOptions OverlayAppearance { get; set; } = new();
    public List<string> OverlayVisibleMetrics { get; set; } = OverlayMetrics.Defaults().ToList();
    public string OverlayCorner { get; set; } = "TL";
    public bool OverlayCustomPosition { get; set; }
    public double OverlayPositionX { get; set; }
    public double OverlayPositionY { get; set; }
    public double OverlayScale { get; set; } = 1;
    public bool HardwareSensorsSetupAttempted { get; set; } = false;
    public bool HardwareSensorsAutoSetup { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool StartupDefaultsInitialized { get; set; } = false;
    public int StartupRegistrationVersion { get; set; }
    public bool DockMagnification { get; set; } = true;
    public double DockMagnificationAmount { get; set; } = .5;
    public double DockMagnificationSpeed { get; set; } = 1;
    public int DockMagnificationNeighbors { get; set; } = 2;
    public bool HideWindowsTaskbar { get; set; }
    public double DockCollapseSeconds { get; set; } = .8;
    public string DockSearchMode { get; set; } = "Bar";
    public List<Shortcut> StartPins { get; set; } = new() { new("Explorador", "explorer.exe"), new("Configurações", "ms-settings:") };
    public List<Shortcut> RecentLaunches { get; set; } = new();
    public List<string> TrayFavorites { get; set; } = new();
    public Dictionary<string, TrayActivity> TrayActivity { get; set; } = new();
    public bool ShowMemory { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public bool ShowBatteryPercentage { get; set; }
    public bool ShowAudio { get; set; } = true;
    public int MonitorIndex { get; set; }
    public string? MonitorDeviceName { get; set; }
    public bool MultiMonitorEnabled { get; set; }
    public bool SecondaryIslandsEnabled { get; set; }
    public bool AllMonitors { get; set; } = true;
    public List<string> ActiveMonitors { get; set; } = new();
    public string Theme { get; set; } = "Auto";
    public double FontScale { get; set; } = 1;
    public string FontFamily { get; set; } = Typography.DefaultFamily;
    public string FontColor { get; set; } = Typography.DefaultColor;
    public bool AutomaticTextContrast { get; set; } = true;
    public string AccentColor { get; set; } = "#60CDFF";
    public bool UseCustomBarColor { get; set; }
    public string BarBackgroundColor { get; set; } = "#111318";
    public List<string> WidgetOrder { get; set; } = new() { "weather", "notes", "favorites" };
    public List<string> HiddenWidgets { get; set; } = new();
    public bool UnifiedWidgetLayout { get; set; }
    public string WidgetNote { get; set; } = "";
    public List<Shortcut> WidgetFavorites { get; set; } = new();
    public double? DockOpacity { get; set; }
    public double? IslandOpacity { get; set; }
    public string DockMaterial { get; set; } = "Acrylic";
    public string IslandMaterial { get; set; } = "Acrylic";
    public double DockRadius { get; set; } = 22;
    public double IslandRadius { get; set; } = 24;
    public double DockSpacing { get; set; } = 6;
    public double IconSize { get; set; } = 28;
    public double DockBottomOffset { get; set; } = 12;
    public double DockHorizontalOffset { get; set; }
    public double IslandHorizontalOffset { get; set; }
    public double IslandScale { get; set; } = 1;
    public double IslandExpandedWidth { get; set; } = 780;
    public string IslandPanelSide { get; set; } = "Right";
    public double IslandSpacing { get; set; } = 6;
    public bool IslandAutoCollapse { get; set; } = true;
    public bool IslandReduceOverMaximized { get; set; } = true;
    public bool IslandMatchAppColor { get; set; } = true;
    public string WeatherCity { get; set; } = "";
    public bool ShowWeatherInHeader { get; set; } = true;
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public double IslandCollapseSeconds { get; set; } = .45;
    public double AnimationMilliseconds { get; set; } = 220;
    public List<string> DockOrder { get; set; } = new();
    public List<string> QuickOrder { get; set; } = new() { "clock", "battery", "wifi", "bluetooth", "audio", "tray", "notifications", "calendar", "airplane", "settings" };
    public List<string> HiddenQuickControls { get; set; } = new() { "calendar", "airplane", "settings", "keyboard" };
    public List<string> DisabledQuickControls { get; set; } = new();
    public List<string> ModuleOrder { get; set; } = new() { "connections", "media", "audio", "metrics", "calendar" };
    public List<string> HiddenModules { get; set; } = new();
    public List<Shortcut> Shortcuts { get; set; } = new() { new("Explorador", "explorer.exe"), new("Configurações", "ms-settings:") };
}
public sealed class SettingsStore
{
    private readonly string path;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool pending;
    public event EventHandler? Changed;
    public event EventHandler? PersistenceChanged;
    public string? SaveError { get; private set; }
    public bool IsEditing { get; private set; }
    internal bool IsCustomizing { get; private set; }
    internal int DockLayoutRevision { get; private set; }
    internal bool IsNewProfile { get; private set; }
    internal string TutorialStatePath => Path.Combine(Path.GetDirectoryName(path)!, "tutorial.json");
    internal string AlarmStatePath => Path.Combine(Path.GetDirectoryName(path)!, "alarms.json");
    public Preferences Value { get; private set; } = new();
    public string? LoadWarning { get; private set; }
    public SettingsStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "settings.json")) { }
    internal SettingsStore(string settingsPath)
    {
        path = Path.GetFullPath(settingsPath);
        IsNewProfile = !File.Exists(path);
        saveTimer.Tick += (_, _) => Flush();
        try
        {
            if (File.Exists(path)) Value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
            Normalize();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { LoadWarning = "Não foi possível ler as configurações. Serão usados os valores padrão; o arquivo existente só será substituído ao salvar."; }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
        pending = false;
        SaveError = null;
    }
    public void Update(Action<Preferences> change)
    {
        change(Value); Normalize(); pending = true;
        saveTimer.Stop(); saveTimer.Start();
        Changed?.Invoke(this, EventArgs.Empty);
        PersistenceChanged?.Invoke(this, EventArgs.Empty);
    }
    public bool HasPendingChanges => pending;
    public void Flush()
    {
        saveTimer.Stop();
        if (!pending) return;
        try { Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { SaveError = "Não foi possível salvar: " + e.Message; }
        PersistenceChanged?.Invoke(this, EventArgs.Empty);
    }
    public void SetEditing(bool enabled) { IsEditing = enabled; Changed?.Invoke(this, EventArgs.Empty); }
    internal void SetCustomizing(bool enabled) { IsCustomizing = enabled; Changed?.Invoke(this, EventArgs.Empty); }
    public bool ModuleVisible(string id) => !Value.HiddenModules.Contains(id) && (id != "audio" || Value.ShowAudio);
    public void ResetDock()
    {
        DockLayoutRevision++;
        Update(p =>
        {
            var defaults = new Preferences();
            p.Shortcuts = defaults.Shortcuts; p.DockOrder.Clear(); p.DockScale = 1;
            p.DockRadius = defaults.DockRadius; p.DockSpacing = defaults.DockSpacing; p.IconSize = defaults.IconSize;
            p.DockHorizontalOffset = 0; p.DockBottomOffset = defaults.DockBottomOffset;
            p.DockMaterial = defaults.DockMaterial; p.DockOpacity = .72; p.AutoHide = defaults.AutoHide;
            p.DockCollapseSeconds = defaults.DockCollapseSeconds; p.DockSearchMode = defaults.DockSearchMode;
            p.DockVisibleOnDesktop = defaults.DockVisibleOnDesktop;
            p.WindowPreviewsEnabled = defaults.WindowPreviewsEnabled; p.WindowPreviewDelayMilliseconds = defaults.WindowPreviewDelayMilliseconds;
            p.CallPreviewsEnabled = defaults.CallPreviewsEnabled;
            p.DockMagnification = defaults.DockMagnification; p.DockMagnificationAmount = defaults.DockMagnificationAmount;
            p.DockMagnificationSpeed = defaults.DockMagnificationSpeed; p.DockMagnificationNeighbors = defaults.DockMagnificationNeighbors;
        });
    }
    public void ResetIsland()
    {
        Update(p =>
        {
            var defaults = new Preferences();
            p.ModuleOrder = defaults.ModuleOrder; p.HiddenModules.Clear();
            p.QuickOrder = defaults.QuickOrder; p.HiddenQuickControls = defaults.HiddenQuickControls; p.DisabledQuickControls.Clear();
            p.ShowWeatherInHeader = defaults.ShowWeatherInHeader;
            p.ShowBatteryPercentage = defaults.ShowBatteryPercentage;
            p.IslandScale = 1; p.IslandExpandedWidth = defaults.IslandExpandedWidth; p.IslandSpacing = defaults.IslandSpacing;
            p.IslandRadius = defaults.IslandRadius; p.IslandHorizontalOffset = 0;
            p.IslandPanelSide = defaults.IslandPanelSide;
            p.IslandReserveTop = defaults.IslandReserveTop; p.HideOverFullscreen = defaults.HideOverFullscreen;
            p.IslandMatchAppColor = defaults.IslandMatchAppColor;
            p.IslandAutoCollapse = defaults.IslandAutoCollapse; p.IslandCollapseSeconds = defaults.IslandCollapseSeconds;
            p.IslandMaterial = defaults.IslandMaterial; p.IslandOpacity = .72;
            p.ShowAudio = p.ShowMemory = p.ShowBattery = true;
        });
    }
    public void ResetAll() { DockLayoutRevision++; Value = new Preferences(); Update(_ => { }); }
    private static double Limit(double value, double min, double max, double fallback) => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    private void Normalize()
    {
        Value.MonitoringDiskId = (Value.MonitoringDiskId ?? "").Trim().TrimEnd('\\').ToUpperInvariant();
        if (Value.MonitoringDiskId.Length != 2 || Value.MonitoringDiskId[0] is < 'A' or > 'Z' || Value.MonitoringDiskId[1] != ':') Value.MonitoringDiskId = "";
        Value.CompactMonitoringMetrics = (Value.CompactMonitoringMetrics ?? new() { "ram" })
            .Where(id => id is "cpu" or "gpu" or "ram" or "disk").Distinct().Take(3).ToList();
        Value.KeyboardLayout = Value.KeyboardLayout == "US" ? "US" : "PT-BR";
        Value.KeyboardScale = Limit(Value.KeyboardScale, .7, 1.3, 1);
        Value.KeyboardWidth = Value.KeyboardWidth is double keyboardWidth && double.IsFinite(keyboardWidth) ? Math.Clamp(keyboardWidth, 480, 2400) : null;
        Value.KeyboardHeight = Value.KeyboardHeight is double keyboardHeight && double.IsFinite(keyboardHeight) ? Math.Clamp(keyboardHeight, 260, 1200) : null;
        Value.OverlayAutomatic = true;
        Value.OverlayAppearance = (Value.OverlayAppearance ?? new()).Normalize();
        Value.OverlayVisibleMetrics = (Value.OverlayVisibleMetrics ?? OverlayMetrics.Defaults().ToList())
            .Where(key => OverlayMetrics.All.Any(metric => metric.Key == key)).Distinct().ToList();
        Value.OverlayCorner = Value.OverlayCorner is "TL" or "TR" or "BL" or "BR" ? Value.OverlayCorner : "TL";
        Value.OverlayPositionX = double.IsFinite(Value.OverlayPositionX) ? Math.Clamp(Value.OverlayPositionX, 0, 100) : 0;
        Value.OverlayPositionY = double.IsFinite(Value.OverlayPositionY) ? Math.Clamp(Value.OverlayPositionY, 0, 100) : 0;
        Value.OverlayScale = double.IsFinite(Value.OverlayScale) ? Math.Clamp(Value.OverlayScale, .7, 1.5) : 1;
        var p = Value;
        p.ActiveMonitors = (p.ActiveMonitors ?? new()).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        p.DockScale = Limit(p.DockScale, .75, 1.5, 1); p.TintOpacity = Limit(p.TintOpacity, .2, 1, .72);
        p.DockCollapseSeconds = Limit(p.DockCollapseSeconds, 0, 10, .8);
        p.InnerSurfaceOpacity = Limit(p.InnerSurfaceOpacity, .1, .9, .5);
        p.WindowPreviewDelayMilliseconds = Limit(p.WindowPreviewDelayMilliseconds, 150, 1500, 400);
        p.GameModeInclude = (p.GameModeInclude ?? "").Trim(); p.GameModeExclude = (p.GameModeExclude ?? "").Trim();
        p.MonitoringGpuId = (p.MonitoringGpuId ?? "").Trim();
        p.DockMagnificationAmount = Limit(p.DockMagnificationAmount, .4, .6, .5);
        p.DockMagnificationSpeed = Limit(p.DockMagnificationSpeed, .5, 2, 1);
        p.DockMagnificationNeighbors = Math.Clamp(p.DockMagnificationNeighbors, 0, 4);
        p.DockSearchMode = p.DockSearchMode is "Icon" or "Bar" ? p.DockSearchMode : "Bar";
        p.StartPins = (p.StartPins ?? new()).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Target)).DistinctBy(s => s.Target, StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        p.RecentLaunches = (p.RecentLaunches ?? new()).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Target)).DistinctBy(s => s.Target, StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        p.TrayFavorites = (p.TrayFavorites ?? new()).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).Take(128).ToList();
        p.TrayActivity = (p.TrayActivity ?? new()).Where(item => !string.IsNullOrWhiteSpace(item.Key) && item.Value is not null).OrderByDescending(item => item.Value.LastUsedUtc > item.Value.FirstSeenUtc ? item.Value.LastUsedUtc : item.Value.FirstSeenUtc).Take(512)
            .ToDictionary(item => item.Key, item => item.Value with { Count = Math.Clamp(item.Value.Count, 0, 1_000_000) }, StringComparer.Ordinal);
        p.DockOpacity = Limit(p.DockOpacity ?? p.TintOpacity, .05, 1, .72);
        p.IslandOpacity = Limit(p.IslandOpacity ?? p.TintOpacity, .05, 1, .72);
        p.DockRadius = Limit(p.DockRadius, 0, 48, 22); p.IslandRadius = Limit(p.IslandRadius, 0, 48, 24);
        p.DockSpacing = Limit(p.DockSpacing, 0, 24, 6); p.IslandSpacing = Limit(p.IslandSpacing, 0, 20, 6);
        p.IconSize = Limit(p.IconSize, 20, 48, 28); p.DockBottomOffset = Limit(p.DockBottomOffset, 4, 80, 12);
        p.DockHorizontalOffset = Limit(p.DockHorizontalOffset, -4000, 4000, 0);
        p.IslandHorizontalOffset = Limit(p.IslandHorizontalOffset, -4000, 4000, 0);
        p.IslandScale = Limit(p.IslandScale, .8, 1.4, 1); p.IslandExpandedWidth = Limit(p.IslandExpandedWidth, 400, 900, 780);
        p.IslandCollapseSeconds = Limit(p.IslandCollapseSeconds, 0, 10, .45);
        p.IslandPanelSide = p.IslandPanelSide is "Left" or "Right" ? p.IslandPanelSide : "Right";
        p.AnimationMilliseconds = Limit(p.AnimationMilliseconds, 0, 600, 220);
        p.Theme = p.Theme is "Auto" or "Dark" or "Light" ? p.Theme : "Auto";
        p.FontScale = Limit(p.FontScale, .8, 2.5, 1);
        p.FontFamily = Typography.NormalizeFamily(p.FontFamily);
        if (!OverlayAppearanceOptions.IsValidColor(p.FontColor)) p.FontColor = Typography.DefaultColor;
        p.DockMaterial = Material(p.DockMaterial); p.IslandMaterial = Material(p.IslandMaterial);
        try { if (System.Windows.Media.ColorConverter.ConvertFromString(p.AccentColor) is not System.Windows.Media.Color c || c.A != 255) p.AccentColor = "#60CDFF"; }
        catch (Exception e) when (e is FormatException or NotSupportedException or ArgumentException) { p.AccentColor = "#60CDFF"; }
        try { if (System.Windows.Media.ColorConverter.ConvertFromString(p.BarBackgroundColor) is not System.Windows.Media.Color background || background.A != 255) p.BarBackgroundColor = "#111318"; }
        catch (Exception e) when (e is FormatException or NotSupportedException or ArgumentException) { p.BarBackgroundColor = "#111318"; }
        p.Shortcuts = (p.Shortcuts ?? new()).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Target)).DistinctBy(s => s.Target, StringComparer.OrdinalIgnoreCase).ToList();
        p.DockOrder = (p.DockOrder ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(256).ToList();
        var quickIds = LayoutCatalog.QuickControls.Select(q => q.Id).ToArray();
        // Existing profiles did not have this optional control. Introduce it
        // hidden once; subsequent saves preserve the user's chosen visibility.
        if (p.QuickOrder?.Contains("keyboard") != true)
        {
            p.HiddenQuickControls ??= new();
            if (!p.HiddenQuickControls.Contains("keyboard")) p.HiddenQuickControls.Add("keyboard");
        }
        // Introduce the battery beside the clock once. Visibility follows real
        // battery presence at runtime; later user placement/disable choices win.
        p.QuickOrder ??= new();
        if (!p.QuickOrder.Contains("battery")) p.QuickOrder.Insert(Math.Max(0, p.QuickOrder.IndexOf("clock") + 1), "battery");
        p.QuickOrder = LayoutCatalog.Complete(p.QuickOrder, quickIds);
        if (!p.UnifiedWidgetLayout)
        {
            var previousWidgets = LayoutCatalog.Complete(p.WidgetOrder, new[] { "weather", "notes", "favorites" });
            p.ModuleOrder = (p.ModuleOrder ?? new()).SelectMany(id => id == "widgets" ? previousWidgets : new[] { id }.AsEnumerable()).ToList();
            p.HiddenModules ??= new();
            if (p.HiddenModules.Contains("widgets")) p.HiddenModules.AddRange(previousWidgets);
            p.HiddenModules.AddRange(p.HiddenWidgets ?? new());
            p.UnifiedWidgetLayout = true;
        }
        // Replace the old shortcuts-only notification card in place. The
        // independent notification quick control is intentionally unchanged.
        p.ModuleOrder = LayoutCatalog.Complete((p.ModuleOrder ?? new()).Select(id => id == "notifications" ? "calculator" : id), LayoutCatalog.Modules);
        p.HiddenQuickControls = (p.HiddenQuickControls ?? new()).Where(quickIds.Contains).Distinct().ToList();
        p.DisabledQuickControls = (p.DisabledQuickControls ?? new()).Where(id => id == "weather" || (id != "settings" && quickIds.Contains(id))).Distinct().ToList();
        p.HiddenModules = (p.HiddenModules ?? new()).Select(id => id == "notifications" ? "calculator" : id).Where(LayoutCatalog.Modules.Contains).Distinct().ToList();
        p.WidgetOrder = LayoutCatalog.Complete(p.WidgetOrder, new[] { "weather", "notes", "favorites" });
        p.HiddenWidgets = (p.HiddenWidgets ?? new()).Where(id => id is "weather" or "notes" or "favorites").Distinct().ToList();
        p.WidgetNote = (p.WidgetNote ?? "")[..Math.Min(4000, (p.WidgetNote ?? "").Length)];
        p.WidgetFavorites = (p.WidgetFavorites ?? new()).Where(item => item is not null && !string.IsNullOrWhiteSpace(item.Target))
            .DistinctBy(item => item.Target, StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }
    private static string Material(string? value) => value is "Acrylic" or "Mica" or "Solid" ? value : "Acrylic";
}
