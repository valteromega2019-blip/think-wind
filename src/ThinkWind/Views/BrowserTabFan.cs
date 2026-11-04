using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class BrowserTabFan : Canvas, IDisposable
{
    private readonly List<(Border Area, WindowThumbnail Host)> images = new();
    private readonly List<Border> surfaces = new();
    private readonly List<FrameworkElement> cards = new();
    private readonly List<ContextMenu> tabMenus = new();
    internal bool IsInteracting => openingWindow || tabMenus.Any(menu => menu.IsOpen);
    private readonly List<BackdropHost> cardBackdrops = new();
    private readonly List<(FrameworkElement Panel, BackdropHost Host)> companionBackdrops = new();
    private double cardRadius;
    private readonly List<(Border Area, nint Window, bool Live)> cardImages = new();
    private readonly List<bool> cachedImages = new();
    private int hoveredCard = -1;
    private nint thumbnailOwner;
    private bool liveImageAttempted;
    private Point lastPointer = new(double.NaN, double.NaN);
    private readonly Button expansion = new() { Height = 30, Width = 76, Padding = new Thickness(8, 3, 8, 3) };
    private readonly TextBlock extraCount = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock expansionIcon = new() { FontSize = 14, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private int previewCard = -1;
    private int hiddenTabCount;
    private bool openingWindow;
    internal event Action? LayoutChanged;
    internal event Action? DismissRequested;
    internal event Action<nint>? WindowHovered;
    internal event Action? WindowHoverEnded;
    internal void AttachPanelMaterial(FrameworkElement panel)
    {
        var host = new BackdropHost(thumbnailOwner, false, 0, "Orby — material da integração");
        host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
        companionBackdrops.Add((panel, host));
    }
    internal BrowserTabFan()
    {
        Width = 320;
        expansion.SetResourceReference(StyleProperty, "Think.IconButton");
        expansion.SetResourceReference(Control.ForegroundProperty, "Think.Accent");
        expansion.SetResourceReference(Control.BackgroundProperty, "Think.Control");
        var expansionContent = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        expansionContent.Children.Add(extraCount); expansionContent.Children.Add(expansionIcon); expansion.Content = expansionContent;
        expansion.Click += async (_, _) =>
        {
            if (openingWindow || previewCard < 0) return;
            openingWindow = true;
            try { if (await Desktop.ActivateFromDock(cardImages[previewCard].Window, false)) DismissRequested?.Invoke(); }
            finally { openingWindow = false; }
        };
        PreviewMouseMove += (_, e) =>
        {
            var point = e.GetPosition(this);
            // Reflow under a stationary cursor must not promote another card.
            if (point == lastPointer) return;
            lastPointer = point;
            if (point.X < 10 || point.X > 310) return;
            for (int i = 0; i < cards.Count; i++)
            {
                double top = GetTop(cards[i]);
                if (point.Y >= top && point.Y < top + cards[i].Height)
                {
                    BringCardForward(i);
                    if (cardImages[i].Live) WindowHovered?.Invoke(cardImages[i].Window);
                    else WindowHoverEnded?.Invoke();
                    break;
                }
            }
        };
        MouseLeave += (_, _) => { WindowHoverEnded?.Invoke(); lastPointer = new(double.NaN, double.NaN); BringCardForward(cards.Count - 1); };
    }
    internal void Present(IReadOnlyList<BrowserTab> tabs, nint owner, Preferences preferences)
    {
        Dispose(); Children.Clear(); surfaces.Clear(); cards.Clear();
        hoveredCard = -1; cardImages.Clear(); cachedImages.Clear();
        liveImageAttempted = false;
        thumbnailOwner = owner;
        // The parent owns one horizontal group per HWND. Keep this window's
        // tabs stacked, with its selected tab initially at the front.
        var all = tabs.Where(tab => tab.Selected).Concat(tabs).Distinct().ToArray();
        var shown = all.Take(6).OrderBy(tab => tab.Selected).ToArray();
        hiddenTabCount = Math.Max(0, all.Length - shown.Length);
        int count = shown.Length;
        // Reserve room for the rotated corners instead of clipping them.
        Height = 220 + Math.Max(0, count - 1) * 46;
        int index = 0;
        foreach (var tab in shown)
        {
            var row = new StackPanel { Width = 300 };
            var heading = new DockPanel { Height = 36, Margin = new Thickness(10, 0, 10, 0) };
            var windowControls = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(windowControls, Dock.Right); heading.Children.Add(windowControls);
            windowControls.Children.Add(WindowControl("Minimizar esta janela do navegador", "\uE921", async () =>
            {
                if (!Desktop.IsVisibleAndRestored(tab.Window)) return;
                bool minimized = await Desktop.ActivateFromDock(tab.Window, true);
                if (!minimized && ElevatedWindowAction.NeedsAdminFallback(tab.Window))
                    minimized = await ElevatedWindowAction.Request(tab.Window, close: false, minimize: true, owner: Window.GetWindow(this));
                if (minimized) DismissRequested?.Invoke();
                else MessageBox.Show("O Windows não permitiu minimizar esta janela.", "Orby Launcher");
            }));
            windowControls.Children.Add(WindowControl("Fechar esta janela do navegador (todas as abas desta janela)", "\uE711", async () =>
            {
                bool closed = WindowThumbnail.CloseWindow(tab.Window);
                if (!closed && ElevatedWindowAction.NeedsAdminFallback(tab.Window))
                    closed = await ElevatedWindowAction.Request(tab.Window, close: true, owner: Window.GetWindow(this));
                if (closed) DismissRequested?.Invoke();
                else MessageBox.Show("O Windows não permitiu solicitar o fechamento desta janela.", "Orby Launcher");
            }));
            var marker = new TextBlock { Text = tab.Selected ? "●" : "○", Margin = new Thickness(0, 0, 7, 0), FontSize = 12 };
            marker.SetResourceReference(TextBlock.ForegroundProperty, tab.Selected ? "Think.Accent" : "Think.Muted"); heading.Children.Add(marker);
            heading.Children.Add(new TextBlock { Text = tab.Title, FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }); row.Children.Add(heading);
            var area = new Border { Height = 112, Margin = new Thickness(10, 0, 10, 8) };
            bool live = tab.Selected || tab.RuntimeId.Length == 0;
            if (live)
            {
                area.MouseEnter += (_, _) => WindowHovered?.Invoke(tab.Window);
                area.MouseLeave += (_, _) => WindowHoverEnded?.Invoke();
            }
            cardImages.Add((area, tab.Window, live));
            var snapshot = live ? null : BrowserTabImages.Get(tab);
            cachedImages.Add(snapshot is not null);
            if (snapshot is not null)
            {
                var picture = new Image { Source = snapshot, Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
                System.Windows.Automation.AutomationProperties.SetHelpText(picture, "Última imagem capturada ao visitar esta aba.");
                area.Child = picture;
            }
            else if (!live)
            {
                var hint = new TextBlock { Text = "Aba em segundo plano. Clique para abrir seu conteúdo.", TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Margin = new Thickness(12) };
                hint.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); area.Child = hint;
            }
            row.Children.Add(area);
            var surface = new Border { Child = row, BorderThickness = new Thickness(1) };
            surfaces.Add(surface);
            var button = new Button { Width = 300, Height = 160, Padding = new Thickness(0), Margin = new Thickness(0), Content = surface, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent };
            button.SetResourceReference(StyleProperty, "Think.IconButton");
            var windowTabs = tabs.Where(candidate => candidate.Window == tab.Window).ToArray();
            if (windowTabs.Length > 1)
            {
                var menu = new ContextMenu();
                foreach (var candidate in windowTabs)
                {
                    var entry = new MenuItem { Header = candidate.Title, IsCheckable = true, IsChecked = candidate.Selected };
                    entry.Click += async (_, _) =>
                    {
                        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                        try { if (await BrowserTabs.SelectAsync(candidate, cancellation.Token)) DismissRequested?.Invoke(); }
                        catch (OperationCanceledException) { }
                    };
                    menu.Items.Add(entry);
                }
                button.ContextMenu = menu;
                tabMenus.Add(menu);
            }
            button.MouseEnter += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Accent");
            button.MouseLeave += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
            System.Windows.Automation.AutomationProperties.SetName(button, "Abrir aba: " + tab.Title);
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                try
                {
                    bool opened = tab.RuntimeId.Length == 0 ? await Desktop.ActivateFromDock(tab.Window, false)
                        : await BrowserTabs.SelectAsync(tab, cancellation.Token);
                    if (opened) DismissRequested?.Invoke();
                    else System.Windows.Automation.AutomationProperties.SetHelpText(button, "A aba não está mais disponível. Reabra a prévia.");
                }
                catch (OperationCanceledException) { }
                finally { button.IsEnabled = true; }
            };
            FrameworkElement card = button;
            cards.Add(card);
            SetLeft(card, 10); SetTop(card, 12 + index * 46); SetZIndex(card, index);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = .22, BlurRadius = 14, ShadowDepth = 2 };
            Children.Add(card);
            cardBackdrops.Add(new BackdropHost(owner, false, 0, "Orby — material da prévia"));
            if (SystemParameters.ClientAreaAnimation)
                button.BeginAnimation(OpacityProperty, new DoubleAnimation(.5, 1, TimeSpan.FromMilliseconds(160 + index * 35)));
            index++;
        }
        Children.Add(expansion); SetLeft(expansion, 10); SetZIndex(expansion, 200);
        ConfigureAppearance(preferences);
        previewCard = cardImages.FindLastIndex(image => image.Live);
        BringCardForward(count - 1, animate: false);
        LayoutChanged?.Invoke();
    }
    private void BringCardForward(int index, bool animate = true)
    {
        if (index < 0 || index >= cards.Count || hoveredCard == index) return;
        hoveredCard = index;
        // Tabs without a saved image remain compact; the actual window stays
        // available in its selected tab's card.
        int nextPreview = cardImages[index].Live || cachedImages[index] ? index : cardImages.FindLastIndex(image => image.Live);
        if (nextPreview != previewCard)
        {
            foreach (var item in images) item.Host.Dispose();
            images.Clear(); liveImageAttempted = false; previewCard = nextPreview;
        }
        Width = 320;
        Height = 220 + Math.Max(0, cards.Count - 1) * 46;
        expansion.Visibility = Visibility.Visible;
        extraCount.Text = hiddenTabCount > 0 ? $"+{hiddenTabCount}" : "";
        extraCount.Visibility = hiddenTabCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        expansionIcon.Margin = new Thickness(hiddenTabCount > 0 ? 8 : 0, 0, 0, 0);
        expansionIcon.Text = "\uE8A7";
        expansion.ToolTip = "Abrir janela do navegador";
        System.Windows.Automation.AutomationProperties.SetName(expansion, (string)expansion.ToolTip);
        SetLeft(expansion, Width - expansion.Width - 10); SetTop(expansion, 6);
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            SetZIndex(card, i == index ? 100 : i);
            // Keep each remaining title reachable. The selected card uses the
            // existing full slot; subsequent title strips move below it.
            double top = 48 + i * 46 + (previewCard >= 0 && i > previewCard ? 120 : 0);
            SetLeft(card, 10);
            bool full = i == previewCard;
            card.Height = full ? 160 : 40;
            cardImages[i].Area.Visibility = full ? Visibility.Visible : Visibility.Collapsed;
            if (animate && SystemParameters.ClientAreaAnimation)
                card.BeginAnimation(TopProperty, new DoubleAnimation(top, TimeSpan.FromMilliseconds(150))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { card.BeginAnimation(TopProperty, null); SetTop(card, top); }
            surfaces[i].SetResourceReference(Border.BorderBrushProperty, i == index ? "Think.Accent" : "Think.Hover");
        }
        PlaceImages();
    }
    private static Button WindowControl(string name, string glyph, Func<System.Threading.Tasks.Task> action)
    {
        var button = new Button { Width = 22, Height = 22, Padding = new Thickness(2), Margin = new Thickness(2, 0, 0, 0), ToolTip = name,
            Content = new TextBlock { Text = glyph, FontSize = 11, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets") } };
        button.SetResourceReference(StyleProperty, "Think.IconButton");
        System.Windows.Automation.AutomationProperties.SetName(button, name);
        button.Click += async (_, e) =>
        {
            // These controls are inside the tab's opening button. Do not bubble
            // the click and accidentally restore the window just minimized.
            e.Handled = true; button.IsEnabled = false;
            try { await action(); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }
    internal void ConfigureAppearance(Preferences preferences)
    {
        var tint = preferences.UseCustomBarColor ? (Color)ColorConverter.ConvertFromString(preferences.BarBackgroundColor) : ThemeService.BaseColor;
        // Match the dock's material tint rather than reducing it to 48% again.
        // The existing native Acrylic remains behind each rounded card.
        double opacity = Math.Clamp(preferences.DockOpacity ?? preferences.TintOpacity, .05, 1);
        cardRadius = Math.Clamp(preferences.DockRadius, 0, 24);
        foreach (var host in cardBackdrops) host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
        foreach (var companion in companionBackdrops) companion.Host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
        tint.A = (byte)Math.Round(255 * opacity);
        foreach (var surface in surfaces)
        {
            surface.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(tint);
            surface.CornerRadius = new CornerRadius(Math.Clamp(preferences.DockRadius, 0, 24));
            surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
        }
    }
    internal void PlaceImages()
    {
        if (!IsVisible || !IsMeasureValid || !IsArrangeValid) return;
        foreach (var companion in companionBackdrops)
        {
            var panel = companion.Panel;
            if (!panel.IsVisible || !panel.IsLoaded || panel.ActualWidth <= 0 || panel.ActualHeight <= 0) { companion.Host.Hide(); continue; }
            var start = panel.PointToScreen(new Point());
            var end = panel.PointToScreen(new Point(panel.ActualWidth, panel.ActualHeight));
            double scale = Math.Max(.1, (end.X - start.X) / panel.ActualWidth);
            companion.Host.Synchronize(new OverlayChrome.PixelRect { Left = (int)Math.Floor(start.X), Top = (int)Math.Floor(start.Y),
                Right = (int)Math.Ceiling(end.X), Bottom = (int)Math.Ceiling(end.Y) }, cardRadius * scale, false, true);
        }
        for (int i = 0; i < cards.Count && i < cardBackdrops.Count; i++)
        {
            var card = cards[i];
            if (!card.IsVisible || !card.IsLoaded || PresentationSource.FromVisual(card) is null) { cardBackdrops[i].Hide(); continue; }
            double height = i == previewCard ? 160 : 40;
            var start = card.PointToScreen(new Point(0, 0));
            var end = card.PointToScreen(new Point(300, height));
            double scale = Math.Max(.1, (end.X - start.X) / 300);
            cardBackdrops[i].Synchronize(new OverlayChrome.PixelRect { Left = (int)Math.Floor(start.X), Top = (int)Math.Floor(start.Y),
                Right = (int)Math.Ceiling(end.X), Bottom = (int)Math.Ceiling(end.Y) }, cardRadius * scale, false, true);
        }
        if (!liveImageAttempted && previewCard >= 0 && previewCard < cardImages.Count)
        {
            liveImageAttempted = true;
            foreach (var image in new[] { cardImages[previewCard] })
            {
            if (!image.Live) continue;
            var host = new WindowThumbnail(thumbnailOwner, image.Window);
            if (host.Available) images.Add((image.Area, host));
            else
            {
                host.Dispose();
                var unavailable = new TextBlock { Text = "Prévia indisponível nesta janela. Clique para abrir.", TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                unavailable.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); image.Area.Child = unavailable;
            }
            }
        }
        foreach (var item in images)
        {
            if (!item.Area.IsVisible || item.Area.ActualWidth <= 0 || item.Area.ActualHeight <= 0) continue;
            var start = item.Area.PointToScreen(new Point());
            var end = item.Area.PointToScreen(new Point(item.Area.ActualWidth, item.Area.ActualHeight));
            item.Host.Place(start, new Size(Math.Max(1, end.X - start.X), Math.Max(1, end.Y - start.Y)));
        }
    }
    public void Dispose()
    {
        WindowHoverEnded?.Invoke();
        foreach (var menu in tabMenus) menu.IsOpen = false; tabMenus.Clear();
        foreach (var item in images) item.Host.Dispose(); images.Clear(); liveImageAttempted = true;
        foreach (var host in cardBackdrops) host.Dispose(); cardBackdrops.Clear();
        foreach (var companion in companionBackdrops) companion.Host.Dispose(); companionBackdrops.Clear();
    }
}
