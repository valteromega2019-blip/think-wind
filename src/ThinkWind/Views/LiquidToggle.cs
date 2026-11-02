using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Small vector-only adaptation of the supplied liquid spring. No looping demo,
// bitmap blur/threshold or idle rendering; ToggleButton keeps keyboard/UIA behavior.
internal sealed class LiquidToggle : ToggleButton
{
    private readonly FrameClock frames = new();
    private readonly Stopwatch time = new();
    private readonly double[] position = new double[3], velocity = new double[3];
    private double previous, changedAt, oldTarget;
    internal LiquidToggle()
    {
        Width = 60; Height = 30; Cursor = Cursors.Hand; Padding = new Thickness(0);
        Template = new ControlTemplate(typeof(ToggleButton));
        SetResourceReference(BackgroundProperty, "Think.Control");
        SetResourceReference(ForegroundProperty, "Think.Text");
        SetResourceReference(BorderBrushProperty, "Think.Accent");
        frames.Tick += (_, _) => Step();
        Loaded += (_, _) => Settle();
        Unloaded += (_, _) => { frames.Stop(); time.Stop(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { frames.Stop(); time.Stop(); } else Settle(); };
    }
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
        double amount = Math.Clamp(position[0], 0, 1), radius = h / 2 - 4;
        drawing.DrawRoundedRectangle(Background, null, new Rect(0, 0, w, h), h / 2, h / 2);
        drawing.PushOpacity(amount);
        drawing.DrawRoundedRectangle(TryFindResource("Think.AccentSurface") as Brush ?? BorderBrush, null, new Rect(0, 0, w, h), h / 2, h / 2); drawing.Pop();
        drawing.PushOpacity(IsKeyboardFocused ? 1 : .2);
        drawing.DrawRoundedRectangle(null, new Pen(BorderBrush, 1), new Rect(.5, .5, w - 1, h - 1), h / 2, h / 2); drawing.Pop();
        Brush knob = Foreground;
        // Same brush across overlapping droplets avoids seams between the blobs.
        for (int i = 2; i >= 0; i--)
        {
            double stretch = i == 0 ? Math.Min(Math.Abs(velocity[0]) * .055, .55) : 0;
            double over = Math.Max(0, Math.Max(position[i] - 1, -position[i]));
            double sx = (1 + stretch) * Math.Max(.75, 1 - 1.6 * over);
            double sy = 1 / Math.Sqrt(1 + stretch) * (1 + 1.1 * over);
            double r = radius * (i == 1 ? .6 : i == 2 ? .4 : 1);
            double x = h / 2 + (w - h) * Math.Clamp(position[i], 0, 1);
            x = Math.Clamp(x, 4 + r * sx, w - 4 - r * sx);
            drawing.DrawEllipse(knob, null, new Point(x, h / 2), r * sx, Math.Min(h / 2 - 2, r * sy));
        }
    }
}
