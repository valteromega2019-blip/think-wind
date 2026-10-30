using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class SystemToolsWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override bool HideOnClose => false;
    protected override string? MaterialOverride => "Acrylic";
    private readonly Button anchor;

    internal SystemToolsWindow(SettingsStore store, Button source) : base(store)
    {
        anchor = source; Title = "Think Wind — Ferramentas do Windows"; ShowActivated = true;
        Surface.Padding = new Thickness(10);
        var root = new DockPanel();
        var heading = new DockPanel { Margin = new Thickness(6, 0, 0, 8) };
        var close = ActionButton("Fechar ferramentas", Close);
        close.Style = (Style)FindResource("Think.IconButton"); close.Content = Glyph("\uE711", 12);
        close.Width = close.Height = 28; close.Padding = new Thickness(4);
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = "Ferramentas do Windows", FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var commands = SystemToolsMenu.Create(source);
        var items = new StackPanel();
        foreach (var item in commands.Items) AddItem(items, item);
        var scroll = new ScrollViewer { Content = items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        root.Children.Add(scroll); Surface.Child = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Deactivated += (_, _) => { if (IsVisible) Close(); };
        store.Changed += ThemeChanged;
        Closed += (_, _) => store.Changed -= ThemeChanged;
    }
    private void AddItem(Panel target, object item)
    {
        if (item is Separator)
        {
            var line = new Border { Height = 1, Margin = new Thickness(8, 6, 8, 6) };
            line.SetResourceReference(Border.BackgroundProperty, "Think.Hover"); target.Children.Add(line); return;
        }
        if (item is not MenuItem command) return;
        if (command.Items.Count > 0)
        {
            var children = new StackPanel { Margin = new Thickness(8, 2, 0, 2) };
            foreach (var child in command.Items) AddItem(children, child);
            var group = new Expander { Header = command.Header, Content = children, Padding = new Thickness(6), FontSize = 12 };
            group.SetResourceReference(Control.ForegroundProperty, "Think.Text"); target.Children.Add(group); return;
        }
        var button = ActionButton(command.Header?.ToString() ?? "", () =>
        {
            Close();
            command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, command));
        });
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Padding = new Thickness(10, 8, 10, 8); button.Margin = new Thickness(0, 1, 0, 1);
        button.FontSize = 12; button.Background = Brushes.Transparent;
        // The shared launcher template centers content; stretch a label to keep
        // the command list left aligned without introducing another style.
        button.Content = new TextBlock { Text = command.Header?.ToString(), Width = 236, TextTrimming = TextTrimming.CharacterEllipsis };
        target.Children.Add(button);
    }
    private void ThemeChanged(object? sender, EventArgs e) => RefreshAppearance();
    public override void Position()
    {
        if (!anchor.IsLoaded || PresentationSource.FromVisual(anchor) is null) return;
        var first = anchor.PointToScreen(new Point());
        var last = anchor.PointToScreen(new Point(anchor.ActualWidth, anchor.ActualHeight));
        var bounds = new OverlayChrome.PixelRect { Left = (int)first.X, Top = (int)first.Y, Right = (int)last.X, Bottom = (int)last.Y };
        PlaceNear(Math.Min(300, Display.Width - 24), Math.Min(600, Display.Height - 48), bounds, false);
    }
}
