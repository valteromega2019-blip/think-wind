using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

// Shared by the complete monitor and the configurable compact widget.
internal sealed class HardwareSummaryCard : Border
{
    private readonly string id;
    private readonly TextBlock value = new(), detail = new();
    private readonly ProgressBar progress = new() { Maximum = 100, Height = 3, BorderThickness = new Thickness(0) };
    internal HardwareSummaryCard(string metric, bool compact = false)
    {
        id = metric; CornerRadius = new CornerRadius(20); Padding = new Thickness(compact ? 6 : 10); Margin = new Thickness(compact ? 0 : 3);
        SetResourceReference(BackgroundProperty, "Think.GlassCard");
        var panel = new StackPanel(); var heading = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = new TextBlock { Text = metric switch { "cpu" => "\uE950", "gpu" => "\uE7F4", "ram" => "\uE964", _ => "\uEDA2" }, FontSize = 13, FontFamily = new FontFamily("Segoe Fluent Icons"), Margin = new Thickness(0, 0, 8, 0) };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); heading.Children.Add(icon);
        var title = new TextBlock { Text = Label(metric), FontSize = 11 }; title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); heading.Children.Add(title);
        value.FontSize = compact ? 20 : 25; value.FontWeight = FontWeights.SemiBold; value.Margin = new Thickness(0, compact ? 4 : 8, 0, 2);
        value.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        detail.FontSize = 10; detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        progress.Margin = new Thickness(0, compact ? 4 : 8, 0, 0);
        progress.SetResourceReference(Control.ForegroundProperty, "Think.Accent"); progress.SetResourceReference(Control.BackgroundProperty, "Think.Hover");
        panel.Children.Add(heading); panel.Children.Add(value); panel.Children.Add(detail); panel.Children.Add(progress); Child = panel;
        Present(null);
    }
    internal static string Label(string id) => id switch { "cpu" => "Processador", "gpu" => "Placa de vídeo", "ram" => "Memória RAM", _ => "Armazenamento" };
    internal void Present(HardwareSample? s)
    {
        string N(double? n, string unit, string format = "0") => HardwareMonitorPanel.Number(n, unit, format);
        double? percent;
        switch (id)
        {
            case "cpu":
            case "gpu":
                var device = id == "cpu" ? s?.Cpu : s?.Gpu;
                percent = device?.Load;
                value.Text = device?.Temperature.HasValue == true ? N(device.Temperature, " °C") : N(percent, "%");
                detail.Text = N(percent, "%") + " de uso · " + (device?.Name ?? Label(id)); break;
            case "ram":
                percent = s?.RamTotal > 0 ? s.RamUsed / s.RamTotal * 100 : null;
                value.Text = N(s?.RamUsed, " GB", "0.0"); detail.Text = N(percent, "%") + " de " + N(s?.RamTotal, " GB"); break;
            default:
                percent = s?.DiskTotal > 0 ? (1 - s.DiskFree / s.DiskTotal) * 100 : null;
                value.Text = N(s?.DiskFree, " GB", "0.0"); detail.Text = "livres em " + (s?.Disk ?? "C:"); break;
        }
        detail.ToolTip = detail.Text;
        progress.Value = Math.Clamp(percent ?? 0, 0, 100); progress.Opacity = percent.HasValue ? 1 : .25;
    }
}
