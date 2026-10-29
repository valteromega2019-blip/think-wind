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
    private bool expanded;
    public IslandWindow(SettingsStore store, Action openSettings) : base(store)
    {
        Title = "Think Wind — Dynamic Island";
        var root = new StackPanel();
        var header = new DockPanel { LastChildFill = false };
        header.Children.Add(ActionButton("◉", () => Toggle(!expanded), "Expandir ou recolher a ilha"));
        header.Children.Add(clock);
        header.Children.Add(ActionButton("⌄", () => Toggle(!expanded), "Acesso rápido"));
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
        media.Children.Add(ActionButton("− Volume", () => Desktop.Key(0xAE)));
        media.Children.Add(ActionButton("Mudo", () => Desktop.Key(0xAD)));
        media.Children.Add(ActionButton("+ Volume", () => Desktop.Key(0xAF)));
        media.Children.Add(ActionButton("⏮", () => Desktop.Key(0xB1), "Mídia anterior"));
        media.Children.Add(ActionButton("⏯", () => Desktop.Key(0xB3), "Reproduzir ou pausar mídia"));
        media.Children.Add(ActionButton("⏭", () => Desktop.Key(0xB0), "Próxima mídia"));
        details.Children.Add(media);
        var tools = new WrapPanel();
        tools.Children.Add(ActionButton("Calendário", () => { calendar.Visibility = calendar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; ResizeExpanded(); }));
        tools.Children.Add(ActionButton("Notificações", () => SystemActions.Run("ms-settings:notifications")));
        tools.Children.Add(ActionButton("Bandeja nativa", () => SystemActions.Run("ms-settings:taskbar"), "Configurações da bandeja do Windows; os ícones originais permanecem na barra nativa"));
        tools.Children.Add(ActionButton("Personalizar", openSettings));
        details.Children.Add(tools);
        details.Children.Add(metrics);
        details.Children.Add(calendar);
        root.Children.Add(details);
        Surface.Child = root;
        Loaded += (_, _) => { Position(); UpdateClock(); timer.Start(); };
        timer.Tick += (_, _) => UpdateClock();
        Closed += (_, _) => timer.Stop();
        Deactivated += (_, _) => Toggle(false);
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Toggle(false); };
    }
    private void UpdateClock()
    {
        clock.Text = DateTime.Now.ToString("HH:mm  ·  dd MMM", CultureInfo.GetCultureInfo("pt-BR"));
        if (!expanded) return;
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
        expanded = value;
        details.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        if (!value) calendar.Visibility = Visibility.Collapsed;
        Position();
        AnimateHeight(value ? ExpandedHeight() : 64);
        UpdateClock();
    }
    private double ExpandedHeight() => Math.Min(WorkArea().Height - 40, calendar.Visibility == Visibility.Visible ? 570 : 360);
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
