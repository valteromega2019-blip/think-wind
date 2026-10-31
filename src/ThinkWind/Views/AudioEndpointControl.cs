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
    private readonly TextBlock percent = new() { MinWidth = 42, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, FontSize = 11, Opacity = .8, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Button mute = new() { Content = "Mudo", IsEnabled = false, MinWidth = 78, Padding = new Thickness(8, 6, 8, 6) };
    private AudioState? current;
    private string deviceSignature = "";
    private bool updating;
    private bool dragging;
    private bool compact;
    private readonly Button devicePicker = new() { Content = "\u22EF", Width = 28, Height = 24, Padding = new Thickness(0), Margin = new Thickness(0), Visibility = Visibility.Collapsed, ToolTip = "Escolher dispositivo a controlar" };
    private ContextMenu? deviceMenu;
    private AudioObserver? observer;
    private string? selectionError;
    internal event Action? LayoutChanged;
    internal bool IsInteracting => dragging || IsMouseCaptureWithin || devices.IsDropDownOpen || deviceMenu?.IsOpen == true;

    internal AudioEndpointControl(AudioService audioService, CoreAudio.Flow direction)
    {
        audio = audioService; flow = direction;
        Margin = new Thickness(0, 4, 0, 4); Padding = new Thickness(12);
        CornerRadius = new CornerRadius(18);
        SetResourceReference(BackgroundProperty, "Think.GlassCard");
        var panel = new StackPanel();
        var name = direction == CoreAudio.Flow.Render ? "SAÍDA DE ÁUDIO" : "MICROFONE";
        var heading = new DockPanel(); DockPanel.SetDock(devicePicker, Dock.Right); heading.Children.Add(devicePicker);
        heading.Children.Add(new TextBlock { Text = name, FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }); panel.Children.Add(heading);
        devicePicker.Click += (_, _) => OpenDeviceMenu();
        System.Windows.Automation.AutomationProperties.SetName(devicePicker, "Escolher dispositivo · " + name);
        System.Windows.Automation.AutomationProperties.SetName(devices, "Dispositivo a controlar · " + name);
        System.Windows.Automation.AutomationProperties.SetName(level, "Volume · " + name);
        System.Windows.Automation.AutomationProperties.SetName(mute, "Alternar mudo · " + name);
        devices.ToolTip = direction == CoreAudio.Flow.Render ? "Altera a saída padrão do Windows para sons do sistema e multimídia." : "Altera o microfone padrão do Windows e das comunicações.";
        devicePicker.ToolTip = devices.ToolTip;
        panel.Children.Add(devices);
        var row = new DockPanel();
        DockPanel.SetDock(mute, Dock.Right); row.Children.Add(mute);
        DockPanel.SetDock(percent, Dock.Right); row.Children.Add(percent);
        row.Children.Add(level); panel.Children.Add(row); panel.Children.Add(status); Child = panel;
        devices.SelectionChanged += (_, _) =>
        {
            if (updating) return;
            selectionError = null;
            if (devices.SelectedItem is Choice choice && choice.Id is not null)
            {
                try { if (flow == CoreAudio.Flow.Render) audio.SetDefaultOutput(choice.Id); else audio.SetDefaultInput(choice.Id); }
                catch (AudioUnavailableException) { selectionError = flow == CoreAudio.Flow.Render
                    ? "O Windows não confirmou a troca de saída. A seleção foi sincronizada novamente com o sistema."
                    : "O Windows não confirmou a troca do microfone. A seleção foi sincronizada novamente com o sistema."; }
            }
            observer?.Rebind(); Poll(true); LayoutChanged?.Invoke();
        };
        Loaded += (_, _) => Observe(); Unloaded += (_, _) => { observer?.Dispose(); observer = null; };
        IsVisibleChanged += (_, _) => Observe();
        level.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => dragging = true));
        level.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((_, _) => { dragging = false; Poll(false); }));
        level.ValueChanged += (_, _) =>
        {
            var target = current;
            if (updating || target is null) return;
            Execute(() =>
            {
                var actual = audio.Read(flow, null);
                audio.SetVolume(flow, actual.Id, level.Value); current = audio.Read(flow, actual.Id); percent.Text = $"{current.Volume:0}%";
            });
        };
        mute.Click += (_, _) =>
        {
            var target = current;
            if (target is null) return;
            // Read again to avoid toggling a stale mute state changed elsewhere.
            Execute(() => { var latest = audio.Read(flow, null); audio.SetMuted(flow, latest.Id, !latest.Muted); });
            Poll(false);
        };
    }

    internal void Poll(bool refreshDevices)
    {
        if (dragging || deviceMenu?.IsOpen == true || devices.IsDropDownOpen) return;
        try
        {
            updating = true;
            var systemDefault = audio.Read(flow, null);
            if (refreshDevices || devices.Items.Count == 0 || !devices.Items.Cast<Choice>().Any(choice => choice.Id == systemDefault.Id))
            {
                var available = audio.Devices(flow);
                var signature = string.Join("|", available.Select(d => d.Id + ":" + d.Name));
                if (devices.Items.Count == 0 || signature != deviceSignature)
                {
                    var selected = systemDefault.Id;
                    devices.Items.Clear();
                    foreach (var device in available) devices.Items.Add(new Choice(device.Id, device.Name));
                    // Always show the endpoint actually reported by Windows.
                    if (!available.Any(d => d.Id == selected))
                        devices.Items.Add(new Choice(selected, "Dispositivo desconectado"));
                    devices.SelectedItem = devices.Items.Cast<Choice>().FirstOrDefault(c => c.Id == selected) ?? (devices.Items.Count > 0 ? devices.Items[0] : null);
                    deviceSignature = signature;
                }
            }
            current = systemDefault;
            devices.SelectedItem = devices.Items.Cast<Choice>().FirstOrDefault(choice => choice.Id == systemDefault.Id);
            // Programmatic refresh must never write back to the endpoint.
            // Mouse drags are excluded above; keyboard input runs synchronously.
            level.Value = current.Volume;
            percent.Text = $"{current.Volume:0}%";
            mute.Content = compact ? MuteGlyph(current.Muted) : current.Muted ? "Ativar som" : "Mudo";
            mute.ToolTip = current.Muted ? "Ativar som" : "Silenciar";
            mute.SetResourceReference(Control.BackgroundProperty, current.Muted ? "Think.AccentSurface" : "Think.Control");
            mute.SetResourceReference(Control.ForegroundProperty, "Think.Text");
            status.Text = selectionError ?? current.Name + (current.Muted ? " · silenciado" : "");
            status.Visibility = compact && selectionError is null ? Visibility.Collapsed : Visibility.Visible;
            ToolTip = status.Text;
            level.IsEnabled = mute.IsEnabled = true;
        }
        catch (AudioUnavailableException e) { Unavailable(e.Message); }
        finally { updating = false; }
    }
    private void Observe()
    {
        if (!IsLoaded || !IsVisible) { observer?.Dispose(); observer = null; return; }
        observer ??= new AudioObserver(Dispatcher, flow, () => null, refresh => Poll(refresh));
        Poll(true);
    }
    internal void SetCompact(bool value)
    {
        compact = value;
        devices.Visibility = status.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        devicePicker.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        Padding = new Thickness(value ? 4 : 12); Margin = new Thickness(0, 2, 0, 2);
        percent.Width = double.NaN; percent.MinWidth = value ? 40 : 42; percent.FontSize = 12;
        mute.MinWidth = value ? 28 : 78; mute.Width = value ? 28 : double.NaN; mute.Height = value ? 28 : double.NaN;
        mute.Padding = new Thickness(value ? 0 : 8); mute.Margin = new Thickness(3, 0, 0, 0);
        if (current is not null) mute.Content = value ? MuteGlyph(current.Muted) : current.Muted ? "Ativar som" : "Mudo";
    }
    private static TextBlock MuteGlyph(bool muted)
    {
        var glyph = new TextBlock { Text = muted ? "\uE74F" : "\uE767", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14 };
        glyph.SetResourceReference(TextBlock.ForegroundProperty, muted ? "Think.AccentInk" : "Think.Text"); return glyph;
    }
    private void OpenDeviceMenu()
    {
        Poll(true); deviceMenu = new ContextMenu { PlacementTarget = devicePicker };
        foreach (var choice in devices.Items.Cast<Choice>())
        {
            var item = new MenuItem { Header = choice.Label, IsCheckable = true, IsChecked = Equals(choice, devices.SelectedItem) };
            item.Click += (_, _) => devices.SelectedItem = choice; deviceMenu.Items.Add(item);
        }
        deviceMenu.IsOpen = true;
        deviceMenu.Closed += (_, _) => Poll(true);
    }

    private bool Execute(Action action)
    {
        try { action(); return true; }
        catch (AudioUnavailableException e) { Unavailable(e.Message); return false; }
    }
    private void Unavailable(string message)
    {
        current = null; status.Text = message; ToolTip = message; percent.Text = "—";
        status.Visibility = Visibility.Visible;
        level.IsEnabled = mute.IsEnabled = false;
    }
}
