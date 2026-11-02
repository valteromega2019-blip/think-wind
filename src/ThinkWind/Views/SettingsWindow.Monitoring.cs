using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private FrameworkElement CompactMonitoringOptions()
    {
        var body = new StackPanel { Margin = new Thickness(26, 2, 0, 12) };
        var count = ClockDesign.Text("", 12); count.FontWeight = FontWeights.SemiBold; body.Children.Add(count);
        body.Children.Add(ClockDesign.Text("Escolha até 3 blocos para este widget.", 11, true));
        var message = ClockDesign.Text("", 11, true);
        var choices = new List<(string Id, CheckBox Box)>();
        bool syncing = false;
        void Sync(object? sender, EventArgs args)
        {
            syncing = true;
            try
            {
                foreach (var choice in choices) choice.Box.IsChecked = store.Value.CompactMonitoringMetrics.Contains(choice.Id);
                count.Text = $"{store.Value.CompactMonitoringMetrics.Count} de 3 blocos selecionados";
            }
            finally { syncing = false; }
        }
        foreach (string metric in new[] { "cpu", "gpu", "ram", "disk" })
        {
            string id = metric;
            var box = new CheckBox { Content = HardwareSummaryCard.Label(id), IsChecked = store.Value.CompactMonitoringMetrics.Contains(id), Margin = new Thickness(0, 5, 0, 5),
                Style = SelectionCircleStyle.Create(id switch { "cpu" => "\uE950", "gpu" => "\uE7F4", "ram" => "\uE964", _ => "\uEDA2" }) };
            System.Windows.Automation.AutomationProperties.SetName(box, HardwareSummaryCard.Label(id));
            void ChangeSelection(bool selected)
            {
                if (building || syncing) return;
                if (selected && !store.Value.CompactMonitoringMetrics.Contains(id) && store.Value.CompactMonitoringMetrics.Count >= 3)
                { Sync(null, EventArgs.Empty); message.Text = "Desmarque um bloco antes de adicionar outro."; return; }
                message.Text = "";
                Change(p => { if (selected) { if (!p.CompactMonitoringMetrics.Contains(id)) p.CompactMonitoringMetrics.Add(id); } else p.CompactMonitoringMetrics.Remove(id); });
            }
            box.Checked += (_, _) => ChangeSelection(true); box.Unchecked += (_, _) => ChangeSelection(false);
            choices.Add((id, box)); body.Children.Add(box);
        }
        body.Children.Add(message);
        body.Loaded += (_, _) => { store.Changed += Sync; Sync(null, EventArgs.Empty); };
        body.Unloaded += (_, _) => store.Changed -= Sync;
        Sync(null, EventArgs.Empty);
        return body;
    }
}
