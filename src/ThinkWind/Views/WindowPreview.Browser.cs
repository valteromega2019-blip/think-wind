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
    private BrowserTabFan? browserFan;
    private readonly StackPanel browserLayout = new() { Orientation = Orientation.Horizontal };
    private IReadOnlyList<BrowserTab> browserTabs = Array.Empty<BrowserTab>();
    private CancellationTokenSource? browserLifetime;
    private string browserSignature = "";
    internal void PresentBrowser(IEnumerable<Desktop.RunningWindow> source, IReadOnlyList<BrowserTab> tabs, CallSnapshot? meeting = null)
    {
        refresh.Stop(); browserLifetime?.Cancel(); browserLifetime?.Dispose(); browserLifetime = new CancellationTokenSource();
        Release(); spotifyMode = false; spotify?.SetActive(false); call?.Stop();
        viewport.Child = null;
        DetachCallCard();
        browserMode = true; callMode = meeting is not null;
        windows = source.ToList(); browserTabs = tabs.ToArray(); browserSignature = TabsSignature(tabs);
        Surface.Padding = new Thickness(10); browserLayout.Children.Clear();
        browserFan ??= new BrowserTabFan();
        browserFan.DismissRequested -= DismissBrowser; browserFan.DismissRequested += DismissBrowser;
        var shown = tabs.Count > 0 ? tabs : windows.Take(1).Select(window => new BrowserTab(window.Handle, 0, window.Title, Array.Empty<int>(), true)).ToArray();
        browserFan.Present(shown, new WindowInteropHelper(this).EnsureHandle(), Store.Value);
        browserLayout.Children.Add(browserFan);
        if (meeting is not null)
        {
            if (call is null)
            {
                call = new CallPreviewCard();
                call.LayoutChanged += () => { if (IsVisible && callMode) Position(); };
                call.CallEnded += () => { if (IsVisible && callMode) { if (browserMode) PresentBrowser(windows.ToArray(), browserTabs); else Present(windows.ToArray()); } };
            }
            call.ConfigureCompact(true);
            call.Width = 300; call.Margin = new Thickness(0);
            var compactCall = new Viewbox { Width = 320, Height = 160, Margin = new Thickness(10, 0, 0, 12), VerticalAlignment = VerticalAlignment.Bottom,
                Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = System.Windows.Media.StretchDirection.DownOnly, Child = call };
            browserLayout.Children.Add(compactCall); call.Present(meeting, windows);
        }
        else if (Store.Value.CallPreviewsEnabled && tabs.FirstOrDefault(tab => tab.IsMeet) is { } meet)
        {
            var details = new StackPanel { Width = 290, Margin = new Thickness(18, 10, 6, 6) };
            details.Children.Add(new TextBlock { Text = "Google Meet", FontSize = 18, FontWeight = FontWeights.SemiBold });
            details.Children.Add(new TextBlock { Text = "Aba encontrada. Abra a reunião para disponibilizar os controles de chamada.", FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
            var open = new Button { Content = "Abrir aba do Meet", Padding = new Thickness(10, 8, 10, 8) };
            open.Click += async (_, _) =>
            {
                var token = browserLifetime?.Token ?? CancellationToken.None;
                try { if (await BrowserTabs.SelectAsync(meet, token)) { await Desktop.ActivateFromDock(meet.Window, false); Hide(); } }
                catch (OperationCanceledException) { }
            };
            details.Children.Add(open); browserLayout.Children.Add(details);
        }
        viewport.Child = browserLayout; Show(); Position(); refresh.Interval = TimeSpan.FromSeconds(2); refresh.Start();
    }
    private void DismissBrowser() => Hide();
    private static string TabsSignature(IReadOnlyList<BrowserTab> tabs) => string.Join("|", tabs.Select(tab => $"{tab.Window}:{string.Join(",", tab.RuntimeId)}:{tab.Selected}:{tab.Title}"));
    private async Task RefreshBrowserAsync()
    {
        if (browserReading || browserLifetime is null || IsInteracting) return;
        browserReading = true; var token = browserLifetime.Token;
        try
        {
            var tabs = await BrowserTabs.ReadAsync(windows, token);
            if (token.IsCancellationRequested || !IsVisible || !browserMode) return;
            if (TabsSignature(tabs) != browserSignature)
            {
                // A tab change can change the call as well. Re-identify both,
                // rather than assigning the last active tab's image to a new title.
                var meeting = Store.Value.CallPreviewsEnabled ? await CallIntegration.FindAsync(windows, token) : null;
                if (!token.IsCancellationRequested && IsVisible && browserMode) PresentBrowser(windows.ToArray(), tabs, meeting);
            }
        }
        catch (OperationCanceledException) { }
        finally { browserReading = false; }
    }
    private void PositionBrowser()
    {
        var bounds = anchor();
        double naturalWidth = browserLayout.Children.Count > 1 ? 650 : 320;
        double width = Math.Min(Display.Width - 16, naturalWidth + 20);
        browserLayout.Width = naturalWidth;
        viewport.Width = width - 20;
        viewport.MaxHeight = Math.Max(48, Math.Min(490, (bounds.Top - Display.Work.Top) / Display.Scale - 36));
        viewport.Measure(new Size(viewport.Width, viewport.MaxHeight));
        PlaceAbove(width, viewport.DesiredSize.Height + 20, bounds, false);
    }
}
