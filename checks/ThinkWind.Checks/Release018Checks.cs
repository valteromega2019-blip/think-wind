using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

internal static class Release018Checks
{
    internal static void CalendarCells()
    {
        var theme = new ResourceDictionary { Source = new Uri("/ThinkWind;component/Views/CalendarTheme.xaml", UriKind.Relative) };
        foreach (double font in new[] { 11.2, 14, 35 })
        foreach (double width in new[] { 220d, 320 })
        {
            var calendar = new Calendar { Style = (Style)theme["Think.Calendar"], FontSize = font, DisplayDate = new DateTime(2026, 10, 30) };
            calendar.Measure(new Size(width, double.PositiveInfinity));
            calendar.Arrange(new Rect(0, 0, width, calendar.DesiredSize.Height));
            calendar.UpdateLayout();
            var days = Descendants(calendar).OfType<CalendarDayButton>().Where(day => day.Visibility == Visibility.Visible).ToArray();
            if (days.Length != 42) throw new InvalidOperationException("Grade mensal incompleta: " + days.Length);
            foreach (var day in days)
                if (day.RenderSize.Height + day.Margin.Top + day.Margin.Bottom + .5 < day.DesiredSize.Height
                    || day.RenderSize.Width + day.Margin.Left + day.Margin.Right + .5 < day.DesiredSize.Width)
                    throw new InvalidOperationException($"Dia {day.Content} recortado com fonte {font}, largura {width}: {day.DesiredSize} / {day.RenderSize}");
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
