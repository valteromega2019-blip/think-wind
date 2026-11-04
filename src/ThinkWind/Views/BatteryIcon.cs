using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Vector silhouette and charge fill remain sharp at every DPI/scale.
internal sealed class BatteryIcon : Control
{
    private static readonly DependencyProperty ChargeProperty = DependencyProperty.Register(nameof(Charge), typeof(int), typeof(BatteryIcon), new FrameworkPropertyMetadata(-1, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty ChargingProperty = DependencyProperty.Register(nameof(Charging), typeof(bool), typeof(BatteryIcon), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty PluggedProperty = DependencyProperty.Register(nameof(Plugged), typeof(bool), typeof(BatteryIcon), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty OutlineProperty = DependencyProperty.Register(nameof(Outline), typeof(Brush), typeof(BatteryIcon), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly Geometry Bolt = MakeBolt();
    internal int Charge { get => (int)GetValue(ChargeProperty); set => SetValue(ChargeProperty, value); }
    internal bool Charging { get => (bool)GetValue(ChargingProperty); set => SetValue(ChargingProperty, value); }
    internal bool Plugged { get => (bool)GetValue(PluggedProperty); set => SetValue(PluggedProperty, value); }
    private Brush Outline => (Brush)GetValue(OutlineProperty);
    private static Geometry MakeBolt() { var shape = Geometry.Parse("M 13,5 L 8,12 L 11,12 L 10,19 L 16,10 L 13,10 Z"); shape.Freeze(); return shape; }
    internal BatteryIcon()
    {
        Focusable = false; IsHitTestVisible = false;
        SetResourceReference(ForegroundProperty, "Think.Accent");
        SetResourceReference(OutlineProperty, "Think.Text");
    }
    protected override Size MeasureOverride(Size available) => new(FontSize * 1.25, FontSize);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double scale = Math.Min(ActualWidth / 26, ActualHeight / 24);
        if (scale <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - 26 * scale) / 2, (ActualHeight - 24 * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        Brush outline = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Outline;
        Brush fill = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Foreground;
        var edge = new Pen(outline, 1.5);
        dc.DrawRoundedRectangle(null, edge, new Rect(2, 6, 19, 12), 2.5, 2.5);
        dc.DrawRoundedRectangle(outline, null, new Rect(22, 10, 2, 4), .8, .8);
        if (Charge >= 0)
        {
            double width = 15 * Math.Clamp(Charge, 0, 100) / 100;
            if (width > 0) dc.DrawRoundedRectangle(fill, null, new Rect(4, 8, width, 8), Math.Min(1, width / 2), 1);
        }
        else for (int i = 0; i < 3; i++) dc.DrawEllipse(outline, null, new Point(8 + i * 3.5, 12), .75, .75);
        if (Charging) dc.DrawGeometry(outline, new Pen(SystemParameters.HighContrast ? SystemColors.WindowBrush : Brushes.Black, .7), Bolt);
        else if (Plugged) dc.DrawLine(new Pen(outline, 1.5), new Point(8, 21), new Point(15, 21));
        dc.Pop(); dc.Pop();
    }
}
