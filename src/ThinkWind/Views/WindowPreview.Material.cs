using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class WindowPreview
{
    private readonly List<(Border Surface, BackdropHost Host)> individualSurfaces = new();
    private Border PreviewSurface(FrameworkElement content, bool padded = true)
    {
        var surface = new Border { Child = content, Padding = new Thickness(padded ? 10 : 0),
            Margin = new Thickness(padded ? 4 : 0), BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { Color = Colors.Black, Opacity = .22, BlurRadius = 14, ShadowDepth = 2 } };
        var host = new BackdropHost(new WindowInteropHelper(this).EnsureHandle(), false, 0, "Prévia de aplicativo");
        individualSurfaces.Add((surface, host));
        surface.MouseEnter += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Accent");
        surface.MouseLeave += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
        ConfigureSurfaces();
        return surface;
    }
    private void ConfigureSurfaces()
    {
        var p = Store.Value;
        var tint = p.UseCustomBarColor ? (Color)ColorConverter.ConvertFromString(p.BarBackgroundColor) : ThemeService.BaseColor;
        tint.A = (byte)Math.Round(255 * Math.Clamp(p.DockOpacity ?? p.TintOpacity, .05, 1));
        var background = new SolidColorBrush(tint); background.Freeze();
        foreach (var item in individualSurfaces)
        {
            item.Surface.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : background;
            item.Surface.CornerRadius = new CornerRadius(Math.Clamp(p.DockRadius, 0, 24));
            item.Surface.SetResourceReference(Border.BorderBrushProperty, item.Surface.IsMouseOver ? "Think.Accent" : "Think.Hover");
            item.Host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
        }
    }
    private void PlaceSurfaces()
    {
        foreach (var item in individualSurfaces)
        {
            var surface = item.Surface;
            if (!surface.IsVisible || !surface.IsLoaded || surface.ActualWidth <= 0 || surface.ActualHeight <= 0)
            { item.Host.Hide(); continue; }
            var start = surface.PointToScreen(new Point());
            var end = surface.PointToScreen(new Point(surface.ActualWidth, surface.ActualHeight));
            double scale = Math.Max(.1, (end.X - start.X) / surface.ActualWidth);
            item.Host.Synchronize(new OverlayChrome.PixelRect { Left = (int)Math.Floor(start.X), Top = (int)Math.Floor(start.Y),
                Right = (int)Math.Ceiling(end.X), Bottom = (int)Math.Ceiling(end.Y) },
                Math.Clamp(Store.Value.DockRadius, 0, 24) * scale, false, true);
        }
    }
    private void ReleaseSurfaces()
    {
        foreach (var item in individualSurfaces) { item.Host.Dispose(); item.Surface.Child = null; }
        individualSurfaces.Clear();
    }
}
