using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class AgendaWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override bool HideOnClose => false;
    private readonly AgendaPanel agenda;
    private readonly Grid layout = new();
    private readonly ScrollViewer scroll;
    private readonly Func<OrbyLauncher.Native.OverlayChrome.PixelRect> getCalendarBounds;
    private bool positionQueued, positioning, closed;
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
        Title = "Orby · Agenda"; ShowActivated = true;
        Surface.Padding = new Thickness(16);
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = ClockDesign.Button("\uE711", "Fechar agenda", Close, true);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        var title = ClockDesign.Text("Eventos e notas", 18); title.FontWeight = FontWeights.SemiBold; heading.Children.Add(title);
        layout.Children.Add(heading);
        agenda = new AgendaPanel(((App)Application.Current).Alarms);
        scroll = new ScrollViewer { Content = agenda, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Top };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll); Surface.Child = layout;
        agenda.LayoutChanged += () => { scroll.ScrollToTop(); QueuePosition(); };
        agenda.Loaded += (_, _) => QueuePosition();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => { closed = true; agenda.Dispose(); };
    }
    internal void Present(DateTime day, bool add)
    {
        agenda.SelectDay(day, add); Show(); RefreshSettings(); Position(); Activate();
    }
    internal void SelectDay(DateTime day)
    {
        agenda.SelectDay(day, false);
        if (IsVisible) Position();
    }
    public override void Position()
    {
        if (positioning || closed) return;
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
        PlaceNear(width, height,
            new OrbyLauncher.Native.OverlayChrome.PixelRect { Left = x, Right = x + pixelWidth, Top = bounds.Top, Bottom = bounds.Top - (int)(8 * Display.Scale) }, false);
        }
        finally { positioning = false; }
    }
}
