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
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? Math.Max(1, available.Width) : 760;
        slots.Clear(); var all = Children.Cast<UIElement>().ToArray();
        // Every item is an independent widget, with equal columns and natural
        // row heights. No narrow side column or nested Widgets container.
        double y = Group(all, width, width >= 640 ? 3 : width >= 420 ? 2 : 1, 96, 0);
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
