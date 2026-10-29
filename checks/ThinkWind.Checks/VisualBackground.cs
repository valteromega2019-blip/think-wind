using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ThinkWind.Native;
using ThinkWind.Services;

internal static class VisualBackground
{
    internal static int Run()
    {
        var app = new Application();
        var store = new SettingsStore();
        var display = OverlayChrome.GetDisplay(store.Value.MonitorIndex, store.Value.MonitorDeviceName);
        var window = new Window { Title = "Think Wind · Fundo de verificação", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = true, Background = Brushes.Black };
        var columns = new Grid();
        foreach (var hex in new[] { "#D52D36", "#1266D0", "#E5EAF0", "#16191F", "#D52D36", "#1266D0" })
        {
            int column = columns.ColumnDefinitions.Count;
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            var stripe = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)) };
            Grid.SetColumn(stripe, column); columns.Children.Add(stripe);
        }
        var label = new TextBlock { Text = "FUNDO DE VERIFICAÇÃO DO ACRYLIC · Feche esta janela ao terminar", Foreground = Brushes.White, Background = Brushes.Black, Padding = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 18 };
        var root = new Grid(); root.Children.Add(columns); root.Children.Add(label); window.Content = root;
        window.SourceInitialized += (_, _) => OverlayChrome.Move(new WindowInteropHelper(window).Handle, new OverlayChrome.PixelRect { Left = display.Bounds.Left, Top = display.Bounds.Top, Right = display.Bounds.Right, Bottom = display.Bounds.Bottom });
        return app.Run(window);
    }
}
