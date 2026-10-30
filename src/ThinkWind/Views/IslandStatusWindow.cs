using System.Windows;
using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

// Keep the status strip in its own small HWND while the control panel expands
// below it. The empty area between them never becomes a click-blocking canvas.
internal sealed class IslandStatusWindow : FloatingWindow
{
    protected override bool AttachedTop => true;
    protected override bool FullWidthTop => true;
    private Color? sampled;
    private DateTime nextSample;
    private bool MatchApp => Store.Value.IslandMatchAppColor && !SystemParameters.HighContrast;
    protected override Color MaterialTint => MatchApp && sampled.HasValue ? sampled.Value : base.MaterialTint;
    protected override bool MaterialLight => MatchApp && sampled.HasValue ? IsLight(sampled.Value) : base.MaterialLight;
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(nint dc, int x, int y);
    [StructLayout(LayoutKind.Sequential)] private struct PixelPoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(PixelPoint point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    internal IslandStatusWindow(SettingsStore store) : base(store)
    {
        Title = "Think Wind — Barra de status"; Surface.Padding = new Thickness(12, 2, 12, 2);
        IsVisibleChanged += (_, _) => { if (!IsVisible && Surface.Background is SolidColorBrush brush) brush.BeginAnimation(SolidColorBrush.ColorProperty, null); };
    }
    internal void Attach(UIElement? content) => Surface.Child = content;
    public override void Position() => Place(Display.Bounds.Width / Display.Scale, 28, false);
    internal void RefreshAppTint()
    {
        if (!MatchApp)
        {
            if (sampled.HasValue) { sampled = null; RefreshAppearance(); }
            return;
        }
        if (!IsVisible || DateTime.UtcNow < nextSample) return;
        nextSample = DateTime.UtcNow.AddSeconds(1);
        var bounds = Bounds;
        if (bounds.Right <= bounds.Left || bounds.Bottom <= bounds.Top) return;
        // Physical screen coordinates; Bottom is exclusive, so this is exactly
        // the first pixel below the strip, outside our own Acrylic surface.
        int x = bounds.Left + (bounds.Right - bounds.Left) / 2, y = bounds.Bottom;
        if (!Display.Bounds.Contains(x, y)) return;
        GetWindowThreadProcessId(WindowFromPoint(new PixelPoint { X = x, Y = y }), out uint process);
        if (process == Environment.ProcessId) return;
        var dc = GetDC(0); if (dc == 0) return;
        uint pixel;
        try { pixel = GetPixel(dc, x, y); }
        finally { ReleaseDC(0, dc); }
        if (pixel == 0xFFFFFFFF) return;
        var color = Color.FromRgb((byte)pixel, (byte)(pixel >> 8), (byte)(pixel >> 16));
        if (sampled is Color previous && Math.Abs(color.R - previous.R) + Math.Abs(color.G - previous.G) + Math.Abs(color.B - previous.B) <= 3) return;
        var old = (Surface.Background as SolidColorBrush)?.Color;
        sampled = color;
        base.RefreshAppearance(); ApplyTextContrast();
        if (old.HasValue && Surface.Background is SolidColorBrush brush && SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0)
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(old.Value, brush.Color, TimeSpan.FromMilliseconds(Math.Min(180, Store.Value.AnimationMilliseconds)))
            { FillBehavior = FillBehavior.Stop }, HandoffBehavior.SnapshotAndReplace);
    }
    private static bool IsLight(Color color)
    {
        static double Linear(byte value) { double channel = value / 255d; return channel <= .04045 ? channel / 12.92 : Math.Pow((channel + .055) / 1.055, 2.4); }
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B) > .179;
    }
    private void ApplyTextContrast()
    {
        if (!MatchApp || !sampled.HasValue)
        { Resources.Remove("Think.Text"); Resources.Remove("Think.Muted"); return; }
        var ink = new SolidColorBrush(IsLight(sampled.Value) ? Color.FromRgb(20, 24, 30) : Color.FromRgb(248, 249, 252));
        ink.Freeze(); Resources["Think.Text"] = ink; Resources["Think.Muted"] = ink;
    }
    public override void RefreshAppearance()
    {
        if (!MatchApp) sampled = null;
        base.RefreshAppearance(); ApplyTextContrast();
    }
}
