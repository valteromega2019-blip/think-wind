using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class AgendaWindow : FloatingWindow
{
    private static readonly System.Collections.Generic.List<AgendaWindow> minimized = new();
    internal static AgendaWindow[] DockItems => minimized.ToArray();
    internal bool MinimizedToDock { get; private set; }
    internal event Action? MinimizedTogether;
    internal event Action? RestoringTogether;
    internal string DockKey { get; } = Guid.NewGuid().ToString("N");
    internal OrbyLauncher.Native.Desktop.RunningWindow DockWindow => new(
        new System.Windows.Interop.WindowInteropHelper(this).Handle, "Agenda · eventos e notas", null, true);
    internal void RestoreFromDock()
    {
        MinimizedToDock = false; minimized.Remove(this);
        RestoringTogether?.Invoke();
        WindowState = WindowState.Normal; Show(); RefreshSettings(); Activate();
    }
    private void MinimizeToDock()
    {
        MinimizedToDock = true;
        if (!minimized.Contains(this)) minimized.Add(this);
        WindowState = WindowState.Minimized;
        MinimizedTogether?.Invoke();
    }
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override bool HideOnClose => false;
    private readonly AgendaPanel agenda;
    private readonly Grid layout = new();
    private readonly ScrollViewer scroll;
    private readonly Func<OrbyLauncher.Native.OverlayChrome.PixelRect> getCalendarBounds;
    private bool positionQueued, positioning, closed;
    private OrbyLauncher.Native.OverlayChrome.PixelRect? movedBounds;
    private void QueuePosition()
    {
        if (positionQueued || closed) return;
        positionQueued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            positionQueued = false;
            if (IsVisible && !closed) Position();
        }));
    }
    internal AgendaWindow(SettingsStore store, Func<OrbyLauncher.Native.OverlayChrome.PixelRect> calendarBounds) : base(store)
    {
        getCalendarBounds = calendarBounds;
        Title = "Orby · Agenda"; ShowActivated = true; ShowInTaskbar = false;
        Surface.Padding = new Thickness(16);
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Background = System.Windows.Media.Brushes.Transparent };
        var close = ClockDesign.Button("\uE711", "Fechar agenda", Close, true);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        var minimize = ClockDesign.Button("\uE921", "Minimizar agenda na barra", MinimizeToDock, true);
        DockPanel.SetDock(minimize, Dock.Right); heading.Children.Add(minimize);
        heading.MouseLeftButtonDown += (_, e) =>
        {
            var source = e.OriginalSource as DependencyObject;
            while (source is not null && source is not System.Windows.Controls.Primitives.ButtonBase)
            {
                if (ReferenceEquals(source, heading)) break;
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            if (source is System.Windows.Controls.Primitives.ButtonBase || e.ClickCount != 1) return;
            try
            {
                DragMove();
                if (OrbyLauncher.Native.OverlayChrome.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle, out var bounds)) movedBounds = bounds;
            }
            catch (InvalidOperationException) { }
        };
        var title = ClockDesign.Text("Eventos e notas", 18); title.FontWeight = FontWeights.SemiBold; heading.Children.Add(title);
        layout.Children.Add(heading);
        agenda = new AgendaPanel(((App)Application.Current).Alarms);
        scroll = new ScrollViewer { Content = agenda, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Top };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll); Surface.Child = layout;
        agenda.LayoutChanged += () => { scroll.ScrollToTop(); QueuePosition(); };
        agenda.Loaded += (_, _) => QueuePosition();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => { closed = true; minimized.Remove(this); agenda.Dispose(); };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Normal && MinimizedToDock)
            { MinimizedToDock = false; minimized.Remove(this); RestoringTogether?.Invoke(); QueuePosition(); }
        };
    }
    internal void Present(DateTime day, bool add)
    {
        agenda.SelectDay(day, add); MinimizedToDock = false; minimized.Remove(this); WindowState = WindowState.Normal; Show(); RefreshSettings(); Position(); Activate();
    }
    internal void SelectDay(DateTime day)
    {
        agenda.SelectDay(day, false);
        if (IsVisible) Position();
    }
    public override void Position()
    {
        if (positioning || closed || WindowState == WindowState.Minimized) return;
        positioning = true;
        try
        {
        double width = Math.Min(460, Display.Width - 32);
        layout.Width = Math.Max(280, width - 32);
        // The previous viewport must not constrain the new form's natural measure.
        layout.Height = double.NaN;
        scroll.MaxHeight = double.PositiveInfinity;
        FontSizing.PrepareForMeasure(layout);
        agenda.InvalidateMeasure();
        var heading = (FrameworkElement)layout.Children[0];
        heading.Measure(new Size(layout.Width, double.PositiveInfinity));
        agenda.Measure(new Size(layout.Width, double.PositiveInfinity));
        double available = Math.Max(160, (Display.Work.Bottom - Display.Work.Top) / Display.Scale - 24);
        double height = Math.Min(available, agenda.DesiredSize.Height + heading.DesiredSize.Height + 32);
        layout.Height = Math.Max(1, height - 32);
        scroll.MaxHeight = Math.Max(1, layout.Height - heading.DesiredSize.Height);
        layout.Measure(new Size(layout.Width, layout.Height));
        var bounds = getCalendarBounds();
        int pixelWidth = (int)Math.Round(width * Display.Scale);
        int gap = (int)Math.Round(10 * Display.Scale);
        int x = bounds.Right + gap;
        if (x + pixelWidth > Display.Work.Right - 8) x = bounds.Left - gap - pixelWidth;
        if (movedBounds is { } moved) { x = moved.Left; bounds.Top = moved.Top; }
        PlaceNear(width, height,
            new OrbyLauncher.Native.OverlayChrome.PixelRect { Left = x, Right = x + pixelWidth, Top = bounds.Top, Bottom = bounds.Top - (int)(8 * Display.Scale) }, false);
        }
        finally { positioning = false; }
    }
}
