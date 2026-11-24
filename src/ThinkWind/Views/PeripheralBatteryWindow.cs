using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class PeripheralBatteryWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool HideOnClose => false;
    protected override bool UsesIslandAppearance => true;
    private readonly string deviceName;
    private readonly TextBlock title = new() { FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock state = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly BatteryIcon battery = new() { Width = 66, Height = 48, FontSize = 40, Margin = new Thickness(0, 12, 0, 0) };
    internal PeripheralBatteryWindow(SettingsStore store, PeripheralBattery device) : base(store)
    {
        deviceName = device.Name;
        Title = "Orby — Bateria do dispositivo"; ShowActivated = true;
        Surface.Padding = new Thickness(20);
        var panel = new StackPanel();
        panel.Children.Add(title); panel.Children.Add(battery); panel.Children.Add(state);
        var close = ActionButton("Fechar", Close); close.Margin = new Thickness(0, 14, 0, 0); panel.Children.Add(close);
        Surface.Child = panel;
        Loaded += (_, _) => { PeripheralBatteries.Changed += RefreshDevice; RefreshDevice(); };
        Closed += (_, _) => PeripheralBatteries.Changed -= RefreshDevice;
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }
    private void RefreshDevice()
    {
        var device = Array.Find(PeripheralBatteries.Current, item => item.Name == deviceName);
        title.Text = deviceName;
        battery.Charge = device?.VisualCharge ?? -1; battery.Charging = device?.Charging == true;
        state.Text = device is null ? "Dispositivo desconectado ou leitura indisponível."
            : device.Display + (device.Charging ? " · Carregando ⚡" : "")
                + (device.Estimated ? "\nEstimativa obtida pela tensão da bateria." : "")
                + (device.Stale ? "\nÚltima leitura válida; aguardando nova resposta do dispositivo." : "");
    }
    public override void Position()
    {
        var display = Display;
        int width = (int)Math.Round(Math.Min(400, display.Width - 24) * display.Scale);
        int height = (int)Math.Round(Math.Min(270, display.Height - 24) * display.Scale);
        int left = display.Work.Left + (display.Work.Width - width) / 2, top = display.Work.Top + (display.Work.Height - height) / 2;
        OverlayChrome.Move(new WindowInteropHelper(this).Handle, new OverlayChrome.PixelRect { Left = left, Top = top, Right = left + width, Bottom = top + height }); UpdateClip();
    }
}
