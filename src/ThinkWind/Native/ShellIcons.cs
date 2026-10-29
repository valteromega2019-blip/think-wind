using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ThinkWind.Native;

internal sealed class ShellIcons
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct FileInfo
    {
        internal nint Icon;
        internal int Index;
        internal uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] internal string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] internal string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern nint SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", EntryPoint = "SHGetFileInfoW")] private static extern nint SHGetPidlInfo(nint pidl, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHParseDisplayName(string name, nint binding, out nint pidl, uint attributes, out uint result);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeout(nint hwnd, uint message, nuint wParam, nint lParam, uint flags, uint timeout, out nint result);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern nint GetClassIcon(nint hwnd, int index);
    private readonly Dictionary<string, ImageSource?> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> resolved = new(StringComparer.OrdinalIgnoreCase);

    internal string Resolve(string target)
    {
        if (resolved.TryGetValue(target, out var cached)) return cached;
        if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
        {
            string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "ImmersiveControlPanel", "SystemSettings.exe");
            if (File.Exists(settings)) { resolved[target] = settings; return settings; }
        }
        var path = Environment.ExpandEnvironmentVariables(target);
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
        {
            object? shell = null, shortcut = null;
            try
            {
                var type = Type.GetTypeFromProgID("WScript.Shell");
                if (type is not null)
                {
                    shell = Activator.CreateInstance(type);
                    shortcut = ((dynamic)shell!).CreateShortcut(path);
                    string linked = ((dynamic)shortcut).TargetPath;
                    if (!string.IsNullOrWhiteSpace(linked)) path = Environment.ExpandEnvironmentVariables(linked);
                }
            }
            catch (Exception e) when (e is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
            finally { CoreAudio.Release(shortcut); CoreAudio.Release(shell); }
        }
        if (!Path.IsPathFullyQualified(path) && !path.Contains(':'))
        {
            foreach (var directory in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Windows), Environment.SystemDirectory }.ConcatPath())
            {
                try { var candidate = Path.Combine(directory, path); if (File.Exists(candidate)) { path = candidate; break; } }
                catch (ArgumentException) { }
            }
        }
        if (resolved.Count > 256) resolved.Clear();
        resolved[target] = path; return path;
    }
    internal ImageSource? Get(string? target, nint window = 0)
    {
        // Borrowed window icons belong to their process and must not be destroyed.
        if (window != 0)
        {
            var key = "window:" + window;
            if (cache.TryGetValue(key, out var existing)) return existing;
            try
            {
                SendMessageTimeout(window, 0x007F, 1, 0, 2, 80, out var icon);
                if (icon == 0) SendMessageTimeout(window, 0x007F, 0, 0, 2, 80, out icon);
                if (icon == 0) icon = GetClassIcon(window, -14);
                if (icon != 0) { var image = FromIcon(icon); Remember(key, image); return image; }
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        if (string.IsNullOrWhiteSpace(target)) return null;
        if (cache.TryGetValue(target, out var value)) return value;
        FileInfo info = default;
        try
        {
            if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase))
            {
                nint pidl = 0;
                try
                {
                    if (SHParseDisplayName(@"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel", 0, out pidl, 0, out _) >= 0)
                        SHGetPidlInfo(pidl, 0, out info, (uint)Marshal.SizeOf<FileInfo>(), 0x108);
                }
                finally { if (pidl != 0) Marshal.FreeCoTaskMem(pidl); }
            }
            else
            {
                var path = File.Exists(target) ? target : Resolve(target);
                SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf<FileInfo>(), 0x100);
            }
            value = info.Icon == 0 ? null : FromIcon(info.Icon);
        }
        catch (Exception e) when (e is ArgumentException or IOException or System.ComponentModel.Win32Exception) { value = null; }
        finally { if (info.Icon != 0) DestroyIcon(info.Icon); }
        Remember(target, value); return value;
    }
    private void Remember(string key, ImageSource? image) { if (image is null) return; if (cache.Count > 256) cache.Clear(); cache[key] = image; }
    private static ImageSource FromIcon(nint icon)
    {
        var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        image.Freeze(); return image;
    }
}
internal static class SearchDirectories
{
    internal static IEnumerable<string> ConcatPath(this IEnumerable<string> system)
    {
        foreach (var entry in system) yield return entry;
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)) yield return entry.Trim('"');
    }
}
