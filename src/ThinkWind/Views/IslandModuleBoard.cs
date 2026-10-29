using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ThinkWind.Views;
// Three leading cards are prominent; the remaining cards form a smaller grid below.
internal sealed class IslandModuleBoard : Panel
{
    private readonly List<Rect> slots = new();
    private double gap = 8;
    internal double Gap { get => gap; set { if (gap == value) return; gap = value; InvalidateMeasure(); } }
    internal void SetCards(IEnumerable<Border> cards)
    {
        var next = cards.ToArray();
        if (Children.Cast<UIElement>().SequenceEqual(next)) return;
        Children.Clear(); foreach (var card in next) Children.Add(card);
        InvalidateMeasure();
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsFinite(availableSize.Width) ? Math.Max(1, availableSize.Width) : 600;
        slots.Clear(); double y = 0;
        int leading = Math.Min(3, Children.Count);
        MeasureGroup(0, leading, width >= 520 ? 3 : width >= 350 ? 2 : 1, width, 132, ref y);
        MeasureGroup(leading, Children.Count - leading, width >= 700 ? 4 : width >= 330 ? 2 : 1, width, 82, ref y);
        return new Size(width, Math.Max(0, y - (Children.Count > 0 ? Gap : 0)));
    }
    private void MeasureGroup(int start, int count, int columns, double width, double minimum, ref double y)
    {
        for (int offset = 0; offset < count; offset += columns)
        {
            int inRow = Math.Min(columns, count - offset);
            double cellWidth = Math.Max(1, (width - Gap * (inRow - 1)) / inRow);
            double height = minimum;
            for (int cell = 0; cell < inRow; cell++)
            {
                var child = Children[start + offset + cell];
                child.Measure(new Size(cellWidth, double.PositiveInfinity));
                height = Math.Max(height, child.DesiredSize.Height);
            }
            for (int cell = 0; cell < inRow; cell++) slots.Add(new Rect(cell * (cellWidth + Gap), y, cellWidth, height));
            y += height + Gap;
        }
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < Children.Count && i < slots.Count; i++) Children[i].Arrange(slots[i]);
        return finalSize;
    }
}
