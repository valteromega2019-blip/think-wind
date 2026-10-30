using System;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal enum MiniIslandKind { Volume, Network, Calendar, Tray, Bluetooth }
internal sealed class MiniIslandWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    private readonly Func<OverlayChrome.PixelRect> anchor;
    private readonly StackPanel panel = new();
    private readonly Viewbox viewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly AudioEndpointControl? audio;
    private readonly AudioEndpointControl? microphone;
    private readonly TrayCard? tray;
    private readonly WifiControl? wifi;
    private readonly BluetoothControl? bluetooth;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime nextRefresh;
    internal MiniIslandKind Kind { get; }
    internal event Action? Dismissed;
    internal int ActiveBluetoothWatchers => bluetooth?.ActiveWatchers ?? 0;
    internal bool IsInteracting => IsMouseCaptureWithin || audio?.IsInteracting == true || microphone?.IsInteracting == true || tray?.IsInteracting == true || wifi?.IsInteracting == true || bluetooth?.IsInteracting == true;
    internal MiniIslandWindow(SettingsStore store, MiniIslandKind kind, Func<OverlayChrome.PixelRect> getAnchor) : base(store)
    {
        Kind = kind; anchor = getAnchor;
        Title = kind switch { MiniIslandKind.Bluetooth => "Think Wind — Bluetooth", MiniIslandKind.Volume => "Think Wind — Mini volume", MiniIslandKind.Calendar => "Think Wind — Calendário", MiniIslandKind.Tray => "Think Wind — Bandeja", _ => "Think Wind — Mini rede" };
        ShowActivated = true;
        var heading = new DockPanel();
        var close = ActionButton("Fechar", Dismiss); close.Padding = new Thickness(8, 4, 8, 4);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = kind switch { MiniIslandKind.Bluetooth => "Bluetooth", MiniIslandKind.Volume => "Áudio e microfone", MiniIslandKind.Calendar => "Calendário", MiniIslandKind.Tray => "Aplicativos em segundo plano", _ => "Conexão de rede" }, FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6) });
        panel.Children.Add(heading);
        if (kind == MiniIslandKind.Volume)
        {
            var service = new AudioService();
            audio = new AudioEndpointControl(service, CoreAudio.Flow.Render); panel.Children.Add(audio);
            microphone = new AudioEndpointControl(service, CoreAudio.Flow.Capture); panel.Children.Add(microphone);
            audio.LayoutChanged += Position;
            microphone.LayoutChanged += Position;
            panel.Children.Add(ActionButton("Configurações de som", () => SystemActions.Run("ms-settings:sound")));
        }
        else if (kind == MiniIslandKind.Tray)
        {
            tray = new TrayCard(store); tray.LayoutChanged += Position; panel.Children.Add(tray);
        }
        else if (kind == MiniIslandKind.Calendar)
        {
            var calendar = new System.Windows.Controls.Calendar(); calendar.SetResourceReference(StyleProperty, "Think.Calendar");
            calendar.DisplayModeChanged += (_, _) => Position(); panel.Children.Add(calendar);
        }
        else if (kind == MiniIslandKind.Bluetooth)
        {
            bluetooth = new BluetoothControl(); bluetooth.LayoutChanged += Position; panel.Children.Add(bluetooth);
        }
        else
        {
            wifi = new WifiControl(); wifi.LayoutChanged += Position; panel.Children.Add(wifi);
        }
        viewport.Child = panel; Surface.Child = viewport;
        Deactivated += (_, _) => { if (!IsInteracting) Dismiss(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; } };
        Loaded += (_, _) => { RefreshContent(true); timer.Start(); };
        timer.Tick += (_, _) => { if (IsVisible) RefreshContent(false); };
        IsVisibleChanged += (_, _) => { wifi?.SetActive(IsVisible); bluetooth?.SetActive(IsVisible); if (!IsVisible) { timer.Stop(); Dismissed?.Invoke(); } };
        Closed += (_, _) => { timer.Stop(); wifi?.Dispose(); bluetooth?.Dispose(); };
    }
    internal void Present()
    {
        nextRefresh = DateTime.MinValue;
        Show(); RefreshSettings(); RefreshContent(true); timer.Start(); Activate();
        if (SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0)
        {
            var move = new TranslateTransform(); panel.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(150)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        }
    }
    internal void Dismiss()
    {
        if (!IsVisible) return;
        Hide(); timer.Stop();
    }
    private void RefreshContent(bool force)
    {
        bool refresh = force || DateTime.UtcNow >= nextRefresh;
        if (refresh) nextRefresh = DateTime.UtcNow.AddSeconds(5);
        if (audio is not null) { audio.Poll(refresh); microphone?.Poll(refresh); }
        else if (tray is not null) tray.Refresh();
        Position();
    }
    public override void Position()
    {
        double width = Math.Min(390, Display.Width - 16);
        viewport.Width = Math.Max(1, width - Surface.Padding.Left - Surface.Padding.Right);
        viewport.MaxHeight = Math.Max(60, CompactLayout.PanelHeight(Display.Height) - 24);
        panel.Width = viewport.Width;
        panel.Measure(new Size(Math.Max(1, width - 24), double.PositiveInfinity));
        PlaceNear(width, Math.Min(panel.DesiredSize.Height, viewport.MaxHeight) + 24, anchor(), false);
    }
}
