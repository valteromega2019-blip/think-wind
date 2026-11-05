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
    private readonly StackPanel layout = new();
    private readonly ScrollViewer scroll;
    private readonly Func<OrbyLauncher.Native.OverlayChrome.PixelRect> getCalendarBounds;
    internal AgendaWindow(SettingsStore store, Func<OrbyLauncher.Native.OverlayChrome.PixelRect> calendarBounds) : base(store)
    {
        getCalendarBounds = calendarBounds;
        Title = "Orby · Agenda"; ShowActivated = true;
        Surface.Padding = new Thickness(16);
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = ClockDesign.Button("\uE711", "Fechar agenda", Close, true);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        var title = ClockDesign.Text("Eventos e notas", 18); title.FontWeight = FontWeights.SemiBold; heading.Children.Add(title);
        layout.Children.Add(heading);
        agenda = new AgendaPanel(((App)Application.Current).Alarms);
        scroll = new ScrollViewer { Content = agenda, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalAlignment = VerticalAlignment.Top };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        layout.Children.Add(scroll); Surface.Child = layout;
        agenda.LayoutChanged += () => { if (IsVisible) Position(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => agenda.Dispose();
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
        double width = Math.Min(460, Display.Width - 32);
        layout.Width = Math.Max(280, width - 32);
        scroll.MaxHeight = Math.Max(180, Display.Height - 160);
        layout.Measure(new Size(layout.Width, double.PositiveInfinity));
        var bounds = getCalendarBounds();
        int pixelWidth = (int)Math.Round(width * Display.Scale);
        int gap = (int)Math.Round(10 * Display.Scale);
        int x = bounds.Right + gap;
        if (x + pixelWidth > Display.Work.Right - 8) x = bounds.Left - gap - pixelWidth;
        PlaceNear(width, Math.Min(Display.Height - 48, layout.DesiredSize.Height + 32),
            new OrbyLauncher.Native.OverlayChrome.PixelRect { Left = x, Right = x + pixelWidth, Top = bounds.Top, Bottom = bounds.Top - (int)(8 * Display.Scale) }, false);
    }
}
