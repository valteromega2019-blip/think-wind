using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly System.Windows.Controls.Primitives.UniformGrid diskPicker = new() { Rows = 1, Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
    private readonly Dictionary<string, Button> diskButtons = new();
    private readonly SettingsStore? settings;
    private string? diskStructure, selectedDisk;
    internal event Action? LayoutChanged;
    internal HardwareSummaryCard(string metric, bool compact = false, SettingsStore? store = null)
    {
        settings = store;
        id = metric; CornerRadius = new CornerRadius(20); Padding = new Thickness(12); Margin = new Thickness(compact ? 0 : 3);
        if (compact) FontSizing.LimitScale(this, 1.35);
        SetResourceReference(BackgroundProperty, "Think.GlassCard");
        var panel = new StackPanel(); var heading = new DockPanel();
        var icon = new TextBlock { Text = metric switch { "cpu" => "\uE950", "gpu" => "\uE7F4", "ram" => "\uE964", _ => "\uEDA2" }, FontSize = compact ? 16 : 13, FontFamily = new FontFamily("Segoe Fluent Icons"), Margin = new Thickness(0, 0, 8, 0) };
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); DockPanel.SetDock(icon, Dock.Left); heading.Children.Add(icon);
        var title = new TextBlock { Text = Label(metric), FontSize = compact ? 14 : 11, TextWrapping = compact ? TextWrapping.Wrap : TextWrapping.NoWrap }; title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); heading.Children.Add(title);
        value.FontSize = compact ? 30 : 25; value.FontWeight = FontWeights.SemiBold; value.Margin = new Thickness(0, compact ? 6 : 8, 0, 2);
        value.TextWrapping = compact ? TextWrapping.Wrap : TextWrapping.NoWrap;
        value.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        detail.FontSize = compact ? 12 : 10; detail.TextWrapping = TextWrapping.NoWrap; detail.TextTrimming = TextTrimming.CharacterEllipsis;
        detail.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        progress.Margin = new Thickness(0, compact ? 4 : 8, 0, 0);
        progress.SetResourceReference(Control.ForegroundProperty, "Think.Accent"); progress.SetResourceReference(Control.BackgroundProperty, "Think.Hover");
        if (metric == "disk") panel.Children.Add(diskPicker);
        panel.Children.Add(heading); panel.Children.Add(value); panel.Children.Add(detail); panel.Children.Add(progress); Child = panel;
        Present(null);
    }
    internal static string Label(string id) => id switch { "cpu" => "Processador", "gpu" => "Placa de vídeo", "ram" => "Memória RAM", _ => "Armazenamento" };
    internal void Present(HardwareSample? s)
    {
        if (id == "disk") PresentDiskPicker(s);
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
    private void PresentDiskPicker(HardwareSample? sample)
    {
        var disks = sample?.Disks?.Take(4).ToArray() ?? Array.Empty<DiskReading>();
        string signature = string.Join("|", disks.Select(d => d.Id));
        if (diskStructure != signature)
        {
            diskStructure = signature; selectedDisk = null; diskPicker.Children.Clear(); diskButtons.Clear();
            diskPicker.Columns = Math.Max(1, disks.Length);
            diskPicker.Visibility = disks.Length > 1 && settings is not null ? Visibility.Visible : Visibility.Collapsed;
            foreach (var disk in disks)
            {
                string root = disk.Id;
                var button = new Button { Content = root, FontSize = 12, MinHeight = 28, Padding = new Thickness(4, 4, 4, 4), Margin = new Thickness(2), ToolTip = "Mostrar armazenamento de " + root };
                System.Windows.Automation.AutomationProperties.SetName(button, "Armazenamento do disco " + root);
                button.Click += (_, _) => settings?.Update(p => p.MonitoringDiskId = root);
                diskButtons[root] = button; diskPicker.Children.Add(button);
            }
            LayoutChanged?.Invoke();
        }
        if (selectedDisk == sample?.Disk) return;
        selectedDisk = sample?.Disk;
        foreach (var entry in diskButtons)
        {
            bool selected = entry.Key.Equals(selectedDisk, StringComparison.OrdinalIgnoreCase);
            entry.Value.SetResourceReference(Control.BackgroundProperty, selected ? "Think.AccentSurface" : "Think.Control");
            entry.Value.SetResourceReference(Control.ForegroundProperty, selected ? "Think.Accent" : "Think.Text");
            System.Windows.Automation.AutomationProperties.SetHelpText(entry.Value, selected ? "Disco selecionado" : "Selecionar este disco");
        }
    }
}
