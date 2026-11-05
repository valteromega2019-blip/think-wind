using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrbyLauncher.Views;

internal sealed partial class AgendaPanel
{
    private bool markersQueued;
    private Brush AgendaBrush(string? hex)
    {
        string color = Services.AlarmService.NormalizeAgendaColor(hex);
        return color.Length == 0 ? (Brush)FindResource("Think.Accent") : new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    private TextBox AddColorPicker(string initial)
    {
        body.Children.Add(ClockDesign.Text("Cor no calendário", 12));
        var field = new TextBox { Text = initial, MaxLength = 7, ToolTip = "#RRGGBB · vazio usa a cor de destaque do Orby", Margin = new Thickness(0, 4, 0, 6) };
        var choices = new WrapPanel();
        foreach (var color in new[] { "", "#66CDAA", "#60A5FA", "#A78BFA", "#F59E0B", "#F472B6", "#F87171" })
        {
            var dot = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = AgendaBrush(color) };
            var pick = new Button { Content = dot, Padding = new Thickness(5), Margin = new Thickness(2), ToolTip = color.Length == 0 ? "Cor do tema" : color };
            System.Windows.Automation.AutomationProperties.SetName(pick, (string)pick.ToolTip);
            pick.Click += (_, _) => field.Text = color;
            choices.Children.Add(pick);
        }
        body.Children.Add(choices); body.Children.Add(field); return field;
    }
    private void ConfigureCalendarDays()
    {
        // Scoped to this agenda: preserve the regular calendar throughout the launcher.
        var style = new Style(typeof(CalendarDayButton), (Style)FindResource("Think.CalendarDay"));
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:p="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework" TargetType="p:CalendarDayButton">
 <Border x:Name="Day" Background="Transparent" CornerRadius="7" BorderThickness="1" BorderBrush="Transparent" Padding="3">
  <Grid><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="0,0,0,3"/>
   <Border Height="3" Margin="3,0,3,0" CornerRadius="1.5" VerticalAlignment="Bottom" Background="{TemplateBinding Tag}" IsHitTestVisible="False"/>
  </Grid>
 </Border>
 <ControlTemplate.Triggers>
  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Day" Property="Background" Value="{DynamicResource Think.Hover}"/></Trigger>
  <Trigger Property="IsToday" Value="True"><Setter TargetName="Day" Property="BorderBrush" Value="{DynamicResource Think.Accent}"/></Trigger>
  <Trigger Property="IsSelected" Value="True"><Setter TargetName="Day" Property="Background" Value="{DynamicResource Think.AccentSurface}"/><Setter TargetName="Day" Property="BorderBrush" Value="{DynamicResource Think.Accent}"/></Trigger>
  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Day" Property="BorderBrush" Value="{DynamicResource Think.Accent}"/></Trigger>
  <Trigger Property="IsInactive" Value="True"><Setter Property="Opacity" Value="0.4"/></Trigger>
 </ControlTemplate.Triggers>
</ControlTemplate>
""")));
        calendar.CalendarDayButtonStyle = style;
        calendar.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler((_, e) =>
        {
            if (e.ClickCount != 2 || calendar.DisplayMode != CalendarMode.Month) return;
            var node = e.OriginalSource as DependencyObject;
            while (node is not null && node is not CalendarDayButton) node = VisualTreeHelper.GetParent(node);
            if (node is not CalendarDayButton { DataContext: DateTime day, IsBlackedOut: false }) return;
            selected = day.Date; calendar.SelectedDate = selected;
            if (Mouse.Captured is UIElement captured) captured.ReleaseMouseCapture();
            Edit(null, false); e.Handled = true;
        }), true);
        calendar.Loaded += (_, _) => QueueDayMarkers();
        calendar.DisplayDateChanged += (_, _) => QueueDayMarkers();
        calendar.DisplayModeChanged += (_, _) => QueueDayMarkers();
    }
    private void QueueDayMarkers()
    {
        if (markersQueued) return;
        markersQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            markersQueued = false;
            var groups = service.Items.Where(a => a.IsAgenda && a.EventDate.HasValue).GroupBy(a => a.EventDate!.Value.Date).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var day in DayButtons(calendar))
            {
                day.Tag = null; day.ClearValue(ToolTipProperty);
                if (day.DataContext is not DateTime date || !groups.TryGetValue(date.Date, out var entries)) continue;
                var colors = entries.Select(a => (AgendaBrush(a.ColorHex) as SolidColorBrush)?.Color ?? SystemColors.HighlightColor).Distinct().Take(4).ToArray();
                var marker = new LinearGradientBrush { StartPoint = new Point(0, .5), EndPoint = new Point(1, .5) };
                for (int i = 0; i < colors.Length; i++)
                {
                    marker.GradientStops.Add(new GradientStop(colors[i], (double)i / colors.Length));
                    marker.GradientStops.Add(new GradientStop(colors[i], (double)(i + 1) / colors.Length));
                }
                marker.Freeze(); day.Tag = marker;
                day.ToolTip = string.Join("\n", entries.Take(5).Select(a => a.Name)) + (entries.Length > 5 ? $"\n+{entries.Length - 5}" : "");
            }
        }));
    }
    private static IEnumerable<CalendarDayButton> DayButtons(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is CalendarDayButton day) yield return day;
            else foreach (var nested in DayButtons(child)) yield return nested;
        }
    }
}
