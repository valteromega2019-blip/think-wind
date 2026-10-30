using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ThinkWind.Native;

namespace ThinkWind.Services;

// Read-only shell metadata supplements accessible LIVE icons. Cached history
// alone never establishes that an application still has a notification icon.
internal sealed record TrayIconMetadata(Guid Guid, string Tooltip, string Executable, ImageSource? Icon, uint? Id = null)
{
    internal string Name => Tooltip.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim()
        ?? Path.GetFileNameWithoutExtension(Executable);
}
internal static class TrayIconCatalog
{
    private static List<TrayIconMetadata> cached = new();
    private static DateTime refreshAfter;
    [StructLayout(LayoutKind.Sequential)] private struct Identifier { internal uint Size; internal nint Window; internal uint Id; internal Guid Guid; }
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref Identifier identifier, out OverlayChrome.PixelRect rectangle);
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, nint token, out nint path);
    internal static bool IsLive(TrayIconMetadata item)
    {
        if (item.Guid == Guid.Empty) return false;
        var id = new Identifier { Size = (uint)Marshal.SizeOf<Identifier>(), Guid = item.Guid };
        return Shell_NotifyIconGetRect(ref id, out _) >= 0;
    }
    private static string Expand(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path).Trim('"');
        int end = path.IndexOf('}');
        if (path.StartsWith('{') && end > 0 && Guid.TryParse(path[..(end + 1)], out var folder))
        {
            if (SHGetKnownFolderPath(ref folder, 0, 0, out var location) >= 0)
            { try { path = Path.Combine(Marshal.PtrToStringUni(location) ?? "", path[(end + 1)..].TrimStart('\\', '/')); } finally { Marshal.FreeCoTaskMem(location); } }
        }
        return path;
    }
    internal static List<TrayIconMetadata> Read()
    {
        if (DateTime.UtcNow < refreshAfter) return cached;
        var result = new List<TrayIconMetadata>();
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(@"Control Panel\NotifyIconSettings");
            if (root is null) return result;
            var icons = new ShellIcons();
            foreach (var name in root.GetSubKeyNames().Take(1024))
            {
                try
                {
                    using var item = root.OpenSubKey(name); if (item is null) continue;
                    string executable = Expand(item.GetValue("ExecutablePath") as string ?? "");
                    string tooltip = item.GetValue("InitialTooltip") as string ?? "";
                    Guid.TryParse(item.GetValue("IconGuid") as string, out var guid);
                    ImageSource? icon = null;
                    if (item.GetValue("IconSnapshot") is byte[] bytes && bytes.Length is > 0 and < 1048576)
                    {
                        try { using var stream = new MemoryStream(bytes); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze(); icon = image; }
                        catch (Exception error) when (error is NotSupportedException or FileFormatException or ArgumentException or IOException) { }
                    }
                    if (icon is null && File.Exists(executable)) icon = icons.Get(executable);
                    if (tooltip.Length == 0 && File.Exists(executable)) tooltip = FileVersionInfo.GetVersionInfo(executable).FileDescription ?? "";
                    object? rawId = item.GetValue("UID");
                    uint? id = rawId is int number ? unchecked((uint)number) : rawId is long large ? unchecked((uint)large) : null;
                    if (icon is not null && (tooltip.Length > 0 || executable.Length > 0)) result.Add(new(guid, tooltip, executable, icon, id));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or COMException or System.ComponentModel.Win32Exception) { }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        cached = result; refreshAfter = DateTime.UtcNow.AddSeconds(30);
        return result;
    }
    internal static TrayIconMetadata? Match(IEnumerable<TrayIconMetadata> items, string name)
    {
        string key = name.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? name;
        return items.FirstOrDefault(item => item.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
    }
}
