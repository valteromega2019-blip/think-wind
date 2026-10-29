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
internal enum MiniIslandKind { Volume, Network, Calendar }
internal sealed class MiniIslandWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    private readonly Func<OverlayChrome.PixelRect> anchor;
    private readonly StackPanel panel = new();
    private readonly Viewbox viewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly AudioEndpointControl? audio;
    private readonly TextBlock network = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 8, 6, 12) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime nextRefresh;
    internal MiniIslandKind Kind { get; }
    internal event Action? Dismissed;
    internal bool IsInteracting => IsMouseCaptureWithin || audio?.IsInteracting == true;
    internal MiniIslandWindow(SettingsStore store, MiniIslandKind kind, Func<OverlayChrome.PixelRect> getAnchor) : base(store)
    {
        Kind = kind; anchor = getAnchor;
        Title = kind switch { MiniIslandKind.Volume => "Think Wind — Mini volume", MiniIslandKind.Calendar => "Think Wind — Calendário", _ => "Think Wind — Mini rede" };
        ShowActivated = true;
        var heading = new DockPanel();
        var close = ActionButton("Fechar", Dismiss); close.Padding = new Thickness(8, 4, 8, 4);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = kind switch { MiniIslandKind.Volume => "Volume", MiniIslandKind.Calendar => "Calendário", _ => "Conexão de rede" }, FontSize = 16, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6) });
        panel.Children.Add(heading);
        if (kind == MiniIslandKind.Volume)
        {
            audio = new AudioEndpointControl(new AudioService(), CoreAudio.Flow.Render); panel.Children.Add(audio);
            panel.Children.Add(ActionButton("Configurações de som", () => SystemActions.Run("ms-settings:sound")));
        }
        else if (kind == MiniIslandKind.Calendar)
        {
            var calendar = new System.Windows.Controls.Calendar(); calendar.SetResourceReference(StyleProperty, "Think.Calendar");
            calendar.DisplayModeChanged += (_, _) => Position(); panel.Children.Add(calendar);
        }
        else
        {
            panel.Children.Add(network);
            var shortcuts = new WrapPanel();
            shortcuts.Children.Add(ActionButton("Wi-Fi", () => SystemActions.Run("ms-settings:network-wifi")));
            shortcuts.Children.Add(ActionButton("Rede e internet", () => SystemActions.Run("ms-settings:network")));
            panel.Children.Add(shortcuts);
        }
        viewport.Child = panel; Surface.Child = viewport;
        Deactivated += (_, _) => Dismiss();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; } };
        Loaded += (_, _) => { RefreshContent(true); timer.Start(); };
        timer.Tick += (_, _) => { if (IsVisible) RefreshContent(false); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { timer.Stop(); Dismissed?.Invoke(); } };
        Closed += (_, _) => timer.Stop();
    }
    internal void Present()
    {
        nextRefresh = DateTime.MinValue;
        Show(); RefreshSettings(); RefreshContent(true); timer.Start(); Activate();
        if (SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0)
            Surface.BeginAnimation(OpacityProperty, new DoubleAnimation(.6, 1, TimeSpan.FromMilliseconds(120)));
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
        if (audio is not null) audio.Poll(refresh);
        else if (refresh && Kind == MiniIslandKind.Network)
        {
            try
            {
                var state = NetworkService.Read();
                network.Text = state.Connected
                    ? $"{state.Kind} · {state.Name}\n{(state.Address is null ? "Conexão ativa" : "IP local · " + state.Address)}" + (state.Signal is null ? "" : $"\nSinal · {state.Signal}%")
                    : "Nenhuma conexão de rede ativa.";
            }
            catch (Exception e) when (e is NetworkInformationException or System.ComponentModel.Win32Exception)
            { network.Text = "Não foi possível consultar a conexão. Abra as configurações de rede para conferir."; }
        }
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
