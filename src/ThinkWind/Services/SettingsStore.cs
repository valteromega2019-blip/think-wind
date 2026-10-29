using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ThinkWind.Services;
public sealed record Shortcut(string Name, string Target);
public sealed class Preferences
{
    public double DockScale { get; set; } = 1;
    public double TintOpacity { get; set; } = .72;
    public bool AutoHide { get; set; }
    public bool ShowMemory { get; set; } = true;
    public bool ShowBattery { get; set; } = true;
    public int MonitorIndex { get; set; }
    public List<Shortcut> Shortcuts { get; set; } = new() { new("Explorador", "explorer.exe"), new("Configurações", "ms-settings:") };
}
public sealed class SettingsStore
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThinkWind", "settings.json");
    public Preferences Value { get; private set; } = new();
    public string? LoadWarning { get; private set; }
    public SettingsStore()
    {
        try
        {
            if (File.Exists(path)) Value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
            Value.DockScale = double.IsFinite(Value.DockScale) ? Math.Clamp(Value.DockScale, .75, 1.5) : 1;
            Value.TintOpacity = double.IsFinite(Value.TintOpacity) ? Math.Clamp(Value.TintOpacity, .2, 1) : .72;
            Value.Shortcuts = (Value.Shortcuts ?? new()).Where(s => s is not null && !string.IsNullOrWhiteSpace(s.Target)).ToList();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { LoadWarning = "Não foi possível ler as configurações. Serão usados os valores padrão; o arquivo existente só será substituído ao salvar."; }
    }
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(Value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
