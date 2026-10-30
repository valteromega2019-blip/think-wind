using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;

namespace ThinkWind.Services;
public sealed record Shortcut(string Name, string Target);
public sealed record TrayActivity(int Count, DateTime LastUsedUtc, DateTime FirstSeenUtc);
public sealed class Preferences
{
    public double DockScale { get; set; } = 1;
    public double TintOpacity { get; set; } = .72;
    public double InnerSurfaceOpacity { get; set; } = .5;
    public bool GameModeEnabled { get; set; } = true;
    public bool GameModeProtectAllForeground { get; set; }
    public bool StartHotkeyEnabled { get; set; }
    public bool WindowsKeyOpensLauncher { get; set; } = true;
    public bool WindowPreviewsEnabled { get; set; } = true;
    public double WindowPreviewDelayMilliseconds { get; set; } = 400;
    public string GameModeInclude { get; set; } = "";
    public string GameModeExclude { get; set; } = "msedge;chrome;firefox;Code;steam;steamwebhelper";
    public bool AutoHide { get; set; } = true;
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
    public bool ShowAudio { get; set; } = true;
    public int MonitorIndex { get; set; }
    public string? MonitorDeviceName { get; set; }
    public string Theme { get; set; } = "Auto";
    public string AccentColor { get; set; } = "#60CDFF";
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
    public double? WeatherLatitude { get; set; }
    public double? WeatherLongitude { get; set; }
    public double IslandCollapseSeconds { get; set; } = .45;
    public double AnimationMilliseconds { get; set; } = 220;
    public List<string> DockOrder { get; set; } = new();
    public List<string> QuickOrder { get; set; } = new() { "clock", "wifi", "bluetooth", "audio", "tray", "notifications", "calendar", "airplane", "settings" };
    public List<string> HiddenQuickControls { get; set; } = new() { "calendar", "airplane", "settings" };
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
    public Preferences Value { get; private set; } = new();
    public string? LoadWarning { get; private set; }
    public SettingsStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThinkWind", "settings.json")) { }
    internal SettingsStore(string settingsPath)
    {
        path = Path.GetFullPath(settingsPath);
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
            p.WindowPreviewsEnabled = defaults.WindowPreviewsEnabled; p.WindowPreviewDelayMilliseconds = defaults.WindowPreviewDelayMilliseconds;
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
            p.QuickOrder = defaults.QuickOrder; p.HiddenQuickControls = defaults.HiddenQuickControls;
            p.IslandScale = 1; p.IslandExpandedWidth = defaults.IslandExpandedWidth; p.IslandSpacing = defaults.IslandSpacing;
            p.IslandRadius = defaults.IslandRadius; p.IslandHorizontalOffset = 0;
            p.IslandPanelSide = defaults.IslandPanelSide;
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
        var p = Value;
        p.DockScale = Limit(p.DockScale, .75, 1.5, 1); p.TintOpacity = Limit(p.TintOpacity, .2, 1, .72);
        p.DockCollapseSeconds = Limit(p.DockCollapseSeconds, 0, 10, .8);
        p.InnerSurfaceOpacity = Limit(p.InnerSurfaceOpacity, .1, .9, .5);
        p.WindowPreviewDelayMilliseconds = Limit(p.WindowPreviewDelayMilliseconds, 150, 1500, 400);
        p.GameModeInclude = (p.GameModeInclude ?? "").Trim(); p.GameModeExclude = (p.GameModeExclude ?? "").Trim();
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
        p.DockMaterial = Material(p.DockMaterial); p.IslandMaterial = Material(p.IslandMaterial);
        try { if (System.Windows.Media.ColorConverter.ConvertFromString(p.AccentColor) is not System.Windows.Media.Color c || c.A != 255) p.AccentColor = "#60CDFF"; }
        catch (Exception e) when (e is FormatException or NotSupportedException or ArgumentException) { p.AccentColor = "#60CDFF"; }
        p.Shortcuts = (p.Shortcuts ?? new()).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Target)).DistinctBy(s => s.Target, StringComparer.OrdinalIgnoreCase).ToList();
        p.DockOrder = (p.DockOrder ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(256).ToList();
        var quickIds = LayoutCatalog.QuickControls.Select(q => q.Id).ToArray();
        p.QuickOrder = LayoutCatalog.Complete(p.QuickOrder, quickIds);
        p.ModuleOrder = LayoutCatalog.Complete(p.ModuleOrder, LayoutCatalog.Modules);
        p.HiddenQuickControls = (p.HiddenQuickControls ?? new()).Where(quickIds.Contains).Distinct().ToList();
        p.HiddenModules = (p.HiddenModules ?? new()).Where(LayoutCatalog.Modules.Contains).Distinct().ToList();
    }
    private static string Material(string? value) => value is "Acrylic" or "Mica" or "Solid" ? value : "Acrylic";
}
