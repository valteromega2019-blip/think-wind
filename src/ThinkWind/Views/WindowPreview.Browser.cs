using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class WindowPreview
{
    private bool browserMode, browserReading;
    private BrowserWindowFans? browserFan;
    private readonly StackPanel browserLayout = new() { Orientation = Orientation.Horizontal };
    private IReadOnlyList<BrowserTab> browserTabs = Array.Empty<BrowserTab>();
    private CancellationTokenSource? browserLifetime;
    private string browserSignature = "";
    private MusicPreviewSelection? browserMusic;
    private CallSnapshot? browserMeeting;
    private bool browserPreferMusic;
    private string browserContext = "";
    private bool BrowserControlsBusy => WindowActionInteracting || browserFan?.IsInteracting == true || musicCard?.IsInteracting == true || call?.IsUserInteracting == true;
    internal void PresentBrowser(IEnumerable<Desktop.RunningWindow> source, IReadOnlyList<BrowserTab> tabs, CallSnapshot? meeting = null, MusicPreviewSelection? music = null)
    {
        var currentWindows = source.ToList();
        Surface.Child = viewport;
        string context = string.Join('|', currentWindows.Select(window => window.Handle).OrderBy(handle => handle));
        if (!browserMode || browserContext != context || meeting is not null && (browserMeeting is null || browserMeeting.RoomAddress != meeting.RoomAddress)) browserPreferMusic = false;
        browserContext = context; browserMeeting = meeting;
        refresh.Stop(); browserLifetime?.Cancel(); browserLifetime?.Dispose(); browserLifetime = new CancellationTokenSource();
        Release(); musicMode = false; musicCard?.SetActive(false); call?.Stop();
        viewport.Child = null;
        DetachCallCard();
        browserMode = true; callMode = meeting is not null && (!browserPreferMusic || music is null);
        browserMusic = music;
        windows = currentWindows; browserTabs = tabs.ToArray(); browserSignature = TabsSignature(tabs);
        Surface.Padding = new Thickness(10); browserLayout.Children.Clear();
        browserFan ??= new BrowserWindowFans();
        browserFan.LayoutChanged -= PositionBrowser;
        browserFan.LayoutChanged += PositionBrowser;
        browserFan.DismissRequested -= DismissBrowser; browserFan.DismissRequested += DismissBrowser;
        var shown = tabs.Concat(windows.Where(window => !tabs.Any(tab => tab.Window == window.Handle))
            .Select(window => new BrowserTab(window.Handle, 0, window.Title, Array.Empty<int>(), true))).ToArray();
        browserFan.Present(shown, new WindowInteropHelper(this).EnsureHandle(), Store.Value);
        browserLayout.Children.Add(browserFan);
        var integration = new StackPanel { Width = 320, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 12) };
        if (meeting is not null && music is not null)
        {
            var choices = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            AddChoice("Reunião", false); AddChoice("Música", true);
            integration.Children.Add(choices);
            void AddChoice(string label, bool showMusic)
            {
                var button = new Button { Content = label, Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(3, 0, 3, 6) };
                button.SetResourceReference(Control.BackgroundProperty, browserPreferMusic == showMusic ? "Think.AccentSurface" : "Think.Control");
                button.SetResourceReference(Control.ForegroundProperty, browserPreferMusic == showMusic ? "Think.AccentInk" : "Think.Text");
                button.Click += (_, _) =>
                {
                    browserPreferMusic = showMusic;
                    PresentBrowser(windows.ToArray(), browserTabs, browserMeeting, browserMusic);
                };
                choices.Children.Add(button);
            }
        }
        if (callMode && meeting is not null)
        {
            if (call is null)
            {
                call = new CallPreviewCard();
                call.LayoutChanged += () => { if (IsVisible && callMode) Position(); };
                call.CallEnded += () => { if (IsVisible && callMode) { if (browserMode) PresentBrowser(windows.ToArray(), browserTabs, music: browserMusic); else Present(windows.ToArray()); } };
            }
            call.ConfigureCompact(true);
            call.Width = 300; call.Margin = new Thickness(0);
            var compactCall = new Viewbox { Width = 320, Height = 160, VerticalAlignment = VerticalAlignment.Bottom,
                Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = WithWindowActions(call, windows, meeting.Window) };
            integration.Children.Add(compactCall); call.Present(meeting, windows);
        }
        else if (music is not null)
        {
            PrepareMusic(music); ConfigurePlayerSurface(); musicCard!.Width = 320;
            var playerBox = new Viewbox { Width = 320, Height = 230, VerticalAlignment = VerticalAlignment.Bottom,
                Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = WithWindowActions(musicCard!, windows) };
            integration.Children.Add(playerBox); musicCard.SetActive(true);
        }
        if (integration.Children.Count > 0)
        {
            browserLayout.Children.Add(integration);
            browserFan.AttachPanelMaterial(integration);
        }
        viewport.Child = browserLayout; RefreshAppearance(); Show(); Position(); refresh.Interval = TimeSpan.FromSeconds(2); refresh.Start();
    }
    private void DismissBrowser() => Hide();
    private static string TabsSignature(IReadOnlyList<BrowserTab> tabs) => string.Join("|", tabs.Select(tab => $"{tab.Window}:{string.Join(",", tab.RuntimeId)}:{tab.Selected}:{tab.Title}:{tab.Cached}"));
    private async Task RefreshBrowserAsync()
    {
        if (browserReading || browserLifetime is null || BrowserControlsBusy) return;
        browserReading = true; var token = browserLifetime.Token;
        try
        {
            var tabs = await BrowserTabs.ReadAsync(windows, token);
            if (token.IsCancellationRequested || !IsVisible || !browserMode) return;
            var meeting = Store.Value.CallPreviewsEnabled
                ? callMode ? call?.CurrentCall : await CallIntegration.FindAsync(windows, token) : null;
            var ids = windows.Select(window => window.AppId).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var titles = await BrowserTabs.KnownTitlesAsync(windows, token);
            var identity = new MediaAppIdentity(ids, windows.Select(window => window.Executable).Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => path!).ToArray(),
                windows.Select(window => window.Title).Concat(titles).ToArray(), "", !ids.Any(id => id.Contains("_crx_", StringComparison.OrdinalIgnoreCase)));
            var music = await MediaService.FindForAppAsync(identity, token);
            if (token.IsCancellationRequested || !IsVisible || !browserMode || BrowserControlsBusy) return;
            if (TabsSignature(tabs) != browserSignature || MeetingSignature(meeting) != MeetingSignature(browserMeeting)
                || music?.SourceId != browserMusic?.SourceId || !callMode && music is not null && musicCard?.Available == false)
            {
                PresentBrowser(windows.ToArray(), tabs, meeting, music);
            }
            else { browserMeeting = meeting; if (callMode) browserMusic = music; }
        }
        catch (OperationCanceledException) { }
        finally { browserReading = false; }
    }
    private static string MeetingSignature(CallSnapshot? meeting) => meeting is null ? "" : $"{meeting.Window}:{meeting.ProcessId}:{meeting.App}:{meeting.RoomAddress}:{meeting.NeedsValidation}";
    private void PositionBrowser()
    {
        var bounds = anchor();
        double naturalWidth = (browserFan?.Width ?? 320) + (browserLayout.Children.Count > 1 ? 330 : 0);
        double width = Math.Min(Display.Width - 16, naturalWidth + 20);
        browserLayout.Width = naturalWidth;
        viewport.Width = width - 20;
        viewport.MaxHeight = Math.Max(48, Math.Min(490, (bounds.Top - Display.Work.Top) / Display.Scale - 36));
        viewport.Measure(new Size(viewport.Width, viewport.MaxHeight));
        PlaceAbove(width, viewport.DesiredSize.Height + 20, bounds, false);
    }
}
