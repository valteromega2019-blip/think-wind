using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation.Peers;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Small vector-only adaptation of the supplied liquid spring. No looping demo,
// bitmap blur/threshold or idle rendering; ToggleButton keeps keyboard/UIA behavior.
internal class LiquidToggle : ToggleButton
{
    internal static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(LiquidToggle), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    internal static readonly DependencyProperty FocusIndicatorProperty = DependencyProperty.Register(nameof(FocusIndicator), typeof(bool), typeof(LiquidToggle), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    internal string? Glyph { get => (string?)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    internal bool FocusIndicator { get => (bool)GetValue(FocusIndicatorProperty); set => SetValue(FocusIndicatorProperty, value); }
    private SolidColorBrush? shade;
    private Color shadeSource;
    private double shadeOpacity;
    private readonly FrameClock frames = new();
    private readonly Stopwatch time = new();
    private readonly double[] position = new double[3], velocity = new double[3];
    private double previous, changedAt, oldTarget;
    // FrameworkElementFactory instantiates this through Activator.CreateInstance;
    // its parameterless constructor must be public even for an internal type.
    public LiquidToggle()
    {
        Width = 52; Height = 26; Cursor = Cursors.Hand; Padding = new Thickness(0);
        Template = new ControlTemplate(typeof(ToggleButton));
        SetResourceReference(BackgroundProperty, "Think.Control");
        SetResourceReference(ForegroundProperty, "Think.Text");
        SetResourceReference(BorderBrushProperty, "Think.Accent");
        frames.Tick += (_, _) => Step();
        Loaded += (_, _) => Settle();
        Unloaded += (_, _) => { frames.Stop(); time.Stop(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { frames.Stop(); time.Stop(); } else Settle(); };
    }
    // Embedded indicators are decorative; the outer CheckBox supplies the
    // accessible name and TogglePattern. Standalone controls retain their peer.
    protected override AutomationPeer? OnCreateAutomationPeer() => IsHitTestVisible ? base.OnCreateAutomationPeer() : null;
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == IsKeyboardFocusedProperty) InvalidateVisual();
        if (e.Property != IsCheckedProperty || frames is null) return;
        if (!IsLoaded || !IsVisible || !SystemParameters.ClientAreaAnimation || ((App)Application.Current).AnimationDuration <= 0) { Settle(); return; }
        oldTarget = e.OldValue is true ? 1 : 0;
        if (!time.IsRunning) { time.Restart(); previous = 0; }
        changedAt = time.Elapsed.TotalSeconds; frames.Start(); InvalidateVisual();
    }
    private void Settle()
    {
        frames.Stop(); time.Reset(); previous = changedAt = 0;
        for (int i = 0; i < 3; i++) { position[i] = IsChecked == true ? 1 : 0; velocity[i] = 0; }
        InvalidateVisual();
    }
    private void Step()
    {
        if (!IsVisible || !SystemParameters.ClientAreaAnimation || ((App)Application.Current).AnimationDuration <= 0) { Settle(); return; }
        double now = time.Elapsed.TotalSeconds, dt = Math.Min(.05, Math.Max(0, now - previous)); previous = now;
        double speed = Math.Clamp(220 / Math.Max(1, ((App)Application.Current).AnimationDuration), .5, 3);
        dt *= speed;
        double age = (now - changedAt) * speed;
        for (int i = 0; i < 3; i++)
        {
            double target = age < (i == 1 ? .045 : i == 2 ? .095 : 0) ? oldTarget : IsChecked == true ? 1 : 0;
            // Semi-implicit integration with bounded substeps, independent of Hz.
            for (double left = dt; left > 0;)
            {
                double h = Math.Min(left, 1d / 240); left -= h;
                velocity[i] += (256 * (target - position[i]) - 16 * velocity[i]) * h;
                position[i] += velocity[i] * h;
            }
        }
        InvalidateVisual();
        if (age > .75 && Math.Abs(position[0] - (IsChecked == true ? 1 : 0)) < .002 && Math.Abs(velocity[0]) < .02) Settle();
        else if (age > 1.5) Settle();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        double w = ActualWidth, h = ActualHeight; if (w <= 0 || h <= 0) return;
        double amount = Math.Clamp(position[0], 0, 1), radius = h / 2;
        drawing.DrawRoundedRectangle(Background, null, new Rect(0, 0, w, h), h / 2, h / 2);
        drawing.PushOpacity(amount);
        drawing.DrawRoundedRectangle(BorderBrush, null, new Rect(0, 0, w, h), h / 2, h / 2); drawing.Pop();
        Brush knob = BorderBrush;
        // Match the volume thumb: the same accent shaded by 28%, without a
        // fixed green palette or overlapping translucent shadow layers.
        if (!SystemParameters.HighContrast && BorderBrush is SolidColorBrush accent)
        {
            if (shade is null || shadeSource != accent.Color || shadeOpacity != accent.Opacity)
            {
                shadeSource = accent.Color; shadeOpacity = accent.Opacity;
                shade = new SolidColorBrush(Color.FromArgb(accent.Color.A, (byte)(accent.Color.R * .72), (byte)(accent.Color.G * .72), (byte)(accent.Color.B * .72))) { Opacity = accent.Opacity }; shade.Freeze();
            }
            knob = shade;
        }
        double knobX = h / 2;
        // Same brush across overlapping droplets avoids seams between the blobs.
        for (int i = 2; i >= 0; i--)
        {
            double stretch = i == 0 ? Math.Min(Math.Abs(velocity[0]) * .055, .55) : 0;
            double over = Math.Max(0, Math.Max(position[i] - 1, -position[i]));
            double sx = (1 + stretch) * Math.Max(.75, 1 - 1.6 * over);
            double sy = 1 / Math.Sqrt(1 + stretch) * (1 + 1.1 * over);
            double r = radius * (i == 1 ? .6 : i == 2 ? .4 : 1);
            double x = h / 2 + (w - h) * Math.Clamp(position[i], 0, 1);
            x = Math.Clamp(x, r * sx, w - r * sx);
            drawing.DrawEllipse(knob, null, new Point(x, h / 2), r * sx, Math.Min(h / 2, r * sy));
            if (i == 0) knobX = x;
        }
        if (!string.IsNullOrEmpty(Glyph))
        {
            var icon = new FormattedText(Glyph, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface("Segoe Fluent Icons, Segoe MDL2 Assets"), h * .52,
                TryFindResource("Think.OnAccent") as Brush ?? Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawing.DrawText(icon, new Point(knobX - icon.Width / 2, (h - icon.Height) / 2));
        }
        if (IsKeyboardFocused || FocusIndicator)
            drawing.DrawRoundedRectangle(null, new Pen(BorderBrush, 1.5), new Rect(.75, .75, w - 1.5, h - 1.5), h / 2, h / 2);
    }
}
