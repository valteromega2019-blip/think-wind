using System;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class UpdateNotice : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool HideOnClose => false;
    internal UpdateNotice(SettingsStore store, string tag, Action open) : base(store)
    {
        Width = 300; SizeToContent = SizeToContent.Height; ShowActivated = false; Topmost = true;
        var panel = new StackPanel { Margin = new Thickness(14) };
        panel.Children.Add(new TextBlock { Text = "Orby Launcher · atualização", FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = tag + " disponível. Nenhum download iniciado.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) });
        var button = new Button { Content = "Ver novidades e atualizar" }; button.Click += (_, _) => { Close(); open(); }; panel.Children.Add(button);
        var close = new Button { Content = "Agora não", Margin = new Thickness(0, 5, 0, 0) }; close.Click += (_, _) => Close(); panel.Children.Add(close);
        Surface.Child = panel; Loaded += (_, _) => Position();
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        timer.Tick += (_, _) => { timer.Stop(); Close(); }; Closed += (_, _) => timer.Stop(); timer.Start();
    }
    public override void Position()
    {
        Left = (Display.Bounds.Left + Display.Bounds.Width / 2d) / Display.Scale - Width / 2;
        Top = Display.Bounds.Top / Display.Scale + 44;
    }
}
