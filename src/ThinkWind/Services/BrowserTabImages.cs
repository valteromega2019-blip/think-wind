using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using WpfPixelFormats = System.Windows.Media.PixelFormats;
using OrbyLauncher.Native;
using Drawing = System.Drawing;

namespace OrbyLauncher.Services;

// Last-seen images only: no tab activation, disk files, capture loop or browser extension.
internal static class BrowserTabImages
{
    private sealed record Entry(string Title, BitmapSource Image, DateTime Updated);
    private static readonly object sync = new();
    private static readonly Dictionary<string, Entry> images = new();
    private const int Limit = 24;
    private static int capturing;
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window, uint flags);
    private static string Key(BrowserTab tab) => $"{tab.Window}:{tab.ProcessId}:{string.Join(",", tab.RuntimeId)}";
    private static string Title(string name)
    {
        foreach (string suffix in new[] { " - Uso de memória", " - Uso da memória", " - Memory usage" })
        {
            int at = name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (at >= 0) name = name[..at];
        }
        return name.Trim();
    }
    internal static BitmapSource? Get(BrowserTab tab)
    {
        if (tab.RuntimeId.Length == 0) return null;
        GetWindowThreadProcessId(tab.Window, out uint process);
        if (process != tab.ProcessId) return null;
        lock (sync)
            return images.TryGetValue(Key(tab), out var entry) && entry.Title == Title(tab.Title) ? entry.Image : null;
    }
    internal static async Task CaptureAsync(BrowserTab tab)
    {
        if (tab.RuntimeId.Length == 0 || GetForegroundWindow() != tab.Window || Interlocked.CompareExchange(ref capturing, 1, 0) != 0) return;
        try
        {
            // Allow the selected page to paint once; verify identity again on
            // the worker before and after reading any pixels.
            await Task.Delay(250);
            await Task.Run(() => Capture(tab));
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or ElementNotAvailableException
            or UnauthorizedAccessException or ArgumentException or NotSupportedException or IOException or ExternalException) { }
        finally { Volatile.Write(ref capturing, 0); }
    }
    private static bool ForegroundMatches(BrowserTab tab)
    {
        GetWindowThreadProcessId(tab.Window, out uint process);
        return process == tab.ProcessId && GetForegroundWindow() == tab.Window && Desktop.IsVisibleAndRestored(tab.Window);
    }
    private static bool SelectedMatches(AutomationElement root, BrowserTab tab)
    {
        foreach (var item in BrowserTabs.ReadTabElements(root))
            if (item.GetRuntimeId().SequenceEqual(tab.RuntimeId) && Title(item.Current.Name) == Title(tab.Title)
                && item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selected))
                return ((SelectionItemPattern)selected).Current.IsSelected;
        return false;
    }
    private static bool Uncovered(BrowserTab tab, Drawing.Rectangle bounds)
    {
        if (!ForegroundMatches(tab)) return false;
        int left = bounds.Left + 3, right = bounds.Right - 4, top = bounds.Top + 3, bottom = bounds.Bottom - 4;
        foreach (var point in new[] { new Point { X = left, Y = top }, new Point { X = right, Y = top },
            new Point { X = left, Y = bottom }, new Point { X = right, Y = bottom },
            new Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 } })
            if (GetAncestor(WindowFromPoint(point), 2) != tab.Window) return false;
        return true;
    }
    private static void Capture(BrowserTab tab)
    {
        if (!ForegroundMatches(tab)) return;
        var root = AutomationElement.FromHandle(tab.Window);
        if (!SelectedMatches(root, tab)) return;
        var document = root.FindFirst(TreeScope.Descendants,
            new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
                new PropertyCondition(AutomationElement.IsOffscreenProperty, false)));
        if (document is null || !OverlayChrome.GetWindowRect(tab.Window, out var window)) return;
        Rect rectangle = document.Current.BoundingRectangle;
        if (rectangle.IsEmpty || !double.IsFinite(rectangle.X) || !double.IsFinite(rectangle.Y)
            || !double.IsFinite(rectangle.Width) || !double.IsFinite(rectangle.Height)) return;
        rectangle.Intersect(new Rect(window.Left, window.Top, window.Right - window.Left, window.Bottom - window.Top));
        var desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
        rectangle.Intersect(new Rect(desktop.X, desktop.Y, desktop.Width, desktop.Height));
        if (rectangle.IsEmpty || rectangle.Width < 32 || rectangle.Height < 32 || rectangle.Width > 8192 || rectangle.Height > 8192
            || rectangle.Width * rectangle.Height > 20000000) return;
        var bounds = new Drawing.Rectangle((int)Math.Ceiling(rectangle.Left), (int)Math.Ceiling(rectangle.Top),
            (int)Math.Floor(rectangle.Width), (int)Math.Floor(rectangle.Height));
        if (!Uncovered(tab, bounds)) return;
        using var original = new Drawing.Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(original))
            graphics.CopyFromScreen(bounds.Location, Drawing.Point.Empty, bounds.Size, Drawing.CopyPixelOperation.SourceCopy);
        if (!Uncovered(tab, bounds) || !SelectedMatches(root, tab)) return;
        double ratio = Math.Min(384d / bounds.Width, 216d / bounds.Height);
        using var small = new Drawing.Bitmap(Math.Max(1, (int)(bounds.Width * ratio)), Math.Max(1, (int)(bounds.Height * ratio)), PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(small))
        {
            graphics.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original, 0, 0, small.Width, small.Height);
        }
        // Copy the reduced pixels directly. PNG encoding and decoding added CPU
        // work and kept an unnecessary compressed stream for every cached image.
        var pixels = small.LockBits(new Drawing.Rectangle(0, 0, small.Width, small.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        BitmapSource bitmap;
        try
        {
            bitmap = BitmapSource.Create(small.Width, small.Height, 96, 96, WpfPixelFormats.Bgra32, null,
                pixels.Scan0, checked(pixels.Stride * small.Height), pixels.Stride);
            bitmap.Freeze();
        }
        finally { small.UnlockBits(pixels); }
        lock (sync)
        {
            images[Key(tab)] = new(Title(tab.Title), bitmap, DateTime.UtcNow);
            foreach (string key in images.OrderByDescending(pair => pair.Value.Updated).Skip(Limit).Select(pair => pair.Key).ToArray()) images.Remove(key);
        }
    }
}
