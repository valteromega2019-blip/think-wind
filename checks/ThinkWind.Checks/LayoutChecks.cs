using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ThinkWind.Services;
using ThinkWind.Views;

internal static class LayoutChecks
{
    internal static void DrawerSizes()
    {
        foreach (double width in new[] { 360d, 720, 1366, 1920 })
        foreach (double height in new[] { 200d, 400, 660, 1000 })
        {
            var layout = AppDrawerLayout.Fit(width, height);
            Require(layout.Width <= width && layout.Height <= height);
            Require(layout.Columns is >= 1 and <= 6 && layout.Rows is >= 1 and <= 3);
            Require(layout.Capacity > 0);
        }
        Require(AppDrawerLayout.Fit(1920, 800).Width == 720);
    }
    internal static void DrawerTabs()
    {
        var layout = AppDrawerLayout.Fit(1920, 800);
        Size? previous = null;
        foreach (int count in new[] { 1, 3, layout.Capacity, 0 })
        {
            var grid = new UniformGrid { Width = layout.Width - 28, Columns = layout.Columns, Rows = layout.Rows, Height = layout.Rows * AppDrawerLayout.CellHeight };
            for (int i = 0; i < count; i++) grid.Children.Add(new Border { Height = AppDrawerLayout.CellHeight - 8, Margin = new Thickness(4) });
            grid.Measure(new Size(layout.Width - 28, double.PositiveInfinity)); grid.Arrange(new Rect(new Point(), grid.DesiredSize));
            Require(previous is null || previous.Value == grid.DesiredSize);
            previous = grid.DesiredSize;
            foreach (FrameworkElement item in grid.Children) Require(item.ActualHeight >= 120);
        }
    }
    internal static void IslandPacking()
    {
        var board = new IslandModuleBoard();
        var cards = new[] { "connections", "media", "audio", "metrics", "calendar", "background", "notifications" }
            .Select(id => new Border { Tag = id, MinHeight = id == "calendar" ? 310 : id == "background" ? 260 : 80 }).ToArray();
        board.SetCards(cards); board.Measure(new Size(780, double.PositiveInfinity)); board.Arrange(new Rect(new Point(), board.DesiredSize));
        Rect Bounds(FrameworkElement item) => new(item.TranslatePoint(new Point(), board), item.RenderSize);
        var calendar = Bounds(cards[4]); var tray = Bounds(cards[5]); var last = Bounds(cards[6]);
        Require(Math.Abs(calendar.Bottom - tray.Bottom) < .01 && Math.Abs(last.Bottom - tray.Bottom) < .01);
        for (int i = 0; i < cards.Length; i++) for (int j = i + 1; j < cards.Length; j++)
        { var intersection = Rect.Intersect(Bounds(cards[i]), Bounds(cards[j])); Require(intersection.IsEmpty || intersection.Width == 0 || intersection.Height == 0); }
    }
    internal static void WifiDispatcher()
    {
        var control = new WifiControl();
        var context = System.Threading.SynchronizationContext.Current;
        try
        {
            // Reproduce refresh raised outside WPF's normal event context.
            System.Threading.SynchronizationContext.SetSynchronizationContext(null);
            control.SetActive(true);
            System.Threading.SynchronizationContext.SetSynchronizationContext(context);
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            while (control.IsRefreshing && elapsed.Elapsed.TotalSeconds < 20)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            }
            Require(!control.IsRefreshing);
        }
        finally { System.Threading.SynchronizationContext.SetSynchronizationContext(context); control.SetActive(false); control.Dispose(); }
    }
    internal static void DockHeadroom()
    {
        foreach (double icon in new[] { 18d, 20, 28, 48, 72 })
        foreach (double amount in new[] { .4, .5, .6 })
        {
            double row = icon + 22, reserve = DockGeometry.Headroom(row, amount);
            Require(reserve - row * amount * 1.08 >= 12);
            Require(DockGeometry.StartWidth(icon) >= icon + 8);
        }
    }
    internal static void SurfaceOpacity()
    {
        foreach (string theme in new[] { "Dark", "Light" })
        {
            var p = new Preferences { Theme = theme, InnerSurfaceOpacity = .1 };
            ThemeService.Apply(p); var low = ((System.Windows.Media.SolidColorBrush)Application.Current.Resources["Think.Control"]).Color.A;
            p.InnerSurfaceOpacity = .9; ThemeService.Apply(p);
            var high = ((System.Windows.Media.SolidColorBrush)Application.Current.Resources["Think.Control"]).Color.A;
            Require(SystemParameters.HighContrast || (low > 0 && high < 255 && high > low));
            Require(((System.Windows.Media.SolidColorBrush)Application.Current.Resources["Think.Text"]).Color.A == 255);
            Require(((System.Windows.Media.SolidColorBrush)Application.Current.Resources["Think.Popup"]).Color.A == 255);
        }
    }
    private static void Require(bool value) { if (!value) throw new InvalidOperationException("Geometria inconsistente."); }
}
