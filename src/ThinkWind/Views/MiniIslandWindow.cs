using System;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal enum MiniIslandKind { Volume, Network, Calendar, Tray, Bluetooth, Clock, Weather, Brightness }
internal sealed class MiniIslandWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    private readonly Func<OverlayChrome.PixelRect> anchor;
    private readonly StackPanel panel = new();
    private readonly Viewbox viewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly AudioEndpointControl? audio;
    private readonly AudioEndpointControl? microphone;
    private readonly AppMixerControl? mixer;
    private bool mixerSelected;
    private readonly TrayCard? tray;
    private readonly WifiControl? wifi;
    private readonly BluetoothControl? bluetooth;
    private readonly ClockPanel? clockPanel;
    private readonly AgendaPanel? calendarView;
    private AgendaWindow? agendaWindow;
    private readonly WeatherWidget? weatherLocation;
    private readonly BrightnessControl? brightness;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime nextRefresh;
    private OverlayChrome.PixelRect positionedAnchor;
    private bool hasPositionedAnchor;
    private int presentationRevision;
    internal MiniIslandKind Kind { get; }
    internal event Action? Dismissed;
    internal int ActiveBluetoothWatchers => bluetooth?.ActiveWatchers ?? 0;
    internal bool IsInteracting => agendaWindow?.IsVisible == true || IsMouseCaptureWithin || audio?.IsInteracting == true || microphone?.IsInteracting == true || mixer?.IsInteracting == true || tray?.IsInteracting == true || wifi?.IsInteracting == true || bluetooth?.IsInteracting == true || weatherLocation?.IsInteracting == true || brightness?.IsInteracting == true;
    internal MiniIslandWindow(SettingsStore store, MiniIslandKind kind, Func<OverlayChrome.PixelRect> getAnchor) : base(store)
    {
        Kind = kind; anchor = getAnchor;
        Title = kind switch { MiniIslandKind.Brightness => "Orby Launcher — Brilho da tela", MiniIslandKind.Clock => "Orby Launcher — Relógio", MiniIslandKind.Bluetooth => "Orby Launcher — Bluetooth", MiniIslandKind.Volume => "Orby Launcher — Mini volume", MiniIslandKind.Calendar => "Orby Launcher — Calendário", MiniIslandKind.Tray => "Orby Launcher — Bandeja", _ => "Orby Launcher — Mini rede" };
        ShowActivated = true;
        var heading = new DockPanel();
        var close = ActionButton("Fechar este painel", Dismiss);
        close.Style = (Style)FindResource("Think.IconButton");
        close.Content = Glyph("\uE711", 13); close.Width = close.Height = 28;
        close.Padding = new Thickness(4);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = kind switch { MiniIslandKind.Brightness => "Brilho da tela", MiniIslandKind.Clock => "Relógio", MiniIslandKind.Bluetooth => "Bluetooth", MiniIslandKind.Volume => "Áudio e microfone", MiniIslandKind.Calendar => "Calendário", MiniIslandKind.Tray => "Aplicativos em segundo plano", _ => "Conexão de rede" }, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6) });
        panel.Children.Add(heading);
        if (kind == MiniIslandKind.Weather)
        {
            Title = "Orby Launcher — Cidade do clima";
            if (heading.Children[heading.Children.Count - 1] is TextBlock label) label.Text = "Cidade do clima";
        }
        if (kind == MiniIslandKind.Volume)
        {
            var service = new AudioService();
            var tabs = new WrapPanel { Margin = new Thickness(0, 4, 0, 6) };
            var devicesPanel = new StackPanel();
            audio = new AudioEndpointControl(service, CoreAudio.Flow.Render); devicesPanel.Children.Add(audio);
            microphone = new AudioEndpointControl(service, CoreAudio.Flow.Capture); devicesPanel.Children.Add(microphone);
            mixer = new AppMixerControl { Visibility = Visibility.Collapsed };
            var devicesTab = ActionButton("Dispositivos", () => { }); var appsTab = ActionButton("Aplicativos", () => { });
            void Select(bool apps)
            {
                mixerSelected = apps; devicesPanel.Visibility = apps ? Visibility.Collapsed : Visibility.Visible;
                mixer.Visibility = apps ? Visibility.Visible : Visibility.Collapsed; mixer.SetActive(apps && IsVisible);
                devicesTab.SetResourceReference(Control.BackgroundProperty, apps ? "Think.Control" : "Think.AccentSurface");
                appsTab.SetResourceReference(Control.BackgroundProperty, apps ? "Think.AccentSurface" : "Think.Control");
                Position();
            }
            devicesTab.Click += (_, _) => Select(false); appsTab.Click += (_, _) => Select(true);
            devicesTab.SetResourceReference(Control.BackgroundProperty, "Think.AccentSurface");
            tabs.Children.Add(devicesTab); tabs.Children.Add(appsTab); panel.Children.Add(tabs); panel.Children.Add(devicesPanel); panel.Children.Add(mixer);
            mixer.LayoutChanged += Position;
            audio.LayoutChanged += Position;
            microphone.LayoutChanged += Position;
            panel.Children.Add(ActionButton("Configurações de som", () => SystemActions.Run("ms-settings:sound")));
        }
        else if (kind == MiniIslandKind.Brightness)
        {
            brightness = new BrightnessControl(() => Display.Bounds, showStatusWhenUnavailable: true);
            brightness.LayoutChanged += Position;
            panel.Children.Add(new Border { Child = brightness, MinHeight = 160 * Math.Clamp(store.Value.FontScale, .8, 1.4) });
            panel.Children.Add(ActionButton("Configurações de Tela", () => SystemActions.Run("ms-settings:display")));
        }
        else if (kind == MiniIslandKind.Tray)
        {
            tray = new TrayCard(store); tray.LayoutChanged += Position; panel.Children.Add(tray);
        }
        else if (kind == MiniIslandKind.Weather)
        {
            weatherLocation = new WeatherWidget(store, configureLocation: true) { Height = double.NaN, Margin = new Thickness(6) };
            panel.Children.Add(weatherLocation);
        }
        else if (kind == MiniIslandKind.Clock)
        {
            clockPanel = new ClockPanel(); clockPanel.LayoutChanged += Position; panel.Children.Add(clockPanel);
        }
        else if (kind == MiniIslandKind.Calendar)
        {
            calendarView = new AgendaPanel(((App)Application.Current).Alarms, calendarOnly: true);
            panel.Children.Add(calendarView);
            void OpenAgenda(bool add)
            {
                var day = calendarView.SelectedDay;
                if (agendaWindow is null)
                {
                    agendaWindow = new AgendaWindow(Store, () => OverlayChrome.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(this).Handle, out var bounds) ? bounds : anchor());
                    agendaWindow.Owner = this;
                    agendaWindow.Closed += (_, _) => agendaWindow = null;
                }
                agendaWindow.Present(day, add);
            }
            panel.Children.Add(ClockDesign.Button("\uE787", "Ver agenda", () => OpenAgenda(false)));
            calendarView.AddRequested += _ => OpenAgenda(true);
        }
        else if (kind == MiniIslandKind.Bluetooth)
        {
            bluetooth = new BluetoothControl(store); bluetooth.LayoutChanged += Position; panel.Children.Add(bluetooth);
        }
        else
        {
            wifi = new WifiControl(); wifi.LayoutChanged += Position; panel.Children.Add(wifi);
        }
        viewport.Child = panel; Surface.Child = viewport;
        Deactivated += (_, _) =>
        {
            int revision = presentationRevision;
            // Deactivation can happen inside the next icon mouse-down. Hiding
            // synchronously interrupts activation before its Click is delivered.
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (revision == presentationRevision && IsVisible && !IsActive && !IsInteracting) Dismiss();
            }));
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; } };
        Loaded += (_, _) => { RefreshContent(true); timer.Start(); };
        timer.Tick += (_, _) => { if (IsVisible) RefreshContent(false); };
        IsVisibleChanged += (_, _) => { wifi?.SetActive(IsVisible); bluetooth?.SetActive(IsVisible); mixer?.SetActive(IsVisible && mixerSelected); if (!IsVisible) { timer.Stop(); Dismissed?.Invoke(); } };
        IsVisibleChanged += (_, _) => clockPanel?.SetActive(IsVisible);
        IsVisibleChanged += (_, _) => weatherLocation?.SetActive(IsVisible);
        Closed += (_, _) => weatherLocation?.Dispose();
        Closed += (_, _) => clockPanel?.Dispose();
        Closed += (_, _) => { calendarView?.Dispose(); agendaWindow?.Close(); };
        Closed += (_, _) => { timer.Stop(); wifi?.Dispose(); bluetooth?.Dispose(); mixer?.Dispose(); };
    }
    internal void Present()
    {
        ++presentationRevision;
        calendarView?.Refresh();
        nextRefresh = DateTime.MinValue;
        // Establish the reserved content geometry before the first visible frame.
        RefreshSettings(); RefreshContent(true); Show(); Position(); timer.Start(); Activate();
        if (SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0)
        {
            var move = new TranslateTransform(); panel.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    internal void Dismiss()
    {
        agendaWindow?.Close();
        if (!IsVisible) return;
        ++presentationRevision; Hide(); timer.Stop();
    }
    private void RefreshContent(bool force)
    {
        bool refresh = force || DateTime.UtcNow >= nextRefresh;
        if (refresh) nextRefresh = DateTime.UtcNow.AddSeconds(5);
        if (audio is not null) { if (mixerSelected) mixer?.Poll(); else { audio.Poll(refresh); microphone?.Poll(refresh); } }
        else if (tray is not null) tray.Refresh();
        brightness?.Refresh();
        weatherLocation?.Configure();
        // Data polling must not remeasure an unchanged calendar/network tree.
        // Keep following the anchor and still resize whenever content is dirty.
        // Weather search has async result rows and no LayoutChanged contract.
        // Keep its existing sizing cadence even if WPF measured between ticks.
        if (force || weatherLocation is not null || !panel.IsMeasureValid || !hasPositionedAnchor || !positionedAnchor.Equals(anchor())) Position();
    }
    private void PlaceAtAnchor(double width, double height)
    {
        positionedAnchor = anchor(); hasPositionedAnchor = true;
        PlaceNear(width, height, positionedAnchor, false);
    }
    public override void Position()
    {
        if (Kind == MiniIslandKind.Calendar) { PositionCalendar(); return; }
        if (Kind == MiniIslandKind.Clock) { PositionClock(); return; }
        double width = Math.Min(390, Display.Width - 16);
        viewport.Width = Math.Max(1, width - Surface.Padding.Left - Surface.Padding.Right);
        viewport.MaxHeight = Math.Max(60, CompactLayout.PanelHeight(Display.Height) - 24);
        panel.Width = viewport.Width;
        FontSizing.PrepareForMeasure(panel);
        if (mixer is not null) mixer.MaximumListHeight = Math.Max(100, Math.Min(340, CompactLayout.PanelHeight(Display.Height) - 160));
        panel.InvalidateMeasure();
        panel.Measure(new Size(Math.Max(1, width - 24), double.PositiveInfinity));
        PlaceAtAnchor(width, Math.Min(panel.DesiredSize.Height, viewport.MaxHeight) + 24);
    }
    private void PositionCalendar()
    {
        if (calendarView is null) return;
        FontSizing.PrepareForMeasure(panel);
        calendarView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        // Size the host around the calendar instead of reserving 390 DIP while
        // its intrinsic 280-DIP month grid remains aligned to the left.
        double contentWidth = Math.Max(280, calendarView.DesiredSize.Width);
        panel.Width = contentWidth;
        panel.Measure(new Size(contentWidth, double.PositiveInfinity));
        double contentHeight = Math.Max(1, panel.DesiredSize.Height);
        double horizontal = Surface.Padding.Left + Surface.Padding.Right;
        double vertical = Surface.Padding.Top + Surface.Padding.Bottom;
        double fit = Math.Min(1, Math.Min(Math.Max(1, Display.Width - 16 - horizontal) / contentWidth,
            Math.Max(1, CompactLayout.PanelHeight(Display.Height) - vertical) / contentHeight));
        viewport.Width = contentWidth * fit; viewport.Height = contentHeight * fit;
        viewport.MaxHeight = double.PositiveInfinity;
        PlaceAtAnchor(viewport.Width + horizontal, viewport.Height + vertical);
    }
    private void PositionClock()
    {
        // The generic 520-DIP limit shrank the whole clock inside a fixed-width
        // window: large side gutters, but almost no space beneath its last card.
        // Give the shared three-tab layout its natural height first.
        double width = Math.Min(390, Display.Width - 16);
        double edge = Surface.Padding.Left;
        double inner = Math.Max(1, width - edge * 2);
        panel.Width = inner;
        FontSizing.PrepareForMeasure(panel);
        panel.Measure(new Size(inner, double.PositiveInfinity));
        double natural = Math.Max(1, panel.DesiredSize.Height);
        double available = Math.Max(100, Display.Height - 80);
        double fit = Math.Min(1, Math.Max(.05, (available - edge * 2 - inner / 2) / Math.Max(1, natural - inner / 2)));
        double sideGutter = inner * (1 - fit) / 2;
        // On small displays/font scales that still need downscaling, reserve
        // the same extra inset below as the Viewbox creates at either side.
        Surface.Padding = new Thickness(edge, edge, edge, edge + sideGutter);
        viewport.Width = inner; viewport.MaxHeight = double.PositiveInfinity;
        viewport.Height = Math.Ceiling(natural * fit);
        viewport.VerticalAlignment = VerticalAlignment.Top;
        PlaceAtAnchor(width, viewport.Height + edge * 2 + sideGutter);
    }
}
