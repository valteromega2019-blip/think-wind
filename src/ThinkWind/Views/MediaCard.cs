using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed partial class MediaCard : Grid, IDisposable
{
    private readonly MediaService service;
    private readonly Grid body = new() { Margin = new Thickness(12) };
    private readonly Grid metadataRow = new();
    private FrameworkElement? windowActions;
    private readonly ImageBrush artworkBackground = new() { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
    private readonly Border backgroundCover, backgroundShade;
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
    private bool dockPreview;
    private readonly bool keepIdlePreview;
    internal void ConfigureDockPreview()
    {
        dockPreview = true;
        body.Margin = new Thickness(16, 14, 16, 14);
        body.VerticalAlignment = VerticalAlignment.Center;
        MinHeight = 190;
        title.FontSize = 18;
        artist.FontSize = 14;
        player.FontSize = time.FontSize = 13;
        progress.Width = 280;
        progress.HorizontalAlignment = HorizontalAlignment.Center;
        progress.LayoutTransform = new ScaleTransform(.7, .7);
        progress.Margin = new Thickness(0, 5, 0, 0);
        time.FontSize = 11;
        foreach (var button in new[] { previous, toggle, next })
        {
            button.Width = button.Height = 40;
            button.Margin = new Thickness(5, 8, 5, 0);
        }
        Render();
    }
    internal bool Available => service.State is not null;
    internal async Task<bool> PreparePreviewAsync()
    {
        await service.SetActiveAsync(true);
        return Available;
    }
    internal bool IsInteracting => seeking || dragging || busy || volumeBusy || IsMouseCaptureWithin;
    internal event Action? AvailabilityChanged;
    internal event Action? LayoutChanged;
    internal event Action? PreviewPlaybackStopped;
    internal void ConfigureWidgetCorners(double radius)
    {
        backgroundCover.CornerRadius = backgroundShade.CornerRadius = new CornerRadius(Math.Clamp(radius, 0, 28));
    }
    internal void SetWindowActions(FrameworkElement actions)
    {
        if (windowActions is not null) Children.Remove(windowActions);
        windowActions = actions;
        actions.Margin = new Thickness(0, 6, 6, 0);
        Panel.SetZIndex(actions, 10);
        metadataRow.Margin = new Thickness(0, 20, 0, 0);
        Children.Add(actions);
    }
    internal void ConfigurePreviewSurface(double radius, double opacity)
    {
        var corners = new CornerRadius(Math.Max(0, radius));
        backgroundCover.CornerRadius = backgroundShade.CornerRadius = corners;
        // Only the artwork layers follow surface opacity. Text and controls
        // retain their contrast and hit areas.
        backgroundCover.Opacity = backgroundShade.Opacity = SystemParameters.HighContrast ? 1 : Math.Clamp(opacity, .05, 1);
    }
    internal MediaCard(string? sourceFilter = null, MusicPreviewSelection? selection = null)
    {
        keepIdlePreview = selection?.KeepIdle == true;
        service = new MediaService(Dispatcher, sourceFilter, selection);
        backgroundCover = new Border { Background = artworkBackground, CornerRadius = new CornerRadius(20), IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        backgroundShade = new Border { CornerRadius = new CornerRadius(20), IsHitTestVisible = false, Visibility = Visibility.Collapsed,
            Background = new LinearGradientBrush(Color.FromArgb(155, 8, 12, 18), Color.FromArgb(220, 8, 12, 18), 90) };
        RenderOptions.SetBitmapScalingMode(backgroundCover, BitmapScalingMode.HighQuality);
        Children.Add(backgroundCover); Children.Add(backgroundShade); Children.Add(body);
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < 3; i++) body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var row = metadataRow; row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); row.ColumnDefinitions.Add(new ColumnDefinition());
        cover = new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), ClipToBounds = true, Child = artwork, Margin = new Thickness(0, 0, 8, 0) };
        artwork.Width = artwork.Height = 44;
        artwork.Clip = new RectangleGeometry(new Rect(0, 0, 44, 44), 12, 12);
        cover.SetResourceReference(Border.BackgroundProperty, "Think.Control"); row.Children.Add(cover);
        var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; details.Children.Add(title); details.Children.Add(artist); details.Children.Add(player);
        artist.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); player.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); time.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        Grid.SetColumn(details, 1); row.Children.Add(details); body.Children.Add(row);
        transport = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        previous = Transport("Anterior", "\uE892", MediaCommand.Previous); toggle = Transport("Reproduzir / pausar", "\uE768", MediaCommand.Toggle); next = Transport("Próxima", "\uE893", MediaCommand.Next);
        transport.Children.Add(previous); transport.Children.Add(toggle); transport.Children.Add(next); body.Children.Add(transport); body.Children.Add(progress); body.Children.Add(time);
        Grid.SetRow(transport, 1); Grid.SetRow(progress, 2); Grid.SetRow(time, 3);
        foreach (var element in new FrameworkElement[] { metadataRow, transport, progress, time }) Grid.SetColumnSpan(element, 2);
        SizeChanged += (_, _) => FitWidgetLayout();
        System.Windows.Automation.AutomationProperties.SetName(progress, "Posição da reprodução");
        progress.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((_, _) => { seeking = true; dragging = true; }));
        progress.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(async (_, e) => { dragging = seeking = false; if (!e.Canceled) await Seek(); }));
        progress.PreviewMouseLeftButtonDown += (_, _) => seeking = true;
        progress.PreviewMouseLeftButtonUp += async (_, _) => { if (!seeking || dragging) return; seeking = false; await Seek(); };
        progress.PreviewKeyDown += (_, e) => { if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageUp or Key.PageDown) seeking = true; };
        progress.PreviewKeyUp += async (_, _) => { if (seeking) { seeking = false; await Seek(); } };
        progress.LostKeyboardFocus += (_, _) => { seeking = false; UpdateProgress(); };
        service.Changed += Render; ThemeService.Changed += AppearanceChanged; timer.Tick += (_, _) => UpdateProgress(); Render();
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
        UpdateArtworkBackground(state?.Artwork);
        cover.Visibility = Available && (!dockPreview || state?.Artwork is not null) ? Visibility.Visible : Visibility.Collapsed;
        transport.Visibility = Available ? Visibility.Visible : Visibility.Collapsed;
        string nextTitle = string.IsNullOrWhiteSpace(state?.Title) ? (Available ? "Reprodução em " + state!.Player : "Nenhuma mídia ativa") : state!.Title;
        string nextArtist = state?.Artist ?? service.Error ?? "Abra um player compatível com o Windows";
        string nextPlayer = state?.Player ?? "";
        bool metadataChanged = title.Text != nextTitle || artist.Text != nextArtist || player.Text != nextPlayer;
        title.Text = nextTitle; artist.Text = nextArtist; player.Text = nextPlayer;
        artist.Visibility = string.IsNullOrWhiteSpace(nextArtist) ? Visibility.Collapsed : Visibility.Visible;
        toggle.Content = Icon(state?.Playing == true ? "\uE769" : "\uE768");
        title.ToolTip = title.Text; artist.ToolTip = artist.Text; player.ToolTip = player.Text;
        RenderButtons(); UpdateProgress(); FitWidgetLayout();
        if (dockPreview && IsVisible && state?.Playing != true && (!keepIdlePreview || state is null)) PreviewPlaybackStopped?.Invoke();
        if (wasAvailable != Available) AvailabilityChanged?.Invoke();
        if (wasAvailable != Available || metadataChanged) { InvalidateMeasure(); LayoutChanged?.Invoke(); }
    }
    private void FitWidgetLayout()
    {
        bool wide = !dockPreview && ActualWidth >= 480;
        Grid.SetColumnSpan(metadataRow, wide ? 1 : 2);
        Grid.SetColumn(transport, wide ? 1 : 0);
        Grid.SetColumnSpan(transport, wide ? 1 : 2);
        Grid.SetRow(transport, wide ? 0 : 1);
        transport.VerticalAlignment = VerticalAlignment.Center;
        metadataRow.VerticalAlignment = VerticalAlignment.Center;
        if (dockPreview) return;
        // Bound metadata at each breakpoint; long names remain available in
        // tooltips instead of pushing transport/progress outside the widget.
        title.MaxHeight = Math.Ceiling(title.FontSize * 2.8);
        artist.MaxHeight = Math.Ceiling(artist.FontSize * 1.5);
        title.TextTrimming = artist.TextTrimming = TextTrimming.CharacterEllipsis;
        player.TextWrapping = TextWrapping.NoWrap; player.TextTrimming = TextTrimming.CharacterEllipsis;
        double size = wide ? Math.Clamp(Math.Round(ActualWidth * .12), 44, 72) : 44;
        if (cover.Width != size)
        {
            cover.Width = cover.Height = artwork.Width = artwork.Height = size;
            artwork.Clip = new RectangleGeometry(new Rect(0, 0, size, size), 12, 12);
        }
        progress.Width = double.NaN;
        progress.HorizontalAlignment = HorizontalAlignment.Stretch;
    }
    private void UpdateArtworkBackground(ImageSource? source)
    {
        bool visible = source is not null && !SystemParameters.HighContrast;
        artworkBackground.ImageSource = source;
        backgroundCover.Visibility = backgroundShade.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        // These resources are local to this player. No change to the global
        // theme/accent, and no image decoding or blur loop during playback.
        if (visible && (!Resources.Contains("Think.Text") || !ReferenceEquals(Resources["Think.Text"], Application.Current.Resources["Think.ArtworkText"])))
        {
            Resources["Think.Text"] = Resources["Think.Muted"] = Application.Current.Resources["Think.ArtworkText"];
        }
        else if (!visible) { Resources.Remove("Think.Text"); Resources.Remove("Think.Muted"); }
        player.SetResourceReference(TextBlock.ForegroundProperty, visible ? "Think.Muted" : "Think.Accent");
        progress.SetResourceReference(Control.ForegroundProperty, visible ? "Think.Text" : "Think.Accent");
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
    internal async void SetActive(bool active) { SetVolumeActive(active); if (active) timer.Start(); else { timer.Stop(); seeking = false; } await service.SetActiveAsync(active); }
    private void AppearanceChanged() => UpdateArtworkBackground(service.State?.Artwork);
    public void Dispose() { SetVolumeActive(false); timer.Stop(); ThemeService.Changed -= AppearanceChanged; service.Changed -= Render; service.Dispose(); }
}
