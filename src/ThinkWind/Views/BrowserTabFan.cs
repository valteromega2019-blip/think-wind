using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class BrowserTabFan : Canvas, IDisposable
{
    private readonly List<(Border Area, WindowThumbnail Host)> images = new();
    private readonly List<Border> surfaces = new();
    private readonly List<FrameworkElement> cards = new();
    private readonly List<RectangleGeometry> cardClips = new();
    private readonly List<BackdropHost> cardBackdrops = new();
    private readonly List<(FrameworkElement Panel, BackdropHost Host)> companionBackdrops = new();
    private double cardRadius;
    private readonly List<(Border Area, nint Window, bool Live)> cardImages = new();
    private int hoveredCard = -1;
    private nint thumbnailOwner;
    private bool liveImageAttempted;
    private Point lastPointer = new(double.NaN, double.NaN);
    internal event Action? DismissRequested;
    internal void AttachPanelMaterial(FrameworkElement panel)
    {
        var host = new BackdropHost(thumbnailOwner, false, thumbnailOwner, "Orby — material da integração");
        host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
        companionBackdrops.Add((panel, host));
    }
    internal BrowserTabFan()
    {
        Width = 320;
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
                if (point.Y >= top && point.Y < top + 40) { BringCardForward(i); break; }
            }
        };
        MouseLeave += (_, _) => { lastPointer = new(double.NaN, double.NaN); BringCardForward(cards.Count - 1); };
    }
    internal void Present(IReadOnlyList<BrowserTab> tabs, nint owner, Preferences preferences)
    {
        Dispose(); Children.Clear(); surfaces.Clear(); cards.Clear(); cardClips.Clear();
        hoveredCard = -1; cardImages.Clear();
        thumbnailOwner = owner;
        // Include the selected tab of each real window before filling spare
        // slots with background tabs. Otherwise all four slots can lack images.
        var shown = tabs.Where(t => t.Selected).Concat(tabs).Distinct().Take(4).OrderBy(t => t.Selected).ToArray();
        int count = shown.Length;
        // Reserve room for the rotated corners instead of clipping them.
        Height = 184 + Math.Max(0, count - 1) * 46;
        int index = 0;
        foreach (var tab in shown)
        {
            var row = new StackPanel { Width = 300 };
            var heading = new DockPanel { Margin = new Thickness(10, 8, 10, 6) };
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
            heading.Children.Add(new TextBlock { Text = tab.Title, FontSize = 12, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis }); row.Children.Add(heading);
            var area = new Border { Height = 112, Margin = new Thickness(10, 0, 10, 8) };
            bool live = tab.Selected || tab.RuntimeId.Length == 0;
            cardImages.Add((area, tab.Window, live));
            if (!live)
            {
                var unavailable = new TextBlock { Text = "A imagem desta aba em segundo plano não está disponível. Clique para abrir.",
                    TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10) };
                unavailable.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
                area.Child = unavailable;
            }
            // Rear tabs are identified by their exposed title; avoid placeholder
            // text bleeding across the card immediately in front of them.
            row.Children.Add(area);
            var surface = new Border { Child = row, BorderThickness = new Thickness(1) };
            surfaces.Add(surface);
            var button = new Button { Width = 300, Height = 160, Padding = new Thickness(0), Margin = new Thickness(0), Content = surface, HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = tab.Title,
                Background = Brushes.Transparent };
            if (tab.Cached) button.ToolTip = tab.Title + " · Última lista identificada; clique para restaurar e selecionar.";
            button.SetResourceReference(StyleProperty, "Think.IconButton");
            button.MouseEnter += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Accent");
            button.MouseLeave += (_, _) => surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
            System.Windows.Automation.AutomationProperties.SetName(button, "Abrir aba: " + tab.Title);
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                try
                {
                    if (tab.RuntimeId.Length == 0 || await BrowserTabs.SelectAsync(tab, cancellation.Token))
                    { await Desktop.ActivateFromDock(tab.Window, false); DismissRequested?.Invoke(); }
                    else button.ToolTip = "A aba não está mais disponível. Reabra a prévia.";
                }
                catch (OperationCanceledException) { }
                finally { button.IsEnabled = true; }
            };
            FrameworkElement card = index == count - 1 ? button : TiltVertically(button, (count - index - 1) * 5);
            cards.Add(card);
            var clip = new RectangleGeometry(new Rect(-8, -8, 316, 176));
            card.Clip = clip; cardClips.Add(clip);
            SetLeft(card, 10); SetTop(card, 12 + index * 46); SetZIndex(card, index);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, Opacity = .22, BlurRadius = 14, ShadowDepth = 2 };
            Children.Add(card);
            cardBackdrops.Add(new BackdropHost(owner, false, owner, "Orby — material da prévia"));
            if (SystemParameters.ClientAreaAnimation)
                button.BeginAnimation(OpacityProperty, new DoubleAnimation(.5, 1, TimeSpan.FromMilliseconds(160 + index * 35)));
            index++;
        }
        ConfigureAppearance(preferences);
        BringCardForward(count - 1, animate: false);
    }
    private void BringCardForward(int index, bool animate = true)
    {
        if (index < 0 || index >= cards.Count || hoveredCard == index) return;
        hoveredCard = index;
        foreach (var image in images) image.Host.Dispose();
        images.Clear();
        liveImageAttempted = false;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            SetZIndex(card, i == index ? 100 : i);
            if (card is Viewport3D tilted && tilted.Children.OfType<Viewport2DVisual3D>().FirstOrDefault()?.Transform is RotateTransform3D rotation
                && rotation.Rotation is AxisAngleRotation3D angle)
            {
                double targetAngle = i == index ? 0 : (cards.Count - i - 1) * 5;
                if (animate && SystemParameters.ClientAreaAnimation)
                    angle.BeginAnimation(AxisAngleRotation3D.AngleProperty, new DoubleAnimation(targetAngle, TimeSpan.FromMilliseconds(150)));
                else { angle.BeginAnimation(AxisAngleRotation3D.AngleProperty, null); angle.Angle = targetAngle; }
            }
            // Keep each remaining title reachable. The selected card uses the
            // existing full slot; subsequent title strips move below it.
            double top = 12 + i * 46 + (i > index ? 114 : 0);
            if (animate && SystemParameters.ClientAreaAnimation)
                card.BeginAnimation(TopProperty, new DoubleAnimation(top, TimeSpan.FromMilliseconds(150))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            else { card.BeginAnimation(TopProperty, null); SetTop(card, top); }
            cardClips[i].Rect = new Rect(-8, -8, 316, i == index ? 176 : 48);
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
    private static Viewport3D TiltVertically(Button button, double degrees)
    {
        // Tilt around X (top/bottom depth), never around the screen's Z axis.
        // Viewport2DVisual3D preserves real button hit-testing and accessibility.
        var viewport = new Viewport3D { Width = 300, Height = 160, ClipToBounds = false,
            Camera = new PerspectiveCamera(new Point3D(0, 0, 3), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 36.86989765) };
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection { new(-1, -.533333, 0), new(1, -.533333, 0), new(1, .533333, 0), new(-1, .533333, 0) },
            TextureCoordinates = new PointCollection { new(0, 1), new(1, 1), new(1, 0), new(0, 0) },
            TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
        };
        var material = new DiffuseMaterial(Brushes.White);
        Viewport2DVisual3D.SetIsVisualHostMaterial(material, true);
        viewport.Children.Add(new ModelVisual3D { Content = new AmbientLight(Colors.White) });
        viewport.Children.Add(new Viewport2DVisual3D { Geometry = mesh, Material = material, Visual = button,
            Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(1, 0, 0), degrees)) });
        return viewport;
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
        if (!IsVisible) return;
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
            double height = i == hoveredCard ? 160 : 40;
            var start = card.PointToScreen(new Point(0, 0));
            var end = card.PointToScreen(new Point(300, height));
            double scale = Math.Max(.1, (end.X - start.X) / 300);
            cardBackdrops[i].Synchronize(new OverlayChrome.PixelRect { Left = (int)Math.Floor(start.X), Top = (int)Math.Floor(start.Y),
                Right = (int)Math.Ceiling(end.X), Bottom = (int)Math.Ceiling(end.Y) }, cardRadius * scale, false, true);
        }
        if (!liveImageAttempted && hoveredCard >= 0 && hoveredCard < cardImages.Count && cardImages[hoveredCard] is var image && image.Live)
        {
            liveImageAttempted = true;
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
        foreach (var item in images) item.Host.Dispose(); images.Clear(); liveImageAttempted = true;
        foreach (var host in cardBackdrops) host.Dispose(); cardBackdrops.Clear();
        foreach (var companion in companionBackdrops) companion.Host.Dispose(); companionBackdrops.Clear();
    }
}
