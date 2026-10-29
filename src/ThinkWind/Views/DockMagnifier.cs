using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThinkWind.Services;

namespace ThinkWind.Views;

// One distance field and one spring clock drive the entire row. Transform the
// actual buttons so hit testing and OLE drop targets follow the visible icons.
internal sealed class DockMagnifier : IDisposable
{
    private sealed class Item
    {
        internal readonly FrameworkElement Element;
        internal readonly ScaleTransform Scale = new(1, 1);
        internal readonly TranslateTransform Move = new();
        internal readonly bool Magnify;
        internal double Growth, Velocity, Center;
        internal Item(FrameworkElement element, bool magnify)
        {
            Element = element; Magnify = magnify;
            var transforms = new TransformGroup(); transforms.Children.Add(Scale); transforms.Children.Add(Move);
            element.RenderTransformOrigin = new Point(.5, 1); element.RenderTransform = transforms;
        }
    }
    private readonly Panel row;
    private readonly Func<bool> permitted;
    private readonly FrameClock clock = new();
    private readonly Stopwatch elapsed = new();
    private readonly List<Item> items = new();
    private double previous, intensity, speed, reserve;
    private int neighbors;
    private bool enabled;
    internal DockMagnifier(Panel panel, Func<bool> canAnimate)
    {
        row = panel; permitted = canAnimate; clock.Tick += Frame;
        row.MouseMove += PointerChanged; row.MouseEnter += PointerChanged; row.MouseLeave += PointerChanged;
        row.IsVisibleChanged += VisibilityChanged;
    }
    internal void Bind(IEnumerable<FrameworkElement> applications, Preferences settings, double horizontalReserve)
    {
        Reset(); items.Clear(); var targets = applications.ToHashSet();
        foreach (FrameworkElement child in row.Children) items.Add(new Item(child, targets.Contains(child)));
        Configure(settings, horizontalReserve);
    }
    internal void Configure(Preferences settings, double horizontalReserve)
    {
        enabled = settings.DockMagnification && SystemParameters.ClientAreaAnimation;
        intensity = settings.DockMagnificationAmount; speed = settings.DockMagnificationSpeed;
        neighbors = settings.DockMagnificationNeighbors; reserve = horizontalReserve;
        if (!enabled) Reset(); else Wake();
    }
    private void PointerChanged(object sender, MouseEventArgs args) => Wake();
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs args) { if (!row.IsVisible) Reset(); else Wake(); }
    internal void Wake()
    {
        if (!enabled || !row.IsVisible || clock.IsRunning) return;
        previous = 0; elapsed.Restart(); clock.Start();
    }
    private void Frame(object? sender, EventArgs args)
    {
        double now = elapsed.Elapsed.TotalSeconds, dt = Math.Clamp(now - previous, 0, .04); previous = now;
        bool active = row.IsMouseOver && permitted();
        double pointer = Mouse.GetPosition(row).X, offset = 0;
        foreach (var item in items)
        {
            var margin = item.Element.Margin;
            item.Center = offset + margin.Left + item.Element.ActualWidth / 2;
            offset += margin.Left + item.Element.ActualWidth + margin.Right;
        }
        var apps = items.Where(item => item.Magnify && item.Element.ActualWidth > 0).ToArray();
        if (apps.Length == 0) { Reset(); return; }
        // Use stable layout centres, avoiding feedback from the animated shifts.
        double pitch = apps.Length > 1 ? (apps[^1].Center - apps[0].Center) / (apps.Length - 1) : apps[0].Element.ActualWidth;
        var nearest = apps.MinBy(item => Math.Abs(pointer - item.Center))!;
        double radius = Math.Max(1, pitch * (neighbors == 0 ? .5 : neighbors + 1));
        double omega = 18 * speed;
        bool settled = true;
        foreach (var item in apps)
        {
            double distance = Math.Abs(pointer - item.Center);
            double target = active && (neighbors > 0 || ReferenceEquals(item, nearest)) && distance < radius
                ? intensity * .5 * (1 + Math.Cos(Math.PI * distance / radius)) : 0;
            // Substeps keep a lightly damped spring stable at slow frame rates.
            int steps = Math.Max(1, (int)Math.Ceiling(dt * 120)); double step = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                item.Velocity += (omega * omega * (target - item.Growth) - 1.7 * omega * item.Velocity) * step;
                item.Growth = Math.Clamp(item.Growth + item.Velocity * step, 0, intensity * 1.08);
            }
            if (Math.Abs(item.Growth - target) > .0003 || Math.Abs(item.Velocity) > .003) settled = false;
            else { item.Growth = target; item.Velocity = 0; }
        }
        double total = apps.Sum(item => item.Element.ActualWidth * item.Growth);
        double factor = total > reserve && total > 0 ? reserve / total : 1;
        double shift = -Math.Min(total, reserve) / 2;
        foreach (var item in items)
        {
            double growth = item.Magnify ? item.Growth * factor : 0;
            double extra = item.Element.ActualWidth * growth;
            item.Scale.ScaleX = item.Scale.ScaleY = 1 + growth;
            item.Move.X = shift + extra / 2;
            shift += extra;
        }
        if (settled) clock.Stop();
    }
    internal void Reset()
    {
        clock.Stop();
        foreach (var item in items) { item.Growth = item.Velocity = 0; item.Scale.ScaleX = item.Scale.ScaleY = 1; item.Move.X = 0; }
    }
    public void Dispose()
    {
        Reset(); row.MouseMove -= PointerChanged; row.MouseEnter -= PointerChanged; row.MouseLeave -= PointerChanged; row.IsVisibleChanged -= VisibilityChanged;
    }
}
