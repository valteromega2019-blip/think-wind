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
            if (IsSpotify(requested)) preview.PresentSpotify(requested.Windows);
            else
            {
                preview.Present(requested.Windows);
                bool browser = requested.Windows.Any(BrowserTabs.IsBrowser);
                if (!browser && (!Store.Value.CallPreviewsEnabled || !requested.Windows.Any(CallIntegration.IsCandidate))) return;
                callProbe?.Cancel(); callProbe?.Dispose(); callProbe = new CancellationTokenSource(); var token = callProbe.Token;
                try
                {
                    if (browser)
                    {
                        var tabs = await BrowserTabs.ReadAsync(requested.Windows, token);
                        if (token.IsCancellationRequested || !ReferenceEquals(previewItem, requested) || !preview.IsVisible) return;
                        preview.PresentBrowser(requested.Windows, tabs);
                    }
                    if (!Store.Value.CallPreviewsEnabled) return;
                    var call = await CallIntegration.FindAsync(requested.Windows, token);
                    if (!token.IsCancellationRequested && call is not null && ReferenceEquals(previewItem, requested)
                        && IsVisible && !MenuOpen && Store.Value.CallPreviewsEnabled && preview.IsVisible)
                        preview.PresentCall(call, requested.Windows);
                }
                catch (OperationCanceledException) { }
            }
        };
        previewClose.Tick += (_, _) =>
        {
            if (preview?.IsInteracting == true) return;
            previewClose.Stop(); if (preview?.PointerInside != true && !previewIconHovered) preview?.Hide();
        };
    }
    private static bool IsSpotify(Item item) => string.Equals(item.Name, "Spotify", StringComparison.OrdinalIgnoreCase)
        || (item.Target?.Contains("spotify", StringComparison.OrdinalIgnoreCase) == true)
        || item.Windows.Any(window => window.Executable?.Contains("spotify", StringComparison.OrdinalIgnoreCase) == true);
    private void AttachPreview(FrameworkElement element, Item item)
    {
        element.MouseEnter += (_, _) =>
        {
            if (MenuOpen) return;
            if (item.Windows.Count == 0 && !IsSpotify(item)) { StopPreviews(); return; }
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
