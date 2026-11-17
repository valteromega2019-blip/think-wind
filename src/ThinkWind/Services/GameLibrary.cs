using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OrbyLauncher.Services;

internal static class GameLibrary
{
    private static HashSet<string> identities = new(StringComparer.Ordinal);
    private static HashSet<string> names = new(StringComparer.CurrentCultureIgnoreCase);
    internal static void Update(IEnumerable<AppEntry> entries)
    {
        var games = entries.Where(entry => entry.Category == "Jogos").ToArray();
        names = games.Select(entry => entry.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        identities = games
            .SelectMany(entry => CustomAppIcons.Keys(entry.Target, entry.AppId)
                .Concat(CustomAppIcons.Keys(entry.ResolvedTarget ?? "", entry.AppId)))
            .ToHashSet(StringComparer.Ordinal);
    }
    internal static bool IsGame(AppEntry entry) => entry.Category == "Jogos"
        || names.Contains(entry.Name) || IsGame(entry.Target, entry.AppId) || IsGame(entry.ResolvedTarget ?? "", entry.AppId);
    internal static bool IsGame(string target, string? appId) => CustomAppIcons.Keys(target, appId).Any(identities.Contains);
    // Called only on the existing catalog worker, never while drawing icons.
    internal static bool Detect(AppEntry entry)
    {
        if (entry.Category.Equals("Jogos", StringComparison.OrdinalIgnoreCase)) return true;
        string path = entry.ResolvedTarget ?? entry.Target;
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if (name is "steam" or "epicgameslauncher" or "goggalaxy" or "galaxyclient" or "battle.net" or "ubisoftconnect") return false;
        if (IsGameUri(path) || IsGameUri(entry.Target)) return true;
        if (Path.IsPathFullyQualified(path) && InstalledGameCatalog.Contains(path)) return true;
        return entry.Category.Equals("Jogos", StringComparison.OrdinalIgnoreCase)
            || entry.Category.Equals("Games", StringComparison.OrdinalIgnoreCase)
            || entry.Category.StartsWith("Games" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || entry.Category.StartsWith("Jogos" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
    internal static bool IsGameUri(string target) => target.StartsWith("steam://rungameid/", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("steam://run/", StringComparison.OrdinalIgnoreCase)
        || target.StartsWith("com.epicgames.launcher://apps/", StringComparison.OrdinalIgnoreCase)
            && target.Contains("action=launch", StringComparison.OrdinalIgnoreCase);
}
