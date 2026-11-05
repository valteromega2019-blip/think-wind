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
    internal static readonly DependencyProperty ExtraHeightProperty = DependencyProperty.RegisterAttached(
        "ExtraHeight", typeof(double), typeof(IslandModuleBoard), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsParentMeasure));
    internal double Gap { get => gap; set { if (gap == value) return; gap = value; InvalidateMeasure(); } }
    internal string[] RowPeers(Border card) => slots.TryGetValue(card, out var slot)
        ? slots.Where(pair => Math.Abs(pair.Value.Y - slot.Y) < .5).Select(pair => (pair.Key as Border)?.Tag as string).OfType<string>().ToArray()
        : Array.Empty<string>();
    internal void SetCards(IEnumerable<Border> cards)
    {
        var next = cards.ToArray(); if (Children.Cast<UIElement>().SequenceEqual(next)) return;
        Children.Clear(); foreach (var card in next) Children.Add(card); InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size available)
    {
        double width = double.IsFinite(available.Width) ? Math.Max(1, available.Width) : 760;
        slots.Clear(); var all = Children.Cast<UIElement>().ToArray();
        // Independent widths share each complete row; natural content height
        // remains the lower bound even when the user shrinks the row.
        double y = Customized(all, width);
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
            double height = 64;
            while (offset < items.Length)
            {
                int columns = (int)items[offset].GetValue(ColumnsProperty);
                if (columns == 0) columns = width >= 640 ? 3 : 2;
                int maximumColumns = Math.Clamp((int)((width + Gap) / (220 + Gap)), 1, 3);
                columns = Math.Clamp(columns, 1, maximumColumns);
                int span = columns == 1 ? 6 : columns == 2 ? 3 : 2;
                if (used + span > 6) break;
                var item = items[offset++];
                row.Add((item, span)); used += span;
            }
            // Redistribute unused columns inside this row. A half-width widget
            // next to a third-width one must not leave an empty strip at the side.
            foreach (var entry in row)
            {
                double cell = Math.Max(1, (width + Gap) * entry.Span / used - Gap);
                entry.Item.Measure(new Size(cell, double.PositiveInfinity));
                height = Math.Max(height, entry.Item.DesiredSize.Height + (double)entry.Item.GetValue(ExtraHeightProperty));
            }
            double x = 0;
            foreach (var entry in row)
            {
                double cell = Math.Max(1, (width + Gap) * entry.Span / used - Gap);
                slots[entry.Item] = new Rect(x, y, cell, height); x += cell + Gap;
            }
            y += height + Gap;
        }
        return y;
    }
    protected override Size ArrangeOverride(Size finalSize)
    { foreach (UIElement child in Children) if (slots.TryGetValue(child, out var rectangle)) child.Arrange(rectangle); return finalSize; }
}
