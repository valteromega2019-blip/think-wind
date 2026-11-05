using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbyLauncher.Views;
internal sealed class IslandModuleBoard : Panel
{
    private readonly Dictionary<UIElement, Rect> slots = new();
    private double gap = 8;
    internal static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
        "Columns", typeof(int), typeof(IslandModuleBoard), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
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
        double y = all.Any(item => (int)item.GetValue(ColumnsProperty) != 0)
            ? Customized(all, width) : Group(all, width, width >= 640 ? 3 : width >= 420 ? 2 : 1, 96, 0);
        return new Size(width, Math.Max(0, y - (all.Length > 0 ? Gap : 0)));
    }
    private double Customized(UIElement[] items, double width)
    {
        double y = 0;
        int offset = 0;
        while (offset < items.Length)
        {
            var row = new List<(UIElement Item, int Span)>();
            int used = 0;
            double height = 96;
            while (offset < items.Length)
            {
                int columns = (int)items[offset].GetValue(ColumnsProperty);
                if (columns == 0) columns = width >= 640 ? 3 : 2;
                int span = width < 420 ? 6 : columns == 2 ? 3 : 2;
                if (used + span > 6) break;
                var item = items[offset++];
                double cell = Math.Max(1, (width + Gap) * span / 6 - Gap);
                item.Measure(new Size(cell, double.PositiveInfinity));
                height = Math.Max(height, item.DesiredSize.Height);
                row.Add((item, span)); used += span;
            }
            double x = 0;
            foreach (var entry in row)
            {
                double cell = Math.Max(1, (width + Gap) * entry.Span / 6 - Gap);
                slots[entry.Item] = new Rect(x, y, cell, height); x += cell + Gap;
            }
            y += height + Gap;
        }
        return y;
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
