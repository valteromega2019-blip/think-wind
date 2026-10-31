using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class HardwareOverlayWindow : Window
{
    private readonly StackPanel rows = new();
    private readonly Border surface = new() { Padding = new Thickness(12, 9, 12, 9), BorderThickness = new Thickness(1) };
    private readonly HardwareTelemetry telemetry;
    private readonly SettingsStore store;
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint window, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    internal HardwareOverlayWindow(SettingsStore settings, HardwareTelemetry source)
    {
        store = settings; telemetry = source;
        Title = "Think Wind — Overlay de monitoramento";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight; IsHitTestVisible = false; Focusable = false;
        surface.Child = rows; Content = surface;
        SourceInitialized += (_, _) =>
        {
            nint window = new WindowInteropHelper(this).Handle;
            SetWindowLong(window, -20, GetWindowLong(window, -20) | 0x20 | 0x80 | 0x08000000);
            HwndSource.FromHwnd(window).AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
            { if (message == 0x84) { handled = true; return -1; } if (message == 0x21) { handled = true; return 3; } return 0; });
        };
        IsVisibleChanged += (_, _) => telemetry.SetActive(this, IsVisible);
        SizeChanged += (_, _) => PositionOverlay();
        telemetry.Changed += Update;
        Closed += (_, _) => { telemetry.Changed -= Update; telemetry.SetActive(this, false); };
    }
    internal void Apply(bool gameActive = false)
    {
        var p = store.Value;
        bool visible = p.HardwareOverlayEnabled && gameActive;
        telemetry.CaptureFrames = visible && p.OverlayVisibleMetrics.Any(key => key is "fps" or "frametime" or "low1" or "graph" or "game");
        if (!visible) { Hide(); return; }
        surface.LayoutTransform = new ScaleTransform(p.OverlayScale, p.OverlayScale);
        Render(telemetry.Latest);
        Show(); PositionOverlay();
    }
    private void Update(HardwareSample sample) { if (store.Value.HardwareOverlayEnabled) Render(sample); }
    private void Render(HardwareSample? sample)
    {
        var options = store.Value.OverlayAppearance;
        var palette = OverlayPalette.Resolve(options, Application.Current.Resources, store.Value);
        Brush B(Color color) => OverlayPalette.Brush(color);
        surface.Background = B(palette.Background); surface.BorderBrush = B(palette.Border);
        surface.CornerRadius = new CornerRadius(options.SyncWithAppTheme ? Math.Clamp(store.Value.IslandRadius, 0, 24) : options.CornerRadius);
        rows.Children.Clear();
        TextBlock Text(string value, double size = 12, bool muted = false) => new() {
            Text = value, FontFamily = new FontFamily("Cascadia Mono, Consolas"), FontSize = size,
            Foreground = B(muted ? palette.Muted : palette.Text), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
        Border Block(UIElement content) => new() { Background = B(palette.Block), CornerRadius = surface.CornerRadius,
            Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 0, 0, 3), Child = content };
        var selected = store.Value.OverlayVisibleMetrics;
        string V(string key) => sample is null ? "—" : OverlayReadings.Value(sample, key);
        if (selected.Contains("fps") || selected.Contains("frametime"))
        {
            var frameRow = new StackPanel { Orientation = Orientation.Horizontal };
            if (selected.Contains("fps")) { var fps = Text(V("fps"), 24); fps.FontWeight = FontWeights.Bold; fps.Foreground = B(palette.Accent); frameRow.Children.Add(fps); }
            if (selected.Contains("frametime")) { var time = Text(V("frametime")); time.Margin = new Thickness(frameRow.Children.Count > 0 ? 12 : 0, 0, 0, 4); time.VerticalAlignment = VerticalAlignment.Bottom; frameRow.Children.Add(time); }
            rows.Children.Add(Block(frameRow));
        }
        if (selected.Contains("low1")) rows.Children.Add(Text("1% low " + V("low1"), muted: true));
        if (selected.Contains("graph"))
        {
            var canvas = new Canvas { Width = 220, Height = 36, ClipToBounds = true };
            var graph = new Polyline { Stroke = B(Color.FromArgb((byte)Math.Round((100-options.GraphTransparency)*2.55), palette.Accent.R, palette.Accent.G, palette.Accent.B)), StrokeThickness = 1.5 };
            var times = sample?.Frames?.RecentFrameTimes;
            if (times is { Count: > 1 })
            {
                double max = Math.Max(16.7, times.Max());
                for (int i = 0; i < times.Count; i++) graph.Points.Add(new Point(i * 220d / (times.Count - 1), 34 - Math.Clamp(times[i] / max, 0, 1) * 32));
            }
            canvas.Children.Add(graph);
            rows.Children.Add(new Border { Background = B(palette.Graph), CornerRadius = surface.CornerRadius, Padding = new Thickness(5, 3, 5, 3), Margin = new Thickness(0, 6, 0, 4), Child = canvas });
        }
        if (sample is not null)
            foreach (var row in OverlayReadings.Rows(sample, selected))
            {
                var line = new Grid(); line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) }); line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) }); line.ColumnDefinitions.Add(new ColumnDefinition());
                line.Children.Add(new Border { Background = B(palette.Accent), CornerRadius = new CornerRadius(1) });
                var label = Text(row.Group, muted: true); label.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(label, 1); line.Children.Add(label);
                var value = Text(row.Value); Grid.SetColumn(value, 2); line.Children.Add(value); rows.Children.Add(Block(line));
            }
        string extra = string.Join(" · ", new[] { selected.Contains("game") ? V("game") : null, selected.Contains("clock") ? V("clock") : null }.Where(value => value is not null));
        if (extra.Length > 0) rows.Children.Add(Text(extra, 10, true));
        if (rows.Children.Count == 0) rows.Children.Add(Text("Nenhuma métrica selecionada", 11, true));
        PositionOverlay();
    }
    private void PositionOverlay()
    {
        if (!IsVisible) return;
        nint window = new WindowInteropHelper(this).Handle;
        if (!OverlayChrome.GetWindowRect(window, out var bounds)) return;
        var display = OverlayChrome.GetDisplay(store.Value.MonitorIndex, store.Value.MonitorDeviceName);
        int margin = (int)Math.Round(12 * display.Scale), width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
        bool right = store.Value.OverlayCorner.EndsWith("R"), bottom = store.Value.OverlayCorner.StartsWith("B");
        int x = right ? display.Bounds.Right - width - margin : display.Bounds.Left + margin;
        int y = bottom ? display.Bounds.Bottom - height - margin : display.Bounds.Top + margin;
        SetWindowPos(window, (nint)(-1), Math.Max(display.Bounds.Left, x), Math.Max(display.Bounds.Top, y), 0, 0, 0x0010 | 0x0001 | 0x0200);
    }
}
