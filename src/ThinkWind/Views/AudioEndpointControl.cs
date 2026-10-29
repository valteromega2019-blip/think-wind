using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class AudioEndpointControl : Border
{
    private sealed record Choice(string? Id, string Label)
    {
        public override string ToString() => Label;
    }
    private readonly AudioService audio;
    private readonly CoreAudio.Flow flow;
    private readonly ComboBox devices = new() { DisplayMemberPath = nameof(Choice.Label), Margin = new Thickness(0, 8, 0, 6), MinHeight = 28 };
    private readonly Slider level = new() { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 5, VerticalAlignment = VerticalAlignment.Center, IsEnabled = false };
    private readonly TextBlock percent = new() { Width = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = .8, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button mute = new() { Content = "Mudo", IsEnabled = false, MinWidth = 78, Padding = new Thickness(8, 6, 8, 6) };
    private AudioState? current;
    private string deviceSignature = "";
    private bool updating;
    private bool dragging;

    internal AudioEndpointControl(AudioService audioService, CoreAudio.Flow direction)
    {
        audio = audioService; flow = direction;
        Margin = new Thickness(0, 4, 0, 4); Padding = new Thickness(12);
        CornerRadius = new CornerRadius(18);
        SetResourceReference(BackgroundProperty, "Think.GlassCard");
        var panel = new StackPanel();
        var name = direction == CoreAudio.Flow.Render ? "SAÍDA DE ÁUDIO" : "MICROFONE";
        panel.Children.Add(new TextBlock { Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold });
        System.Windows.Automation.AutomationProperties.SetName(devices, "Dispositivo a controlar · " + name);
        System.Windows.Automation.AutomationProperties.SetName(level, "Volume · " + name);
        System.Windows.Automation.AutomationProperties.SetName(mute, "Alternar mudo · " + name);
        devices.ToolTip = "Seleciona o dispositivo a controlar; não altera a saída ou entrada padrão do Windows.";
        panel.Children.Add(devices);
        var row = new DockPanel();
        DockPanel.SetDock(mute, Dock.Right); row.Children.Add(mute);
        DockPanel.SetDock(percent, Dock.Right); row.Children.Add(percent);
        row.Children.Add(level); panel.Children.Add(row); panel.Children.Add(status); Child = panel;
        devices.SelectionChanged += (_, _) => { if (!updating) Poll(false); };
        level.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => dragging = true));
        level.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => { dragging = false; Poll(false); }));
        level.ValueChanged += (_, _) =>
        {
            var target = current;
            if (updating || target is null) return;
            if (Execute(() => audio.SetVolume(flow, target.Id, level.Value))) percent.Text = $"{level.Value:0}%";
        };
        mute.Click += (_, _) =>
        {
            var target = current;
            if (target is null) return;
            // Read again to avoid toggling a stale mute state changed elsewhere.
            Execute(() => { var latest = audio.Read(flow, target.Id); audio.SetMuted(flow, latest.Id, !latest.Muted); });
            Poll(false);
        };
    }

    internal void Poll(bool refreshDevices)
    {
        if (dragging) return;
        try
        {
            updating = true;
            if (refreshDevices || devices.Items.Count == 0)
            {
                var available = audio.Devices(flow);
                var signature = string.Join("|", available.Select(d => d.Id + ":" + d.Name));
                if (devices.Items.Count == 0 || signature != deviceSignature)
                {
                    var selected = (devices.SelectedItem as Choice)?.Id;
                    devices.Items.Clear();
                    devices.Items.Add(new Choice(null, "Seguir padrão do Windows"));
                    foreach (var device in available) devices.Items.Add(new Choice(device.Id, device.Name));
                    // Keep the user's explicit target even when it disconnects.
                    // Never silently redirect a volume/mute action to another device.
                    if (selected is not null && !available.Any(d => d.Id == selected))
                        devices.Items.Add(new Choice(selected, "Dispositivo desconectado"));
                    devices.SelectedItem = devices.Items.Cast<Choice>().FirstOrDefault(c => c.Id == selected) ?? devices.Items[0];
                    deviceSignature = signature;
                }
            }
            current = audio.Read(flow, (devices.SelectedItem as Choice)?.Id);
            // Programmatic refresh must never write back to the endpoint.
            // Mouse drags are excluded above; keyboard input runs synchronously.
            level.Value = current.Volume;
            percent.Text = $"{current.Volume:0}%";
            mute.Content = current.Muted ? "Ativar som" : "Mudo";
            mute.SetResourceReference(Control.BackgroundProperty, current.Muted ? "Think.Accent" : "Think.Control");
            mute.SetResourceReference(Control.ForegroundProperty, current.Muted ? "Think.Background" : "Think.Text");
            status.Text = current.Name + (current.Muted ? " · silenciado" : "");
            level.IsEnabled = mute.IsEnabled = true;
        }
        catch (AudioUnavailableException e) { Unavailable(e.Message); }
        finally { updating = false; }
    }

    private bool Execute(Action action)
    {
        try { action(); return true; }
        catch (AudioUnavailableException e) { Unavailable(e.Message); return false; }
    }
    private void Unavailable(string message)
    {
        current = null; status.Text = message; percent.Text = "—";
        level.IsEnabled = mute.IsEnabled = false;
    }
}
