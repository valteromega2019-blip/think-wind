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
    private readonly Dictionary<string, long> resolvedVersions = new(StringComparer.OrdinalIgnoreCase);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern uint ExtractIconEx(string file, int index, out nint large, out nint small, uint count);
    private static long Stamp(string path)
    { try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0; } catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException) { return 0; } }
    private static ImageSource? ShortcutIcon(string target)
    {
        if (!Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) return null;
        object? shell = null, shortcut = null; nint large = 0, small = 0;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!); shortcut = ((dynamic)shell!).CreateShortcut(target);
            string location = ((dynamic)shortcut).IconLocation; int comma = location.LastIndexOf(',');
            string file = Environment.ExpandEnvironmentVariables((comma >= 0 ? location[..comma] : location).Trim().Trim('"'));
            int index = comma >= 0 && int.TryParse(location[(comma + 1)..], out int number) ? number : 0;
            if (file.Length > 0 && File.Exists(file) && ExtractIconEx(file, index, out large, out small, 1) > 0 && large != 0) return FromIcon(large);
        }
        catch (Exception error) when (error is COMException or ArgumentException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
        finally { if (large != 0) DestroyIcon(large); if (small != 0) DestroyIcon(small); CoreAudio.Release(shortcut); CoreAudio.Release(shell); }
        return null;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ImageSize { internal int Width, Height; }
    [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ImageFactory { [PreserveSig] int GetImage(ImageSize size, uint flags, out nint bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path, nint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out ImageFactory factory);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint bitmap);
    [StructLayout(LayoutKind.Sequential)] private struct NativeBitmap { internal int Type, Width, Height, Stride; internal ushort Planes, BitsPerPixel; internal nint Bits; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapHeader { internal uint Size; internal int Width, Height; internal ushort Planes, Bits; internal uint Compression, ImageSize; internal int XPixels, YPixels; internal uint Used, Important; }
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")] private static extern int GetBitmapObject(nint bitmap, int size, out NativeBitmap info);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] pixels, ref BitmapHeader info, uint usage);
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    // Called only by an STA worker: extracting a shell icon may perform disk I/O.
    internal static ImageSource? ReadLarge(string target)
    {
        var shortcutIcon = ShortcutIcon(target); if (shortcutIcon is not null) return shortcutIcon;
        ImageFactory? factory = null; nint bitmap = 0;
        try
        {
            string path = new ShellIcons().Resolve(target); var iid = typeof(ImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, 0, ref iid, out factory) < 0 || factory.GetImage(new ImageSize { Width = 96, Height = 96 }, 5, out bitmap) < 0 || bitmap == 0) return null;
            if (GetBitmapObject(bitmap, Marshal.SizeOf<NativeBitmap>(), out var info) == 0 || info.Width is < 1 or > 512 || info.Height is < 1 or > 512) return null;
            var pixels = new byte[info.Width * info.Height * 4];
            var header = new BitmapHeader { Size = 40, Width = info.Width, Height = -info.Height, Planes = 1, Bits = 32 };
            nint dc = GetDC(0);
            try { if (GetDIBits(dc, bitmap, 0, (uint)info.Height, pixels, ref header, 0) != info.Height) return null; }
            finally { if (dc != 0) ReleaseDC(0, dc); }
            bool alpha = false; for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) { alpha = true; break; }
            if (!alpha) for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            var image = BitmapSource.Create(info.Width, info.Height, 96, 96, PixelFormats.Pbgra32, null, pixels, info.Width * 4); image.Freeze(); return image;
        }
        catch (Exception error) when (error is COMException or ArgumentException or IOException or UnauthorizedAccessException) { return null; }
        finally { if (bitmap != 0) DeleteObject(bitmap); CoreAudio.Release(factory); }
    }

    internal string Resolve(string target)
    {
        long version = Stamp(target);
        if (resolved.TryGetValue(target, out var cached) && resolvedVersions.GetValueOrDefault(target) == version) return cached;
        resolvedVersions[target] = version;
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
        if (resolved.Count > 256) { resolved.Clear(); resolvedVersions.Clear(); resolvedVersions[target] = version; }
        resolved[target] = path; return path;
    }
    internal ImageSource? Get(string? target, nint window = 0, string? appId = null)
    {
        // Packaged apps may expose a blank window/host icon. Their registered
        // AppsFolder identity is a better source than the host executable.
        bool settings = target is not null && (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(target).Equals("SystemSettings.exe", StringComparison.OrdinalIgnoreCase));
        string? registeredTarget = settings ? @"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel"
            : !string.IsNullOrWhiteSpace(appId) && appId.Contains('!') ? @"shell:AppsFolder\" + appId : null;
        if (registeredTarget is not null && !registeredTarget.Equals(target, StringComparison.OrdinalIgnoreCase))
        {
            var registered = Get(registeredTarget);
            if (registered is not null) return registered;
        }
        // Borrowed window icons belong to their process and must not be destroyed.
        if (window != 0)
        {
            var key = "window:" + window + "|" + target + "|" + appId;
            if (cache.TryGetValue(key, out var existing)) return existing;
            try
            {
                foreach (nuint kind in new nuint[] { 1, 0, 2 })
                {
                    SendMessageTimeout(window, 0x007F, kind, 0, 2, 80, out var icon);
                    var image = icon == 0 ? null : FromIcon(icon);
                    if (image is not null) { Remember(key, image); return image; }
                }
                foreach (int index in new[] { -14, -34 })
                {
                    var icon = GetClassIcon(window, index);
                    var image = icon == 0 ? null : FromIcon(icon);
                    if (image is not null) { Remember(key, image); return image; }
                }
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        if (string.IsNullOrWhiteSpace(target)) return null;
        string targetKey = VersionedKey(target);
        if (cache.TryGetValue(targetKey, out var value)) return value;
        var shortcutIcon = ShortcutIcon(target);
        if (shortcutIcon is not null) { Remember(targetKey, shortcutIcon); return shortcutIcon; }
        FileInfo info = default;
        try
        {
            if (target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) || target.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
            {
                nint pidl = 0;
                try
                {
                    string shellTarget = target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) ? @"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel" : target;
                    if (SHParseDisplayName(shellTarget, 0, out pidl, 0, out _) >= 0)
                        SHGetPidlInfo(pidl, 0, out info, (uint)Marshal.SizeOf<FileInfo>(), 0x108);
                }
                finally { if (pidl != 0) Marshal.FreeCoTaskMem(pidl); }
            }
            else
            {
                var path = Resolve(target);
                SHGetFileInfo(path, 0, out info, (uint)Marshal.SizeOf<FileInfo>(), 0x100);
            }
            value = info.Icon == 0 ? null : FromIcon(info.Icon);
        }
        catch (Exception e) when (e is ArgumentException or IOException or System.ComponentModel.Win32Exception) { value = null; }
        finally { if (info.Icon != 0) DestroyIcon(info.Icon); }
        Remember(targetKey, value); return value;
    }
    private void Remember(string key, ImageSource? image) { if (image is null) return; if (cache.Count > 256) cache.Clear(); cache[key] = image; }
    internal string VersionedKey(string target) => target + "|" + Stamp(target) + "|" + Stamp(Resolve(target));
    private static ImageSource? FromIcon(nint icon)
    {
        var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        // A valid HICON can still contain no visible pixels. Do not cache it
        // as a successful result or prevent the remaining sources from trying.
        var rgba = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        int stride = rgba.PixelWidth * 4;
        var pixels = new byte[stride * rgba.PixelHeight];
        rgba.CopyPixels(pixels, stride, 0);
        bool visible = false;
        for (int i = 3; i < pixels.Length; i += 4)
            if (pixels[i] != 0) { visible = true; break; }
        if (!visible) return null;
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
