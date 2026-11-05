using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading.Tasks;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class BrightnessControl : Border
{
    private readonly Func<System.Drawing.Rectangle> display;
    private readonly Slider level = new() { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 5, IsMoveToPointEnabled = true, Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, IsEnabled = false };
    private readonly TextBlock label = new() { Text = "BRILHO DA TELA", FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock percent = new() { Text = "—", MinWidth = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new() { Text = "Verificando suporte da tela…", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
    private readonly DispatcherTimer throttle = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly Border capsule;
    private readonly StackPanel content;
    private BrightnessState? state;
    private bool busy, dragging, updating, writing;
    private double? pending;
    private System.Drawing.Rectangle stateBounds;
    private DateTime refreshAfter;
    internal event Action? LayoutChanged;
    internal bool IsInteracting => dragging || writing || pending.HasValue || IsMouseCaptureWithin;
    internal BrightnessControl(Func<System.Drawing.Rectangle> getDisplay)
    {
        display = getDisplay; CornerRadius = new CornerRadius(18);
        SetResourceReference(BackgroundProperty, "Think.GlassCard");
        var panel = content = new StackPanel { Visibility = Visibility.Collapsed }; panel.Children.Add(label);
        var row = new DockPanel();
        var icon = new TextBlock { Text = "\uE706", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 18, Margin = new Thickness(4, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(icon, Dock.Left); row.Children.Add(icon); DockPanel.SetDock(percent, Dock.Right); row.Children.Add(percent); row.Children.Add(level);
        capsule = new Border { Child = row, CornerRadius = new CornerRadius(22), MinHeight = 44, Padding = new Thickness(4, 0, 8, 0), BorderThickness = new Thickness(1) };
        capsule.SetResourceReference(BackgroundProperty, "Think.Control"); capsule.SetResourceReference(BorderBrushProperty, "Think.Hover");
        status.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        panel.Children.Add(capsule); panel.Children.Add(status); Child = panel;
        System.Windows.Automation.AutomationProperties.SetName(level, "Brilho do monitor");
        level.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => dragging = true));
        level.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, e) => { dragging = false; throttle.Stop(); if (!e.Canceled) await Write(); else { pending = null; refreshAfter = DateTime.MinValue; Refresh(); } }));
        level.ValueChanged += (_, _) =>
        {
            if (updating || state is null) return;
            percent.Text = $"{level.Value:0}%"; pending = level.Value;
            if (!throttle.IsEnabled) throttle.Start();
        };
        throttle.Tick += async (_, _) => { throttle.Stop(); await Write(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { throttle.Stop(); pending = null; dragging = false; } else { refreshAfter = DateTime.MinValue; Refresh(); } };
        Unloaded += (_, _) => { throttle.Stop(); pending = null; };
    }
    internal async void Refresh()
    {
        if (!IsVisible || busy || IsInteracting || DateTime.UtcNow < refreshAfter) return;
        busy = true; refreshAfter = DateTime.UtcNow.AddSeconds(10);
        try
        {
            var bounds = display(); var result = await BrightnessService.ReadAsync(bounds);
            state = display() == bounds ? result : null;
            stateBounds = bounds;
            if (display() != bounds) refreshAfter = DateTime.MinValue;
            if (state is null) pending = null;
            if (state is null || (!dragging && !pending.HasValue)) Render();
        }
        catch (Exception e) when (Recoverable(e)) { state = null; pending = null; Render(); status.Text = "Não foi possível consultar o brilho. Use as configurações de Tela."; }
        finally { busy = false; if (pending.HasValue) _ = Write(); }
    }
    private async Task Write()
    {
        if (busy || state is null || !IsVisible) return;
        if (!pending.HasValue) { if (!dragging) Render(); return; }
        busy = writing = true;
        try
        {
            while (pending is double requested && IsVisible)
            {
                pending = null; var bounds = display();
                if (bounds != stateBounds) { state = null; refreshAfter = DateTime.MinValue; Render(); break; }
                var result = await BrightnessService.SetAsync(bounds, state, requested);
                if (display() != bounds) { state = null; pending = null; refreshAfter = DateTime.MinValue; Render(); break; }
                if (result is null) { pending = null; Render(); status.Text = "O driver não confirmou a alteração. Confira o brilho nas configurações de Tela."; refreshAfter = DateTime.MinValue; break; }
                state = result;
                if (!pending.HasValue && !dragging) Render();
                break; // At most one device write per throttle cycle.
            }
        }
        catch (Exception e) when (Recoverable(e)) { pending = null; Render(); status.Text = "Não foi possível alterar o brilho. Tente novamente."; }
        finally { busy = writing = false; if (pending.HasValue && IsVisible && !throttle.IsEnabled) throttle.Start(); }
    }
    private void Render()
    {
        bool available = state is not null;
        ToolTip = state?.Monitor;
        bool layoutChanged = (content.Visibility == Visibility.Visible) != available;
        // Keep the host visible so the existing refresh cadence can discover a
        // newly supported monitor. Collapse its contents and spacing instead.
        content.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        Padding = available ? new Thickness(12) : new Thickness(0);
        Margin = available ? new Thickness(4, 8, 4, 0) : new Thickness(0);
        capsule.Visibility = available ? Visibility.Visible : Visibility.Collapsed; level.IsEnabled = available;
        if (state is null) { percent.Text = "—"; status.Text = "Esta tela não expõe controle de brilho ao Windows (painel interno ou DDC/CI)."; }
        else
        {
            updating = true;
            try { level.Minimum = state.Backend == "WMI" ? state.Minimum : 0; level.Maximum = state.Backend == "WMI" ? state.Maximum : 100;
                level.Value = state.Backend == "WMI" ? state.Value : Math.Clamp((state.Value - (double)state.Minimum) * 100 / (state.Maximum - state.Minimum), 0, 100); }
            finally { updating = false; }
            percent.Text = $"{level.Value:0}%"; status.Text = state.Backend == "WMI" ? "Painel interno · sincronizado com o Windows" : "Monitor externo · DDC/CI"; ToolTip = state.Monitor;
        }
        if (layoutChanged) LayoutChanged?.Invoke();
    }
    private static bool Recoverable(Exception e) => e is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or UnauthorizedAccessException;
}
