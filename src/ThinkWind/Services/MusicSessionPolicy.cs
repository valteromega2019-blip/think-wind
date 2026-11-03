using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Media;
using Windows.Media.Control;

namespace OrbyLauncher.Services;

internal sealed record MediaAppIdentity(IReadOnlyList<string> AppIds, IReadOnlyList<string> Executables, IReadOnlyList<string> WindowTitles, string Name, bool BrowserGroup = false);
internal sealed record MusicPreviewSelection(GlobalSystemMediaTransportControlsSession Session, bool MusicIdentity)
{
    internal bool KeepIdle { get; init; }
    internal MediaState? LastState { get; init; }
    internal string SourceId { get; } = Session.SourceAppUserModelId;
}

internal static class MusicSessionPolicy
{
    private static readonly string[] MusicApps = { "spotify", "musicbee", "foobar", "aimp", "itunes", "applemusic", "zunemusic", "tidal", "deezer", "qobuz", "winamp", "youtubemusic", "ytmusic", "ytmdesktop" };
    private static string Compact(string text) => string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    internal static bool KnownMusic(string text) => MusicApps.Any(Compact(text).Contains);
    internal static bool BrowserSource(string id) => new[] { "chrome", "msedge", "microsoftedge", "firefox", "brave", "opera", "vivaldi", "chromium", "browser" }.Any(Compact(id).Contains);
    private static string BrowserFamily(string id)
    {
        string value = Compact(id);
        if (value.Contains("msedge") || value.Contains("microsoftedge")) return "edge";
        foreach (string family in new[] { "brave", "vivaldi", "opera", "firefox", "chromium", "chrome" })
            if (value.Contains(family)) return family;
        return "";
    }
    internal static bool HasControls(GlobalSystemMediaTransportControlsSessionPlaybackControls controls) =>
        controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled || controls.IsNextEnabled || controls.IsPreviousEnabled;
    internal static bool IsMusic(string source, MediaPlaybackType? type, bool verifiedMusicIdentity = false)
    {
        // A video must never become music just because its source name looks familiar.
        if (type is MediaPlaybackType.Video or MediaPlaybackType.Image) return false;
        if (type == MediaPlaybackType.Music) return true;
        // Unknown-type browser media is rejected unless a dedicated app identity
        // has been matched, rather than guessing from a YouTube video title.
        return verifiedMusicIdentity || (!BrowserSource(source) && KnownMusic(source));
    }
    internal static bool Matches(string source, string title, MediaAppIdentity identity, out bool dedicatedMusic)
    {
        bool exactApp = identity.AppIds.Any(id => id.Equals(source, StringComparison.OrdinalIgnoreCase));
        bool browser = BrowserSource(source);
        bool pwaIdentity = identity.AppIds.Any(id => id.Equals(source, StringComparison.OrdinalIgnoreCase)
            && id.Contains("_crx_", StringComparison.OrdinalIgnoreCase));
        dedicatedMusic = exactApp && KnownMusic(identity.Name) && (!browser || pwaIdentity);
        if (exactApp && !BrowserSource(source)) return true;
        string sourceFile = FileStem(source);
        bool executable = identity.Executables.Any(path => FileStem(path).Equals(sourceFile, StringComparison.OrdinalIgnoreCase));
        if (browser && BrowserFamily(source) is { Length: > 0 } family)
            executable |= identity.Executables.Any(path => BrowserFamily(FileStem(path)) == family);
        bool knownAlias = MusicApps.Any(alias => Compact(source).Contains(alias) && identity.Executables.Any(path => Compact(FileStem(path)).Contains(alias)));
        if (BrowserSource(source))
        {
            // A browser-wide Dock group can own a background music session.
            // Per-app/PWA items still require a title or exact identity match.
            // IsMusic and unique-session selection remain mandatory afterwards.
            return (exactApp && dedicatedMusic) || ((exactApp || executable) && identity.BrowserGroup)
                || ((exactApp || executable) && !string.IsNullOrWhiteSpace(title)
                && identity.WindowTitles.Any(window => window.Contains(title, StringComparison.OrdinalIgnoreCase)));
        }
        return exactApp || executable || knownAlias;
    }
    private static string FileStem(string value)
    {
        value = value.Trim().Trim('"');
        if (value.Contains('!')) return value;
        try { return Path.GetFileNameWithoutExtension(value); }
        catch (ArgumentException) { return value; }
    }
}
