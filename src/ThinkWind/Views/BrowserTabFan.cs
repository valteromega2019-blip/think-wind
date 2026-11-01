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
    internal event Action? DismissRequested;
    internal BrowserTabFan() { Width = 320; }
    internal void Present(IReadOnlyList<BrowserTab> tabs, nint owner)
    {
        Dispose(); Children.Clear();
        Height = 160 + Math.Max(0, tabs.Count - 1) * 42;
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
            if (!live) area.Child = new TextBlock { Text = "Clique para abrir esta aba", FontSize = 12, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(area);
            var button = new Button { Width = 300, Height = 160, Padding = new Thickness(0), Margin = new Thickness(0), Content = row, HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = tab.Title };
            button.SetResourceReference(Control.BackgroundProperty, "Think.Surface");
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
            SetLeft(button, index % 2 == 0 ? 0 : 10); SetTop(button, index * 42); SetZIndex(button, index);
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
