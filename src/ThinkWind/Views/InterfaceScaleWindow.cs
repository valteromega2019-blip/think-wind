using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class InterfaceScaleWindow : FloatingWindow
{
    protected override bool UsesIslandAppearance => true;
    protected override bool HideOnClose => false;
    private readonly double original;
    private bool applied;
    internal InterfaceScaleWindow(SettingsStore store) : base(store)
    {
        original = store.Value.InterfaceScale;
        Title = "Orby — Ajustar escala"; ShowActivated = true;
        Surface.Padding = new Thickness(22);
        var panel = new StackPanel(); Surface.Child = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        panel.Children.Add(new TextBlock { Text = "A escala do Windows mudou", FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text = "Ajuste o tamanho do Orby para esta tela. A prévia muda na hora; suas outras personalizações serão mantidas.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 16) });
        var value = new TextBlock { Text = original.ToString("P0"), FontSize = 18 };
        panel.Children.Add(value);
        var slider = new Slider { Minimum = .6, Maximum = 1.4, Value = original, TickFrequency = .05, IsSnapToTickEnabled = true, Margin = new Thickness(0, 10, 0, 18) };
        slider.ValueChanged += (_, _) => { value.Text = slider.Value.ToString("P0"); store.Update(p => p.InterfaceScale = slider.Value); };
        System.Windows.Automation.AutomationProperties.SetName(slider, "Escala geral do Orby");
        panel.Children.Add(slider);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(ActionButton("Manter atual", Close));
        buttons.Children.Add(ActionButton("Aplicar", () => { applied = true; Close(); }));
        panel.Children.Add(buttons);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Closed += (_, _) => { if (!applied && store.Value.InterfaceScale != original) store.Update(p => p.InterfaceScale = original); };
    }
    public override void Position()
    {
        var display = Display;
        int width = (int)Math.Round(Math.Min(440, display.Width - 32) * display.Scale);
        int height = (int)Math.Round(Math.Min(300, display.Height - 32) * display.Scale);
        int left = display.Work.Left + (display.Work.Width - width) / 2;
        int top = display.Work.Top + (display.Work.Height - height) / 2;
        OverlayChrome.Move(new WindowInteropHelper(this).Handle, new OverlayChrome.PixelRect { Left = left, Top = top, Right = left + width, Bottom = top + height });
        UpdateClip();
    }
}
