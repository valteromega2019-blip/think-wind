using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class DockWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    private readonly StackPanel items = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Action settings;
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
    private double TotalWidth => contentWidth + Surface.Padding.Left + Surface.Padding.Right;
    private double ExpandedHeight => Math.Max(44, contentHeight + Surface.Padding.Top + Surface.Padding.Bottom);
    private DateTime lastHover = DateTime.UtcNow;
    private sealed record Item(string Key, string Name, string? Target, string? Glyph, List<Desktop.RunningWindow> Windows, Shortcut? Pin, Action? Action);
    public DockWindow(SettingsStore store, Action openSettings) : base(store)
    {
        settings = openSettings;
        Title = "Think Wind — Dock";
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(NoActivate);
        Surface.Child = new AdornerDecoratorHost(items);
        Loaded += (_, _) => { RefreshSettings(); timer.Start(); };
        timer.Tick += (_, _) => { if (IsVisible) { RefreshApps(); UpdateVisibility(); } };
        MouseEnter += (_, _) => { lastHover = DateTime.UtcNow; SetHidden(false); };
        MouseLeave += (_, _) => lastHover = DateTime.UtcNow;
        LayoutDrag.Completed += DragCompleted;
        Closed += (_, _) => { timer.Stop(); LayoutDrag.Completed -= DragCompleted; };
    }
    private void DragCompleted() { signature = ""; RefreshApps(); }
    private nint NoActivate(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x0021) { handled = true; return 3; } return 0; }
    public override void Position()
    {
        if (!rebuilding && Math.Abs(fittedWidth - Display.Width) > .5 && entries.Count > 0) RenderPage();
        Place(TotalWidth, hidden ? 7 : ExpandedHeight);
    }
    public override void RefreshSettings()
    {
        if (LayoutDrag.Active || rebuilding) return;
        if (layoutRevision != Store.DockLayoutRevision) { layoutRevision = Store.DockLayoutRevision; visibleOrder.Clear(); }
        signature = "";
        if (!Store.Value.AutoHide || Store.IsEditing || Store.IsCustomizing) hidden = false;
        items.Visibility = hidden ? Visibility.Hidden : Visibility.Visible;
        ApplyAppearance(); RefreshApps();
    }
    private bool MenuOpen => items.Children.OfType<Button>().Any(b => b.ContextMenu?.IsOpen == true);
    private void UpdateVisibility()
    {
        if (Store.Value.AutoHide && !Store.IsEditing && !Store.IsCustomizing && !LayoutDrag.Active && !MenuOpen && !IsMouseOver && !IsKeyboardFocusWithin && DateTime.UtcNow - lastHover > TimeSpan.FromSeconds(3)) SetHidden(true);
    }
    private void SetHidden(bool value)
    {
        if (hidden == value) return;
        hidden = value; items.Visibility = value ? Visibility.Hidden : Visibility.Visible;
        Place(TotalWidth, hidden ? 7 : ExpandedHeight, true);
    }
    private void RefreshApps()
    {
        if (LayoutDrag.Active || MenuOpen || rebuilding) return;
        var windows = Desktop.Windows();
        var active = Desktop.GetForegroundWindow();
        var next = string.Join("|", windows.Select(w => $"{w.Handle}:{w.Title}:{w.Minimized}")) + active;
        if (next == signature) return;
        rebuilding = true;
        try
        {
            signature = next;
            var entries = new List<Item> { new("system:start", "Iniciar", null, "\uE782", new(), null, () => Desktop.Key(0x5B)) };
            var pinnedExecutables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pin in Store.Value.Shortcuts)
            {
                var path = icons.Resolve(pin.Target);
                var matching = windows.Where(w => string.Equals(w.Executable, path, StringComparison.OrdinalIgnoreCase)).ToList();
                if (matching.Count > 0) pinnedExecutables.Add(path);
                entries.Add(new("pin:" + pin.Target, pin.Name, pin.Target, null, matching, pin, null));
            }
            foreach (var group in windows.Where(w => w.Executable is null || !pinnedExecutables.Contains(w.Executable))
                .GroupBy(w => w.Executable ?? "window:" + w.Handle, StringComparer.OrdinalIgnoreCase))
            {
                var groupWindows = group.ToList();
                var first = groupWindows[0];
                entries.Add(new("app:" + group.Key, first.Title, first.Executable, null, groupWindows, null, null));
            }
            entries.Add(new("system:add", "Adicionar atalho", null, "\uE710", new(), null, AddShortcut));
            entries.Add(new("system:settings", "Personalizar Think Wind", null, "\uE713", new(), null, settings));
            var present = entries.Select(i => i.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var order = Store.Value.DockOrder.Concat(visibleOrder).Where(present.Contains).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var entry in entries.Where(i => !order.Contains(i.Key, StringComparer.OrdinalIgnoreCase)))
            {
                int insertion = order.Count;
                if (entry.Key is not ("system:add" or "system:settings"))
                    while (insertion > 0 && (order[insertion - 1] is "system:add" or "system:settings")) insertion--;
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
        double available = Math.Max(60, fittedWidth - 16 - Surface.Padding.Left - Surface.Padding.Right - (Store.IsEditing ? 30 : 0));
        fit = CompactLayout.FitDock(entries.Count, available, Store.Value.IconSize * Store.Value.DockScale, Store.Value.DockSpacing);
        int pages = fit.Pages(entries.Count); page = Math.Clamp(page, 0, pages - 1);
        items.Children.Clear();
        if (Store.IsEditing)
        {
            var grip = Glyph("\uE784", 16); grip.Width = 22; grip.Margin = new Thickness(4);
            grip.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); AttachPositionGrip(grip); items.Children.Add(grip);
        }
        if (pages > 1) items.Children.Add(PageButton(-1, pages));
        foreach (var entry in entries.Skip(page * fit.PerPage).Take(fit.PerPage)) items.Children.Add(CreateButton(entry, activeWindow));
        if (pages > 1) items.Children.Add(PageButton(1, pages));
        items.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        contentWidth = items.DesiredSize.Width; contentHeight = items.DesiredSize.Height;
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
        }
        return button;
    }
    private Button CreateButton(Item item, nint active)
    {
        var focused = item.Windows.FirstOrDefault(w => w.Handle == active);
        var selected = focused ?? item.Windows.FirstOrDefault(w => !w.Minimized) ?? item.Windows.FirstOrDefault();
        bool minimized = item.Windows.Count > 0 && item.Windows.All(w => w.Minimized);
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var size = fit.Icon;
        var icon = item.Glyph is null ? icons.Get(item.Target, selected?.Handle ?? 0) : null;
        if (icon is not null) panel.Children.Add(new Image { Source = icon, Width = size, Height = size, Stretch = Stretch.Uniform });
        else panel.Children.Add(new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = Glyph(item.Glyph ?? "\uE8A5", 32) });
        var indicator = new Border
        {
            Height = 3,
            Width = focused is not null ? 18 : minimized ? 10 : 5,
            CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 6, 0, 0),
            Opacity = item.Windows.Count == 0 ? 0 : minimized ? .45 : 1
        };
        indicator.SetResourceReference(Border.BackgroundProperty, focused is not null ? "Think.Accent" : "Think.Muted");
        panel.Children.Add(indicator);
        var button = ActionButton(item.Name, () =>
        {
            if (Store.IsEditing && item.Key is not ("system:add" or "system:settings")) return;
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
        return button;
    }
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
