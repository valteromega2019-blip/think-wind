using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class DockItemsPanel : StackPanel
{
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => null;
}

internal sealed partial class DockWindow : FloatingWindow
{
    internal bool HasPrimaryControls => System.Windows.Forms.Screen.AllScreens.Any(screen =>
        screen.Primary && screen.Bounds == Display.Bounds);
    protected override bool AttachedTop => false;
    protected override bool CollapsedDock => hidden;
    private readonly DockItemsPanel items = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly DockMagnifier magnifier;
    private double magnificationReserve;
    private double magnificationHeadroom;
    private readonly AdornerDecoratorHost iconLayer;
    protected override Thickness TransparentInsets
    {
        get
        {
            double progress = 1;
            if (IsPositionAnimating || hidden)
            {
                var bounds = Bounds;
                progress = Math.Clamp(((bounds.Bottom - bounds.Top) / Display.Scale - 6) / Math.Max(1, ExpandedHeight - 6), 0, 1);
            }
            return new Thickness(magnificationReserve / 2 * progress, magnificationHeadroom * progress, magnificationReserve / 2 * progress, 0);
        }
    }
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly ApplicationCatalog catalog;
    private LauncherPanelWindow? launcher;
    private LauncherPanelMode panelMode;
    private bool temporaryFullscreenStart;
    private readonly TextBox dockSearch = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center, MaxLength = 160 };
    private DateTime nextAppRefresh;
    private readonly ShellIcons icons = new();
    private readonly List<string> visibleOrder = new();
    private string signature = "";
    private bool hidden, rebuilding;
    private bool applicationOverDock;
    private bool readingWindows;
    private ContextMenu? activeContextMenu;
    private DateTime contextMenuRequested;
    private bool settingsAfterMenu;
    private int layoutRevision;
    private double contentWidth, contentHeight = 48, fittedWidth;
    private List<Item> entries = new();
    private DockFit fit = new(1, 32, 10, 4, 0);
    private int page;
    private nint activeWindow;
    private double TotalWidth => contentWidth + 20 + magnificationReserve;
    private double ExpandedHeight => Math.Max(44, contentHeight + 20) + magnificationHeadroom;
    private DateTime lastHover = DateTime.UtcNow;
    private sealed record Item(string Key, string Name, string? Target, string? Glyph, List<Desktop.RunningWindow> Windows, Shortcut? Pin, Action? Action);
    public DockWindow(SettingsStore store) : base(store)
    {
        Title = "Think Wind — Dock";
        items.VerticalAlignment = VerticalAlignment.Bottom;
        items.Background = Brushes.Transparent;
        PageWheel.Attach(items, () => fit.Pages(entries.Count), () => page, value => { page = value; RenderPage(); Position(); }, () => fileDragging || MenuOpen || LayoutDrag.Active || Store.IsEditing || dockSearch.IsMouseOver);
        magnifier = new DockMagnifier(items, () => !hidden && !IsPositionAnimating && !Store.IsEditing && !LayoutDrag.Active && !fileDragging && !MenuOpen);
        catalog = new ApplicationCatalog(Dispatcher);
        catalog.Changed += () => { signature = ""; nextAppRefresh = DateTime.MinValue; };
        dockSearch.SetResourceReference(TextBox.ForegroundProperty, "Think.Text");
        System.Windows.Automation.AutomationProperties.SetName(dockSearch, "Pesquisar no Windows pelo Think Wind");
        dockSearch.TextChanged += (_, _) => { if (dockSearch.IsKeyboardFocusWithin) { EnsureLauncher(); panelMode = LauncherPanelMode.Search; launcher!.SearchFromDock(dockSearch.Text); } };
        dockSearch.GotKeyboardFocus += (_, _) => { AnimateSearch(true); EnsureLauncher(); panelMode = LauncherPanelMode.Search; launcher!.SearchFromDock(dockSearch.Text); };
        dockSearch.LostKeyboardFocus += (_, _) => AnimateSearch(false);
        dockSearch.PreviewMouseLeftButtonDown += (_, _) => Activate();
        dockSearch.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { launcher?.OpenFirstResult(); e.Handled = true; }
            else if (e.Key == Key.Down) { launcher?.FocusSearch(); e.Handled = true; }
            else if (e.Key == Key.Escape) { launcher?.Dismiss(); Keyboard.ClearFocus(); e.Handled = true; }
        };
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(NoActivate);
        iconLayer = new AdornerDecoratorHost(items, true);
        iconLayer.VerticalAlignment = VerticalAlignment.Bottom;
        LayoutUpdated += (_, _) =>
        {
            // Keep the row at its measured height while the native canvas grows.
            // Reveal only when there is room, rather than clipping scaled icons.
            if (!IsVisible) return;
            iconLayer.Opacity = hidden ? 0 : Math.Clamp((ActualHeight - ExpandedHeight + 8) / 8, 0, 1);
        };
        ((Grid)Content).Children.Add(iconLayer);
        Loaded += (_, _) => { RefreshSettings(); timer.Start(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) timer.Start(); else { timer.Stop(); magnifier.Reset(); launcher?.Dismiss(); } };
        timer.Tick += (_, _) => { if (!IsVisible) return; PollFileDrag(); if (DateTime.UtcNow >= nextAppRefresh) { nextAppRefresh = DateTime.UtcNow.AddSeconds(2); RefreshApps(); } UpdateVisibility(); };
        MouseEnter += (_, _) => { lastHover = DateTime.UtcNow; SetHidden(false); };
        MouseLeave += (_, _) => lastHover = DateTime.UtcNow;
        LayoutDrag.Completed += DragCompleted;
        InitializeFileDrop();
        InitializePreviews();
        InitializeStartTools();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                StopPreviews();
                if (activeContextMenu is not null) activeContextMenu.IsOpen = false;
                activeContextMenu = null;
            }
        };
        Closed += (_, _) => { magnifier.Dispose(); timer.Stop(); DisposeFileDrop(); catalog.Dispose(); LayoutDrag.Completed -= DragCompleted; };
    }
    private void DragCompleted() { signature = ""; RefreshApps(); }
    internal void SuspendForGame(bool active)
    {
        if (!active) temporaryFullscreenStart = false;
        catalog.SetSuspended(active);
        if (active) { StopPreviews(); magnifier.Reset(); timer.Stop(); launcher?.Dismiss(); FinishFileDrag(); }
    }
    private nint NoActivate(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        // Keep normal hover/left-click nonactivating, but let a right-click
        // activate the owner before WPF gives the popup mouse capture.
        int mouseMessage = (int)((lParam.ToInt64() >> 16) & 0xFFFF);
        if (message == 0x0021 && !dockSearch.IsMouseOver && mouseMessage != 0x0204 && !MenuOpen)
        { handled = true; return 3; }
        return 0;
    }
    public override void Position()
    {
        if (MenuOpen) return;
        if (!rebuilding && Math.Abs(fittedWidth - Display.Width) > .5 && entries.Count > 0) RenderPage();
        Place(TotalWidth, hidden ? 6 : ExpandedHeight, IsVisible && !hidden);
        if (launcher?.IsVisible == true) launcher.Position();
    }
    public override void RefreshSettings()
    {
        if (MenuOpen) { settingsAfterMenu = true; return; }
        if (LayoutDrag.Active || rebuilding) return;
        if (layoutRevision != Store.DockLayoutRevision) { layoutRevision = Store.DockLayoutRevision; visibleOrder.Clear(); }
        signature = "";
        if (!Store.Value.AutoHide || Store.IsEditing) hidden = false;
        items.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
        ApplyAppearance(); RefreshApps();
        launcher?.RefreshSettings();
    }
    private bool MenuOpen => startTools?.IsVisible == true || activeContextMenu is not null || items.Children.OfType<Button>().Any(b => b.ContextMenu?.IsOpen == true);
    private bool PointerInsideDock
    {
        get
        {
            if (!IsVisible) return false;
            var cursor = System.Windows.Forms.Cursor.Position;
            var point = new Point(cursor.X, cursor.Y);
            bool Inside(FrameworkElement element) => element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0
                && new Rect(0, 0, element.ActualWidth, element.ActualHeight).Contains(element.PointFromScreen(point));
            if (Inside(Surface)) return true;
            // Magnified icons can extend above the Acrylic. The transparent
            // canvas itself must not count as an interaction surface.
            return items.Children.OfType<FrameworkElement>().Any(Inside);
        }
    }
    private void FinishContextMenu(ContextMenu menu)
    {
        if (!ReferenceEquals(activeContextMenu, menu)) return;
        activeContextMenu = null;
        lastHover = DateTime.UtcNow; signature = "";
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible) return;
            if (settingsAfterMenu) { settingsAfterMenu = false; RefreshSettings(); }
            else RefreshApps();
        }));
    }
    private void UpdateVisibility()
    {
        if (Store.Value.DockVisibleOnDesktop && !applicationOverDock)
        {
            lastHover = DateTime.UtcNow;
            SetHidden(false, !Desktop.LeftButtonDown);
        }
        bool pointerInsideReveal = PointerInsideRevealArea();
        if (hidden && !IsPositionAnimating && pointerInsideReveal)
        {
            lastHover = DateTime.UtcNow;
            SetHidden(false, !Desktop.LeftButtonDown);
        }
        if (activeContextMenu is { IsOpen: false } stale && DateTime.UtcNow - contextMenuRequested > TimeSpan.FromMilliseconds(250))
            FinishContextMenu(stale);
        // Keep the activation region valid after expansion too: the dock may
        // float above a cursor that is still resting on the original handle.
        bool pointerInside = pointerInsideReveal || PointerInsideDock;
        if (pointerInside) lastHover = DateTime.UtcNow;
        if (preview?.IsVisible == true && !preview.IsMouseOver && !pointerInside
            && DateTime.UtcNow - lastHover >= TimeSpan.FromMilliseconds(350)) StopPreviews();
        // An ordinary focused button isn't an ongoing interaction. In particular,
        // activating the owner for a right-click must not pin the dock forever.
        if (Store.Value.AutoHide && (!Store.Value.DockVisibleOnDesktop || applicationOverDock) && !Store.IsEditing && !Store.IsCustomizing && !LayoutDrag.Active && !fileDragging && launcher?.Busy != true && preview?.IsVisible != true && !MenuOpen && !pointerInside && !IsMouseCaptureWithin && DateTime.UtcNow - lastHover >= TimeSpan.FromSeconds(Store.Value.DockCollapseSeconds)) SetHidden(true);
    }
    private bool PointerInsideRevealArea()
    {
        if (!IsVisible) return false;
        var display = Display;
        var cursor = System.Windows.Forms.Cursor.Position;
        // Passive detection uses the existing timer, not a transparent window
        // over other applications. The visible handle remains 96 x 6 DIP.
        int halfWidth = Desktop.LeftButtonDown ? display.Bounds.Width / 2 : (int)Math.Round(88 * display.Scale);
        int height = (int)Math.Round(32 * display.Scale);
        int center = display.Bounds.Left + display.Bounds.Width / 2;
        return cursor.X >= center - halfWidth && cursor.X < center + halfWidth
            && cursor.Y >= display.Bounds.Bottom - height && cursor.Y < display.Bounds.Bottom;
    }
    private void SetHidden(bool value, bool animate = true)
    {
        if (value && MenuOpen) return;
        if (hidden == value) return;
        hidden = value; items.Visibility = value ? Visibility.Hidden : Visibility.Visible;
        if (value) magnifier.Reset(); else magnifier.Wake();
        ApplyAppearance(); Place(TotalWidth, hidden ? 6 : ExpandedHeight, animate);
    }
    private async void RefreshApps()
    {
        if (LayoutDrag.Active || fileDragging || MenuOpen || rebuilding || dockSearch.IsKeyboardFocusWithin) return;
        if (readingWindows) { nextAppRefresh = DateTime.MinValue; return; }
        readingWindows = true;
        List<Desktop.RunningWindow> windows;
        var pins = Store.Value.Shortcuts.ToArray();
        var startPins = Store.Value.StartPins.ToArray();
        Dictionary<string, LaunchIdentity> pinIdentities;
        try
        {
            windows = await System.Threading.Tasks.Task.Run(Desktop.Windows);
            pinIdentities = await catalog.ResolvePinsAsync(pins.Concat(startPins).DistinctBy(pin => pin.Target, StringComparer.OrdinalIgnoreCase).ToArray());
        }
        catch (System.Threading.Tasks.TaskCanceledException) { return; }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { return; }
        finally { readingWindows = false; }
        if (!pins.SequenceEqual(Store.Value.Shortcuts) || !startPins.SequenceEqual(Store.Value.StartPins)) { signature = ""; nextAppRefresh = DateTime.MinValue; return; }
        if (!IsVisible || LayoutDrag.Active || fileDragging || MenuOpen) return;
        var screen = Display;
        int dockHeight = (int)Math.Ceiling((ExpandedHeight + Store.Value.DockBottomOffset) * screen.Scale);
        int dockWidth = (int)Math.Ceiling(Math.Max(96, TotalWidth) * screen.Scale);
        int dockLeft = (int)Math.Round(screen.Work.Left + (screen.Work.Width - dockWidth) / 2d + Store.Value.DockHorizontalOffset * screen.Scale);
        applicationOverDock = windows.Any(window => !window.Minimized
            && OverlayChrome.GetWindowRect(window.Handle, out var rectangle)
            && rectangle.Right > dockLeft && rectangle.Left < dockLeft + dockWidth
            && rectangle.Bottom > screen.Work.Bottom - dockHeight && rectangle.Top < screen.Work.Bottom);
        var removed = pinIdentities.Where(pair => pair.Value.Installed == false && !windows.Any(window =>
            (!string.IsNullOrWhiteSpace(window.Executable) && DockPath(window.Executable).Equals(DockPath(pair.Value.Path), StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(pair.Value.AppId) && pair.Value.AppId.Equals(window.AppId, StringComparison.OrdinalIgnoreCase))))
            .Select(pair => pair.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (removed.Count > 0)
        {
            Store.Update(p =>
            {
                p.Shortcuts.RemoveAll(pin => removed.Contains(pin.Target));
                p.StartPins.RemoveAll(pin => removed.Contains(pin.Target));
                p.RecentLaunches.RemoveAll(pin => removed.Contains(pin.Target));
                p.DockOrder.RemoveAll(key => key.StartsWith("pin:", StringComparison.Ordinal) && removed.Contains(key[4..]));
            });
            return;
        }
        var active = Desktop.GetForegroundWindow();
        var next = string.Join("|", windows.Select(w => $"{w.Handle}:{w.Title}:{w.Minimized}:{w.AppId}:{w.Executable}")) + active
            + string.Join("|", pins.Select(pin => $"{pin.Target}:{pinIdentities[pin.Target].Path}:{pinIdentities[pin.Target].AppId}"));
        if (next == signature) return;
        rebuilding = true;
        try
        {
            signature = next;
            var entries = new List<Item>();
            var pinnedWindows = new HashSet<nint>();
            var pinnedIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pin in pins)
            {
                var launch = pinIdentities[pin.Target];
                var path = DockPath(launch.Path);
                string? appId = launch.AppId ?? catalog.Items.FirstOrDefault(app => app.Target.Equals(pin.Target, StringComparison.OrdinalIgnoreCase))?.AppId;
                string identity = !string.IsNullOrWhiteSpace(appId) ? "id:" + appId : "path:" + path;
                if (!pinnedIdentities.Add(identity)) continue;
                var matching = windows.Where(w => !pinnedWindows.Contains(w.Handle) &&
                    ((!string.IsNullOrWhiteSpace(appId) && !string.IsNullOrWhiteSpace(w.AppId))
                        ? string.Equals(w.AppId, appId, StringComparison.OrdinalIgnoreCase)
                        : (!string.IsNullOrWhiteSpace(w.Executable) && string.Equals(DockPath(w.Executable), path, StringComparison.OrdinalIgnoreCase)))).ToList();
                foreach (var window in matching) pinnedWindows.Add(window.Handle);
                entries.Add(new("pin:" + pin.Target, pin.Name, pin.Target, null, matching, pin, null));
            }
            foreach (var group in windows.Where(w => !pinnedWindows.Contains(w.Handle))
                .GroupBy(w => !string.IsNullOrWhiteSpace(w.AppId) ? "id:" + w.AppId : !string.IsNullOrWhiteSpace(w.Executable) ? "path:" + DockPath(w.Executable) : "window:" + w.Handle, StringComparer.OrdinalIgnoreCase))
            {
                var groupWindows = group.ToList();
                var first = groupWindows[0];
                entries.Add(new("app:" + group.Key, first.Title, first.Executable, null, groupWindows, null, null));
            }
            entries.Add(new("system:add", "Biblioteca de Aplicativos", null, "\uE710", new(), null, () => ShowPanel(LauncherPanelMode.Library)));
            var present = entries.Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var order = Store.Value.DockOrder.Concat(visibleOrder).Where(present.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var entry in entries.Where(i => !order.Contains(i.Key, StringComparer.OrdinalIgnoreCase)))
            {
                int insertion = order.Count;
                if (entry.Key != "system:add")
                    while (insertion > 0 && order[insertion - 1] == "system:add") insertion--;
                order.Insert(insertion, entry.Key);
            }
            entries = entries.OrderBy(i => order.FindIndex(k => k.Equals(i.Key, StringComparison.OrdinalIgnoreCase))).ToList();
            visibleOrder.Clear(); visibleOrder.AddRange(entries.Select(i => i.Key));
            this.entries = entries; activeWindow = active;
            RenderPage();
            Position();
        }
        finally { rebuilding = false; }
    }
    private static string DockPath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal)) path = path[4..];
        try { if (System.IO.Path.IsPathFullyQualified(path)) return System.IO.Path.GetFullPath(path); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or System.IO.PathTooLongException) { }
        return path;
    }
    private void RenderPage()
    {
        if (MenuOpen) return;
        fittedWidth = Display.Width;
        bool primaryControls = HasPrimaryControls;
        double searchWidth = primaryControls ? Store.Value.DockSearchMode == "Bar" ? Math.Min(144, Math.Max(48, fittedWidth * .22)) : 36 : 0;
        double fixedControls = primaryControls ? DockGeometry.StartWidth(Store.Value.IconSize * Store.Value.DockScale) + 8 + searchWidth : 0;
        double available = Math.Max(60, fittedWidth - 16 - 20 - fixedControls - ShowDesktopSlotWidth - (Store.IsEditing ? 30 : 0));
        var p = Store.Value;
        bool magnify = p.DockMagnification && SystemParameters.ClientAreaAnimation && !Store.IsEditing;
        magnificationReserve = magnify ? Math.Min(available * .32, (p.IconSize * p.DockScale + 20) * p.DockMagnificationAmount * (p.DockMagnificationNeighbors + 1) * 1.12) : 0;
        fit = CompactLayout.FitDock(entries.Count, available - magnificationReserve, p.IconSize * p.DockScale, p.DockSpacing);
        // Fixed headroom belongs to the expanded layout, never to mouse frames.
        // The independent transparent canvas encloses every enlarged icon.
        magnificationHeadroom = magnify ? (fit.Icon + 26) * p.DockMagnificationAmount * 1.08 : 0;
        Surface.Padding = new Thickness(10); Surface.Margin = TransparentInsets;
        iconLayer.Margin = new Thickness(10 + magnificationReserve / 2, 0, 10 + magnificationReserve / 2, 10);
        int pages = fit.Pages(entries.Count); page = Math.Clamp(page, 0, pages - 1);
        items.Width = double.NaN;
        items.Children.Clear();
        if (primaryControls)
        {
            var start = ActionButton("Iniciar Think Wind", ToggleStart); start.Style = (Style)FindResource("Think.IconButton"); start.Content = WindowsStartIcon(fit.Icon); start.Width = DockGeometry.StartWidth(fit.Icon); start.Height = fit.Icon + 22; start.Padding = new Thickness(4); items.Children.Add(start);
            AttachStartTools(start);
            items.Children.Add(SearchSlot(searchWidth));
            start.Margin = new Thickness(0, 0, 8, 0);
        }
        if (Store.IsEditing)
        {
            var grip = Glyph("\uE784", 16); grip.Width = 22; grip.Margin = new Thickness(4);
            grip.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); AttachPositionGrip(grip); items.Children.Add(grip);
        }
        if (pages > 1) items.Children.Add(PageButton(-1, pages));
        var magnified = new List<FrameworkElement>();
        foreach (var entry in entries.Skip(page * fit.PerPage).Take(fit.PerPage))
        {
            var button = CreateButton(entry, activeWindow); items.Children.Add(button);
            if (!entry.Key.StartsWith("system:", StringComparison.Ordinal)) magnified.Add(button);
        }
        if (pages > 1) items.Children.Add(PageButton(1, pages));
        items.Children.Add(ShowDesktopButton());
        items.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        // Measure the individual slots, including their margins, independently
        // of the previous HWND width. The row must not keep its old layout slot.
        contentWidth = items.Children.OfType<FrameworkElement>().Sum(child => child.DesiredSize.Width);
        contentHeight = items.Children.OfType<FrameworkElement>().Select(child => child.DesiredSize.Height).DefaultIfEmpty(44).Max();
        items.Width = contentWidth;
        magnificationHeadroom = magnify ? DockGeometry.Headroom(contentHeight, p.DockMagnificationAmount) : 0;
        iconLayer.Height = contentHeight;
        Surface.Margin = TransparentInsets;
        iconLayer.Margin = new Thickness(10 + magnificationReserve / 2, 0, 10 + magnificationReserve / 2, 10);
        foreach (FrameworkElement child in items.Children) child.VerticalAlignment = VerticalAlignment.Center;
        magnifier.Bind(magnified, p, magnificationReserve);
    }
    private static FrameworkElement WindowsStartIcon(double size)
    {
        // Four equal panes: a crisp Windows symbol at every display scale,
        // independent of installed icon-font versions and without a tile behind it.
        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 H10 V10 H0 Z M12,0 H22 V10 H12 Z M0,12 H10 V22 H0 Z M12,12 H22 V22 H12 Z"),
            Width = size, Height = size, Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        icon.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "Think.AccentInk");
        return icon;
    }
    private Button PageButton(int direction, int pages)
    {
        var button = ActionButton(direction < 0 ? "Página anterior" : "Próxima página", () => { page += direction; RenderPage(); Position(); }, $"Página {page + 1} de {pages}");
        button.Style = (Style)FindResource("Think.IconButton"); button.Content = Glyph(direction < 0 ? "\uE76B" : "\uE76C", 12);
        button.Width = fit.NavigationWidth / 2; button.Height = 36; button.Padding = new Thickness(0); button.Margin = new Thickness(0);
        button.IsEnabled = direction < 0 ? page > 0 : page + 1 < pages;
        if (button.IsEnabled)
        {
            int target = direction < 0 ? page * fit.PerPage - 1 : (page + 1) * fit.PerPage;
            LayoutDrag.Attach(button, "dock", entries[target].Key, () => Store.IsEditing, Reorder);
            AttachFilePageTarget(button, direction);
        }
        return button;
    }
    private Button CreateButton(Item item, nint active)
    {
        var focused = item.Windows.FirstOrDefault(w => w.Handle == active);
        var selected = focused ?? item.Windows.FirstOrDefault(w => !w.Minimized) ?? item.Windows.FirstOrDefault();
        bool minimized = item.Windows.Count > 0 && item.Windows.All(w => w.Minimized);
        var size = fit.Icon;
        var panel = new Grid { Height = size + 14, VerticalAlignment = VerticalAlignment.Center };
        var catalogApp = item.Glyph is null ? catalog.Items.FirstOrDefault(app =>
            (!string.IsNullOrWhiteSpace(selected?.AppId) && string.Equals(app.AppId, selected?.AppId, StringComparison.OrdinalIgnoreCase))
            || (item.Target is not null && app.Target.Equals(item.Target, StringComparison.OrdinalIgnoreCase))) : null;
        var icon = item.Glyph is null ? catalogApp?.Icon ?? icons.Get(item.Target, selected?.Handle ?? 0, selected?.AppId) : null;
        if (icon is not null) panel.Children.Add(new Image { Source = icon, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Stretch = Stretch.Uniform });
        else
        {
            bool settingsIcon = item.Target?.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) == true
                || string.Equals(System.IO.Path.GetFileName(selected?.Executable ?? item.Target ?? ""), "SystemSettings.exe", StringComparison.OrdinalIgnoreCase);
            panel.Children.Add(new Viewbox { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Stretch = Stretch.Uniform, Child = Glyph(item.Glyph ?? (settingsIcon ? "\uE713" : "\uE8A5"), 32) });
        }
        var indicator = new Border
        {
            Height = 3,
            Width = focused is not null ? 18 : minimized ? 10 : 5,
            CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 0, 4), VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = item.Windows.Count == 0 ? 0 : minimized ? .45 : 1
        };
        indicator.RenderTransform = new TranslateTransform(0, 7);
        indicator.SetResourceReference(Border.BackgroundProperty, focused is not null ? "Think.Accent" : "Think.Muted");
        panel.Children.Add(indicator);
        var dropIndicator = new Border { Height = 2, Width = 20, CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden };
        dropIndicator.RenderTransform = new TranslateTransform(0, 7);
        dropIndicator.SetResourceReference(Border.BackgroundProperty, "Think.Accent"); panel.Children.Add(dropIndicator);
        nint foregroundBeforeClick = 0;
        var button = ActionButton(item.Name, () =>
        {
            if (Store.IsEditing && item.Key != "system:add") return;
            if (item.Action is not null) item.Action();
            else
            {
                var current = item.Windows.FirstOrDefault(window => Desktop.IsActiveWindow(window.Handle, foregroundBeforeClick)) ?? selected;
                OpenDockItem(item, current, true, foregroundBeforeClick);
            }
            foregroundBeforeClick = 0;
        }, item.Name + (minimized ? " · minimizado" : focused is not null ? " · ativo" : item.Windows.Count > 0 ? " · em execução" : ""));
        button.PreviewMouseLeftButtonDown += (_, _) => foregroundBeforeClick = Desktop.GetForegroundWindow();
        button.PreviewKeyDown += (_, _) => foregroundBeforeClick = 0;
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = panel; button.Width = size + 2 * fit.Padding; button.Padding = new Thickness(fit.Padding, 4, fit.Padding, 4);
        button.Margin = new Thickness(fit.Gap / 2, 0, fit.Gap / 2, 0);
        var menu = new ContextMenu();
        if (item.Action is null && item.Target is string adminTarget && SystemActions.CanRunAsAdministrator(adminTarget))
            Menu(menu, "Executar como administrador…", () => SystemActions.RunAsAdministrator(adminTarget));
        foreach (var window in item.Windows) Menu(menu, (window.Minimized ? "Restaurar · " : "Abrir · ") + window.Title, () => OpenDockItem(item, window, false));
        if (item.Pin is not null) Menu(menu, "Remover atalho", () => Store.Update(p => p.Shortcuts.Remove(item.Pin)));
        else if (selected?.Executable is string executable) Menu(menu, "Fixar aplicativo", () => Store.Update(p =>
        {
            var pin = new Shortcut(System.IO.Path.GetFileNameWithoutExtension(executable), executable);
            if (!p.Shortcuts.Any(s => icons.Resolve(s.Target).Equals(executable, StringComparison.OrdinalIgnoreCase))) p.Shortcuts.Add(pin);
            int index = p.DockOrder.IndexOf(item.Key); if (index >= 0) p.DockOrder[index] = "pin:" + executable;
        }));
        Menu(menu, "Mover para a esquerda", () => MoveOne(item.Key, -1));
        if (item.Target is string uninstallTarget && item.Action is null && AppUninstall.CanOffer(uninstallTarget))
            Menu(menu, "Desinstalar…", async () => await AppUninstall.Open(item.Name, uninstallTarget));
        Menu(menu, "Mover para a direita", () => MoveOne(item.Key, 1));
        Menu(menu, Store.IsEditing ? "Concluir edição" : "Editar layout", () => Store.SetEditing(!Store.IsEditing));
        button.ContextMenu = menu;
        button.ContextMenuOpening += (_, _) =>
        {
            activeContextMenu = menu;
            contextMenuRequested = DateTime.UtcNow;
            lastHover = DateTime.UtcNow;
            StopPreviews(); magnifier.Reset();
            Activate();
        };
        menu.Closed += (_, _) => FinishContextMenu(menu);
        LayoutDrag.Attach(button, "dock", item.Key, () => Store.IsEditing, Reorder);
        AttachFileTarget(button, item, dropIndicator);
        AttachPreview(button, item);
        if (item.Key == "system:add") Menu(menu, "Adicionar executável manualmente…", AddShortcut);
        return button;
    }
    private bool openingDockItem;
    private async void OpenDockItem(Item item, Desktop.RunningWindow? selected, bool minimizeIfActive, nint foregroundBeforeClick = 0)
    {
        if (openingDockItem) return;
        openingDockItem = true;
        try
        {
            nint foregroundAtClick = foregroundBeforeClick != 0
                ? foregroundBeforeClick : Desktop.GetForegroundWindow();
            bool shouldMinimize = minimizeIfActive && selected is not null
                && Desktop.IsVisibleAndRestored(selected.Handle)
                && Desktop.IsActiveWindow(selected.Handle, foregroundAtClick);
            StopPreviews();
            // OSK is a UIAccess application: let its own shell activation
            // restore the existing instance instead of forcing foreground.
            if (selected?.Executable is string keyboard
                && System.IO.Path.GetFileName(keyboard).Equals("osk.exe", StringComparison.OrdinalIgnoreCase))
            {
                if (shouldMinimize
                    && Desktop.MinimizeWindow(selected.Handle)) return;
                SystemActions.Run(keyboard); return;
            }
            if (selected is not null && Desktop.WindowExists(selected.Handle))
            {
                if (await Desktop.ActivateFromDock(selected.Handle, shouldMinimize)) return;
                // A denied foreground request must not start another instance.
                if (Desktop.WindowExists(selected.Handle))
                {
                    if (ElevatedWindowAction.IsElevatedTarget(selected.Handle))
                    {
                        await ElevatedWindowAction.Request(selected.Handle, close: false,
                            minimize: shouldMinimize);
                        return;
                    }
                    MessageBox.Show("O Windows não permitiu ativar esta janela. Se o aplicativo estiver sendo executado como administrador, suas permissões podem impedir a ativação pelo launcher.", "Think Wind");
                    return;
                }
            }
            // The HWND captured by the previous refresh may already be gone.
            // In that case use the original pinned shortcut, not a stale handle.
            if (item.Target is not null) SystemActions.Run(item.Target);
        }
        finally { openingDockItem = false; }
    }
    private FrameworkElement SearchSlot(double width)
    {
        var slot = new Border { Width = width, Height = Math.Max(36, fit.Icon + 8), VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(14), Padding = new Thickness(3) };
        slot.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var magnify = ActionButton("Pesquisar aplicativos, arquivos e configurações", () => ShowPanel(LauncherPanelMode.Search)); magnify.Style = (Style)FindResource("Think.IconButton"); magnify.Content = Glyph("\uE721", 16); magnify.Padding = new Thickness(2); magnify.Margin = new Thickness(0); grid.Children.Add(magnify);
        if (Store.Value.DockSearchMode == "Bar")
        {
            if (dockSearch.Parent is Panel old) old.Children.Remove(dockSearch);
            dockSearch.BeginAnimation(WidthProperty, null);
            dockSearch.ToolTip = "Pesquisar no Windows"; dockSearch.Width = Math.Max(1, width - 42); dockSearch.HorizontalAlignment = HorizontalAlignment.Left; Grid.SetColumn(dockSearch, 1); grid.Children.Add(dockSearch);
        }
        slot.Child = grid; return slot;
    }
    private void AnimateSearch(bool expanded)
    {
        if (dockSearch.Parent is not Grid grid || grid.Parent is not Border slot) return;
        double maximum = Math.Max(1, slot.Width - 36), target = expanded ? maximum : Math.Max(1, maximum - 12);
        if (!SystemParameters.ClientAreaAnimation || Store.Value.AnimationMilliseconds == 0) { dockSearch.BeginAnimation(WidthProperty, null); dockSearch.Width = target; }
        else dockSearch.BeginAnimation(WidthProperty, new DoubleAnimation(dockSearch.ActualWidth, target, TimeSpan.FromMilliseconds(120)) { FillBehavior = FillBehavior.HoldEnd });
    }
    private void EnsureLauncher()
    {
        if (launcher is not null) return;
        launcher = new LauncherPanelWindow(Store, catalog, () => TargetBounds, () => dockSearch.IsKeyboardFocusWithin) { Owner = this, MonitorDeviceOverride = MonitorDeviceOverride };
        launcher.Dismissed += () =>
        {
            lastHover = DateTime.UtcNow; AnimateSearch(false);
            if (temporaryFullscreenStart) { temporaryFullscreenStart = false; Hide(); }
        };
    }
    private void ShowPanel(LauncherPanelMode mode)
    { StopPreviews(); SetHidden(false, false); EnsureLauncher(); panelMode = mode; launcher!.Present(mode); }
    private void ToggleStart()
    { if (launcher?.IsVisible == true && panelMode == LauncherPanelMode.Start) launcher.Dismiss(); else ShowPanel(LauncherPanelMode.Start); }
    internal void ToggleStartFromKeyboard(bool overFullscreen = false)
    {
        temporaryFullscreenStart |= overFullscreen && !IsVisible;
        if (!IsVisible) Show();
        ToggleStart();
        if (launcher?.IsVisible == true) launcher.FocusSearch();
    }
    public override void RefreshAppearance() { base.RefreshAppearance(); magnifier.Configure(Store.Value, magnificationReserve); launcher?.RefreshAppearance(); }
    private void MoveOne(string source, int direction)
    {
        int index = visibleOrder.IndexOf(source), targetIndex = index + direction;
        if (index < 0 || targetIndex < 0 || targetIndex >= visibleOrder.Count) return;
        Reorder(source, visibleOrder[targetIndex], direction > 0);
    }
    private void Reorder(string source, string target, bool after) => Store.Update(p =>
    {
        p.DockOrder = visibleOrder.Concat(p.DockOrder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        LayoutCatalog.Move(p.DockOrder, source, target, after);
    });
    private static void Menu(ContextMenu menu, string label, Action action)
    { var entry = new MenuItem { Header = label }; entry.Click += (_, _) => action(); menu.Items.Add(entry); }
    private void AddShortcut()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Selecionar aplicativo ou atalho", Filter = "Aplicativos e atalhos|*.exe;*.lnk|Todos os arquivos|*.*" };
        if (dialog.ShowDialog() == true && !Store.Value.Shortcuts.Any(s => s.Target.Equals(dialog.FileName, StringComparison.OrdinalIgnoreCase)))
            Store.Update(p => p.Shortcuts.Add(new Shortcut(System.IO.Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName)));
    }
}

// WPF's insertion adorners need a layer inside each independently hosted overlay.
internal sealed class AdornerDecoratorHost : System.Windows.Documents.AdornerDecorator
{
    private readonly bool overflow;
    internal AdornerDecoratorHost(UIElement child, bool allowOverflow = false) { overflow = allowOverflow; Child = child; ClipToBounds = false; }
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => overflow ? null : base.GetLayoutClip(layoutSlotSize);
}
