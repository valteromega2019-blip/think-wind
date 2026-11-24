using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OrbyLauncher.Services;

// Read only the battery fields of the existing Start companion card.
// Undocumented local format: fail closed when absent, disconnected or ambiguous.
internal static class PhoneLinkBattery
{
    private static readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Packages", "Microsoft.YourPhone_8wekyb3d8bbwe", "LocalState", "StartMenu", "StartMenuCompanion.json");
    private static DateTime lastWrite;
    private static long lastLength;
    private static PeripheralBattery[] cached = Array.Empty<PeripheralBattery>();
    private static readonly Regex batteryLabel = new(
        @"^(?:Bateria com (?<level>[0-9]{1,3})% restante(?:, (?<charge>carregando|a carregar|em carregamento))?|Battery (?<level>[0-9]{1,3})% remaining(?:, (?<charge>charging))?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    internal static PeripheralBattery[] Read()
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length == 0 || file.Length > 1024 * 1024) return Array.Empty<PeripheralBattery>();
            var age = DateTime.UtcNow - file.LastWriteTimeUtc;
            if (age > TimeSpan.FromMinutes(10) || age < TimeSpan.FromMinutes(-5)) return Array.Empty<PeripheralBattery>();
            if (lastWrite != file.LastWriteTimeUtc || lastLength != file.Length)
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                // Bound reads even if the producer changes the file after the metadata check.
                var bytes = new byte[(int)file.Length + 1];
                int count = 0, read;
                while (count < bytes.Length && (read = stream.Read(bytes, count, bytes.Length - count)) > 0) count += read;
                if (count > file.Length) return Array.Empty<PeripheralBattery>();
                cached = Parse(bytes.AsMemory(0, count));
                lastWrite = file.LastWriteTimeUtc; lastLength = file.Length;
            }
            return age > TimeSpan.FromMinutes(2)
                ? Array.ConvertAll(cached, value => value with { Stale = true }) : cached;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        { return Array.Empty<PeripheralBattery>(); } // Never log card content or notifications.
    }

    internal static PeripheralBattery[] Parse(ReadOnlyMemory<byte> json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("speak", out var speak) || speak.ValueKind != JsonValueKind.String)
            return Array.Empty<PeripheralBattery>();
        string header = speak.GetString() ?? "", name = "";
        foreach (string suffix in new[] { " - Conectado ao computador", " - Connected to your PC" })
            if (header.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) name = header[..^suffix.Length].Trim();
        if (name.Length == 0 || name.Length > 120) return Array.Empty<PeripheralBattery>();
        var found = new HashSet<(int Percent, bool Charging)>();
        Scan(root, found);
        if (found.Count != 1) return Array.Empty<PeripheralBattery>();
        foreach (var value in found) return new[] { new PeripheralBattery(name, value.Percent, "\uE8EA", Charging: value.Charging) };
        return Array.Empty<PeripheralBattery>();
    }

    private static void Scan(JsonElement node, HashSet<(int, bool)> found)
    {
        if (node.ValueKind == JsonValueKind.Array) { foreach (var child in node.EnumerateArray()) Scan(child, found); return; }
        if (node.ValueKind != JsonValueKind.Object) return;
        if (!node.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return;
        if (type.GetString() is not ("AdaptiveCard" or "Container" or "ColumnSet" or "Column" or "Image" or "TextBlock")) return;
        if (node.TryGetProperty("tooltip", out var label) && label.ValueKind == JsonValueKind.String && label.GetString() is string text && text.Length <= 120)
        {
            var match = batteryLabel.Match(text);
            if (match.Success && int.TryParse(match.Groups["level"].Value, out int level) && level <= 100)
            {
                bool agrees = !node.TryGetProperty("text", out var visible) || visible.ValueKind == JsonValueKind.Null
                    || visible.ValueKind == JsonValueKind.String && (string.IsNullOrEmpty(visible.GetString()) || visible.GetString() == level + "%");
                if (agrees) found.Add((level, match.Groups["charge"].Success));
            }
        }
        foreach (string field in new[] { "body", "items", "columns" })
            if (node.TryGetProperty(field, out var children)) Scan(children, found);
    }
}
