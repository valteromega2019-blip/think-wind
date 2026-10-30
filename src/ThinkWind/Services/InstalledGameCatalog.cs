using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ThinkWind.Services;

// Read-only installation metadata. Cached for the existing foreground worker
// (and its initial startup probe);
// no process scanning, input hooks, recursive disk search or additional timers.
internal static class InstalledGameCatalog
{
    private static readonly object Gate = new();
    private static string[] roots = Array.Empty<string>();
    private static DateTime expires;
    internal static bool Contains(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return false;
        lock (Gate)
        {
            if (DateTime.UtcNow >= expires)
            {
                roots = ReadRoots().ToArray();
                expires = DateTime.UtcNow.AddMinutes(1);
            }
            return roots.Any(root => executable.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }
    }
    private static IEnumerable<string> ReadRoots()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return;
            try { result.Add(Path.GetFullPath(path).TrimEnd('\\', '/') + Path.DirectorySeparatorChar); }
            catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException) { }
        }
        void Steam(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            Add(Path.Combine(path, "steamapps", "common"));
            Safe(() =>
            {
                string file = Path.Combine(path, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return;
                foreach (Match match in Regex.Matches(File.ReadAllText(file), "\"path\"\\s*\"([^\"]+)\""))
                    Add(Path.Combine(match.Groups[1].Value.Replace("\\\\", "\\"), "steamapps", "common"));
            });
        }
        Safe(() =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            Steam(key?.GetValue("SteamPath") as string);
        });
        Safe(() =>
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            Steam(key?.GetValue("InstallPath") as string);
        });
        string manifests = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        Safe(() =>
        {
            if (!Directory.Exists(manifests)) return;
            foreach (string file in Directory.EnumerateFiles(manifests, "*.item").Take(1024)) Safe(() =>
            {
                if (new FileInfo(file).Length > 1024 * 1024) return;
                using var json = JsonDocument.Parse(File.ReadAllText(file));
                if (json.RootElement.TryGetProperty("InstallLocation", out var location) && location.ValueKind == JsonValueKind.String)
                    Add(location.GetString());
            });
        });
        foreach (string keyPath in new[] { @"SOFTWARE\GOG.com\Games", @"SOFTWARE\WOW6432Node\GOG.com\Games" }) Safe(() =>
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key is null) return;
            foreach (string name in key.GetSubKeyNames().Take(1024)) Safe(() =>
            {
                using var game = key.OpenSubKey(name); Add(game?.GetValue("path") as string);
            });
        });
        return result;
    }
    private static void Safe(Action read)
    {
        try { read(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException or ArgumentException) { }
    }
}
