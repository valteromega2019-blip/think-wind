using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Views;

// Vector layers allow accent-colored weather details without recoloring clouds.
internal sealed class WeatherIcon : Control
{
    internal static readonly DependencyProperty CodeProperty = DependencyProperty.Register(nameof(Code), typeof(int?), typeof(WeatherIcon), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    internal int? Code { get => (int?)GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    internal WeatherIcon() { Focusable = false; IsHitTestVisible = false; SetResourceReference(ForegroundProperty, "Think.Accent"); }
    protected override Size MeasureOverride(Size constraint) => new(FontSize, FontSize);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double scale = Math.Min(ActualWidth, ActualHeight) / 24;
        dc.PushTransform(new ScaleTransform(scale, scale));
        Brush accent = SystemParameters.HighContrast ? SystemColors.HighlightBrush : Foreground;
        Brush cloud = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Brushes.White;
        var outline = new Pen(SystemParameters.HighContrast ? cloud : new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), .5);
        if (Code is 0 or 1 or 2)
        {
            var center = new Point(Code == 0 ? 12 : 8, Code == 0 ? 12 : 8);
            dc.DrawEllipse(accent, null, center, 4, 4);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                dc.DrawLine(new Pen(accent, 1.5), new Point(center.X + Math.Cos(a)*6, center.Y + Math.Sin(a)*6), new Point(center.X + Math.Cos(a)*8, center.Y + Math.Sin(a)*8));
            }
        }
        if (Code != 0)
        {
            var shape = Geometry.Parse("M 5,18 C 0,18 0,11 5,11 C 6,5 14,5 16,10 C 23,8 26,18 19,18 Z");
            dc.DrawGeometry(cloud, outline, shape);
            if (Code is 45 or 48)
                for (int i = 0; i < 2; i++) dc.DrawLine(new Pen(accent, 1.4), new Point(4, 20+i*3), new Point(20, 20+i*3));
            else if (Code >= 95) dc.DrawGeometry(accent, null, Geometry.Parse("M 13,15 L 9,21 L 13,21 L 11,24 L 18,18 L 14,18 L 16,15 Z"));
            else if (Code is >= 71 and <= 77 or 85 or 86)
                for (int i = 0; i < 3; i++) dc.DrawEllipse(accent, null, new Point(6+i*6, 21), 1.2, 1.2);
            else if (Code is >= 51 and < 95)
                for (int i = 0; i < 3; i++) dc.DrawLine(new Pen(accent, 1.5), new Point(7+i*6, 19), new Point(5+i*6, 23));
        }
        dc.Pop();
    }
}
