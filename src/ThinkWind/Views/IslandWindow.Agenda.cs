using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private AgendaWindow? widgetAgenda;
    private void InitializeCalendarAgenda()
    {
        calendar.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (Store.IsEditing || e.ClickCount != 2 || calendar.DisplayMode != CalendarMode.Month) return;
            var node = e.OriginalSource as DependencyObject;
            while (node is not null && node is not CalendarDayButton) node = VisualTreeHelper.GetParent(node);
            if (node is not CalendarDayButton { DataContext: DateTime day, IsBlackedOut: false }) return;
            calendar.SelectedDate = day;
            if (Mouse.Captured is UIElement captured) captured.ReleaseMouseCapture();
            OpenWidgetAgenda(true); e.Handled = true;
        }), true);
        calendar.SelectedDatesChanged += (_, _) =>
        {
            if (widgetAgenda?.IsVisible == true && !Store.IsEditing)
                widgetAgenda.SelectDay(calendar.SelectedDate ?? DateTime.Today);
        };
        Closed += (_, _) => widgetAgenda?.Close();
        IsVisibleChanged += (_, _) => { if (!IsVisible) widgetAgenda?.Close(); };
    }
    private void OpenWidgetAgenda(bool add)
    {
        if (Store.IsEditing) return;
        CancelCollapse();
        if (widgetAgenda is null)
        {
            widgetAgenda = new AgendaWindow(Store, () =>
            {
                var top = calendar.PointToScreen(new Point(0, 0));
                var bottom = calendar.PointToScreen(new Point(calendar.ActualWidth, calendar.ActualHeight));
                return new OverlayChrome.PixelRect { Left = (int)top.X, Top = (int)top.Y, Right = (int)bottom.X, Bottom = (int)bottom.Y };
            }) { Owner = this };
            widgetAgenda.Closed += (_, _) => { widgetAgenda = null; ScheduleCollapse(); };
        }
        widgetAgenda.Present(calendar.SelectedDate ?? DateTime.Today, add);
    }
}
