using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class BrightnessControl : Border
{
    private readonly Func<System.Drawing.Rectangle> display;
    private readonly Slider level = new() { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 10, Visibility = Visibility.Collapsed, IsMoveToPointEnabled = true };
    private readonly TextBlock label = new() { Text = "Brilho · verificar suporte do monitor", FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private BrightnessState? state;
    private bool busy, dragging;
    private DateTime refreshAfter;
    internal event Action? LayoutChanged;
    internal BrightnessControl(Func<System.Drawing.Rectangle> getDisplay)
    {
        display = getDisplay; CornerRadius = new CornerRadius(18); Padding = new Thickness(12); Margin = new Thickness(4, 8, 4, 0);
        SetResourceReference(BackgroundProperty, "Think.GlassCard");
        var panel = new StackPanel(); panel.Children.Add(label); panel.Children.Add(level); Child = panel;
        System.Windows.Automation.AutomationProperties.SetName(level, "Brilho do monitor");
        level.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => dragging = true));
        level.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, e) => { dragging = false; if (!e.Canceled) await Write(); }));
        level.PreviewMouseLeftButtonUp += async (_, _) => { if (!dragging) await Write(); };
        level.KeyUp += async (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) await Write(); };
    }
    internal async void Refresh()
    {
        if (busy || dragging || level.IsMouseCaptureWithin || DateTime.UtcNow < refreshAfter) return;
        busy = true; refreshAfter = DateTime.UtcNow.AddSeconds(10);
        try
        {
            var bounds = display(); var result = await BrightnessService.ReadAsync(bounds);
            state = display() == bounds ? result : null;
            if (display() != bounds) refreshAfter = DateTime.MinValue;
            Render();
        }
        finally { busy = false; }
    }
    private async System.Threading.Tasks.Task Write()
    {
        if (busy || state is null) return;
        busy = true; level.IsEnabled = false;
        try
        {
            var bounds = display(); var result = await BrightnessService.SetAsync(bounds, state, level.Value);
            state = display() == bounds ? result : null;
            if (display() != bounds) refreshAfter = DateTime.MinValue;
            Render();
        }
        finally { busy = false; level.IsEnabled = true; }
    }
    private void Render()
    {
        level.Visibility = state is null ? Visibility.Collapsed : Visibility.Visible;
        if (state is null) label.Text = "Brilho indisponível via DDC/CI · use as configurações de Tela";
        else
        {
            level.Value = Math.Clamp((state.Value - (double)state.Minimum) * 100 / (state.Maximum - state.Minimum), 0, 100);
            label.Text = $"Brilho  {level.Value:0}%"; ToolTip = state.Monitor;
        }
        LayoutChanged?.Invoke();
    }
}
