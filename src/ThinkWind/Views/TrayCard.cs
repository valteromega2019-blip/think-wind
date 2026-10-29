using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class TrayCard : StackPanel
{
    private readonly SettingsStore store;
    private readonly UniformGrid grid = new() { Columns = 6, Rows = 4, Height = 224 };
    private readonly TextBlock status = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(4) };
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Dictionary<string, (Image Icon, TextBlock Name)> visuals = new();
    private List<TrayEntry> entries = new();
    private string signature = "";
    private bool reading;
    private int page, menus;
    private DateTime nextRefresh;
    internal bool IsInteracting => menus > 0 || IsMouseCaptureWithin;
    internal event Action? LayoutChanged;
    internal TrayCard(SettingsStore settings)
    {
        store = settings; MinWidth = 288;
        Children.Add(grid); Children.Add(navigation); Children.Add(status);
        grid.Background = Brushes.Transparent;
        foreach (var area in new UIElement[] { grid, navigation })
            PageWheel.Attach(area, () => Math.Max(1, (entries.Count + 23) / 24), () => page, value => { page = value; Render(); }, () => IsInteracting || LayoutDrag.Active);
        var menu = new ContextMenu(); var refresh = new MenuItem { Header = "Atualizar ícones aqui" }; refresh.Click += (_, _) => { nextRefresh = DateTime.MinValue; Refresh(); }; menu.Items.Add(refresh); ContextMenu = menu;
        menu.Opened += (_, _) => menus++; menu.Closed += (_, _) => { menus = Math.Max(0, menus - 1); if (nextRefresh == DateTime.MinValue) Refresh(); };
        Loaded += (_, _) => { store.Changed += PreferencesChanged; Render(); };
        Unloaded += (_, _) => store.Changed -= PreferencesChanged;
    }
    private void PreferencesChanged(object? sender, EventArgs args) { if (IsVisible && !IsInteracting) Render(); }
    internal async void Refresh()
    {
        if (reading || IsInteracting || DateTime.UtcNow < nextRefresh) return;
        reading = true; nextRefresh = DateTime.UtcNow.AddSeconds(5);
        try
        {
            var pending = TrayService.ReadAsync();
            if (await Task.WhenAny(pending, Task.Delay(1800)) != pending) ShowError("Aguardando os ícones dos aplicativos…");
            entries = (await pending).DistinctBy(entry => entry.Key).ToList();
            var unseen = entries.Where(entry => !store.Value.TrayActivity.ContainsKey(entry.Key)).ToArray();
            if (unseen.Length > 0) store.Update(p => { foreach (var entry in unseen) p.TrayActivity.TryAdd(entry.Key, new(0, DateTime.MinValue, DateTime.UtcNow)); });
            status.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed; status.Text = "O Explorer ainda não disponibilizou ícones para esta área. A atualização é automática.";
            Render();
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        { ShowError("Não foi possível atualizar os ícones. Tentaremos novamente automaticamente."); }
        finally { reading = false; }
    }
    private void ShowError(string text) { status.Text = text; status.Visibility = Visibility.Visible; LayoutChanged?.Invoke(); }
    private double Score(TrayEntry entry)
    {
        if (!store.Value.TrayActivity.TryGetValue(entry.Key, out var activity)) return 0;
        var last = activity.LastUsedUtc > activity.FirstSeenUtc ? activity.LastUsedUtc : activity.FirstSeenUtc;
        double days = Math.Max(0, (DateTime.UtcNow - last).TotalDays);
        return Math.Log2(activity.Count + 1) * 3 + 10 / (1 + days);
    }
    private void Render()
    {
        var ordered = entries.OrderBy(entry => { int pin = store.Value.TrayFavorites.IndexOf(entry.Key); return pin < 0 ? int.MaxValue : pin; })
            .ThenByDescending(Score).ThenBy(entry => entry.DisplayName).ToArray();
        grid.Visibility = entries.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        int pages = Math.Max(1, (ordered.Length + 23) / 24); page = Math.Clamp(page, 0, pages - 1);
        var shown = ordered.Skip(page * 24).Take(24).ToArray();
        string next = page + ":" + ordered.Length + ":" + string.Join("|", shown.Select(entry => entry.Key));
        if (next == signature)
        {
            foreach (var entry in shown) if (visuals.TryGetValue(entry.Key, out var visual)) { visual.Icon.Source = entry.Icon; visual.Name.Text = entry.DisplayName; }
            return;
        }
        signature = next; visuals.Clear(); grid.Children.Clear(); navigation.Children.Clear();
        foreach (var entry in shown)
        {
            string key = entry.Key;
            var image = new Image { Source = entry.Icon, Width = 25, Height = 25, Stretch = Stretch.Uniform };
            var label = new TextBlock { Text = entry.DisplayName, FontSize = 9, MaxWidth = 58, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) };
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; content.Children.Add(image); content.Children.Add(label);
            var button = new Button { Content = content, Margin = new Thickness(2), Padding = new Thickness(2), ToolTip = entry.Name + "\nCtrl + botão direito: fixar ou desafixar" };
            button.SetResourceReference(StyleProperty, "Think.IconButton"); System.Windows.Automation.AutomationProperties.SetName(button, entry.DisplayName);
            button.Click += async (_, _) => await Invoke(key, false);
            button.PreviewMouseRightButtonUp += async (_, e) => { e.Handled = true; if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) FavoritesMenu(button, key); else await Invoke(key, true); };
            button.PreviewKeyDown += async (_, e) =>
            {
                if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ToggleFavorite(key); e.Handled = true; }
                else if (e.Key == Key.Apps || e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) FavoritesMenu(button, key); else await Invoke(key, true); }
            };
            visuals[key] = (image, label); grid.Children.Add(button);
        }
        if (pages > 1)
        {
            Button Arrow(string text, int direction)
            {
                var button = new Button { Content = text, Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(2), IsEnabled = direction < 0 ? page > 0 : page + 1 < pages };
                button.Click += (_, _) => { page += direction; Render(); }; return button;
            }
            navigation.Children.Add(Arrow("‹", -1)); navigation.Children.Add(new TextBlock { Text = $"{page + 1}/{pages}", FontSize = 10, Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center }); navigation.Children.Add(Arrow("›", 1));
        }
        LayoutChanged?.Invoke();
    }
    private async Task Invoke(string key, bool context)
    {
        var entry = entries.FirstOrDefault(candidate => candidate.Key == key); if (entry is null) return;
        if (!await TrayService.InvokeAsync(entry, context)) { ShowError(context ? "Este aplicativo não disponibiliza seu menu por acessibilidade." : "Este aplicativo não disponibiliza esta ação por acessibilidade."); return; }
        store.Update(p => { var activity = p.TrayActivity.GetValueOrDefault(key) ?? new(0, DateTime.MinValue, DateTime.UtcNow); p.TrayActivity[key] = activity with { Count = Math.Min(1_000_000, activity.Count + 1), LastUsedUtc = DateTime.UtcNow }; });
    }
    private void ToggleFavorite(string key) => store.Update(p => { if (!p.TrayFavorites.Remove(key)) p.TrayFavorites.Add(key); });
    private void FavoritesMenu(Button button, string key)
    {
        var menu = new ContextMenu { PlacementTarget = button };
        var pin = new MenuItem { Header = store.Value.TrayFavorites.Contains(key) ? "Desafixar do início" : "Fixar no início da grade" }; pin.Click += (_, _) => ToggleFavorite(key); menu.Items.Add(pin);
        var native = new MenuItem { Header = "Menu original do aplicativo" }; native.Click += async (_, _) => await Invoke(key, true); menu.Items.Add(native);
        menu.Opened += (_, _) => menus++; menu.Closed += (_, _) => { menus = Math.Max(0, menus - 1); signature = ""; Render(); }; menu.IsOpen = true;
    }
}
