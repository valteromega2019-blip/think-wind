using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class MediaCard : StackPanel, IDisposable
{
    private readonly MediaService service;
    private readonly Image artwork = new() { Width = 64, Height = 64, Stretch = Stretch.UniformToFill };
    private readonly TextBlock title = new() { FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock artist = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock player = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock time = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly Slider progress = new() { Minimum = 0, Margin = new Thickness(0, 8, 0, 0), IsMoveToPointEnabled = true };
    private readonly Button previous, toggle, next;
    private readonly Border cover;
    private readonly StackPanel transport;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool seeking, dragging, busy;
    internal bool Available => service.State is not null;
    internal bool IsInteracting => seeking || dragging || busy || IsMouseCaptureWithin;
    internal event Action? AvailabilityChanged;
    internal event Action? LayoutChanged;
    internal MediaCard()
    {
        service = new MediaService(Dispatcher);
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition());
        cover = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = artwork, Margin = new Thickness(0, 0, 8, 0) };
        artwork.Width = artwork.Height = 44;
        artwork.Clip = new RectangleGeometry(new Rect(0, 0, 44, 44), 12, 12);
        cover.SetResourceReference(Border.BackgroundProperty, "Think.Control"); row.Children.Add(cover);
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; details.Children.Add(title); details.Children.Add(artist); details.Children.Add(player);
        artist.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); player.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        Grid.SetColumn(details, 1); row.Children.Add(details); Children.Add(row);
        transport = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        previous = Transport("Anterior", "\uE892", MediaCommand.Previous); toggle = Transport("Reproduzir / pausar", "\uE768", MediaCommand.Toggle); next = Transport("Próxima", "\uE893", MediaCommand.Next);
        transport.Children.Add(previous); transport.Children.Add(toggle); transport.Children.Add(next); Children.Add(transport); Children.Add(progress); Children.Add(time);
        System.Windows.Automation.AutomationProperties.SetName(progress, "Posição da reprodução");
        progress.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => { seeking = true; dragging = true; }));
        progress.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, e) => { dragging = seeking = false; if (!e.Canceled) await Seek(); }));
        progress.PreviewMouseLeftButtonDown += (_, _) => seeking = true;
        progress.PreviewMouseLeftButtonUp += async (_, _) => { if (!seeking || dragging) return; seeking = false; await Seek(); };
        progress.PreviewKeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) seeking = true; };
        progress.PreviewKeyUp += async (_, _) => { if (seeking) { seeking = false; await Seek(); } };
        progress.LostKeyboardFocus += (_, _) => { seeking = false; UpdateProgress(); };
        service.Changed += Render; timer.Tick += (_, _) => UpdateProgress(); Render();
    }
    private Button Transport(string label, string glyph, MediaCommand command)
    {
        var button = new Button { Width = 32, Height = 32, Padding = new Thickness(0), Margin = new Thickness(3, 6, 3, 0), ToolTip = label, Content = Icon(glyph) };
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += async (_, _) =>
        {
            if (busy) return;
            busy = true; RenderButtons();
            try { if (!await service.ExecuteAsync(command)) time.Text = "Comando indisponível neste player"; }
            finally { busy = false; RenderButtons(); }
        };
        return button;
    }
    private static TextBlock Icon(string glyph) => new() { Text = glyph, FontSize = 19, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), HorizontalAlignment = HorizontalAlignment.Center };
    private async Task Seek() { if (!busy && service.State?.CanSeek == true) { busy = true; try { await service.SeekAsync(TimeSpan.FromSeconds(progress.Value)); } finally { busy = false; UpdateProgress(); } } }
    private void Render()
    {
        bool wasAvailable = Tag is true; Tag = Available;
        var state = service.State; artwork.Source = state?.Artwork;
        cover.Visibility = transport.Visibility = Available ? Visibility.Visible : Visibility.Collapsed;
        string nextTitle = state?.Title ?? "Nenhuma mídia ativa";
        string nextArtist = state?.Artist ?? service.Error ?? "Abra um player compatível com o Windows";
        string nextPlayer = state?.Player ?? "";
        bool metadataChanged = title.Text != nextTitle || artist.Text != nextArtist || player.Text != nextPlayer;
        title.Text = nextTitle; artist.Text = nextArtist; player.Text = nextPlayer;
        toggle.Content = Icon(state?.Playing == true ? "\uE769" : "\uE768");
        title.ToolTip = title.Text; artist.ToolTip = artist.Text; player.ToolTip = player.Text;
        RenderButtons(); UpdateProgress();
        if (wasAvailable != Available) AvailabilityChanged?.Invoke();
        if (wasAvailable != Available || metadataChanged) { InvalidateMeasure(); LayoutChanged?.Invoke(); }
    }
    private void RenderButtons() { previous.IsEnabled = !busy && service.State?.CanPrevious == true; next.IsEnabled = !busy && service.State?.CanNext == true; toggle.IsEnabled = !busy && service.State?.CanToggle == true; }
    private void UpdateProgress()
    {
        var state = service.State; bool known = state is not null && state.End > state.Start;
        bool changed = (progress.Visibility == Visibility.Visible) != known;
        progress.Visibility = time.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
        if (changed) LayoutChanged?.Invoke();
        progress.IsEnabled = known && state!.CanSeek && !busy;
        if (!known || seeking) return;
        progress.Maximum = Math.Max(1, (state!.End - state.Start).TotalSeconds);
        progress.Value = Math.Clamp((state.CurrentPosition() - state.Start).TotalSeconds, 0, progress.Maximum);
        time.Text = Format(TimeSpan.FromSeconds(progress.Value)) + " / " + Format(state.End - state.Start);
    }
    private static string Format(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
    internal async void SetActive(bool active) { if (active) timer.Start(); else { timer.Stop(); seeking = false; } await service.SetActiveAsync(active); }
    public void Dispose() { timer.Stop(); service.Changed -= Render; service.Dispose(); }
}
