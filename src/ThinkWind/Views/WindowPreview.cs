using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class WindowPreview : FloatingWindow
{
    protected override bool AttachedTop => false;
    private readonly StackPanel body = new();
    private readonly Viewbox viewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly UniformGrid grid = new();
    private readonly List<(Border Area, WindowThumbnail Host)> thumbnails = new();
    private readonly List<Border> previewAreas = new();
    private readonly Func<OverlayChrome.PixelRect> anchor;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ShellIcons icons = new();
    private List<Desktop.RunningWindow> windows = new();
    private int page, perPage = 4;
    internal WindowPreview(SettingsStore store, Func<OverlayChrome.PixelRect> getAnchor) : base(store)
    {
        anchor = getAnchor; Title = "Think Wind — Janelas abertas"; body.Children.Add(grid); viewport.Child = body; Surface.Child = viewport;
        refresh.Tick += (_, _) => { if (windows.Any(window => !WindowThumbnail.IsWindow(window.Handle))) { windows.RemoveAll(window => !WindowThumbnail.IsWindow(window.Handle)); Render(); } };
        LayoutUpdated += (_, _) => PlaceThumbnails();
        IsVisibleChanged += (_, _) => { if (!IsVisible) { refresh.Stop(); Release(); } };
        Closed += (_, _) => Release();
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Hide(); };
    }
    internal void Present(IEnumerable<Desktop.RunningWindow> source)
    { windows = source.Where(window => WindowThumbnail.IsWindow(window.Handle)).ToList(); page = 0; if (windows.Count == 0) return; Show(); Render(); refresh.Start(); }
    private void Release() { foreach (var item in thumbnails) item.Host.Dispose(); thumbnails.Clear(); }
    private void Render()
    {
        Release(); grid.Children.Clear(); previewAreas.Clear();
        while (body.Children.Count > 1) body.Children.RemoveAt(1);
        if (windows.Count == 0) { Hide(); return; }
        perPage = Math.Clamp((int)((Display.Width - 36) / 240), 1, 4);
        int pages = Math.Max(1, (windows.Count + perPage - 1) / perPage); page = Math.Clamp(page, 0, pages - 1);
        var shown = windows.Skip(page * perPage).Take(perPage).ToArray(); grid.Columns = shown.Length; grid.Rows = 1;
        foreach (var window in shown)
        {
            var card = new StackPanel { Margin = new Thickness(4) }; var heading = new DockPanel();
            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(controls, Dock.Right); heading.Children.Add(controls);
            var minimize = PreviewControl("Minimizar esta janela", "\uE921", async () =>
            {
                if (!Desktop.IsVisibleAndRestored(window.Handle)) return;
                bool minimized = await Desktop.ActivateFromDock(window.Handle, true);
                if (!minimized && ElevatedWindowAction.NeedsAdminFallback(window.Handle))
                    minimized = await ElevatedWindowAction.Request(window.Handle, close: false, minimize: true, owner: Owner ?? this);
                if (minimized) Hide();
                else MessageBox.Show("O Windows não permitiu minimizar esta janela.", "Think Wind");
            });
            var close = PreviewControl("Fechar esta janela", "\uE711", async () => { if (!WindowThumbnail.CloseWindow(window.Handle)) { if (ElevatedWindowAction.NeedsAdminFallback(window.Handle)) await ElevatedWindowAction.Request(window.Handle, close: true, owner: Owner ?? this); else MessageBox.Show("O Windows não permitiu solicitar o fechamento desta janela.", "Think Wind"); } });
            controls.Children.Add(minimize); controls.Children.Add(close);
            var icon = new Image { Source = icons.Get(window.Executable, window.Handle), Width = 18, Height = 18, Margin = new Thickness(2, 0, 6, 0) }; DockPanel.SetDock(icon, Dock.Left); heading.Children.Add(icon);
            heading.Children.Add(new TextBlock { Text = window.Title, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis }); card.Children.Add(heading);
            var area = new Border { Height = 120, Margin = new Thickness(0, 4, 0, 0), Background = Brushes.Transparent };
            previewAreas.Add(area);
            var open = ActionButton(window.Title, async () => { Hide(); if (!await Desktop.ActivateFromDock(window.Handle, false) && ElevatedWindowAction.NeedsAdminFallback(window.Handle)) await ElevatedWindowAction.Request(window.Handle, close: false, owner: Owner ?? this); }); open.ToolTip = null; open.Content = area; open.SetResourceReference(StyleProperty, "Think.IconButton"); open.Padding = new Thickness(0); card.Children.Add(open); grid.Children.Add(card);
            var host = new WindowThumbnail(new WindowInteropHelper(this).Handle, window.Handle);
            if (host.Available) thumbnails.Add((area, host)); else { host.Dispose(); area.Child = new TextBlock { Text = "Prévia indisponível. Clique para abrir.", TextWrapping = TextWrapping.Wrap, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }; }
        }
        if (pages > 1)
        {
            var nav = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var back = ActionButton("Página anterior", () => { page--; Render(); }); back.Content = "‹"; back.IsEnabled = page > 0;
            var next = ActionButton("Próxima página", () => { page++; Render(); }); next.Content = "›"; next.IsEnabled = page + 1 < pages;
            nav.Children.Add(back); nav.Children.Add(new TextBlock { Text = $"{page + 1}/{pages}", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }); nav.Children.Add(next); body.Children.Add(nav);
        }
        Position();
    }
    private Button PreviewControl(string label, string glyph, Action action)
    {
        var button = ActionButton(label, action);
        button.Content = Glyph(glyph, 12);
        button.Width = button.Height = 26;
        button.Padding = new Thickness(4);
        return button;
    }
    private void PlaceThumbnails()
    {
        if (!IsVisible) return;
        foreach (var item in thumbnails) if (item.Area.ActualWidth > 0 && item.Area.ActualHeight > 0)
        {
            var topLeft = item.Area.PointToScreen(new Point()); var bottomRight = item.Area.PointToScreen(new Point(item.Area.ActualWidth, item.Area.ActualHeight));
            if (!item.Host.Place(topLeft, new Size(Math.Max(1, bottomRight.X - topLeft.X), Math.Max(1, bottomRight.Y - topLeft.Y))))
            {
                item.Host.Dispose();
                if (item.Area.Child is null) item.Area.Child = new TextBlock { Text = "Prévia indisponível. Clique para abrir.", TextWrapping = TextWrapping.Wrap, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            }
        }
    }
    public override void Position()
    {
        double width = Math.Min(Display.Width - 16, Math.Max(1, grid.Columns) * 280 + 20);
        var bounds = anchor(); body.Width = width - 20; viewport.Width = body.Width;
        // IconButton centres its ContentPresenter. An empty Border otherwise
        // measures to zero width, so DWM never receives a destination rectangle.
        foreach (var area in previewAreas) area.Width = Math.Max(1, body.Width / Math.Max(1, grid.Columns) - 8);
        viewport.MaxHeight = Math.Max(48, Math.Min(400, (bounds.Top - Display.Work.Top) / Display.Scale - 36));
        viewport.Measure(new Size(body.Width, viewport.MaxHeight));
        PlaceAbove(width, viewport.DesiredSize.Height + 20, bounds, false);
    }
}
