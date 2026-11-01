using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class AppMixerControl : StackPanel, IDisposable
{
    private sealed class Row
    {
        internal AppAudioState State;
        internal bool Updating;
        internal readonly Border Root;
        internal readonly Slider Slider = new() { Minimum = 0, Maximum = 100, SmallChange = 1, LargeChange = 5, IsMoveToPointEnabled = true, Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
        internal readonly Button Mute = new() { Width = 30, Height = 32, Padding = new Thickness(2), Margin = new Thickness(0, 0, 6, 0) };
        internal readonly TextBlock Percent = new() { MinWidth = 40, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right };
        internal readonly TextBlock Title = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6) };
        internal Row(AppAudioState state, Action<Row, double?, bool?> queue)
        {
            State = state;
            var panel = new StackPanel(); panel.Children.Add(Title);
            var controls = new DockPanel(); Mute.SetResourceReference(StyleProperty, "Think.IconButton");
            DockPanel.SetDock(Mute, Dock.Left); controls.Children.Add(Mute); DockPanel.SetDock(Percent, Dock.Right); controls.Children.Add(Percent); controls.Children.Add(Slider);
            var capsule = new Border { Child = controls, CornerRadius = new CornerRadius(22), MinHeight = 44, Padding = new Thickness(4, 0, 8, 0), BorderThickness = new Thickness(1) };
            capsule.SetResourceReference(Border.BackgroundProperty, "Think.Control"); capsule.SetResourceReference(Border.BorderBrushProperty, "Think.Hover"); panel.Children.Add(capsule);
            Root = new Border { Child = panel, CornerRadius = new CornerRadius(16), Padding = new Thickness(10), Margin = new Thickness(0, 4, 0, 4) };
            Root.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
            Slider.ValueChanged += (_, _) => { if (!Updating) { Percent.Text = $"{Slider.Value:0}%"; queue(this, Slider.Value, null); } };
            Mute.Click += (_, _) => queue(this, null, !State.Muted);
            System.Windows.Automation.AutomationProperties.SetName(Slider, "Volume de " + state.Name);
            Apply(state);
        }
        internal void Apply(AppAudioState state)
        {
            State = state; Title.Text = state.Name;
            Root.ToolTip = state.Sessions.Length > 1 ? $"{state.Sessions.Length} sessões de áudio. O controle altera todas as sessões listadas deste aplicativo." : "Sessão de áudio do Windows";
            Updating = true;
            try { if (!Slider.IsMouseCaptureWithin) Slider.Value = state.Volume; }
            finally { Updating = false; }
            Percent.Text = $"{Slider.Value:0}%";
            Mute.Content = new TextBlock { Text = state.Muted ? "\uE74F" : "\uE767", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17 };
            Mute.ToolTip = state.Muted ? "Ativar som de " + state.Name : "Silenciar " + state.Name;
            System.Windows.Automation.AutomationProperties.SetName(Mute, Mute.ToolTip.ToString());
            Mute.SetResourceReference(Control.BackgroundProperty, state.Muted ? "Think.AccentSurface" : "Think.Control");
        }
    }
    private sealed record Change(Row Row, double? Volume, bool? Mute);
    private readonly Dictionary<string, Row> rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Change> changes = new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel list = new();
    private readonly TextBlock status = new() { Text = "Abra um aplicativo e reproduza áudio para vê-lo aqui.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 6) };
    private readonly ScrollViewer scroll;
    private readonly DispatcherTimer throttle = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private CancellationTokenSource? lifetime;
    private bool active, reading, writing, disposed;
    private DateTime nextRead;
    internal event Action? LayoutChanged;
    internal bool IsInteracting => IsMouseCaptureWithin || writing || changes.Count > 0;
    internal double MaximumListHeight { set => scroll.MaxHeight = value; }
    internal AppMixerControl()
    {
        status.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); Children.Add(status);
        scroll = new ScrollViewer { Content = list, MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer"); Children.Add(scroll);
        throttle.Tick += async (_, _) => { throttle.Stop(); await FlushChanges(); };
    }
    internal void SetActive(bool value)
    {
        if (disposed || active == value) return;
        active = value;
        if (!value) { lifetime?.Cancel(); throttle.Stop(); changes.Clear(); return; }
        lifetime?.Dispose(); lifetime = new CancellationTokenSource(); nextRead = DateTime.MinValue; Poll();
    }
    internal async void Poll()
    {
        if (!active || disposed || reading || writing || IsInteracting || DateTime.UtcNow < nextRead || lifetime is null) return;
        reading = true; var token = lifetime.Token; nextRead = DateTime.UtcNow.AddSeconds(1.5);
        try
        {
            var apps = await AppAudioMixer.ReadAsync(token);
            if (token.IsCancellationRequested || !active) return;
            if (IsInteracting) return;
            var ids = apps.Select(app => app.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            bool layout = false;
            foreach (var id in rows.Keys.Where(id => !ids.Contains(id)).ToArray()) { list.Children.Remove(rows[id].Root); rows.Remove(id); layout = true; }
            for (int index = 0; index < apps.Length; index++)
            {
                var app = apps[index];
                if (!rows.TryGetValue(app.Id, out var row)) { row = new Row(app, Queue); rows[app.Id] = row; layout = true; }
                else row.Apply(app);
                if (index >= list.Children.Count || !ReferenceEquals(list.Children[index], row.Root)) { list.Children.Remove(row.Root); list.Children.Insert(index, row.Root); layout = true; }
            }
            status.Text = apps.Length == 0 ? "Nenhum aplicativo com sessão ativa ou silenciada no momento." : "Volume por aplicativo · todas as saídas de áudio. Sessões silenciadas continuam disponíveis para reativar.";
            if (layout) LayoutChanged?.Invoke();
        }
        catch (OperationCanceledException) { }
        catch (AudioUnavailableException) { status.Text = "Não foi possível consultar o mixer do Windows. Tente abrir o painel novamente."; }
        finally { reading = false; if (changes.Count > 0 && active && !throttle.IsEnabled) throttle.Start(); }
    }
    private void Queue(Row row, double? volume, bool? mute)
    {
        if (!active || disposed) return;
        changes.TryGetValue(row.State.Id, out var old);
        changes[row.State.Id] = new(row, volume ?? old?.Volume, mute.HasValue ? !(old?.Mute ?? row.State.Muted) : old?.Mute);
        if (!throttle.IsEnabled) throttle.Start();
    }
    private async Task FlushChanges()
    {
        if (!active || disposed || writing || reading || lifetime is null) return;
        writing = true; var token = lifetime.Token;
        var batch = changes.Values.ToArray(); changes.Clear();
        try
        {
            foreach (var change in batch)
            {
                if (token.IsCancellationRequested) break;
                if (!await AppAudioMixer.SetAsync(change.Row.State, change.Volume, change.Mute, token)) status.Text = "Uma sessão foi encerrada ou não confirmou a alteração. A lista será sincronizada novamente.";
            }
        }
        catch (OperationCanceledException) { }
        catch (AudioUnavailableException) { status.Text = "O Windows não confirmou a alteração de áudio."; }
        finally
        {
            writing = false; nextRead = DateTime.MinValue;
            if (changes.Count > 0 && active) throttle.Start(); else if (active) Poll();
        }
    }
    public void Dispose() { disposed = true; active = false; throttle.Stop(); lifetime?.Cancel(); lifetime?.Dispose(); changes.Clear(); rows.Clear(); list.Children.Clear(); }
}
