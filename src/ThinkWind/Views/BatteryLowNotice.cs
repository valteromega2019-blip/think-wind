using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrbyLauncher.Services;
namespace OrbyLauncher.Views;
internal sealed class BatteryLowNotice : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool HideOnClose => false;
    protected override bool UsesIslandAppearance => true;
    internal BatteryLowNotice(SettingsStore store, PeripheralBattery device) : base(store)
    {
        Width = 320; SizeToContent = SizeToContent.Height; ShowActivated = false; Topmost = true;
        Surface.Padding = new Thickness(16);
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "Bateria baixa", FontWeight = FontWeights.SemiBold, FontSize = 17 });
        panel.Children.Add(new TextBlock { Text = device.Name + " · " + device.Display + "\nConecte o carregador.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
        panel.Children.Add(ActionButton("Dispensar", Close)); Surface.Child = panel;
        Loaded += (_, _) => Position();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(12) };
        timer.Tick += (_, _) => Close(); Closed += (_, _) => timer.Stop(); timer.Start();
    }
    public override void Position()
    {
        Left = (Display.Bounds.Left + Display.Bounds.Width / 2d) / Display.Scale - Width / 2;
        Top = Display.Bounds.Top / Display.Scale + 44;
    }
}
