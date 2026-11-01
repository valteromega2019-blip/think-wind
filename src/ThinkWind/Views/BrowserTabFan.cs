using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class BrowserTabFan : Canvas, IDisposable
{
    private readonly List<(Border Area, WindowThumbnail Host)> images = new();
    private readonly List<Border> surfaces = new();
    internal event Action? DismissRequested;
    internal BrowserTabFan() { Width = 320; }
    internal void Present(IReadOnlyList<BrowserTab> tabs, nint owner, Preferences preferences)
    {
        Dispose(); Children.Clear(); surfaces.Clear();
        int count = Math.Min(4, tabs.Count);
        // Reserve room for the rotated corners instead of clipping them.
        Height = 184 + Math.Max(0, count - 1) * 46;
        int index = 0;
        foreach (var tab in tabs.Take(4))
        {
            var row = new StackPanel { Width = 300 };
            var heading = new DockPanel { Margin = new Thickness(10, 8, 10, 6) };
            var marker = new TextBlock { Text = tab.Selected ? "●" : "○", Margin = new Thickness(0, 0, 7, 0), FontSize = 12 };
            marker.SetResourceReference(TextBlock.ForegroundProperty, tab.Selected ? "Think.Accent" : "Think.Muted"); heading.Children.Add(marker);
            heading.Children.Add(new TextBlock { Text = tab.Title, FontSize = 12, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }); row.Children.Add(heading);
            var area = new Border { Height = 116, Margin = new Thickness(10, 0, 10, 8) };
            bool live = tab.Selected && index == tabs.Count - 1;
            // Rear tabs are identified by their exposed title; avoid placeholder
            // text bleeding across the card immediately in front of them.
            row.Children.Add(area);
            var surface = new Border { Child = row, BorderThickness = new Thickness(1) };
            surfaces.Add(surface);
            var button = new Button { Width = 300, Height = 160, Padding = new Thickness(0), Margin = new Thickness(0), Content = surface, HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = tab.Title,
                Background = Brushes.Transparent, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform((index - count + 1) * 1.35) };
            if (tab.Cached) button.ToolTip = tab.Title + " · Última lista identificada; clique para restaurar e selecionar.";
            button.SetResourceReference(StyleProperty, "Think.IconButton");
            button.MouseEnter += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Accent");
            button.MouseLeave += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
            System.Windows.Automation.AutomationProperties.SetName(button, "Abrir aba: " + tab.Title);
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                try
                {
                    if (tab.RuntimeId.Length == 0 || await BrowserTabs.SelectAsync(tab, cancellation.Token))
                    { await Desktop.ActivateFromDock(tab.Window, false); DismissRequested?.Invoke(); }
                    else button.ToolTip = "A aba não está mais disponível. Reabra a prévia.";
                }
                catch (OperationCanceledException) { }
                finally { button.IsEnabled = true; }
            };
            SetLeft(button, 10); SetTop(button, 12 + index * 46); SetZIndex(button, index);
            Children.Add(button);
            if (live)
            {
                var host = new WindowThumbnail(owner, tab.Window);
                if (host.Available) images.Add((area, host));
                else { host.Dispose(); area.Child = new TextBlock { Text = "Clique para abrir esta aba", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center }; }
            }
            if (SystemParameters.ClientAreaAnimation)
                button.BeginAnimation(OpacityProperty, new DoubleAnimation(.5, 1, TimeSpan.FromMilliseconds(160 + index * 35)));
            index++;
        }
        ConfigureAppearance(preferences);
    }
    internal void ConfigureAppearance(Preferences preferences)
    {
        var tint = preferences.UseCustomBarColor ? (Color)ColorConverter.ConvertFromString(preferences.BarBackgroundColor) : ThemeService.BaseColor;
        // Each rounded surface transmits the preview's existing native Acrylic;
        // no second opaque fill or simulated blur is placed over it.
        tint.A = (byte)Math.Round(255 * Math.Clamp((preferences.DockOpacity ?? preferences.TintOpacity) * .48, .025, .65));
        foreach (var surface in surfaces)
        {
            surface.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(tint);
            surface.CornerRadius = new CornerRadius(Math.Clamp(preferences.DockRadius, 0, 24));
            surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
        }
    }
    internal void PlaceImages()
    {
        foreach (var item in images)
        {
            if (!item.Area.IsVisible || item.Area.ActualWidth <= 0 || item.Area.ActualHeight <= 0) continue;
            var start = item.Area.PointToScreen(new Point());
            var end = item.Area.PointToScreen(new Point(item.Area.ActualWidth, item.Area.ActualHeight));
            item.Host.Place(start, new Size(Math.Max(1, end.X - start.X), Math.Max(1, end.Y - start.Y)));
        }
    }
    public void Dispose() { foreach (var item in images) item.Host.Dispose(); images.Clear(); }
}
