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
    private readonly StackPanel items = new() { Orientation = Orientation.Horizontal };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Action settings;
    private string signature = "";
    private bool hidden;
    private DateTime lastHover = DateTime.UtcNow;
    private readonly Dictionary<string, ImageSource?> icons = new(StringComparer.OrdinalIgnoreCase);
    public DockWindow(SettingsStore store, Action openSettings) : base(store)
    {
        settings = openSettings;
        Title = "Think Wind — Dock";
        ShowActivated = false;
        // Do not take focus before an application button has activated its target.
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(NoActivate);
        Surface.Child = new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Loaded += (_, _) => { RefreshSettings(); RefreshApps(); timer.Start(); };
        timer.Tick += (_, _) => { RefreshApps(); UpdateVisibility(); };
        MouseEnter += (_, _) => { lastHover = DateTime.UtcNow; SetHidden(false); };
        MouseLeave += (_, _) => lastHover = DateTime.UtcNow;
        Closed += (_, _) => timer.Stop();
    }
    private nint NoActivate(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x0021) { handled = true; return 3; } return 0; }
    public override void Position()
    {
        var area = WorkArea();
        Width = Math.Min(area.Width - 24, Math.Max(360, items.Children.Count * 64 * Store.Value.DockScale + 20));
        Height = hidden ? 8 : 78 * Store.Value.DockScale;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Bottom - Height - 10;
    }
    public override void RefreshSettings()
    {
        signature = "";
        if (!Store.Value.AutoHide) SetHidden(false);
        base.RefreshSettings(); RefreshApps();
    }
    private void UpdateVisibility()
    {
        if (Store.Value.AutoHide && !IsMouseOver && !IsKeyboardFocusWithin && DateTime.UtcNow - lastHover > TimeSpan.FromSeconds(3)) SetHidden(true);
    }
    private void SetHidden(bool value)
    {
        if (hidden == value) return;
        hidden = value;
        // Keep a visible edge available to restore the dock with the pointer.
        items.Visibility = value ? Visibility.Hidden : Visibility.Visible;
        Position();
    }
    private void RefreshApps()
    {
        var windows = Desktop.Windows();
        var active = Desktop.GetForegroundWindow();
        var next = string.Join("|", windows.Select(w => $"{w.Handle}:{w.Title}")) + active;
        if (next == signature) return;
        signature = next;
        items.Children.Clear();
        items.Children.Add(ActionButton("⊞", () => Desktop.Key(0x5B), "Abrir Iniciar do Windows"));
        foreach (var shortcut in Store.Value.Shortcuts)
        {
            var match = windows.FirstOrDefault(w => string.Equals(w.Executable, shortcut.Target, StringComparison.OrdinalIgnoreCase));
            var button = AppButton(shortcut.Name, shortcut.Target, match?.Handle == active, match is not null,
                () => { if (match is not null) Desktop.Activate(match.Handle); else SystemActions.Run(shortcut.Target); });
            var menu = new ContextMenu();
            Menu(menu, "Mover para a esquerda", () => MoveShortcut(shortcut, -1));
            Menu(menu, "Mover para a direita", () => MoveShortcut(shortcut, 1));
            Menu(menu, "Remover atalho", () => ChangeShortcuts(() => Store.Value.Shortcuts.Remove(shortcut)));
            button.ContextMenu = menu;
            items.Children.Add(button);
        }
        foreach (var window in windows.Where(w => !Store.Value.Shortcuts.Any(s => string.Equals(s.Target, w.Executable, StringComparison.OrdinalIgnoreCase))))
        {
            var button = AppButton(window.Title, window.Executable, window.Handle == active, true, () => Desktop.Activate(window.Handle));
            if (window.Executable is not null)
            {
                var menu = new ContextMenu();
                Menu(menu, "Fixar aplicativo", () => ChangeShortcuts(() => Store.Value.Shortcuts.Add(new Shortcut(System.IO.Path.GetFileNameWithoutExtension(window.Executable), window.Executable))));
                button.ContextMenu = menu;
            }
            items.Children.Add(button);
        }
        items.Children.Add(ActionButton("+", AddShortcut, "Adicionar aplicativo ou atalho"));
        items.Children.Add(ActionButton("⚙", settings, "Configurações do Think Wind"));
        Position();
    }
    private Button AppButton(string title, string? path, bool active, bool running, Action action)
    {
        var panel = new StackPanel();
        var icon = Icon(path);
        if (icon is not null) panel.Children.Add(new Image { Source = icon, Width = 26, Height = 26 });
        else panel.Children.Add(new TextBlock { Text = title.Length > 0 ? title[..1].ToUpperInvariant() : "□", FontSize = 20, TextAlignment = TextAlignment.Center });
        panel.Children.Add(new Border { Height = 3, Width = active ? 20 : 6, CornerRadius = new CornerRadius(2), Background = running ? new SolidColorBrush(active ? Color.FromRgb(164, 187, 255) : Color.FromRgb(115, 123, 150)) : Brushes.Transparent, Margin = new Thickness(0, 5, 0, 0) });
        var button = ActionButton(title, action, title);
        button.Content = panel;
        button.Width = 54 * Store.Value.DockScale;
        return button;
    }
    private ImageSource? Icon(string? path)
    {
        if (path is null || !System.IO.File.Exists(path)) return null;
        if (icons.TryGetValue(path, out var cached)) return cached;
        ImageSource? source = null;
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is not null) { source = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); source.Freeze(); }
        }
        catch (Exception e) when (e is ArgumentException or System.IO.IOException or System.ComponentModel.Win32Exception) { }
        if (icons.Count > 128) icons.Clear();
        icons[path] = source; return source;
    }
    private static void Menu(ContextMenu menu, string label, Action action)
    { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
    private void MoveShortcut(Shortcut shortcut, int direction)
    {
        var index = Store.Value.Shortcuts.IndexOf(shortcut);
        var target = index + direction;
        if (target < 0 || target >= Store.Value.Shortcuts.Count) return;
        ChangeShortcuts(() => { Store.Value.Shortcuts.RemoveAt(index); Store.Value.Shortcuts.Insert(target, shortcut); });
    }
    private void ChangeShortcuts(Action change)
    {
        change();
        try { Store.Save(); }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException) { MessageBox.Show(e.Message, "Não foi possível salvar os atalhos"); }
        signature = ""; RefreshApps();
    }
    private void AddShortcut()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Selecionar aplicativo ou atalho", Filter = "Aplicativos e atalhos|*.exe;*.lnk|Todos os arquivos|*.*" };
        if (dialog.ShowDialog() == true && !Store.Value.Shortcuts.Any(s => s.Target.Equals(dialog.FileName, StringComparison.OrdinalIgnoreCase)))
            ChangeShortcuts(() => Store.Value.Shortcuts.Add(new Shortcut(System.IO.Path.GetFileNameWithoutExtension(dialog.FileName), dialog.FileName)));
    }
}
