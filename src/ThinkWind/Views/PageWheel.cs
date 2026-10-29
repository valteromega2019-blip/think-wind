using System;
using System.Windows;

namespace ThinkWind.Views;
internal static class PageWheel
{
    internal static void Attach(UIElement area, Func<int> count, Func<int> current, Action<int> change, Func<bool> busy)
    {
        int accumulated = 0;
        area.MouseLeave += (_, _) => accumulated = 0;
        area.PreviewMouseWheel += (_, e) =>
        {
            int pages = Math.Max(1, count());
            if (pages <= 1 || busy() || e.Delta == 0) { accumulated = 0; return; }
            // Accumulate small touchpad/high-resolution wheel deltas. Changing
            // direction discards the old remainder, so navigation responds now.
            if (Math.Sign(accumulated) != Math.Sign(e.Delta)) accumulated = 0;
            accumulated += e.Delta; e.Handled = true;
            if (Math.Abs(accumulated) < 120) return;
            int direction = accumulated > 0 ? -1 : 1;
            accumulated %= 120;
            int next = Math.Clamp(current() + direction, 0, pages - 1);
            if (next != current()) change(next);
        };
    }
}
