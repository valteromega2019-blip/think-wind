using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

// Magnify only the glyph inside its existing button. The button, backdrop and
// window never resize, preserving click areas and the thin top status strip.
internal sealed class HeaderIconMagnifier : IDisposable
{
    private sealed class Item
    {
        internal readonly Button Button;
        internal readonly TextBlock Glyph;
        internal readonly Transform Original;
        internal readonly Point Origin;
        internal readonly TransformGroup Transform = new();
        internal readonly ScaleTransform Scale = new(1, 1);
        internal double Growth, Velocity;
        private Brush? highlight;
        private Brush? highlightFill;
        internal Item(Button button, TextBlock glyph)
        {
            Button = button; Glyph = glyph; Original = glyph.RenderTransform; Origin = glyph.RenderTransformOrigin;
            Transform.Children.Add(Original); Transform.Children.Add(Scale);
            glyph.RenderTransformOrigin = new Point(.5, .5); glyph.RenderTransform = Transform;
        }
        internal void Restore()
        {
            Highlight(null);
            if (!ReferenceEquals(Glyph.RenderTransform, Transform)) return;
            Glyph.RenderTransform = Original; Glyph.RenderTransformOrigin = Origin;
        }
        internal void Highlight(Brush? brush, Brush? fill = null)
        {
            if (ReferenceEquals(highlight, brush) && ReferenceEquals(highlightFill, fill)) return;
            bool updateFill = highlightFill is not null || fill is not null;
            highlight = brush; highlightFill = fill;
            // Animate the effective value, keeping the original style/binding
            // intact underneath so leaving restores the current theme ink.
            ObjectAnimationUsingKeyFrames? animation = null;
            if (brush is not null)
            {
                animation = new ObjectAnimationUsingKeyFrames { Duration = TimeSpan.Zero, FillBehavior = FillBehavior.HoldEnd };
                animation.KeyFrames.Add(new DiscreteObjectKeyFrame(brush, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            }
            Glyph.BeginAnimation(TextBlock.ForegroundProperty, animation);
            ObjectAnimationUsingKeyFrames? background = null;
            if (fill is not null)
            {
                background = new ObjectAnimationUsingKeyFrames { Duration = TimeSpan.Zero, FillBehavior = FillBehavior.HoldEnd };
                background.KeyFrames.Add(new DiscreteObjectKeyFrame(fill, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            }
            if (updateFill) Button.BeginAnimation(Control.BackgroundProperty, background);
        }
    }
    private readonly Panel row;
    private readonly Func<bool> permitted;
    private readonly bool includeText, highlightKeys;
    private readonly FrameClock clock = new();
    private readonly Stopwatch elapsed = new();
    private readonly List<Item> items = new();
    private double previous;
    private bool disposed;
    internal HeaderIconMagnifier(Panel panel, Func<bool> canAnimate, bool includeText = false, bool highlightKeys = false)
    {
        row = panel; permitted = canAnimate; this.includeText = includeText; this.highlightKeys = highlightKeys; clock.Tick += Frame;
        row.MouseMove += PointerChanged; row.MouseEnter += PointerChanged; row.MouseLeave += PointerChanged;
        row.Loaded += Loaded; row.Unloaded += Unloaded; row.IsVisibleChanged += VisibilityChanged;
        ThemeService.Changed += RefreshHighlight;
    }
    private static IEnumerable<(Button Button, TextBlock Glyph)> Targets(Panel panel, bool includeText)
    {
        foreach (UIElement child in panel.Children)
        {
            if (child is Button button && button.Content is TextBlock glyph
                && (includeText || glyph.FontFamily.Source.Contains("Icons", StringComparison.OrdinalIgnoreCase)
                    || glyph.FontFamily.Source.Contains("MDL2", StringComparison.OrdinalIgnoreCase)))
                yield return (button, glyph);
            else if (child is Panel nested)
                foreach (var target in Targets(nested, includeText)) yield return target;
        }
    }
    private void RefreshTargets()
    {
        var targets = Targets(row, includeText).ToArray();
        if (targets.Length == items.Count && targets.Zip(items).All(pair => ReferenceEquals(pair.First.Glyph, pair.Second.Glyph))) return;
        Reset(); foreach (var item in items) item.Restore(); items.Clear();
        foreach (var target in targets) items.Add(new Item(target.Button, target.Glyph));
    }
    private void RefreshHighlight()
    {
        foreach (var item in items)
        {
            bool hover = row.IsVisible && item.Button.IsMouseOver && item.Button.IsEnabled && item.Glyph.IsVisible;
            item.Highlight(hover ? item.Glyph.TryFindResource(highlightKeys ? "Think.OnAccent" : "Think.Accent") as Brush : null,
                hover && highlightKeys ? item.Button.TryFindResource("Think.Accent") as Brush : null);
        }
    }
    private void PointerChanged(object sender, MouseEventArgs args) { RefreshTargets(); RefreshHighlight(); Wake(); }
    private void Loaded(object sender, RoutedEventArgs args) { RefreshTargets(); RefreshHighlight(); Wake(); }
    private void Unloaded(object sender, RoutedEventArgs args) => Reset();
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { if (!row.IsVisible) Reset(); else Wake(); }
    private void Wake()
    {
        if (disposed || !row.IsVisible) { Reset(); return; }
        if (!SystemParameters.ClientAreaAnimation) { Reset(clearHighlight: false); return; }
        if (clock.IsRunning) return;
        previous = 0; elapsed.Restart(); clock.Start();
    }
    private void Frame(object? sender, EventArgs args)
    {
        if (!row.IsVisible) { Reset(); return; }
        if (!SystemParameters.ClientAreaAnimation) { Reset(clearHighlight: false); return; }
        double now = elapsed.Elapsed.TotalSeconds, dt = Math.Clamp(now - previous, 0, .1); previous = now;
        bool active = row.IsMouseOver && permitted();
        Point pointer = Mouse.GetPosition(row);
        const double intensity = .4, omega = 20, damping = .85 * omega;
        double frequency = omega * Math.Sqrt(1 - .85 * .85), decay = Math.Exp(-damping * dt);
        double cosine = Math.Cos(frequency * dt), sine = Math.Sin(frequency * dt);
        bool settled = true;
        foreach (var item in items)
        {
            bool visible = item.Button.IsVisible && item.Button.IsEnabled && item.Glyph.IsVisible;
            double target = 0;
            if (active && visible)
            {
                Point center = item.Button.TranslatePoint(new Point(item.Button.ActualWidth / 2, item.Button.ActualHeight / 2), row);
                double radius = Math.Clamp(item.Button.ActualWidth * 2.2, 40, 90);
                double distance = (pointer - center).Length;
                if (distance < radius) target = intensity * .5 * (1 + Math.Cos(Math.PI * distance / radius));
            }
            double displacement = item.Growth - target, velocity = item.Velocity;
            item.Growth = Math.Clamp(target + decay * (displacement * cosine + (velocity + damping * displacement) / frequency * sine), 0, intensity * 1.08);
            item.Velocity = decay * (velocity * cosine - (damping * velocity + omega * omega * displacement) / frequency * sine);
            if (Math.Abs(item.Growth - target) > .0003 || Math.Abs(item.Velocity) > .003) settled = false;
            else { item.Growth = target; item.Velocity = 0; }
            item.Scale.ScaleX = item.Scale.ScaleY = 1 + item.Growth;
        }
        if (settled) clock.Stop();
    }
    private void Reset(bool clearHighlight = true)
    {
        clock.Stop();
        foreach (var item in items)
        {
            item.Growth = item.Velocity = 0; item.Scale.ScaleX = item.Scale.ScaleY = 1;
            if (clearHighlight) item.Highlight(null);
        }
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; Reset();
        foreach (var item in items) item.Restore(); items.Clear();
        row.MouseMove -= PointerChanged; row.MouseEnter -= PointerChanged; row.MouseLeave -= PointerChanged;
        row.Loaded -= Loaded; row.Unloaded -= Unloaded; row.IsVisibleChanged -= VisibilityChanged;
        ThemeService.Changed -= RefreshHighlight;
        clock.Tick -= Frame;
    }
}
