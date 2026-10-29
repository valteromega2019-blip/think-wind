using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ThinkWind.Views;
internal sealed class IslandMotion
{
    private int revision;
    private readonly Dictionary<FrameworkElement, Point> destinations = new();
    internal Dictionary<FrameworkElement, Point> Capture(IEnumerable<FrameworkElement> elements)
    {
        var positions = new Dictionary<FrameworkElement, Point>();
        foreach (var element in elements)
            if (element.IsVisible && element.ActualWidth > 0 && Window.GetWindow(element) is Window window)
                positions[element] = element.TranslatePoint(new Point(), window);
        return positions;
    }
    internal void Arrange(Window window, Dictionary<FrameworkElement, Point> before, IEnumerable<FrameworkElement> elements, double milliseconds)
    {
        int current = ++revision;
        if (!SystemParameters.ClientAreaAnimation || milliseconds <= 0) { Reset(elements); return; }
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (current != revision || !window.IsVisible) return;
            window.UpdateLayout();
            var live = new HashSet<FrameworkElement>(elements);
            foreach (var old in new List<FrameworkElement>(destinations.Keys)) if (!live.Contains(old)) destinations.Remove(old);
            foreach (var element in live)
            {
                if (!element.IsVisible || PresentationSource.FromVisual(element) is null) continue;
                var translate = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
                element.RenderTransform = translate;
                var visual = element.TranslatePoint(new Point(), window);
                double scaleX = Math.Max(.01, element.TranslatePoint(new Point(1, 0), window).X - visual.X);
                double scaleY = Math.Max(.01, element.TranslatePoint(new Point(0, 1), window).Y - visual.Y);
                var now = new Point(visual.X - translate.X * scaleX, visual.Y - translate.Y * scaleY);
                if (destinations.TryGetValue(element, out var destination) && (destination - now).Length < .5) continue;
                destinations[element] = now;
                var old = before.TryGetValue(element, out var position) ? position : new Point(now.X, now.Y + 8 * scaleY);
                double dx = Math.Clamp((old.X - now.X) / scaleX, -120, 120), dy = Math.Clamp((old.Y - now.Y) / scaleY, -120, 120);
                translate.BeginAnimation(TranslateTransform.XProperty, null); translate.BeginAnimation(TranslateTransform.YProperty, null);
                translate.X = translate.Y = 0;
                if (Math.Abs(dx) < .5 && Math.Abs(dy) < .5) continue;
                var duration = TimeSpan.FromMilliseconds(Math.Min(500, milliseconds));
                var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
                translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(dx, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(dy, 0, duration) { EasingFunction = easing, FillBehavior = FillBehavior.Stop });
            }
        }));
    }
    internal void Cancel(IEnumerable<FrameworkElement> elements) { revision++; Reset(elements); Reset(destinations.Keys); destinations.Clear(); }
    private static void Reset(IEnumerable<FrameworkElement> elements)
    {
        foreach (var element in elements) if (element.RenderTransform is TranslateTransform transform)
        { transform.BeginAnimation(TranslateTransform.XProperty, null); transform.BeginAnimation(TranslateTransform.YProperty, null); transform.X = transform.Y = 0; }
    }
}
