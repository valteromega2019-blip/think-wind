using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed partial class IslandWindow
{
    private readonly Border secondArea = new() { Height = 220, CornerRadius = new CornerRadius(18), Visibility = Visibility.Collapsed };
    private readonly StackPanel areaIndicators = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Height = 14 };
    private bool secondAreaSelected;
    private EmptyIslandAreaWindow? leftArea;
    private bool SecondAreaAvailable => !maximizedOnDisplay || fullscreenReveal;
    private void UpdateSecondAreaMode()
    {
        if (!SecondAreaAvailable) secondAreaSelected = false;
        areaIndicators.Visibility = SecondAreaAvailable ? Visibility.Visible : Visibility.Collapsed;
        moduleViewport.Visibility = secondAreaSelected ? Visibility.Collapsed : Visibility.Visible;
        fullPanel.Children[0].Visibility = moduleViewport.Visibility;
        secondArea.Visibility = secondAreaSelected ? Visibility.Visible : Visibility.Collapsed;
    }
    private void InitializeSecondArea()
    {
        secondArea.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
        secondArea.Background = System.Windows.Media.Brushes.Transparent;
        var monitor = new HardwareMonitorPanel(Store, ((App)Application.Current).Telemetry) { Width = 760, WideLayout = true };
        monitor.LayoutChanged += () => { if (secondAreaSelected) ModuleSizeChanged(); };
        secondArea.Child = new Viewbox { Stretch = System.Windows.Media.Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = monitor };
        fullPanel.Children.Add(secondArea);
        fullPanel.Children.Add(areaIndicators);
        RefreshAreaIndicators();
        IsVisibleChanged += (_, _) => { if (!IsVisible) leftArea?.Hide(); };
        Closed += (_, _) => leftArea?.Close();
    }
    private void SelectSecondArea(bool selected)
    {
        if (!SecondAreaAvailable) return;
        secondAreaSelected = selected;
        moduleViewport.Visibility = selected ? Visibility.Collapsed : Visibility.Visible;
        fullPanel.Children[0].Visibility = moduleViewport.Visibility;
        secondArea.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
        RefreshAreaIndicators(); BuildHeader();
        Resize(true);
    }
    private void RefreshAreaIndicators()
    {
        areaIndicators.Children.Clear();
        for (int index = 0; index < 2; index++)
        {
            bool extra = index == 1;
            var button = ActionButton(extra ? "Área de monitoramento" : "Área principal", () => SelectSecondArea(extra));
            button.Style = (Style)FindResource("Think.IconButton");
            button.Padding = new Thickness(3); button.Margin = new Thickness(2, 0, 2, 0);
            var line = new Border { Width = 24, Height = 3, CornerRadius = new CornerRadius(2), Opacity = extra == secondAreaSelected ? 1 : .35 };
            line.SetResourceReference(Border.BackgroundProperty, extra == secondAreaSelected ? "Think.Accent" : "Think.Muted");
            button.Content = line; areaIndicators.Children.Add(button);
        }
    }
    private void ToggleLeftArea()
    {
        leftArea ??= new EmptyIslandAreaWindow(Store, ((App)Application.Current).Telemetry) { MonitorDeviceOverride = MonitorDeviceOverride };
        if (leftArea.IsVisible) leftArea.Hide();
        else { leftArea.Show(); leftArea.Position(); }
    }
}

internal sealed class EmptyIslandAreaWindow : FloatingWindow
{
    private readonly DispatcherTimer collapse = new() { Interval = System.TimeSpan.FromMilliseconds(100) };
    private System.DateTime? outsideSince;
    private readonly Viewbox monitorViewport = new()
    {
        Stretch = System.Windows.Media.Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top
    };
    protected override bool AttachedTop => true;
    protected override bool SquareTopCorners => false;
    protected override string? TopPanelSide => "Left";
    protected override double TopEdgeOffset => 36;
    internal EmptyIslandAreaWindow(SettingsStore store, HardwareTelemetry telemetry) : base(store)
    {
        Title = "Think Wind — Área 2"; ShowActivated = false;
        Surface.Padding = new Thickness(12);
        var monitor = new HardwareMonitorPanel(store, telemetry) { Width = 520 };
        monitorViewport.Child = monitor; Surface.Child = monitorViewport;
        monitor.LayoutChanged += () => { if (IsVisible) Position(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Hide(); e.Handled = true; } };
        Deactivated += (_, _) => Hide();
        MouseEnter += (_, _) => outsideSince = null;
        IsVisibleChanged += (_, _) => { outsideSince = null; if (IsVisible) collapse.Start(); else collapse.Stop(); };
        collapse.Tick += (_, _) =>
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            var bounds = Bounds;
            int bridge = (int)System.Math.Ceiling(12 * Display.Scale);
            bool inside = cursor.X >= bounds.Left && cursor.X < bounds.Right
                && cursor.Y >= bounds.Top - bridge && cursor.Y < bounds.Bottom;
            if (inside || IsMouseCaptureWithin) { outsideSince = null; return; }
            outsideSince ??= System.DateTime.UtcNow;
            if (System.DateTime.UtcNow - outsideSince.Value >= System.TimeSpan.FromSeconds(System.Math.Max(.35, Store.Value.IslandCollapseSeconds))) Hide();
        };
        store.Changed += AppearanceChanged;
        Closed += (_, _) => { collapse.Stop(); store.Changed -= AppearanceChanged; };
    }
    private void AppearanceChanged(object? sender, System.EventArgs e) { ApplyAppearance(); if (IsVisible) Position(); }
    public override void Position()
    {
        FontSizing.PrepareForMeasure(monitorViewport);
        if (monitorViewport.Child is not FrameworkElement panel) return;
        panel.Measure(new Size(520, double.PositiveInfinity));
        double naturalHeight = panel is HardwareMonitorPanel board
            ? board.MeasureBoard(520).Height : System.Math.Max(1, panel.DesiredSize.Height);
        double scale = System.Math.Min(1, System.Math.Min(System.Math.Max(1, Display.Width - 48) / 520, System.Math.Max(1, Display.Height - 72) / naturalHeight));
        monitorViewport.Width = 520 * scale; monitorViewport.Height = naturalHeight * scale;
        Place(monitorViewport.Width + 24, monitorViewport.Height + 24, false);
    }
}
