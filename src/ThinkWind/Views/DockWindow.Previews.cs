using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Threading;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed partial class DockWindow
{
    private WindowPreview? preview;
    private Item? previewItem;
    private FrameworkElement? previewAnchor;
    private OverlayChrome.PixelRect? retainedPreviewAnchor;
    private bool previewIconHovered;
    private CancellationTokenSource? callProbe;
    private readonly DispatcherTimer previewOpen = new();
    private readonly DispatcherTimer previewClose = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private void InitializePreviews()
    {
        previewOpen.Tick += async (_, _) =>
        {
            previewOpen.Stop();
            if (!IsVisible || MenuOpen || fileDragging || Store.IsEditing || LayoutDrag.Active || !Store.Value.WindowPreviewsEnabled || previewItem is null) return;
            if (preview is null)
            {
                preview = new WindowPreview(Store, PreviewIconBounds) { Owner = this, MonitorDeviceOverride = MonitorDeviceOverride };
                preview.MouseEnter += (_, _) => { lastHover = DateTime.UtcNow; previewClose.Stop(); };
                preview.MouseLeave += (_, _) => { lastHover = DateTime.UtcNow; previewClose.Start(); };
            }
            var requested = previewItem;
            retainedPreviewAnchor = CurrentPreviewIconBounds();
            bool dedicatedPlayer = MusicSessionPolicy.KnownMusic(requested.Name);
            if (dedicatedPlayer) preview.Hide(); else preview.Present(requested.Windows);
            callProbe?.Cancel(); callProbe?.Dispose(); callProbe = new CancellationTokenSource(); var token = callProbe.Token;
            try
            {
                bool browser = requested.Windows.Any(BrowserTabs.IsBrowser);
                bool Current() => !token.IsCancellationRequested && ReferenceEquals(previewItem, requested) && IsVisible && !MenuOpen
                    && Store.Value.WindowPreviewsEnabled && (preview.IsVisible || previewIconHovered);
                if (browser)
                {
                    var tabs = await BrowserTabs.ReadAsync(requested.Windows, token);
                    if (!Current()) return;
                    var identity = MediaIdentity(requested);
                    var titles = await BrowserTabs.KnownTitlesAsync(requested.Windows, token);
                    identity = identity with { WindowTitles = identity.WindowTitles.Concat(titles).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() };
                    var meeting = Store.Value.CallPreviewsEnabled ? await CallIntegration.FindAsync(requested.Windows, token) : null;
                    var browserMedia = await MediaService.FindForAppAsync(identity, token);
                    if (Current())
                    {
                        if (meeting is null && browserMedia?.KeepIdle == true) preview.PresentMusic(browserMedia, requested.Windows);
                        else preview.PresentBrowser(requested.Windows, tabs, meeting, browserMedia);
                    }
                    return;
                }
                var media = await MediaService.FindForAppAsync(MediaIdentity(requested), token);
                if (!Current()) return;
                if (media is not null) preview.PresentMusic(media, requested.Windows);
                else if (dedicatedPlayer) preview.Present(requested.Windows);
                if (Store.Value.CallPreviewsEnabled && requested.Windows.Any(CallIntegration.IsCandidate))
                {
                    var call = await CallIntegration.FindAsync(requested.Windows, token);
                    if (Current() && call is not null && Store.Value.CallPreviewsEnabled)
                        preview.PresentCall(call, requested.Windows);
                }
            }
            catch (OperationCanceledException) { }
        };
        previewClose.Tick += (_, _) =>
        {
            if (preview?.IsInteracting == true) return;
            previewClose.Stop(); if (preview?.PointerInside != true && !previewIconHovered) preview?.Hide();
        };
    }
    private MediaAppIdentity MediaIdentity(Item item)
    {
        var appIds = item.Windows.Select(window => window.AppId).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!).ToList();
        var paths = item.Windows.Select(window => window.Executable).Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => path!).ToList();
        if (item.Target is { } target)
        {
            var registered = catalog.Items.FirstOrDefault(app => app.Target.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (registered?.AppId is { } appId) appIds.Add(appId);
            if (target.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase)) appIds.Add(target[@"shell:AppsFolder\".Length..]);
            paths.Add(icons.Resolve(target));
        }
        bool browserGroup = item.Windows.Count > 0 && item.Windows.All(BrowserTabs.IsBrowser)
            && !appIds.Any(id => id.Contains("_crx_", StringComparison.OrdinalIgnoreCase));
        return new(appIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), item.Windows.Select(window => window.Title).ToArray(), item.Name, browserGroup);
    }
    private void AttachPreview(FrameworkElement element, Item item)
    {
        element.MouseEnter += (_, _) =>
        {
            if (MenuOpen) return;
            if (item.Windows.Count == 0 && item.Target is null) { StopPreviews(); return; }
            previewIconHovered = true;
            previewClose.Stop(); previewOpen.Stop(); previewItem = item; previewAnchor = element;
            previewOpen.Interval = TimeSpan.FromMilliseconds(Store.Value.WindowPreviewDelayMilliseconds);
            if (Store.Value.WindowPreviewsEnabled) previewOpen.Start();
        };
        element.MouseLeave += (_, _) => { previewIconHovered = false; previewOpen.Stop(); previewClose.Start(); };
    }
    private OverlayChrome.PixelRect PreviewIconBounds()
    {
        // A foreground change can rebuild the dock and detach the original icon.
        // Keep the opening anchor until the user leaves this preview.
        if (preview?.IsVisible == true && retainedPreviewAnchor is { } retained) return retained;
        return CurrentPreviewIconBounds();
    }
    private OverlayChrome.PixelRect CurrentPreviewIconBounds()
    {
        if (previewAnchor is not { IsLoaded: true } element || PresentationSource.FromVisual(element) is null) return TargetBounds;
        var first = element.PointToScreen(new Point());
        var last = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new OverlayChrome.PixelRect
        {
            Left = (int)Math.Floor(first.X), Right = (int)Math.Ceiling(last.X),
            Top = Math.Min(TargetBounds.Top, (int)Math.Floor(first.Y)), Bottom = (int)Math.Ceiling(last.Y)
        };
    }
    private void StopPreviews() { callProbe?.Cancel(); callProbe?.Dispose(); callProbe = null; previewIconHovered = false; previewOpen.Stop(); previewClose.Stop(); previewItem = null; previewAnchor = null; preview?.Hide(); retainedPreviewAnchor = null; }
}
