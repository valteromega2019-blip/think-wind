using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal enum LauncherPanelMode { Start, Library, Search }
internal sealed class LauncherPanelWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    private readonly ApplicationCatalog catalog;
    private readonly LauncherSearch search = new();
    private readonly ShellIcons icons = new();
    private readonly StaWorker imageWorker = new("Think Wind — ícones da gaveta");
    private readonly Dictionary<string, ImageSource> largeIcons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task<ImageSource?>> pendingIcons = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<OverlayChrome.PixelRect> anchor;
    private readonly Func<bool> dockHasFocus;
    private readonly StackPanel content = new();
    private readonly Viewbox viewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly TextBlock title = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 2, 6, 10) };
    private readonly TextBox query = new() { MinHeight = 38, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(4), MaxLength = 160 };
    private readonly ComboBox category = new() { Margin = new Thickness(4), MinHeight = 30, MaxDropDownHeight = 220 };
    private readonly UniformGrid resultsGrid = new() { Columns = 2 };
    private readonly TextBlock notice = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 8, 6, 8) };
    private readonly TextBlock pagination = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8) };
    private readonly Button previous = new() { Content = "‹", ToolTip = "Página anterior" }, next = new() { Content = "›", ToolTip = "Próxima página" };
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer visibility = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(30) };
    private CancellationTokenSource? cancellation;
    private LauncherPanelMode mode;
    private string section = "Fixados";
    private List<AppEntry> results = new();
    private AppEntry[] recent = Array.Empty<AppEntry>();
    private int page, perPage = 8, generation, menus;
    private bool updating, disposed, rowsQueued, modal;
    internal bool Busy => IsVisible || menus > 0 || modal;
    internal event Action? Dismissed;
    internal LauncherPanelWindow(SettingsStore store, ApplicationCatalog apps, Func<OverlayChrome.PixelRect> getAnchor, Func<bool> hasDockFocus) : base(store)
    {
        catalog = apps; anchor = getAnchor; dockHasFocus = hasDockFocus; Title = "Think Wind — Iniciar e pesquisa";
        ShowActivated = true; Surface.Padding = new Thickness(14);
        content.Children.Add(title);
        var queryFrame = new Border { Child = query, CornerRadius = new CornerRadius(14), Margin = new Thickness(4) }; queryFrame.SetResourceReference(Border.BackgroundProperty, "Think.Control"); content.Children.Add(queryFrame);
        query.Background = Brushes.Transparent; query.Margin = new Thickness(0); query.SetResourceReference(TextBox.ForegroundProperty, "Think.Text"); query.BorderThickness = new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(query, "Pesquisar aplicativos, arquivos, documentos e configurações");
        var tabs = new WrapPanel();
        foreach (string name in new[] { "Fixados", "Recentes", "Todos", "Biblioteca" })
        {
            var button = ActionButton(name, () => { section = name; mode = name == "Biblioteca" ? LauncherPanelMode.Library : LauncherPanelMode.Start; query.Text = ""; page = 0; Reload(); });
            button.Padding = new Thickness(10, 6, 10, 6); tabs.Children.Add(button);
        }
        content.Children.Add(tabs); content.Children.Add(category); content.Children.Add(new AdornerDecoratorHost(resultsGrid));
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        previous.Click += (_, _) => { page--; RenderRows(); }; next.Click += (_, _) => { page++; RenderRows(); };
        navigation.Children.Add(previous); navigation.Children.Add(pagination); navigation.Children.Add(next); content.Children.Add(navigation); content.Children.Add(notice);
        var footer = new DockPanel { Margin = new Thickness(4, 6, 4, 0) };
        var power = ActionButton("Energia", () => { }); power.Content = Glyph("\uE7E8", 18); power.Width = 38; power.Height = 34; power.Padding = new Thickness(4);
        var powerMenu = new ContextMenu();
        foreach (string action in new[] { "Suspender", "Reiniciar", "Desligar" }) AddMenu(powerMenu, action, () => { modal = true; try { Store.Flush(); SystemActions.Power(action); } finally { modal = false; } });
        TrackMenu(powerMenu); power.Click += (_, _) => { powerMenu.PlacementTarget = power; powerMenu.IsOpen = true; };
        DockPanel.SetDock(power, Dock.Right); footer.Children.Add(power);
        var manual = ActionButton("Executável manual…", ManualShortcut); manual.FontSize = 10; manual.Padding = new Thickness(6, 4, 6, 4); DockPanel.SetDock(manual, Dock.Right); footer.Children.Add(manual);
        footer.Children.Add(new TextBlock { Text = Environment.UserName, ToolTip = Environment.UserDomainName + "\\" + Environment.UserName, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }); content.Children.Add(footer);
        viewport.Child = content; Surface.Child = viewport;
        query.TextChanged += (_, _) => { if (!updating) { page = 0; Reload(); } };
        query.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && results.Count > 0) { Launch(results[0]); e.Handled = true; } };
        category.SelectionChanged += (_, _) => { if (!updating) { page = 0; Reload(); } };
        debounce.Tick += async (_, _) => { debounce.Stop(); await ReadFiles(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; } };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (!IsActive && !dockHasFocus() && menus == 0 && !modal) Dismiss(); }));
        visibility.Tick += (_, _) => { if (IsVisible && menus == 0 && !modal && !Desktop.ForegroundIsOurs()) Dismiss(); };
        refresh.Tick += async (_, _) => { if (IsVisible) await catalog.RefreshAsync(true); };
        catalog.Changed += CatalogChanged; Store.Changed += PreferencesChanged;
        LayoutDrag.Completed += LayoutCompleted;
        Closed += (_, _) => { disposed = true; imageWorker.Dispose(); cancellation?.Cancel(); debounce.Stop(); visibility.Stop(); refresh.Stop(); search.Dispose(); catalog.Changed -= CatalogChanged; Store.Changed -= PreferencesChanged; LayoutDrag.Completed -= LayoutCompleted; };
    }
    internal async void Present(LauncherPanelMode requested, string text = "", bool focusSearch = true)
    {
        mode = requested; section = requested == LauncherPanelMode.Library ? "Biblioteca" : requested == LauncherPanelMode.Start ? "Fixados" : "Todos";
        updating = true; query.Text = text; updating = false; page = 0;
        UpdateCategories(); Reload(); ShowActivated = focusSearch;
        Show(); RefreshSettings(); visibility.Start(); refresh.Start();
        if (focusSearch) { Activate(); query.Focus(); query.CaretIndex = query.Text.Length; }
        if (SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0) content.BeginAnimation(OpacityProperty, new DoubleAnimation(.75, 1, TimeSpan.FromMilliseconds(120)));
        await catalog.RefreshAsync();
        try { var items = await search.RecentAsync(); if (!disposed) { recent = items; if (IsVisible) Reload(false); } }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or COMException or TaskCanceledException) { }
    }
    internal void SearchFromDock(string text)
    {
        if (!IsVisible || mode != LauncherPanelMode.Search) { Present(LauncherPanelMode.Search, text, false); return; }
        updating = true; query.Text = text; updating = false; page = 0; Reload();
    }
    internal void OpenFirstResult() { if (IsVisible && results.Count > 0) Launch(results[0]); }
    internal void FocusSearch() { if (IsVisible) { Activate(); query.Focus(); query.CaretIndex = query.Text.Length; } }
    internal void Dismiss()
    {
        if (!IsVisible) return;
        cancellation?.Cancel(); debounce.Stop(); visibility.Stop(); refresh.Stop(); Hide(); Store.Flush(); Dismissed?.Invoke();
    }
    private void CatalogChanged() { if (!IsVisible) return; UpdateCategories(); Reload(false); }
    private void LayoutCompleted() { if (IsVisible) Reload(false); }
    private void PreferencesChanged(object? sender, EventArgs args)
    {
        RefreshAppearance();
        if (!IsVisible || rowsQueued) return; rowsQueued = true;
        Dispatcher.BeginInvoke(new Action(() => { rowsQueued = false; if (IsVisible && menus == 0 && !LayoutDrag.Active) Reload(false); }));
    }
    private void UpdateCategories()
    {
        string selected = category.SelectedItem as string ?? "Todas as categorias"; updating = true;
        category.ItemsSource = new[] { "Todas as categorias", "Ordem alfabética" }.Concat(catalog.Items.Select(app => app.Category).Distinct().OrderBy(name => name)).ToArray();
        category.SelectedItem = category.Items.Cast<string>().Contains(selected) ? selected : "Todas as categorias"; updating = false;
    }
    private AppEntry FromShortcut(Shortcut shortcut)
    {
        var app = catalog.Items.FirstOrDefault(entry => entry.Target.Equals(shortcut.Target, StringComparison.OrdinalIgnoreCase));
        return app is null ? new(shortcut.Name, shortcut.Target, "Atalho", icons.Get(shortcut.Target)) : app with { Name = shortcut.Name, Target = shortcut.Target };
    }
    private void Reload(bool reset = true)
    {
        generation++; cancellation?.Cancel(); cancellation?.Dispose(); cancellation = new CancellationTokenSource(); debounce.Stop(); if (reset) page = 0;
        string term = query.Text.Trim(); title.Text = mode == LauncherPanelMode.Library ? "Biblioteca de Aplicativos" : term.Length > 0 || mode == LauncherPanelMode.Search ? "Pesquisar no desktop" : "Iniciar";
        category.Visibility = (section is "Todos" or "Biblioteca") && term.Length == 0 ? Visibility.Visible : Visibility.Hidden;
        IEnumerable<AppEntry> source;
        if (term.Length > 0)
        {
            var local = mode == LauncherPanelMode.Library ? catalog.Items : catalog.Items.Concat(LauncherSearch.Settings).Concat(Store.Value.RecentLaunches.Select(FromShortcut)).Concat(recent);
            source = local.Where(app => app.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) || app.Category.Contains(term, StringComparison.CurrentCultureIgnoreCase));
        }
        else source = section switch { "Fixados" => Store.Value.StartPins.Select(FromShortcut), "Recentes" => Store.Value.RecentLaunches.Select(FromShortcut).Concat(recent), _ => catalog.Items };
        if (term.Length == 0 && (section is "Todos" or "Biblioteca"))
        {
            string filter = category.SelectedItem as string ?? "Todas as categorias";
            if (filter is not ("Todas as categorias" or "Ordem alfabética")) source = source.Where(app => app.Category == filter);
            source = filter == "Todas as categorias" ? source.OrderBy(app => app.Category).ThenBy(app => app.Name) : source.OrderBy(app => app.Name);
        }
        results = source.DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).ToList();
        notice.Text = catalog.Warning ?? (results.Count == 0 ? "Nenhum item nesta seção. Use Todos ou Biblioteca para fixar aplicativos." : "Botão direito: fixar, remover atalho e reorganizar.");
        if (term.Length >= 2 && mode != LauncherPanelMode.Library) { notice.Text = "Consultando também o índice de arquivos do Windows…"; debounce.Start(); }
        RenderRows();
    }
    private async Task ReadFiles()
    {
        int current = generation; string term = query.Text.Trim(); var token = cancellation!.Token;
        try
        {
            var batch = await search.FilesAsync(term, token);
            if (disposed || !IsVisible || current != generation || token.IsCancellationRequested) return;
            results = results.Concat(batch.Items).DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).ToList(); notice.Text = batch.Notice ?? notice.Text; RenderRows();
        }
        catch (OperationCanceledException) { }
    }
    private void RenderRows()
    {
        int pages = Math.Max(1, (results.Count + perPage - 1) / perPage); page = Math.Clamp(page, 0, pages - 1);
        resultsGrid.Children.Clear();
        foreach (var entry in results.Skip(page * perPage).Take(perPage)) resultsGrid.Children.Add(ResultRow(entry));
        pagination.Text = $"{page + 1} / {pages} · {results.Count} itens"; previous.IsEnabled = page > 0; next.IsEnabled = page + 1 < pages;
        Position();
    }
    private FrameworkElement ResultRow(AppEntry entry)
    {
        var main = ActionButton(entry.Name, () => Launch(entry)); main.Style = (Style)FindResource("Think.IconButton"); main.Padding = new Thickness(6); main.ToolTip = entry.Name;
        var inside = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var icon = entry.Icon ?? icons.Get(entry.Target);
        var image = new Image { Source = icon, Width = 56, Height = 56, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality); inside.Children.Add(image);
        LoadLargeIcon(image, entry.Target);
        inside.Children.Add(new TextBlock { Text = entry.Name, FontSize = 12, LineHeight = 16, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, Height = 32, Width = 96, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 8, 0, 0) });
        main.Content = inside;
        var card = new Border { Child = main, Margin = new Thickness(4), Height = AppDrawerLayout.CellHeight - 8 };
        if (mode == LauncherPanelMode.Library)
        {
            card.Child = null; var tile = new Grid(); tile.Children.Add(main); card.Child = tile;
            bool pinned = Store.Value.Shortcuts.Any(pin => SameTarget(pin.Target, entry.Target));
            var add = ActionButton(pinned ? "Já está na barra" : "Adicionar à barra", () => PinDock(entry));
            add.Style = (Style)FindResource("Think.IconButton"); add.Content = Glyph(pinned ? "\uE73E" : "\uE710", 13);
            add.IsEnabled = !pinned; add.Padding = new Thickness(2); add.Width = add.Height = 22;
            add.HorizontalAlignment = HorizontalAlignment.Right; add.VerticalAlignment = VerticalAlignment.Top;
            add.SetBinding(VisibilityProperty, new System.Windows.Data.Binding(nameof(IsMouseOver)) { Source = card, Converter = new BooleanToVisibilityConverter() });
            tile.Children.Add(add);
        }
        var menu = new ContextMenu(); AddMenu(menu, "Abrir", () => Launch(entry)); AddMenu(menu, "Fixar na barra", () => PinDock(entry));
        bool startPinned = Store.Value.StartPins.Any(pin => SameTarget(pin.Target, entry.Target));
        AddMenu(menu, startPinned ? "Remover dos fixados do Iniciar" : "Fixar no Iniciar", () => Store.Update(p => { if (startPinned) p.StartPins.RemoveAll(pin => SameTarget(pin.Target, entry.Target)); else p.StartPins.Add(new(entry.Name, entry.Target)); }));
        if (startPinned) { AddMenu(menu, "Mover fixado para a esquerda", () => MovePin(entry.Target, -1)); AddMenu(menu, "Mover fixado para a direita", () => MovePin(entry.Target, 1)); }
        if (Store.Value.Shortcuts.Any(pin => SameTarget(pin.Target, entry.Target))) AddMenu(menu, "Remover da barra", () => Store.Update(p => p.Shortcuts.RemoveAll(pin => SameTarget(pin.Target, entry.Target))));
        TrackMenu(menu); card.ContextMenu = menu;
        LayoutDrag.Attach(card, "start-pins", entry.Target, () => mode == LauncherPanelMode.Start && section == "Fixados" && query.Text.Length == 0, ReorderPins);
        return card;
    }
    private bool SameTarget(string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase)) return true;
        if (left.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase) || right.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) return false;
        return icons.Resolve(left).Equals(icons.Resolve(right), StringComparison.OrdinalIgnoreCase);
    }
    private async void LoadLargeIcon(Image image, string target)
    {
        if (largeIcons.TryGetValue(target, out var cached)) { image.Source = cached; return; }
        if (!pendingIcons.TryGetValue(target, out var pending)) { pending = imageWorker.Run(() => ShellIcons.ReadLarge(target)); pendingIcons[target] = pending; }
        try
        {
            var result = await pending;
            if (disposed || result is null) return;
            if (largeIcons.Count > 256) largeIcons.Clear(); largeIcons[target] = result; image.Source = result;
        }
        catch (TaskCanceledException) { }
        finally { pendingIcons.Remove(target); }
    }
    private void PinDock(AppEntry app) => Store.Update(p => { if (!p.Shortcuts.Any(pin => SameTarget(pin.Target, app.Target))) p.Shortcuts.Add(new(app.Name, app.Target)); });
    private void Launch(AppEntry entry)
    {
        if (LayoutDrag.Active || !SystemActions.Launch(entry.Target)) return;
        Store.Update(p => { p.RecentLaunches.RemoveAll(item => item.Target.Equals(entry.Target, StringComparison.OrdinalIgnoreCase)); p.RecentLaunches.Insert(0, new(entry.Name, entry.Target)); }); Dismiss();
    }
    private void MovePin(string source, int direction)
    {
        int index = Store.Value.StartPins.FindIndex(pin => pin.Target == source), target = index + direction;
        if (index >= 0 && target >= 0 && target < Store.Value.StartPins.Count) ReorderPins(source, Store.Value.StartPins[target].Target, direction > 0);
    }
    private void ReorderPins(string source, string target, bool after) => Store.Update(p =>
    {
        var item = p.StartPins.FirstOrDefault(pin => pin.Target == source); var other = p.StartPins.FirstOrDefault(pin => pin.Target == target);
        if (item is null || other is null || item == other) return; p.StartPins.Remove(item); p.StartPins.Insert(p.StartPins.IndexOf(other) + (after ? 1 : 0), item);
    });
    private void TrackMenu(ContextMenu menu) { menu.Opened += (_, _) => menus++; menu.Closed += (_, _) => { menus = Math.Max(0, menus - 1); if (IsVisible) Reload(false); }; }
    private static void AddMenu(ContextMenu menu, string name, Action action) { var item = new MenuItem { Header = name }; item.Click += (_, _) => action(); menu.Items.Add(item); }
    private void ManualShortcut()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Adicionar executável ou atalho", Filter = "Aplicativos e atalhos|*.exe;*.lnk;*.appref-ms|Todos os arquivos|*.*" };
        modal = true;
        try { if (dialog.ShowDialog(this) == true) PinDock(new(System.IO.Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName, "Manual", null)); }
        finally { modal = false; }
    }
    public override void Position()
    {
        var rect = anchor();
        double available = Math.Max(160, (rect.Top - Display.Work.Top) / Display.Scale - 24);
        var layout = AppDrawerLayout.Fit(Display.Width, available);
        double width = layout.Width, height = layout.Height;
        resultsGrid.Columns = layout.Columns; resultsGrid.Rows = layout.Rows;
        resultsGrid.Height = layout.Rows * AppDrawerLayout.CellHeight;
        int size = layout.Capacity;
        if (perPage != size) { perPage = size; Dispatcher.BeginInvoke(new Action(() => { if (IsVisible) RenderRows(); })); }
        content.Width = Math.Max(1, width - 28); viewport.Width = content.Width; viewport.MaxHeight = Math.Max(60, height - 28);
        content.Measure(new Size(content.Width, double.PositiveInfinity));
        // Tab contents never determine the native window size or grid cell size.
        PlaceAbove(width, height, rect, IsVisible);
    }
}
