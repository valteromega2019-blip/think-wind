using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OrbyLauncher.Views;

internal sealed class AlarmTimePicker : Grid
{
    private readonly TextBox hours = new(), minutes = new();
    private int lastHour = 7, lastMinute;
    internal AlarmTimePicker()
    {
        Margin = new Thickness(0, 6, 0, 10);
        ColumnDefinitions.Add(new ColumnDefinition());
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition());
        Children.Add(Unit(hours, "Horas", 24));
        var separator = ClockDesign.Text(":", 28);
        separator.VerticalAlignment = VerticalAlignment.Center;
        separator.Margin = new Thickness(8, 12, 8, 0);
        separator.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        SetColumn(separator, 1); Children.Add(separator);
        var minute = Unit(minutes, "Minutos", 60); SetColumn(minute, 2); Children.Add(minute);
        MinuteOfDay = 7 * 60;
    }
    internal int MinuteOfDay
    {
        set
        {
            value = Math.Clamp(value, 0, 1439);
            lastHour = value / 60; lastMinute = value % 60;
            hours.Text = lastHour.ToString("00"); minutes.Text = lastMinute.ToString("00");
        }
    }
    internal bool TryGetMinute(out int value)
    {
        value = 0;
        if (!int.TryParse(hours.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int h) || h is < 0 or > 23
            || !int.TryParse(minutes.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int m) || m is < 0 or > 59) return false;
        lastHour = h; lastMinute = m; value = h * 60 + m;
        hours.Text = h.ToString("00"); minutes.Text = m.ToString("00");
        return true;
    }
    private Border Unit(TextBox input, string label, int range)
    {
        var stack = new StackPanel();
        var title = ClockDesign.Text(label, 11, true); title.HorizontalAlignment = HorizontalAlignment.Center; stack.Children.Add(title);
        var up = ClockDesign.Button("\uE70E", "Aumentar " + label.ToLowerInvariant(), () => Step(input, range, 1), true);
        up.MinHeight = 24; up.Padding = new Thickness(8, 2, 8, 2); up.HorizontalAlignment = HorizontalAlignment.Stretch; stack.Children.Add(up);
        input.FontSize = 30; input.FontWeight = FontWeights.SemiBold; input.TextAlignment = TextAlignment.Center;
        input.MaxLength = 2; input.Padding = new Thickness(4); input.BorderThickness = new Thickness(0);
        input.SetResourceReference(Control.ForegroundProperty, "Think.Accent");
        input.SetResourceReference(Control.BackgroundProperty, "Think.Control");
        System.Windows.Automation.AutomationProperties.SetName(input, label + (range == 24 ? ", de 00 a 23" : ", de 00 a 59"));
        input.ToolTip = "Digite ou use as setas e a roda do mouse";
        input.GotKeyboardFocus += (_, _) => input.SelectAll();
        input.PreviewTextInput += (_, e) => e.Handled = !System.Linq.Enumerable.All(e.Text, c => c is >= '0' and <= '9');
        input.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.Up or Key.Down) { Step(input, range, e.Key == Key.Up ? 1 : -1); e.Handled = true; }
        };
        input.LostKeyboardFocus += (_, _) =>
        {
            if (int.TryParse(input.Text, out int n) && n >= 0 && n < range)
            { if (range == 24) lastHour = n; else lastMinute = n; input.Text = n.ToString("00"); }
        };
        stack.Children.Add(input);
        var down = ClockDesign.Button("\uE70D", "Diminuir " + label.ToLowerInvariant(), () => Step(input, range, -1), true);
        down.MinHeight = 24; down.Padding = new Thickness(8, 2, 8, 2); down.HorizontalAlignment = HorizontalAlignment.Stretch; stack.Children.Add(down);
        var surface = ClockDesign.Card(stack, 8);
        surface.PreviewMouseWheel += (_, e) => { if (e.Delta == 0) return; Step(input, range, e.Delta > 0 ? 1 : -1); e.Handled = true; };
        return surface;
    }
    private void Step(TextBox input, int range, int delta)
    {
        int n = int.TryParse(input.Text, out int parsed) && parsed >= 0 && parsed < range ? parsed : range == 24 ? lastHour : lastMinute;
        n = (n + delta + range) % range;
        if (range == 24) lastHour = n; else lastMinute = n;
        input.Text = n.ToString("00");
    }
}
