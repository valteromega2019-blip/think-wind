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
internal sealed partial class DockWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool CollapsedDock => hidden;
    private readonly StackPanel items = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly DockMagnifier magnifier;
    private double magnificationReserve;
    private double magnificationHeadroom;
    private readonly AdornerDecoratorHost iconLayer;
    protected override Thickness TransparentInsets
    {
        get
        {
            double progress = 1;
            if (hidden)
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
    private readonly TextBox dockSearch = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center, MaxLength = 160 };
    private DateTime nextAppRefresh;
    private readonly ShellIcons icons = new();
    private readonly List<string> visibleOrder = new();
    private string signature = "";
    private bool hidden, rebuilding;
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
        magnifier = new DockMagnifier(items, () => !hidden && !Store.IsEditing && !LayoutDrag.Active && !fileDragging && !MenuOpen);
        catalog = new ApplicationCatalog(Dispatcher);
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
        iconLayer = new AdornerDecoratorHost(items);
        ((Grid)Content).Children.Add(iconLayer);
        Loaded += (_, _) => { RefreshSettings(); timer.Start(); };
        timer.Tick += (_, _) => { if (!IsVisible) return; PollFileDrag(); if (DateTime.UtcNow >= nextAppRefresh) { nextAppRefresh = DateTime.UtcNow.AddSeconds(2); RefreshApps(); } UpdateVisibility(); };
        MouseEnter += (_, _) => { lastHover = DateTime.UtcNow; SetHidden(false); };
        MouseLeave += (_, _) => lastHover = DateTime.UtcNow;
        LayoutDrag.Completed += DragCompleted;
        InitializeFileDrop();
        Closed += (_, _) => { magnifier.Dispose(); timer.Stop(); DisposeFileDrop(); catalog.Dispose(); LayoutDrag.Completed -= DragCompleted; };
    }
    private void DragCompleted() { signature = ""; RefreshApps(); }
    private nint NoActivate(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x0021 && !dockSearch.IsMouseOver) { handled = true; return 3; } return 0; }
    public override void Position()
    {
        if (!rebuilding && Math.Abs(fittedWidth - Display.Width) > .5 && entries.Count > 0) RenderPage();
        Place(TotalWidth, hidden ? 6 : ExpandedHeight);
        if (launcher?.IsVisible == true) launcher.Position();
    }
    public override void RefreshSettings()
    {
        if (LayoutDrag.Active || rebuilding) return;
        if (layoutRevision != Store.DockLayoutRevision) { layoutRevision = Store.DockLayoutRevision; visibleOrder.Clear(); }
        signature = "";
        if (!Store.Value.AutoHide || Store.IsEditing) hidden = false;
        items.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
        ApplyAppearance(); RefreshApps();
        launcher?.RefreshSettings();
    }
    private bool MenuOpen => items.Children.OfType<Button>().Any(b => b.ContextMenu?.IsOpen == true);
    private void UpdateVisibility()
    {
        if (Store.Value.AutoHide && !Store.IsEditing && !Store.IsCustomizing && !LayoutDrag.Active && !fileDragging && launcher?.Busy != true && !MenuOpen && !IsMouseOver && (!IsKeyboardFocusWithin || !Desktop.ForegroundIsOurs()) && DateTime.UtcNow - lastHover >= TimeSpan.FromSeconds(Store.Value.DockCollapseSeconds)) SetHidden(true);
    }
    private void SetHidden(bool value, bool animate = true)
    {
        if (hidden == value) return;
        hidden = value; items.Visibility = value ? Visibility.Hidden : Visibility.Visible;
        if (value) magnifier.Reset(); else magnifier.Wake();
        ApplyAppearance(); Place(TotalWidth, hidden ? 6 : ExpandedHeight, animate);
    }
    private void RefreshApps()
    {
        if (LayoutDrag.Active || fileDragging || MenuOpen || rebuilding || dockSearch.IsKeyboardFocusWithin || (items.IsMouseOver && entries.Count > 0 && signature.Length > 0)) return;
        var windows = Desktop.Windows();
        var active = Desktop.GetForegroundWindow();
        var next = string.Join("|", windows.Select(w => $"{w.Handle}:{w.Title}:{w.Minimized}:{w.AppId}")) + active;
        if (next == signature) return;
        rebuilding = true;
        try
        {
            signature = next;
            var entries = new List<Item>();
            var pinnedWindows = new HashSet<nint>();
            foreach (var pin in Store.Value.Shortcuts)
            {
                var path = icons.Resolve(pin.Target);
                string? appId = pin.Target.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase) ? pin.Target.Substring(@"shell:AppsFolder\".Length) : catalog.Items.FirstOrDefault(app => app.Target.Equals(pin.Target, StringComparison.OrdinalIgnoreCase))?.AppId;
                var matching = windows.Where(w => string.Equals(w.Executable, path, StringComparison.OrdinalIgnoreCase) || (appId is not null && string.Equals(w.AppId, appId, StringComparison.OrdinalIgnoreCase))).ToList();
                foreach (var window in matching) pinnedWindows.Add(window.Handle);
                entries.Add(new("pin:" + pin.Target, pin.Name, pin.Target, null, matching, pin, null));
            }
            foreach (var group in windows.Where(w => !pinnedWindows.Contains(w.Handle))
                .GroupBy(w => w.AppId ?? w.Executable ?? "window:" + w.Handle, StringComparer.OrdinalIgnoreCase))
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
    private void RenderPage()
    {
        fittedWidth = Display.Width;
        double searchWidth = Store.Value.DockSearchMode == "Bar" ? Math.Min(144, Math.Max(48, fittedWidth * .22)) : 36;
        double available = Math.Max(60, fittedWidth - 16 - 20 - 48 - searchWidth - (Store.IsEditing ? 30 : 0));
        var p = Store.Value;
        bool magnify = p.DockMagnification && SystemParameters.ClientAreaAnimation && !Store.IsEditing;
        magnificationReserve = magnify ? Math.Min(available * .32, (p.IconSize * p.DockScale + 20) * p.DockMagnificationAmount * (p.DockMagnificationNeighbors + 1) * 1.12) : 0;
        fit = CompactLayout.FitDock(entries.Count, available - magnificationReserve, p.IconSize * p.DockScale, p.DockSpacing);
        // Fixed headroom belongs to the expanded layout, never to mouse frames.
        // The same alpha-clipped Acrylic surface encloses every enlarged icon.
        magnificationHeadroom = magnify ? (fit.Icon + 26) * p.DockMagnificationAmount * 1.08 : 0;
        Surface.Padding = new Thickness(10); Surface.Margin = TransparentInsets;
        iconLayer.Margin = new Thickness(10 + magnificationReserve / 2, 10 + magnificationHeadroom, 10 + magnificationReserve / 2, 10);
        int pages = fit.Pages(entries.Count); page = Math.Clamp(page, 0, pages - 1);
        items.Children.Clear();
        var start = ActionButton("Iniciar Think Wind", ToggleStart); start.Style = (Style)FindResource("Think.IconButton"); start.Content = WindowsStartIcon(fit.Icon); start.Width = 40; start.Height = fit.Icon + 22; start.Padding = new Thickness(4); items.Children.Add(start);
        items.Children.Add(SearchSlot(searchWidth));
        start.Margin = new Thickness(0, 0, 8, 0);
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
        items.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        contentWidth = items.DesiredSize.Width; contentHeight = items.DesiredSize.Height;
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
        var icon = item.Glyph is null ? icons.Get(item.Target, selected?.Handle ?? 0) : null;
        if (icon is not null) panel.Children.Add(new Image { Source = icon, Width = size, Height = size, Stretch = Stretch.Uniform });
        else panel.Children.Add(new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = Glyph(item.Glyph ?? "\uE8A5", 32) });
        var indicator = new Border
        {
            Height = 3,
            Width = focused is not null ? 18 : minimized ? 10 : 5,
            CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 0, 4), VerticalAlignment = VerticalAlignment.Bottom,
            Opacity = item.Windows.Count == 0 ? 0 : minimized ? .45 : 1
        };
        indicator.SetResourceReference(Border.BackgroundProperty, focused is not null ? "Think.Accent" : "Think.Muted");
        panel.Children.Add(indicator);
        var dropIndicator = new Border { Height = 2, Width = 20, CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Hidden };
        dropIndicator.SetResourceReference(Border.BackgroundProperty, "Think.Accent"); panel.Children.Add(dropIndicator);
        var button = ActionButton(item.Name, () =>
        {
            if (Store.IsEditing && item.Key != "system:add") return;
            if (item.Action is not null) item.Action();
            else if (selected is not null) Desktop.Activate(selected.Handle);
            else if (item.Target is not null) SystemActions.Run(item.Target);
        }, item.Name + (minimized ? " · minimizado" : focused is not null ? " · ativo" : item.Windows.Count > 0 ? " · em execução" : ""));
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = panel; button.Width = size + 2 * fit.Padding; button.Padding = new Thickness(fit.Padding, 4, fit.Padding, 4);
        button.Margin = new Thickness(fit.Gap / 2, 0, fit.Gap / 2, 0);
        var menu = new ContextMenu();
        foreach (var window in item.Windows) Menu(menu, (window.Minimized ? "Restaurar · " : "Abrir · ") + window.Title, () => Desktop.Activate(window.Handle));
        if (item.Pin is not null) Menu(menu, "Remover atalho", () => Store.Update(p => p.Shortcuts.Remove(item.Pin)));
        else if (selected?.Executable is string executable) Menu(menu, "Fixar aplicativo", () => Store.Update(p =>
        {
            var pin = new Shortcut(System.IO.Path.GetFileNameWithoutExtension(executable), executable);
            if (!p.Shortcuts.Any(s => icons.Resolve(s.Target).Equals(executable, StringComparison.OrdinalIgnoreCase))) p.Shortcuts.Add(pin);
            int index = p.DockOrder.IndexOf(item.Key); if (index >= 0) p.DockOrder[index] = "pin:" + executable;
        }));
        Menu(menu, "Mover para a esquerda", () => MoveOne(item.Key, -1));
        Menu(menu, "Mover para a direita", () => MoveOne(item.Key, 1));
        Menu(menu, Store.IsEditing ? "Concluir edição" : "Editar layout", () => Store.SetEditing(!Store.IsEditing));
        button.ContextMenu = menu;
        menu.Closed += (_, _) => { signature = ""; Dispatcher.BeginInvoke(new Action(RefreshApps)); };
        LayoutDrag.Attach(button, "dock", item.Key, () => Store.IsEditing, Reorder);
        AttachFileTarget(button, item, dropIndicator);
        if (item.Key == "system:add") Menu(menu, "Adicionar executável manualmente…", AddShortcut);
        return button;
    }
    private FrameworkElement SearchSlot(double width)
    {
        var slot = new Border { Width = width, Height = Math.Max(36, fit.Icon + 8), VerticalAlignment = VerticalAlignment.Center, CornerRadius = new CornerRadius(14), Padding = new Thickness(3), Margin = new Thickness(0) };
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
        launcher = new LauncherPanelWindow(Store, catalog, () => TargetBounds, () => dockSearch.IsKeyboardFocusWithin) { Owner = this };
        launcher.Dismissed += () => { lastHover = DateTime.UtcNow; AnimateSearch(false); };
    }
    private void ShowPanel(LauncherPanelMode mode)
    { SetHidden(false, false); EnsureLauncher(); panelMode = mode; launcher!.Present(mode); }
    private void ToggleStart()
    { if (launcher?.IsVisible == true && panelMode == LauncherPanelMode.Start) launcher.Dismiss(); else ShowPanel(LauncherPanelMode.Start); }
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
    internal AdornerDecoratorHost(UIElement child) { Child = child; }
}
