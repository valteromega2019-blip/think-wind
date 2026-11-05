using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Views;

internal sealed partial class LauncherPanelWindow
{
    private Border CreateSearchField()
    {
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        var searchIcon = Glyph("\uE721", 18);
        searchIcon.Margin = new Thickness(15, 0, 2, 0);
        searchIcon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        layout.Children.Add(searchIcon);
        var input = new Grid(); Grid.SetColumn(input, 1);
        var hint = new TextBlock
        {
            Text = "Pesquisar aplicativos e arquivos", Margin = new Thickness(12, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
            TextTrimming = TextTrimming.CharacterEllipsis, Opacity = .7
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        input.Children.Add(hint); input.Children.Add(query); layout.Children.Add(input);
        query.MinHeight = 44; query.VerticalContentAlignment = VerticalAlignment.Center;
        query.TextChanged += (_, _) => hint.Visibility = query.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var frame = new Border
        {
            Child = layout, CornerRadius = new CornerRadius(16),
            BorderThickness = new Thickness(1), Margin = new Thickness(4, 0, 4, 12)
        };
        frame.SetResourceReference(Border.BackgroundProperty, "Think.Control");
        frame.BorderBrush = Brushes.Transparent;
        query.IsKeyboardFocusWithinChanged += (_, _) =>
        {
            if (query.IsKeyboardFocusWithin) frame.SetResourceReference(Border.BorderBrushProperty, "Think.Accent");
            else frame.BorderBrush = Brushes.Transparent;
        };
        return frame;
    }

    private StackPanel DrawerTabContent(string name)
    {
        string glyph = name switch { "Fixados" => "\uE718", "Biblioteca" => "\uE8F1", "Recentes" => "\uE81C", _ => "\uE80A" };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Glyph(glyph, 14); icon.Margin = new Thickness(0, 0, 7, 0);
        row.Children.Add(icon);
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }
}
