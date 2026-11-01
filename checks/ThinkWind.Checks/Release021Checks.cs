using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

internal static class Release021Checks
{
    internal static void BrowserFan()
    {
        using var fan = new BrowserTabFan();
        // No HWND or real browser: the selected live thumbnail is deliberately absent.
        var tabs = Enumerable.Range(0, 4).Select(index => new BrowserTab(0, 0, "Aba de regressão " + index, new[] { index }, false)).ToArray();
        var preferences = new Preferences { DockOpacity = .7, UseCustomBarColor = true, BarBackgroundColor = "#204060" };
        fan.Present(tabs, 0, preferences); fan.Measure(new Size(320, double.PositiveInfinity)); fan.Arrange(new Rect(new Point(), fan.DesiredSize));
        Require(fan.Children.Count == 4 && fan.Children[3] is Button, "A frente deve permanecer 2D e reta.");
        double? left = null;
        foreach (FrameworkElement item in fan.Children)
        {
            Require(left is null || Canvas.GetLeft(item) == left, "Cartões devem usar o mesmo eixo."); left = Canvas.GetLeft(item);
            Require(Canvas.GetTop(item) >= 0 && Canvas.GetTop(item) + item.ActualHeight <= fan.ActualHeight, "O leque deve reservar altura suficiente.");
        }
        foreach (Viewport3D viewport in fan.Children.OfType<Viewport3D>())
        {
            var host = viewport.Children.OfType<Viewport2DVisual3D>().Single();
            var rotation = (AxisAngleRotation3D)((RotateTransform3D)host.Transform).Rotation;
            Require(rotation.Axis == new Vector3D(1, 0, 0) && rotation.Angle is > 0 and <= 15, "Inclinação deve ser vertical, sem rotação lateral.");
            var surface = (Border)((Button)host.Visual).Content;
            var color = ((SolidColorBrush)surface.Background).Color;
            Require(color.R == 32 && color.G == 64 && color.B == 96 && color.A >= 178, "A tonalidade não deve sofrer o redutor extra de transparência.");
        }
    }
    internal static void CompactCall()
    {
        using var card = new CallPreviewCard();
        card.ConfigureCompact(true); card.Width = 300;
        card.Present(new CallSnapshot(0, 0, "Google Meet", "Chamada de regressão", new[]
        {
            new CallCommand(CallAction.Microphone, "Ativar microfone", true),
            new CallCommand(CallAction.Leave, "Sair da chamada")
        }), Array.Empty<OrbyLauncher.Native.Desktop.RunningWindow>());
        var box = new Viewbox { Width = 320, Height = 160, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = card };
        box.Measure(new Size(320, 160)); box.Arrange(new Rect(0, 0, 320, 160));
        // DownOnly intentionally avoids enlarging content smaller than its slot.
        Require(box.RenderSize.Width is > 0 and <= 320 && box.RenderSize.Height is > 0 and <= 160 && card.DesiredSize.Height > 0,
            $"Comandos devem caber no espaço da miniatura. Viewbox={box.RenderSize}, cartão={card.DesiredSize}.");
        var controls = card.Children.OfType<WrapPanel>().First();
        var mic = controls.Children.OfType<Button>().First();
        Require(mic.Foreground is SolidColorBrush ink && ink.Color.R > 220 && ink.Color.G < 130, "Mudo deve estar vermelho.");
        Require(((StackPanel)mic.Content).Children.OfType<Grid>().Single().Children.OfType<System.Windows.Shapes.Line>().Any(), "Mudo deve usar ícone riscado.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
