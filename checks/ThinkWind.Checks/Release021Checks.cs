using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
        var cards = fan.Children.OfType<Button>().Where(button => button.Content is Border).ToArray();
        Require(cards.Length == 4 && fan.Children.Count == 5, "Quatro cartões 2D e um controle de janela devem aparecer.");
        double? left = null;
        foreach (FrameworkElement item in cards)
        {
            Require(left is null || Canvas.GetLeft(item) == left, "Cartões devem usar o mesmo eixo."); left = Canvas.GetLeft(item);
            Require(Canvas.GetTop(item) >= 0 && Canvas.GetTop(item) + item.ActualHeight <= fan.ActualHeight, "O leque deve reservar altura suficiente.");
            Require(item.Height == 40, "Abas sem imagem salva devem permanecer recolhidas.");
            var surface = (Border)((Button)item).Content;
            var color = ((SolidColorBrush)surface.Background).Color;
            Require(color.R == 32 && color.G == 64 && color.B == 96 && color.A >= 178, "A tonalidade Acrylic deve seguir a personalização.");
        }
        Require(!fan.Children.OfType<Viewport3D>().Any(), "Cartões devem usar composição 2D.");
    }
    internal static void BrowserLimitsAndDispose()
    {
        using var fan = new BrowserTabFan();
        foreach (int count in new[] { 1, 4, 6, 8, 20 })
        {
            var tabs = Enumerable.Range(0, count).Select(index => new BrowserTab(0, 0, "Aba " + index, new[] { index }, false)).ToArray();
            fan.Present(tabs, 0, new Preferences());
            fan.Measure(new Size(320, double.PositiveInfinity)); fan.Arrange(new Rect(new Point(), fan.DesiredSize));
            var cards = fan.Children.OfType<Button>().Where(button => button.Content is Border).ToArray();
            Require(cards.Length == Math.Min(6, count), "Máximo de seis cartões por janela.");
            var control = fan.Children.OfType<Button>().Single(button => button.Content is StackPanel);
            var counter = ((StackPanel)control.Content).Children.OfType<TextBlock>().First();
            int hidden = Math.Max(0, count - 6);
            Require(counter.Text == (hidden > 0 ? "+" + hidden : ""), "O contador deve excluir as abas exibidas.");
            Require(counter.Visibility == (hidden > 0 ? Visibility.Visible : Visibility.Collapsed), "Zero abas extras não deve exibir contador.");
            Require(cards.All(card => Canvas.GetTop(card) + card.ActualHeight <= fan.ActualHeight), "Cartões devem caber no leque.");
            fan.Dispose();
            Require(fan.Children.Count == 0, "Fechar deve liberar os controles das imagens e menus.");
        }
    }
    internal static void BrowserHoverOverlap()
    {
        using var fan = new BrowserTabFan();
        var tabs = Enumerable.Range(0, 4).Select(index => new BrowserTab(0, 0, "Sobreposição " + index, new[] { index }, false)).ToArray();
        fan.Present(tabs, 0, new Preferences());
        var owner = new Window { Content = fan, Width = 340, Height = 420, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            owner.Show(); owner.UpdateLayout();
            var cards = fan.Children.OfType<Button>().Where(button => button.Content is Border).ToArray();
            foreach (var card in cards) { Canvas.SetTop(card, 48); Panel.SetZIndex(card, 0); }
            Panel.SetZIndex(cards[2], 100);
            fan.Measure(new Size(320, 400)); fan.Arrange(new Rect(0, 0, 320, 400)); fan.UpdateLayout();
            int front = fan.CardAt(new Point(30, 60));
            Require(front == 2, "Hover deve identificar o cartão da frente, não o primeiro retângulo da lista. Alvo=" + front);
            Panel.SetZIndex(cards[1], 200);
            Require(fan.CardAt(new Point(30, 60)) == 1, "Trocar a ordem visual deve trocar o alvo real.");
            Require(fan.CardAt(new Point(315, 60)) == -1, "O espaço fora dos cartões não pode destacar outra janela.");
        }
        finally { owner.Content = null; owner.Close(); }
    }
    internal static void DesktopPeekLifecycle()
    {
        var source = new Window { Title = "Regressão da prévia temporária", Width = 280, Height = 160, ShowActivated = false, ShowInTaskbar = false };
        using var peek = new OrbyLauncher.Native.DesktopWindowPeek();
        try
        {
            source.Show(); source.UpdateLayout();
            var handle = new System.Windows.Interop.WindowInteropHelper(source).Handle;
            int before = OrbyLauncher.Native.WindowThumbnail.ActiveCount;
            var foreground = OrbyLauncher.Native.Desktop.GetForegroundWindow();
            OrbyLauncher.Native.OverlayChrome.GetWindowRect(handle, out var bounds);
            peek.Show(handle, 0x00AA88);
            Require(OrbyLauncher.Native.WindowThumbnail.ActiveCount == before + 1, "A prévia deve criar somente um recurso DWM.");
            Require(OrbyLauncher.Native.Desktop.GetForegroundWindow() == foreground, "A prévia não deve roubar foco.");
            OrbyLauncher.Native.OverlayChrome.GetWindowRect(handle, out var after);
            Require(bounds.Equals(after), "A prévia não deve mover nem redimensionar a janela real.");
            peek.Dispose(); Require(OrbyLauncher.Native.WindowThumbnail.ActiveCount == before, "Fechar deve liberar a miniatura DWM.");
            source.WindowState = WindowState.Minimized;
            peek.Show(handle, 0x00AA88);
            Require(!OrbyLauncher.Native.Desktop.IsVisibleAndRestored(handle), "Hover não deve restaurar uma janela minimizada.");
            peek.Dispose(); Require(OrbyLauncher.Native.WindowThumbnail.ActiveCount == before, "A segunda prévia também deve liberar o recurso.");
        }
        finally { peek.Dispose(); source.Close(); }
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
