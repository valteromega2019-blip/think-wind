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
// Explorer owns the notification icons. Use its accessibility provider, never its memory.
internal static class TrayService
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    internal static Task<List<TrayEntry>> ReadAsync() => Task.Run(() =>
    {
        var result = new List<TrayEntry>();
        nint taskbar = FindWindow("Shell_TrayWnd", null);
        nint area = FindWindowEx(taskbar, 0, "TrayNotifyWnd", null);
        Read(area != 0 ? area : taskbar, result, area != 0);
        nint overflow = FindWindow("NotifyIconOverflowWindow", null);
        if (overflow != 0 && IsWindowVisible(overflow)) Read(overflow, result, true);
        return result;
    });
    private static void Read(nint host, List<TrayEntry> result, bool trayRoot)
    {
        if (host == 0 || !IsWindowVisible(host)) return;
        try
        {
            var root = AutomationElement.FromHandle(host);
            var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement element in elements)
            {
                try
                {
                    var info = element.Current;
                    if (info.IsOffscreen || !info.IsEnabled || string.IsNullOrWhiteSpace(info.Name)) continue;
                    // A taskbar-wide fallback must not mistake running app buttons for tray icons.
                    bool notification = info.ClassName.Contains("NotifyIcon", StringComparison.OrdinalIgnoreCase) || info.AutomationId.Contains("NotifyItem", StringComparison.OrdinalIgnoreCase) || (trayRoot && InNotificationToolbar(element, root));
                    if (!notification || result.Exists(e => e.Name == info.Name)) continue;
                    string firstLine = info.Name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? info.Name;
                    result.Add(new(info.Name, CaptureIcon(info.BoundingRectangle), element, host, info.ClassName + ":" + info.AutomationId + ":" + firstLine));
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
    private static ImageSource? CaptureIcon(System.Windows.Rect bounds)
    {
        if (bounds.Width < 16 || bounds.Height < 16 || bounds.Width > 160 || bounds.Height > 160) return null;
        try
        {
            int size = Math.Min(24, (int)Math.Min(bounds.Width, bounds.Height));
            using var bitmap = new System.Drawing.Bitmap(size, size);
            using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) graphics.CopyFromScreen((int)(bounds.X + (bounds.Width - size) / 2), (int)(bounds.Y + (bounds.Height - size) / 2), 0, 0, new System.Drawing.Size(size, size));
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
            if (context) { if (!SetForegroundWindow(GetAncestor(entry.Host, 2))) return false; entry.Element.SetFocus(); Desktop.Chord(0x10, 0x79); return true; }
            if (entry.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) { ((InvokePattern)invoke).Invoke(); return true; }
        }
        catch (Exception e) when (Unavailable(e)) { }
        return false;
    });
    private static bool Unavailable(Exception e) => e is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException or COMException or UnauthorizedAccessException;
}
