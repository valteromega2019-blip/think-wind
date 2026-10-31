using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace OrbyLauncher.Views;

internal static class ColorPickerPopup
{
    internal static void Open(FrameworkElement anchor, string initial, Action<string> changed)
    {
        var wheel = new ColorWheel((Color)ColorConverter.ConvertFromString(initial));
        var popup = new Popup { PlacementTarget = anchor, Placement = PlacementMode.Bottom,
            AllowsTransparency = true, StaysOpen = false, VerticalOffset = 8 };
        var body = new StackPanel { Width = 244 };
        var title = new TextBlock { Text = "Selecionar cor", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); body.Children.Add(title);
        wheel.HorizontalAlignment = HorizontalAlignment.Center; body.Children.Add(wheel);
        var label = new TextBlock { Text = "Luminosidade", Margin = new Thickness(0, 10, 0, 4) };
        label.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); body.Children.Add(label);
        var brightness = new Slider { Minimum = 0, Maximum = 1, Value = wheel.Brightness, SmallChange = .01, LargeChange = .1 };
        System.Windows.Automation.AutomationProperties.SetName(brightness, "Luminosidade da cor");
        body.Children.Add(brightness);
        var row = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var preview = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(wheel.Color) };
        DockPanel.SetDock(preview, Dock.Left); row.Children.Add(preview);
        var hex = new TextBox { Text = initial, MaxLength = 7, MinHeight = 32, VerticalContentAlignment = VerticalAlignment.Center };
        hex.SetResourceReference(Control.BackgroundProperty, "Think.Control"); hex.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        System.Windows.Automation.AutomationProperties.SetName(hex, "Cor hexadecimal");
        row.Children.Add(hex); body.Children.Add(row);
        bool syncing = false;
        wheel.Changed += () =>
        {
            if (syncing) return;
            string value = $"#{wheel.Color.R:X2}{wheel.Color.G:X2}{wheel.Color.B:X2}";
            syncing = true; hex.Text = value; preview.Background = new SolidColorBrush(wheel.Color); syncing = false;
            changed(value);
        };
        brightness.ValueChanged += (_, _) => { if (!syncing) wheel.Brightness = brightness.Value; };
        hex.TextChanged += (_, _) =>
        {
            if (syncing || !OrbyLauncher.Services.OverlayAppearanceOptions.IsValidColor(hex.Text)) return;
            syncing = true; wheel.Select((Color)ColorConverter.ConvertFromString(hex.Text)); brightness.Value = wheel.Brightness;
            preview.Background = new SolidColorBrush(wheel.Color); syncing = false; changed(hex.Text.ToUpperInvariant());
        };
        var frame = new Border { Child = body, Padding = new Thickness(16), CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1) };
        frame.SetResourceReference(Border.BackgroundProperty, "Think.Popup"); frame.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
        popup.Child = frame;
        frame.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { popup.IsOpen = false; e.Handled = true; anchor.Focus(); } };
        Window? owner = Window.GetWindow(anchor);
        EventHandler close = (_, _) => popup.IsOpen = false;
        if (owner is not null) { owner.LocationChanged += close; owner.Deactivated += close; owner.Closed += close; }
        popup.Closed += (_, _) => { if (owner is not null) { owner.LocationChanged -= close; owner.Deactivated -= close; owner.Closed -= close; } };
        popup.IsOpen = true;
        double duration = (owner as FloatingWindow)?.AnimationMilliseconds ?? 160;
        if (SystemParameters.ClientAreaAnimation && duration > 0)
            frame.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.9, 1, TimeSpan.FromMilliseconds(Math.Min(180, duration)))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
        wheel.Focus();
    }
}

internal sealed class ColorWheel : FrameworkElement
{
    private static readonly BitmapSource palette = CreatePalette();
    private double hue, saturation, brightness;
    internal event Action? Changed;
    internal Color Color => Hsv(hue, saturation, brightness);
    internal double Brightness { get => brightness; set { brightness = Math.Clamp(value, 0, 1); InvalidateVisual(); Changed?.Invoke(); } }
    internal ColorWheel(Color color)
    {
        Width = Height = 176; Focusable = true; Cursor = Cursors.Cross;
        System.Windows.Automation.AutomationProperties.SetName(this, "Círculo cromático. Setas esquerda e direita: matiz; cima e baixo: saturação.");
        Select(color);
    }
    internal void Select(Color color)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), delta = max - min;
        if (delta > 0) hue = ((max == r ? (g - b) / delta : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4) * 60 + 360) % 360;
        saturation = max == 0 ? 0 : delta / max; brightness = max; InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var center = new Point(88, 88);
        dc.PushClip(new EllipseGeometry(center, 80, 80)); dc.DrawImage(palette, new Rect(8, 8, 160, 160));
        dc.PushOpacity(1 - brightness); dc.DrawEllipse(Brushes.Black, null, center, 80, 80); dc.Pop(); dc.Pop();
        var point = new Point(88 + Math.Cos(hue * Math.PI / 180) * saturation * 80, 88 - Math.Sin(hue * Math.PI / 180) * saturation * 80);
        dc.DrawEllipse(null, new Pen(Brushes.Black, 4), point, 6, 6); dc.DrawEllipse(null, new Pen(Brushes.White, 2), point, 6, 6);
    }
    private void Pick(Point p)
    {
        double x = p.X - 88, y = 88 - p.Y;
        hue = (Math.Atan2(y, x) * 180 / Math.PI + 360) % 360; saturation = Math.Clamp(Math.Sqrt(x * x + y * y) / 80, 0, 1);
        InvalidateVisual(); Changed?.Invoke();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); Focus(); CaptureMouse(); Pick(e.GetPosition(this)); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed) Pick(e.GetPosition(this)); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); if (IsMouseCaptured) { ReleaseMouseCapture(); e.Handled = true; } }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Left or Key.Right) hue = (hue + (e.Key == Key.Right ? 2 : 358)) % 360;
        else if (e.Key is Key.Up or Key.Down) saturation = Math.Clamp(saturation + (e.Key == Key.Up ? .02 : -.02), 0, 1);
        else return;
        e.Handled = true; InvalidateVisual(); Changed?.Invoke();
    }
    private static BitmapSource CreatePalette()
    {
        var bytes = new byte[160 * 160 * 4];
        for (int y = 0; y < 160; y++) for (int x = 0; x < 160; x++)
        {
            double dx = x - 79.5, dy = 79.5 - y;
            var color = Hsv((Math.Atan2(dy, dx) * 180 / Math.PI + 360) % 360, Math.Min(1, Math.Sqrt(dx * dx + dy * dy) / 80), 1);
            int i = (y * 160 + x) * 4; bytes[i] = color.B; bytes[i + 1] = color.G; bytes[i + 2] = color.R; bytes[i + 3] = 255;
        }
        var image = BitmapSource.Create(160, 160, 96, 96, PixelFormats.Bgra32, null, bytes, 160 * 4); image.Freeze(); return image;
    }
    private static Color Hsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch { < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x), < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x) };
        return System.Windows.Media.Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
}
