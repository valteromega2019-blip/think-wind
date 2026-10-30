using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ThinkWind.Views;
internal sealed class IslandModuleBoard : Panel
{
    private readonly Dictionary<UIElement, Rect> slots = new();
    private double gap = 8;
    internal double Gap { get => gap; set { if (gap == value) return; gap = value; InvalidateMeasure(); } }
    internal void SetCards(IEnumerable<Border> cards)
    {
        var next = cards.ToArray(); if (Children.Cast<UIElement>().SequenceEqual(next)) return;
        Children.Clear(); foreach (var card in next) Children.Add(card); InvalidateMeasure();
    }
    private static bool Detailed(UIElement item) => item is FrameworkElement element && (element.Tag is "calendar" or "background");
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? Math.Max(1, available.Width) : 760;
        slots.Clear(); var all = Children.Cast<UIElement>().ToArray();
        var widgets = all.Where(item => item is FrameworkElement element && element.Tag is "widgets").ToArray();
        var controls = all.Except(widgets).ToArray(); var leading = controls.Take(3).ToArray(); var secondary = controls.Skip(3).ToArray();
        double y = Group(leading, width, leading.Any(Detailed) ? 2 : 3, 132, 0);
        var details = secondary.Where(Detailed).ToArray(); var small = secondary.Where(item => !Detailed(item)).ToArray();
        if (details.Length > 0 && width >= 680)
        {
            double side = small.Length > 0 ? 128 : 0;
            double remaining = width - side - (side > 0 ? Gap : 0);
            double cell = (remaining - Gap * (details.Length - 1)) / details.Length;
            double detailHeight = 0, smallHeight = 0;
            var smallHeights = new double[small.Length];
            for (int i = 0; i < details.Length; i++)
            {
                var item = details[i]; item.Measure(new Size(cell, double.PositiveInfinity));
                detailHeight = Math.Max(detailHeight, item.DesiredSize.Height);
            }
            for (int i = 0; i < small.Length; i++)
            {
                small[i].Measure(new Size(side, double.PositiveInfinity));
                smallHeights[i] = Math.Max(72, small[i].DesiredSize.Height);
                smallHeight += smallHeights[i];
            }
            smallHeight += Math.Max(0, small.Length - 1) * Gap;
            double height = Math.Max(detailHeight, smallHeight);
            for (int i = 0; i < details.Length; i++)
                slots[details[i]] = new Rect(side + (side > 0 ? Gap : 0) + i * (cell + Gap), y, cell, height);
            double smallY = y;
            for (int i = 0; i < small.Length; i++)
            {
                double itemHeight = smallHeights[i] + (height - smallHeight) / small.Length;
                slots[small[i]] = new Rect(0, smallY, side, itemHeight);
                smallY += itemHeight + Gap;
            }
            y += height + Gap;
        }
        else y = Group(secondary, width, width >= 500 ? 2 : 1, 72, y);
        y = Group(widgets, width, 1, 140, y);
        return new Size(width, Math.Max(0, y - (all.Length > 0 ? Gap : 0)));
    }
    private double Group(UIElement[] items, double width, int columns, double minimum, double y)
    {
        for (int offset = 0; offset < items.Length; offset += columns)
        {
            int count = Math.Min(columns, items.Length - offset); double cell = Math.Max(1, (width - Gap * (count - 1)) / count), height = minimum;
            for (int i = 0; i < count; i++) { items[offset + i].Measure(new Size(cell, double.PositiveInfinity)); height = Math.Max(height, items[offset + i].DesiredSize.Height); }
            for (int i = 0; i < count; i++) slots[items[offset + i]] = new Rect(i * (cell + Gap), y, cell, height);
            y += height + Gap;
        }
        return y;
    }
    protected override Size ArrangeOverride(Size finalSize)
    { foreach (UIElement child in Children) if (slots.TryGetValue(child, out var rectangle)) child.Arrange(rectangle); return finalSize; }
}
