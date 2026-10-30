using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.ApplicationModel;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace ThinkWind.Services;
internal sealed record MediaState(string Title, string Artist, string Player, ImageSource? Artwork, bool Playing,
    TimeSpan Start, TimeSpan End, TimeSpan Position, DateTimeOffset Updated, double Rate,
    TimeSpan SeekMin, TimeSpan SeekMax, bool CanSeek, bool CanToggle, bool CanNext, bool CanPrevious)
{
    internal TimeSpan CurrentPosition()
    {
        double elapsed = Playing ? Math.Max(0, (DateTimeOffset.UtcNow - Updated).TotalSeconds) * (double.IsFinite(Rate) ? Rate : 1) : 0;
        return TimeSpan.FromSeconds(Math.Clamp(Position.TotalSeconds + elapsed, Start.TotalSeconds, Math.Max(Start.TotalSeconds, End.TotalSeconds)));
    }
}
internal enum MediaCommand { Toggle, Previous, Next }
internal sealed class MediaService : IDisposable
{
    private readonly Dispatcher dispatcher;
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private volatile bool active, disposed;
    private bool refreshing, refreshAgain;
    private Task? initializing;
    private GlobalSystemMediaTransportControlsSessionMediaProperties? metadata;
    private ImageSource? art;
    private bool metadataDirty = true;
    private string playerId = "", playerName = "";
    private readonly HashSet<GlobalSystemMediaTransportControlsSession> watched = new();
    internal MediaState? State { get; private set; }
    internal string? Error { get; private set; }
    internal event Action? Changed;
    internal MediaService(Dispatcher uiDispatcher) { dispatcher = uiDispatcher; }
    internal async Task SetActiveAsync(bool value)
    {
        if (disposed || active == value) return;
        active = value;
        if (!value) { DetachSession(); UnsubscribeManager(); return; }
        try
        {
            initializing ??= InitializeAsync(); await initializing;
            if (!active || disposed || manager is null) return;
            manager.CurrentSessionChanged -= CurrentChanged; manager.SessionsChanged -= SessionsChanged;
            manager.CurrentSessionChanged += CurrentChanged; manager.SessionsChanged += SessionsChanged;
            await RefreshAsync();
        }
        catch (Exception e) when (Unavailable(e)) { Error = "O Windows não disponibilizou as sessões de mídia."; State = null; Changed?.Invoke(); initializing = null; }
    }
    private async Task InitializeAsync() { manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync(); }
    private void QueueRefresh(bool properties = false)
    {
        if (disposed || !active || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(new Action(() => { if (properties) metadataDirty = true; _ = RefreshAsync(); }));
    }
    private void CurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => QueueRefresh(true);
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) => QueueRefresh(true);
    private void PropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => QueueRefresh(true);
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => QueueRefresh();
    private void TimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) => QueueRefresh();
    private async Task RefreshAsync()
    {
        if (!active || disposed || manager is null) return;
        if (refreshing) { refreshAgain = true; return; }
        refreshing = true;
        try
        {
            do
            {
                refreshAgain = false;
                var candidates = manager.GetSessions().Where(s => !BrowserSource(s.SourceAppUserModelId)).ToArray();
                Watch(candidates);
                var next = await SelectMusic(candidates);
                if (!active || disposed) return;
                if (!ReferenceEquals(next, session)) { DetachSession(); session = next; AttachSession(); metadataDirty = true; }
                var selected = session;
                if (selected is null) { State = null; Error = null; Changed?.Invoke(); continue; }
                var playback = selected.GetPlaybackInfo();
                if (playback.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped)
                { State = null; Error = null; Changed?.Invoke(); continue; }
                if (metadataDirty || metadata is null)
                {
                    metadataDirty = false;
                    var properties = await selected.TryGetMediaPropertiesAsync();
                    if (!active || disposed || !ReferenceEquals(selected, session)) return;
                    art = await ReadArtwork(properties.Thumbnail);
                    metadata = properties;
                }
                if (!active || disposed || !ReferenceEquals(selected, session)) return;
                var timeline = selected.GetTimelineProperties(); var controls = playback.Controls;
                string source = selected.SourceAppUserModelId;
                if (playerId != source || playerName.Length == 0) { playerId = source; playerName = PlayerName(source); }
                string player = playerName;
                State = new(string.IsNullOrWhiteSpace(metadata!.Title) ? player : metadata.Title, metadata.Artist, player, art,
                    playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    timeline.StartTime, timeline.EndTime, timeline.Position, timeline.LastUpdatedTime, playback.PlaybackRate ?? 1,
                    timeline.MinSeekTime, timeline.MaxSeekTime, controls.IsPlaybackPositionEnabled && timeline.EndTime > timeline.StartTime,
                    controls.IsPlayPauseToggleEnabled || controls.IsPlayEnabled || controls.IsPauseEnabled, controls.IsNextEnabled, controls.IsPreviousEnabled);
                Error = null; Changed?.Invoke();
            } while (refreshAgain && active && !disposed);
        }
        catch (Exception e) when (Unavailable(e)) { State = null; Error = "O player não está disponível no momento."; Changed?.Invoke(); }
        finally { refreshing = false; if (refreshAgain && active && !disposed) { refreshAgain = false; QueueRefresh(); } }
    }
    internal async Task<bool> ExecuteAsync(MediaCommand command)
    {
        var selected = session; if (selected is null || disposed) return false;
        try
        {
            bool accepted = command switch
            {
                MediaCommand.Previous => await selected.TrySkipPreviousAsync(),
                MediaCommand.Next => await selected.TrySkipNextAsync(),
                _ => selected.GetPlaybackInfo().Controls.IsPlayPauseToggleEnabled
                    ? await selected.TryTogglePlayPauseAsync()
                    : State?.Playing == true ? await selected.TryPauseAsync() : await selected.TryPlayAsync()
            };
            await RefreshAsync(); return accepted;
        }
        catch (Exception e) when (Unavailable(e)) { QueueRefresh(); return false; }
    }
    internal async Task<bool> SeekAsync(TimeSpan relative)
    {
        var selected = session; var state = State;
        if (selected is null || state?.CanSeek != true || disposed) return false;
        double min = Math.Max(state.Start.TotalSeconds, state.SeekMin.TotalSeconds);
        double max = state.SeekMax > state.SeekMin ? Math.Min(state.End.TotalSeconds, state.SeekMax.TotalSeconds) : state.End.TotalSeconds;
        long ticks = TimeSpan.FromSeconds(Math.Clamp(state.Start.TotalSeconds + relative.TotalSeconds, min, Math.Max(min, max))).Ticks;
        try { bool accepted = await selected.TryChangePlaybackPositionAsync(ticks); await RefreshAsync(); return accepted; }
        catch (Exception e) when (Unavailable(e)) { return false; }
    }
    private static async Task<ImageSource?> ReadArtwork(IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;
        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size is 0 or > 4194304) return null;
            using var reader = new DataReader(stream.GetInputStreamAt(0));
            uint loaded = await reader.LoadAsync((uint)stream.Size); var bytes = new byte[checked((int)loaded)]; reader.ReadBytes(bytes);
            using var memory = new MemoryStream(bytes);
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 160; image.StreamSource = memory; image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception e) when (Unavailable(e) || e is IOException or FormatException) { return null; }
    }
    private static string PlayerName(string id)
    {
        try { var name = AppInfo.GetFromAppUserModelId(id)?.DisplayInfo.DisplayName; if (!string.IsNullOrWhiteSpace(name)) return name; }
        catch (Exception e) when (Unavailable(e) || e is ArgumentException) { }
        return string.IsNullOrWhiteSpace(id) ? "Player de mídia" : id;
    }
    private static bool BrowserSource(string id)
    {
        string source = id.ToLowerInvariant();
        return new[] { "chrome", "msedge", "microsoftedge", "firefox", "brave", "opera", "vivaldi", "browser", "youtube" }
            .Any(source.Contains);
    }
    private async Task<GlobalSystemMediaTransportControlsSession?> SelectMusic(GlobalSystemMediaTransportControlsSession[] candidates)
    {
        foreach (var candidate in candidates.OrderByDescending(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing))
        {
            try
            {
                var playback = candidate.GetPlaybackInfo();
                if (playback.PlaybackStatus is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped) continue;
                var properties = await candidate.TryGetMediaPropertiesAsync();
                if (!active || disposed) return null;
                if (properties.PlaybackType == Windows.Media.MediaPlaybackType.Music) return candidate;
                string source = candidate.SourceAppUserModelId.ToLowerInvariant();
                if (properties.PlaybackType is null && new[] { "spotify", "musicbee", "foobar", "aimp", "itunes", "applemusic", "zunemusic", "tidal", "deezer", "qobuz", "winamp" }.Any(source.Contains)) return candidate;
            }
            catch (Exception e) when (Unavailable(e)) { /* A disappearing player must not hide another music session. */ }
        }
        return null;
    }
    private void Watch(GlobalSystemMediaTransportControlsSession[] candidates)
    {
        foreach (var old in watched.Where(s => !candidates.Contains(s)).ToArray())
        { old.MediaPropertiesChanged -= PropertiesChanged; old.PlaybackInfoChanged -= PlaybackChanged; watched.Remove(old); }
        foreach (var candidate in candidates)
            if (watched.Add(candidate)) { candidate.MediaPropertiesChanged += PropertiesChanged; candidate.PlaybackInfoChanged += PlaybackChanged; }
    }
    private void AttachSession()
    { if (session is not null) session.TimelinePropertiesChanged += TimelineChanged; }
    private void DetachSession()
    {
        if (session is not null) session.TimelinePropertiesChanged -= TimelineChanged;
        session = null; metadata = null; art = null;
    }
    private void UnsubscribeManager() { Watch(Array.Empty<GlobalSystemMediaTransportControlsSession>()); if (manager is not null) { manager.CurrentSessionChanged -= CurrentChanged; manager.SessionsChanged -= SessionsChanged; } }
    private static bool Unavailable(Exception e) => e is COMException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException;
    public void Dispose() { disposed = true; active = false; UnsubscribeManager(); DetachSession(); }
}
