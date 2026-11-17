using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

internal static class SteamLibraryEntries
{
    // Installation metadata only: no recursive search and no Steam process needed.
    internal static IEnumerable<AppEntry> Read(ShellIcons icons)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string steam = key?.GetValue("SteamPath") as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
        string? ReadSmall(string file)
        {
            try { return File.Exists(file) && new FileInfo(file).Length <= 1024 * 1024 ? File.ReadAllText(file) : null; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
        }
        string Value(string text, string field) => Regex.Match(text, "\"" + field + "\"\\s*\"([^\"]*)\"").Groups[1].Value;
        var folders = ReadSmall(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
        if (folders is not null)
            foreach (Match match in Regex.Matches(folders, "\"path\"\\s*\"([^\"]+)\""))
                libraries.Add(match.Groups[1].Value.Replace(@"\\", @"\"));
        var result = new List<AppEntry>();
        foreach (string library in libraries.Take(32))
        {
            try
            {
                string directory = Path.Combine(library, "steamapps");
                if (!Directory.Exists(directory)) continue;
                foreach (string file in Directory.EnumerateFiles(directory, "appmanifest_*.acf").Take(2048))
                {
                    string? text = ReadSmall(file); if (text is null) continue;
                    string id = Value(text, "appid"), name = Value(text, "name"), install = Value(text, "installdir");
                    if (!uint.TryParse(id, out _) || string.IsNullOrWhiteSpace(name) || id == "228980"
                        || name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Artcraft", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("Wallpaper Engine", StringComparison.OrdinalIgnoreCase)) continue;
                    string root = Path.GetFullPath(Path.Combine(directory, "common")) + Path.DirectorySeparatorChar;
                    string location = Path.GetFullPath(Path.Combine(root, install));
                    if (!location.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(location)) continue;
                    string target = "steam://rungameid/" + id;
                    string? executable = Directory.EnumerateFiles(location, "*.exe").FirstOrDefault(path => !Path.GetFileName(path).Contains("uninstall", StringComparison.OrdinalIgnoreCase));
                    result.Add(new(name, target, "Jogos", icons.Get(executable ?? target)));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return result;
    }
}
