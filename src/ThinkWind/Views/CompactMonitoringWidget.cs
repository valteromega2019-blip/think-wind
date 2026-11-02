using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class CompactMonitoringWidget : StackPanel
{
    private readonly HardwareTelemetry telemetry;
    private readonly SettingsStore store;
    private readonly List<HardwareSummaryCard> cards = new();
    private readonly TextBlock battery = new() { FontSize = 14, Margin = new Thickness(6, 4, 6, 0), Visibility = Visibility.Collapsed };
    private string? signature;
    internal event Action? LayoutChanged;
    internal CompactMonitoringWidget(SettingsStore settings, HardwareTelemetry source)
    {
        store = settings; telemetry = source;
        battery.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        Loaded += (_, _) => { store.Changed += Configure; telemetry.Changed += Present; Configure(null, EventArgs.Empty); Active(); };
        Unloaded += (_, _) => { store.Changed -= Configure; telemetry.Changed -= Present; telemetry.SetActive(this, false); };
        IsVisibleChanged += (_, _) => { Active(); if (IsVisible) Present(telemetry.Latest); };
    }
    private void Active() => telemetry.SetActive(this, IsLoaded && IsVisible && cards.Count > 0);
    private void Configure(object? sender, EventArgs e)
    {
        string next = string.Join("|", store.Value.CompactMonitoringMetrics);
        if (signature != next)
        {
            signature = next; Children.Clear(); cards.Clear();
            foreach (string id in store.Value.CompactMonitoringMetrics.Take(3))
            {
                var card = new HardwareSummaryCard(id, compact: true, store: store) { Margin = new Thickness(0, 2, 0, 2) };
                card.LayoutChanged += () => LayoutChanged?.Invoke(); cards.Add(card); Children.Add(card);
            }
            if (cards.Count == 0) Children.Add(new TextBlock { Text = "Escolha até 3 blocos em Personalizar → Widgets → Monitoramento.", TextWrapping = TextWrapping.Wrap });
            Children.Add(battery);
            LayoutChanged?.Invoke();
        }
        Active(); Present(telemetry.Latest); RefreshPower();
    }
    internal void RefreshPower()
    {
        if (!IsVisible) return;
        var power = System.Windows.Forms.SystemInformation.PowerStatus;
        bool show = store.Value.ShowBattery && power.BatteryLifePercent is >= 0 and <= 1
            && !power.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery);
        var visibility = show ? Visibility.Visible : Visibility.Collapsed;
        battery.Text = show ? $"Bateria  {power.BatteryLifePercent:P0}" : "";
        if (battery.Visibility != visibility) { battery.Visibility = visibility; LayoutChanged?.Invoke(); }
    }
    private void Present(HardwareSample? sample) { if (IsVisible) foreach (var card in cards) card.Present(sample); }
}
