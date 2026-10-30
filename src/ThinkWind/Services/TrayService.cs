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
internal sealed record TrayEntry(string Name, ImageSource? Icon, AutomationElement? Element, nint Host, string Id = "", Accessibility.IAccessible? Accessible = null, int Child = 0, string? Executable = null, Action<bool>? LocalAction = null)
{
    internal string DisplayName => Name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? Name;
    internal string Key => Id.Length > 0 ? Id : "name:" + DisplayName;
}
// Explorer owns the notification icons. Read its accessibility provider without
// opening the original tray, injecting code or reading Explorer process memory.
internal static class TrayService
{
    private static readonly object gate = new();
    private static readonly object readGate = new();
    private static Task<List<TrayEntry>>? pendingRead;
    private static readonly object localGate = new();
    private static TrayEntry? localIcon;
    internal static void RegisterLocal(TrayEntry? entry) { lock (localGate) localIcon = entry; }
    internal static TrayEntry[] LocalEntries() { lock (localGate) return localIcon is null ? Array.Empty<TrayEntry>() : new[] { localIcon }; }
    private static Task<List<TrayEntry>>? accessibilityRead;
    private static List<TrayEntry> accessibilityCache = new();
    private static DateTime accessibilityUpdated;
    private static readonly Dictionary<string, ImageSource> iconCache = new();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? className, string? title);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent, nint child);
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(nint window, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out Accessibility.IAccessible accessible);
    [DllImport("oleacc.dll")] private static extern int AccessibleChildren(Accessibility.IAccessible parent, int start, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2, ArraySubType = UnmanagedType.Struct)] object[] children, out int obtained);
    internal static Task<List<TrayEntry>> ReadAsync()
    {
        lock (readGate)
        {
            if (pendingRead is null || pendingRead.IsCompleted) pendingRead = Task.Run(ReadCore);
            return pendingRead;
        }
    }
    private static List<TrayEntry> ReadCore()
    {
        lock (gate)
        {
        var result = new List<TrayEntry>();
        var metadata = TrayIconCatalog.Read();
        foreach (var item in metadata.Where(item => item.Guid != Guid.Empty && TrayIconCatalog.IsLive(item)))
        {
            int existing = result.FindIndex(entry => entry.DisplayName.Equals(item.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) result[existing] = result[existing] with { Executable = item.Executable };
            else result.Add(new(item.Name, item.Icon, null, 0, "guid:" + item.Guid, Executable: item.Executable));
        }
        foreach (var live in LegacyTrayIcons.Read(metadata))
        {
            int existing = result.FindIndex(entry => entry.DisplayName.Equals(live.Icon.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0) result[existing] = result[existing] with { Executable = live.Icon.Executable };
            else result.Add(new(live.Icon.Name, live.Icon.Icon, null, live.Window,
                $"uid:{live.Icon.Executable}:{live.Icon.Id}:{live.Process}", Executable: live.Icon.Executable));
        }
        // A stuck accessibility provider must not hide independent native/local
        // icons. Keep at most one UIA read in flight and bound the wait here.
        if (accessibilityRead is null || accessibilityRead.IsCompleted)
        {
            if (accessibilityRead?.IsCompletedSuccessfully == true)
            { accessibilityCache = accessibilityRead.Result; accessibilityUpdated = DateTime.UtcNow; }
            accessibilityRead = Task.Run(() => ReadAccessible(metadata));
        }
        try
        {
            if (accessibilityRead.Wait(350))
            { accessibilityCache = accessibilityRead.Result; accessibilityUpdated = DateTime.UtcNow; }
        }
        catch (AggregateException) { /* Native/local results remain usable. */ }
        if (DateTime.UtcNow - accessibilityUpdated < TimeSpan.FromSeconds(15))
            foreach (var item in accessibilityCache)
            {
                int existing = result.FindIndex(entry => entry.DisplayName.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase));
                if (existing < 0) result.Add(item);
                else result[existing] = result[existing] with { Element = item.Element, Accessible = item.Accessible, Child = item.Child };
            }
        lock (localGate)
        {
            if (localIcon is not null)
            { result.RemoveAll(entry => entry.DisplayName.Equals(localIcon.DisplayName, StringComparison.OrdinalIgnoreCase)); result.Add(localIcon); }
        }
        return result;
        }
    }
    private static List<TrayEntry> ReadAccessible(List<TrayIconMetadata> metadata)
    {
        var result = new List<TrayEntry>();
        nint taskbar = FindWindow("Shell_TrayWnd", null);
        nint area = FindWindowEx(taskbar, 0, "TrayNotifyWnd", null);
        Read(area != 0 ? area : taskbar, result, area != 0, metadata);
        if (area != 0) ReadLegacy(area, result, metadata);
        foreach (string type in new[] { "NotifyIconOverflowWindow", "TopLevelWindowForOverflowXamlIsland" })
        {
            nint overflow = FindWindow(type, null);
            if (overflow != 0) { Read(overflow, result, true, metadata, true); ReadLegacy(overflow, result, metadata); }
        }
        if (iconCache.Count > 512) { var live = result.Select(item => item.Key).ToHashSet(); foreach (var key in iconCache.Keys.Where(key => !live.Contains(key)).ToArray()) iconCache.Remove(key); }
        return result;
    }
    private static void Read(nint host, List<TrayEntry> result, bool trayRoot, List<TrayIconMetadata> metadata, bool overflowRoot = false)
    {
        if (host == 0) return;
        try
        {
            var root = AutomationElement.FromHandle(host);
            var elements = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
            foreach (AutomationElement element in elements)
            {
                try
                {
                    var info = element.Current;
                    if (!info.IsEnabled || string.IsNullOrWhiteSpace(info.Name)) continue;
                    // A taskbar-wide fallback must not mistake running app buttons for tray icons.
                    bool notification = info.ClassName.Contains("NotifyIcon", StringComparison.OrdinalIgnoreCase) || info.AutomationId.Contains("NotifyItem", StringComparison.OrdinalIgnoreCase) || info.AutomationId.Contains("TrayIcon", StringComparison.OrdinalIgnoreCase) || (trayRoot && (overflowRoot || InNotificationToolbar(element, root) || TrayIconCatalog.Match(metadata, info.Name) is not null));
                    if (!notification || result.Exists(e => e.Name == info.Name)) continue;
                    string firstLine = info.Name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? info.Name;
                    string key = info.ClassName + ":" + info.AutomationId + ":" + firstLine;
                    var original = TrayIconCatalog.Match(metadata, info.Name);
                    var icon = original?.Icon ?? CaptureIcon(info.BoundingRectangle, host);
                    if (icon is not null) iconCache[key] = icon; else iconCache.TryGetValue(key, out icon);
                    // Do not replace real icons with a generic program tile.
                    if (icon is not null) result.Add(new(info.Name, icon, element, host, key, Executable: original?.Executable));
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
    private static void ReadLegacy(nint host, List<TrayEntry> result, List<TrayIconMetadata> metadata)
    {
        try
        {
            var iid = new Guid("618736E0-3C3D-11CF-810C-00AA00389B71");
            if (AccessibleObjectFromWindow(host, 0xFFFFFFFC, ref iid, out var root) < 0) return;
            int visited = 0;
            void Visit(Accessibility.IAccessible accessible, int child, int depth)
            {
                if (++visited > 512 || depth > 8) return;
                try
                {
                    string? name = accessible.get_accName(child);
                    if (accessible.get_accRole(child) is int role && role == 43 && !string.IsNullOrWhiteSpace(name))
                    {
                        accessible.accLocation(out int x, out int y, out int width, out int height, child);
                        string key = "msaa:" + name.Split('\n')[0].Trim();
                        var original = TrayIconCatalog.Match(metadata, name);
                        var icon = original?.Icon ?? CaptureIcon(new System.Windows.Rect(x, y, Math.Max(0, width), Math.Max(0, height)), host);
                        if (icon is not null) iconCache[key] = icon; else iconCache.TryGetValue(key, out icon);
                        int existing = result.FindIndex(item => item.Name == name);
                        if (existing >= 0) result[existing] = result[existing] with { Accessible = accessible, Child = child };
                        else if (icon is not null) result.Add(new(name, icon, null, host, key, accessible, child, original?.Executable));
                    }
                    if (child != 0) return;
                    int count = Math.Clamp(accessible.accChildCount, 0, 128); if (count == 0) return;
                    var children = new object[count];
                    if (AccessibleChildren(accessible, 0, count, children, out int obtained) < 0) return;
                    foreach (var value in children.Take(Math.Clamp(obtained, 0, count)))
                    {
                        if (value is Accessibility.IAccessible next) Visit(next, 0, depth + 1);
                        else if (value is int id && id > 0) Visit(accessible, id, depth + 1);
                    }
                }
                catch (Exception error) when (Unavailable(error) || error is ArgumentException or NotImplementedException) { }
            }
            Visit(root, 0, 0);
        }
        catch (Exception error) when (Unavailable(error) || error is ArgumentException) { }
    }
    private static ImageSource? CaptureIcon(System.Windows.Rect bounds, nint host)
    {
        if (bounds.Width < 16 || bounds.Height < 16 || bounds.Width > 160 || bounds.Height > 160) return null;
        try
        {
            int size = Math.Min(24, (int)Math.Min(bounds.Width, bounds.Height));
            using var bitmap = new System.Drawing.Bitmap(size, size);
            bool Detailed()
            {
                var corner = bitmap.GetPixel(0, 0);
                for (int row = 0; row < size; row++) for (int column = 0; column < size; column++)
                { var pixel = bitmap.GetPixel(column, row); if (Math.Abs(pixel.R - corner.R) + Math.Abs(pixel.G - corner.G) + Math.Abs(pixel.B - corner.B) > 24) return true; }
                return false;
            }
            if (!Detailed())
            {
                // Copy exposed pixels only as a last resort, and only when
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
            if (entry.LocalAction is not null) { entry.LocalAction(context); return true; }
            // Never focus/reveal the Windows taskbar as an implicit fallback.
            if (context)
            {
                if (entry.Element is { } menuElement && menuElement.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expand)) { ((ExpandCollapsePattern)expand).Expand(); return true; }
                return false;
            }
            if (entry.Element is { } element && element.TryGetCurrentPattern(InvokePattern.Pattern, out var invoke)) { ((InvokePattern)invoke).Invoke(); return true; }
            if (entry.Accessible is not null) { entry.Accessible.accDoDefaultAction(entry.Child); return true; }
        }
        catch (Exception e) when (Unavailable(e)) { }
        return false;
    });
    private static bool Unavailable(Exception e) => e is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException or COMException or UnauthorizedAccessException or NotSupportedException or ArgumentException;
}
