using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThinkWind.Views;

internal sealed partial class DockWindow
{
    private readonly Dictionary<string, ImageSource?> detailedIcons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<ImageSource?>> pendingDetailedIcons = new(StringComparer.OrdinalIgnoreCase);
    private bool iconsClosed;
    private long detailedIconBytes;
    private void PrepareDetailedIcon(Image image, string? target, FrameworkElement? fallback)
    {
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        image.SnapsToDevicePixels = true; image.UseLayoutRounding = true;
        if (string.IsNullOrWhiteSpace(target)) return;
        image.Loaded += (_, _) => LoadDetailedIcon(image, target, fallback);
        image.IsVisibleChanged += (_, _) => { if (image.IsVisible) LoadDetailedIcon(image, target, fallback); };
    }
    private async void LoadDetailedIcon(Image image, string target, FrameworkElement? fallback)
    {
        if (iconsClosed || !IsVisible || hidden || !image.IsVisible) return;
        double growth = Store.Value.DockMagnification ? 1 + Store.Value.DockMagnificationAmount : 1;
        int pixels = Math.Clamp((int)Math.Ceiling(image.Width * Display.Scale * growth / 64) * 64, 128, 512);
        string key = icons.VersionedKey(target) + "|" + pixels;
        try
        {
            if (!detailedIcons.TryGetValue(key, out var source))
            {
                if (!pendingDetailedIcons.TryGetValue(key, out var pending))
                { pending = catalog.ReadDockIconAsync(target, pixels); pendingDetailedIcons[key] = pending; }
                source = await pending;
                pendingDetailedIcons.Remove(key);
                if (iconsClosed) return;
                // A suspended catalog returns null; allow a retry after games.
                if (source is not null && !detailedIcons.ContainsKey(key))
                {
                    long bytes = source is BitmapSource bitmap ? (long)bitmap.PixelWidth * bitmap.PixelHeight * 4 : 16384;
                    if (detailedIcons.Count >= 128 || detailedIconBytes + bytes > 24 * 1024 * 1024)
                    { detailedIcons.Clear(); detailedIconBytes = 0; }
                    detailedIcons[key] = source; detailedIconBytes += bytes;
                }
            }
            if (source is null || !image.IsVisible) return;
            if (source is BitmapSource next && image.Source is BitmapSource previous && previous.PixelWidth > next.PixelWidth) return;
            image.Source = source;
            if (fallback is not null) fallback.Visibility = Visibility.Collapsed;
        }
        catch (Exception error) when (error is OperationCanceledException or System.Runtime.InteropServices.COMException
            or ArgumentException or System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        { pendingDetailedIcons.Remove(key); /* Keep the already displayed icon. */ }
    }
}
