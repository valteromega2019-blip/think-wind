using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class IslandWindow : FloatingWindow
{
    private readonly TextBlock clock = new() { FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock metrics = new() { FontSize = 12, Margin = new Thickness(8) };
    private readonly StackPanel details = new() { Visibility = Visibility.Collapsed };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Calendar calendar = new() { Margin = new Thickness(8), Visibility = Visibility.Collapsed };
    private readonly StackPanel audioPanel = new();
    private readonly AudioEndpointControl output;
    private readonly AudioEndpointControl microphone;
    private DateTime nextDeviceRefresh = DateTime.MinValue;
    private bool expanded;
    public IslandWindow(SettingsStore store, Action openSettings) : base(store)
    {
        Title = "Think Wind — Dynamic Island";
        var audio = new AudioService();
        output = new AudioEndpointControl(audio, CoreAudio.Flow.Render);
        microphone = new AudioEndpointControl(audio, CoreAudio.Flow.Capture);
        var root = new DockPanel();
        var header = new DockPanel { LastChildFill = false };
        header.Children.Add(ActionButton("◉", () => Toggle(!expanded), "Expandir ou recolher a ilha"));
        header.Children.Add(clock);
        header.Children.Add(ActionButton("⌄", () => Toggle(!expanded), "Acesso rápido"));
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        details.Children.Add(new TextBlock { Text = "SEU ESPAÇO · THINK WIND", FontSize = 11, Margin = new Thickness(12) });
        var connections = new WrapPanel();
        connections.Children.Add(ActionButton("Wi-Fi", () => SystemActions.Run("ms-settings:network-wifi")));
        connections.Children.Add(ActionButton("Bluetooth", () => SystemActions.Run("ms-settings:bluetooth")));
        connections.Children.Add(ActionButton("Modo avião", () => SystemActions.Run("ms-settings:network-airplanemode")));
        connections.Children.Add(ActionButton("Áudio e saída", () => SystemActions.Run("ms-settings:sound")));
        connections.Children.Add(ActionButton("Microfone", () => SystemActions.Run("ms-settings:privacy-microphone")));
        details.Children.Add(connections);
        var media = new WrapPanel();
        media.Children.Add(ActionButton("⏮", () => Desktop.Key(0xB1), "Mídia anterior"));
        media.Children.Add(ActionButton("⏯", () => Desktop.Key(0xB3), "Reproduzir ou pausar mídia"));
        media.Children.Add(ActionButton("⏭", () => Desktop.Key(0xB0), "Próxima mídia"));
        details.Children.Add(media);
        audioPanel.Children.Add(output);
        audioPanel.Children.Add(microphone);
        audioPanel.Children.Add(new TextBlock { Text = "O seletor escolhe qual dispositivo controlar. Para trocar o padrão, use Áudio e saída.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = .75, Margin = new Thickness(12, 4, 12, 12) });
        details.Children.Add(audioPanel);
        var tools = new WrapPanel();
        tools.Children.Add(ActionButton("Calendário", () => { calendar.Visibility = calendar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; ResizeExpanded(); }));
        tools.Children.Add(ActionButton("Notificações", () => SystemActions.Run("ms-settings:notifications")));
        tools.Children.Add(ActionButton("Bandeja nativa", () => SystemActions.Run("ms-settings:taskbar"), "Configurações da bandeja do Windows; os ícones originais permanecem na barra nativa"));
        tools.Children.Add(ActionButton("Personalizar", openSettings));
        details.Children.Add(tools);
        details.Children.Add(metrics);
        details.Children.Add(calendar);
        root.Children.Add(new ScrollViewer { Content = details, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Surface.Child = root;
        Loaded += (_, _) => { RefreshSettings(); UpdateClock(); timer.Start(); };
        timer.Tick += (_, _) => { UpdateClock(); RefreshAudio(); };
        Closed += (_, _) => timer.Stop();
        Deactivated += (_, _) => Toggle(false);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Toggle(false); };
    }
    private void UpdateClock()
    {
        clock.Text = DateTime.Now.ToString("HH:mm  ·  dd MMM", CultureInfo.GetCultureInfo("pt-BR"));
        if (!expanded || !IsVisible) return;
        var text = Store.Value.ShowMemory ? $"RAM em uso  {Desktop.MemoryPercent()}%" : "";
        if (Store.Value.ShowBattery)
        {
            var battery = System.Windows.Forms.SystemInformation.PowerStatus;
            if (battery.BatteryLifePercent is >= 0 and <= 1 && !battery.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery))
                text += $"     Bateria  {battery.BatteryLifePercent:P0}";
        }
        metrics.Text = text;
    }
    private void Toggle(bool value)
    {
        var previousHeight = ActualHeight;
        expanded = value;
        details.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (!value) calendar.Visibility = Visibility.Collapsed;
        Position();
        Height = previousHeight;
        AnimateHeight(value ? ExpandedHeight() : 64);
        UpdateClock();
        if (value) { nextDeviceRefresh = DateTime.MinValue; RefreshAudio(); }
    }
    private void RefreshAudio()
    {
        if (!expanded || !IsVisible || !Store.Value.ShowAudio) return;
        var refreshDevices = DateTime.UtcNow >= nextDeviceRefresh;
        if (refreshDevices) nextDeviceRefresh = DateTime.UtcNow.AddSeconds(5);
        output.Poll(refreshDevices); microphone.Poll(refreshDevices);
    }
    public override void RefreshSettings()
    {
        audioPanel.Visibility = Store.Value.ShowAudio ? Visibility.Visible : Visibility.Collapsed;
        base.RefreshSettings(); UpdateClock(); RefreshAudio();
    }
    private double ExpandedHeight() => Math.Min(WorkArea().Height - 40, (Store.Value.ShowAudio ? 640 : 360) + (calendar.Visibility == Visibility.Visible ? 210 : 0));
    private void ResizeExpanded() { if (expanded) AnimateHeight(ExpandedHeight()); }
    public override void Position()
    {
        var area = WorkArea();
        Width = Math.Min(expanded ? 520 : 320, area.Width - 24);
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + 12;
        Height = expanded ? ExpandedHeight() : 64;
    }
}
