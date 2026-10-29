using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkWind.Native;

namespace ThinkWind.Services;
internal sealed record TrayEntry(string Name, ImageSource? Icon, AutomationElement Element, nint Host, string Id = "")
{
    internal string DisplayName => Name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? Name;
    internal string Key => Id.Length > 0 ? Id : "name:" + DisplayName;
}
// Explorer owns the notification icons. Read its accessibility provider without
// opening the original tray, injecting code or reading Explorer process memory.
internal static class TrayService
{
    private static readonly object gate = new();
    private static readonly Dictionary<string, ImageSource> iconCache = new();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint window, nint dc, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rectangle rectangle);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rectangle { internal int Left, Top, Right, Bottom; }
    internal static Task<List<TrayEntry>> ReadAsync() => Task.Run(() =>
    {
        lock (gate)
        {
        var result = new List<TrayEntry>();
        nint taskbar = FindWindow("Shell_TrayWnd", null);
        nint area = FindWindowEx(taskbar, 0, "TrayNotifyWnd", null);
        Read(area != 0 ? area : taskbar, result, area != 0);
        nint overflow = FindWindow("NotifyIconOverflowWindow", null);
        if (overflow != 0) Read(overflow, result, true);
        // Windows 11 uses a separate XAML overflow host on some shell builds.
        nint xamlOverflow = FindWindow("TopLevelWindowForOverflowXamlIsland", null);
        if (xamlOverflow != 0) Read(xamlOverflow, result, true);
        if (iconCache.Count > 512) { var live = result.Select(item => item.Key).ToHashSet(); foreach (var key in iconCache.Keys.Where(key => !live.Contains(key)).ToArray()) iconCache.Remove(key); }
        return result;
        }
    });
    private static void Read(nint host, List<TrayEntry> result, bool trayRoot)
    {
        if (host == 0) return;
        try
        {
            var root = AutomationElement.FromHandle(host);
            var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            using var frame = CaptureHost(host, out var rectangle);
            foreach (AutomationElement element in elements)
            {
                try
                {
                    var info = element.Current;
                    if (!info.IsEnabled || string.IsNullOrWhiteSpace(info.Name)) continue;
                    // A taskbar-wide fallback must not mistake running app buttons for tray icons.
                    bool notification = info.ClassName.Contains("NotifyIcon", StringComparison.OrdinalIgnoreCase) || info.AutomationId.Contains("NotifyItem", StringComparison.OrdinalIgnoreCase) || (trayRoot && InNotificationToolbar(element, root));
                    if (!notification || result.Exists(e => e.Name == info.Name)) continue;
                    string firstLine = info.Name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? info.Name;
                    string key = info.ClassName + ":" + info.AutomationId + ":" + firstLine;
                    var icon = CaptureIcon(info.BoundingRectangle, host, frame, rectangle);
                    if (icon is not null) iconCache[key] = icon; else iconCache.TryGetValue(key, out icon);
                    // Do not replace real icons with a generic program tile.
                    if (icon is not null) result.Add(new(info.Name, icon, element, host, key));
                }
                catch (Exception e) when (Unavailable(e)) { }
            }
        }
        catch (Exception e) when (Unavailable(e)) { }
    }
    private static bool InNotificationToolbar(AutomationElement element, AutomationElement root)
    {
        var parent = TreeWalker.ControlViewWalker.GetParent(element);
        for (int depth = 0; parent is not null && depth < 6; depth++, parent = TreeWalker.ControlViewWalker.GetParent(parent))
        {
            if (parent.Current.ClassName == "ToolbarWindow32") return true;
            if (Automation.Compare(parent, root)) break;
        }
        return false;
    }
    private static System.Drawing.Bitmap? CaptureHost(nint host, out Rectangle rectangle)
    {
        rectangle = default;
        if (!GetWindowRect(host, out rectangle)) return null;
        int width = rectangle.Right - rectangle.Left, height = rectangle.Bottom - rectangle.Top;
        if (width < 1 || height < 1 || width > 8192 || height > 2048) return null;
        var bitmap = new System.Drawing.Bitmap(width, height);
        try
        {
            using var graphics = System.Drawing.Graphics.FromImage(bitmap); nint dc = graphics.GetHdc();
            bool rendered;
            try { rendered = PrintWindow(host, dc, 2); } finally { graphics.ReleaseHdc(dc); }
            if (rendered) return bitmap;
        }
        catch (Exception error) when (error is ExternalException or ArgumentException) { }
        bitmap.Dispose(); return null;
    }
    private static ImageSource? CaptureIcon(System.Windows.Rect bounds, nint host, System.Drawing.Bitmap? source, Rectangle rectangle)
    {
        if (bounds.Width < 16 || bounds.Height < 16 || bounds.Width > 160 || bounds.Height > 160) return null;
        try
        {
            int size = Math.Min(24, (int)Math.Min(bounds.Width, bounds.Height));
            using var bitmap = new System.Drawing.Bitmap(size, size);
            // Ask the host to render into a bitmap instead of copying the desktop:
            // launcher overlays and hidden taskbars must not become fake icons.
            int x = (int)(bounds.X - rectangle.Left + (bounds.Width - size) / 2), y = (int)(bounds.Y - rectangle.Top + (bounds.Height - size) / 2);
            if (source is not null && x >= 0 && y >= 0 && x + size <= source.Width && y + size <= source.Height)
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.DrawImage(source, new System.Drawing.Rectangle(0, 0, size, size), new System.Drawing.Rectangle(x, y, size, size), System.Drawing.GraphicsUnit.Pixel);
            bool Detailed()
            {
                var corner = bitmap.GetPixel(0, 0);
                for (int row = 0; row < size; row++) for (int column = 0; column < size; column++)
                { var pixel = bitmap.GetPixel(column, row); if (Math.Abs(pixel.R - corner.R) + Math.Abs(pixel.G - corner.G) + Math.Abs(pixel.B - corner.B) > 24) return true; }
                return false;
            }
            if (!Detailed())
            {
                // Some XAML hosts do not render with PrintWindow. Copy only when
                // the exposed icon is genuinely under the cursor-free sample,
                // with no other window covering its corners/centre.
                if (!IsWindowVisible(host)) return null;
                foreach (var point in new[] { new Point { X = (int)bounds.Left, Y = (int)bounds.Top }, new Point { X = (int)bounds.Right - 1, Y = (int)bounds.Bottom - 1 }, new Point { X = (int)(bounds.X + bounds.Width / 2), Y = (int)(bounds.Y + bounds.Height / 2) } })
                { nint window = WindowFromPoint(point); if (window != host && !IsChild(host, window)) return null; }
                using var graphics = System.Drawing.Graphics.FromImage(bitmap);
                graphics.CopyFromScreen((int)(bounds.X + (bounds.Width - size) / 2), (int)(bounds.Y + (bounds.Height - size) / 2), 0, 0, new System.Drawing.Size(size, size));
                if (!Detailed()) return null;
            }
            // Keep the actual exposed icon pixels; remove only uniform edge-connected tray tint.
            RemoveEdgeTint(bitmap);
            using var stream = new System.IO.MemoryStream(); bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png); stream.Position = 0;
            var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException or ArgumentException) { return null; }
    }
    private static void RemoveEdgeTint(System.Drawing.Bitmap bitmap)
    {
        int width = bitmap.Width, height = bitmap.Height; var background = bitmap.GetPixel(0, 0);
        bool Matches(System.Drawing.Color color) => Math.Abs(color.R - background.R) <= 4 && Math.Abs(color.G - background.G) <= 4 && Math.Abs(color.B - background.B) <= 4;
        if (!Matches(bitmap.GetPixel(width - 1, 0)) || !Matches(bitmap.GetPixel(0, height - 1)) || !Matches(bitmap.GetPixel(width - 1, height - 1))) return;
        var visited = new bool[width, height]; var queue = new Queue<(int X, int Y)>(); var backgroundPixels = new List<(int X, int Y)>();
        for (int x = 0; x < width; x++) { queue.Enqueue((x, 0)); queue.Enqueue((x, height - 1)); }
        for (int y = 0; y < height; y++) { queue.Enqueue((0, y)); queue.Enqueue((width - 1, y)); }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || y < 0 || x >= width || y >= height || visited[x, y]) continue;
            visited[x, y] = true; if (!Matches(bitmap.GetPixel(x, y))) continue;
            backgroundPixels.Add((x, y)); queue.Enqueue((x - 1, y)); queue.Enqueue((x + 1, y)); queue.Enqueue((x, y - 1)); queue.Enqueue((x, y + 1));
        }
        if (backgroundPixels.Count >= width * height * .9) return;
        foreach (var pixel in backgroundPixels) bitmap.SetPixel(pixel.X, pixel.Y, System.Drawing.Color.Transparent);
    }
    internal static Task<bool> InvokeAsync(TrayEntry entry, bool context) => Task.Run(() =>
    {
        try
        {
            // Never focus/reveal the Windows taskbar as an implicit fallback.
            if (context)
            {
                if (entry.Element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) { ((ExpandCollapsePattern)expand).Expand(); return true; }
                return false;
            }
            if (entry.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) { ((InvokePattern)invoke).Invoke(); return true; }
        }
        catch (Exception e) when (Unavailable(e)) { }
        return false;
    });
    private static bool Unavailable(Exception e) => e is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException or COMException or UnauthorizedAccessException;
}
