using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class WidgetArea : DockPanel, IDisposable
{
    private readonly SettingsStore store;
    private readonly WeatherWidget weather;
    private readonly Grid tiles = new();
    private readonly TextBox notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000,
        Height = 136, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8), BorderThickness = new Thickness(0) };
    private readonly ShellIcons icons = new();
    private string signature = "";
    internal WidgetArea(SettingsStore settings, WeatherWidget weatherWidget)
    {
        store = settings; weather = weatherWidget;
        var edit = new Button { Content = "Editar widgets", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(8, 4, 8, 4) };
        edit.SetResourceReference(StyleProperty, "Think.IconButton");
        edit.Click += (_, _) => ((App)Application.Current).OpenWidgetSettings();
        DockPanel.SetDock(edit, Dock.Top); Children.Add(edit); Children.Add(tiles);
        notes.SetResourceReference(Control.BackgroundProperty, "Think.Control"); notes.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        notes.Text = store.Value.WidgetNote;
        System.Windows.Automation.AutomationProperties.SetName(notes, "Notas rápidas, salvas automaticamente");
        notes.TextChanged += (_, _) => { if (notes.Text != store.Value.WidgetNote) store.Update(p => p.WidgetNote = notes.Text); };
        store.Changed += SettingsChanged; Refresh();
    }
    private void SettingsChanged(object? sender, EventArgs e)
    {
        if (!notes.IsKeyboardFocusWithin && notes.Text != store.Value.WidgetNote) notes.Text = store.Value.WidgetNote;
        Refresh();
    }
    private void Refresh()
    {
        string next = string.Join('|', store.Value.WidgetOrder) + "/" + string.Join('|', store.Value.HiddenWidgets)
            + "/" + string.Join('|', store.Value.WidgetFavorites.Select(item => item.Name + ":" + item.Target));
        if (next == signature) return;
        signature = next;
        if (weather.Parent is Panel oldWeather) oldWeather.Children.Remove(weather);
        if (notes.Parent is Panel oldNotes) oldNotes.Children.Remove(notes);
        foreach (Border old in tiles.Children) old.Child = null;
        tiles.Children.Clear(); tiles.ColumnDefinitions.Clear();
        foreach (string id in store.Value.WidgetOrder.Where(id => !store.Value.HiddenWidgets.Contains(id)))
        {
            var body = new StackPanel();
            if (id == "weather") body.Children.Add(weather);
            else
            {
                var title = new TextBlock { Text = id == "notes" ? "Notas rápidas" : "Atalhos favoritos", FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) };
                title.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); body.Children.Add(title);
                if (id == "notes") body.Children.Add(notes);
                else
                {
                    var grid = new UniformGrid { Columns = 3 };
                    foreach (var item in store.Value.WidgetFavorites)
                    {
                        var content = new StackPanel();
                        content.Children.Add(new Image { Source = icons.Get(item.Target), Width = 26, Height = 26, Stretch = Stretch.Uniform });
                        var name = new TextBlock { Text = item.Name, FontSize = 10, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 3, 0, 0) };
                        name.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); content.Children.Add(name);
                        var button = new Button { Content = content, ToolTip = item.Name, Padding = new Thickness(4), Margin = new Thickness(1) };
                        button.SetResourceReference(StyleProperty, "Think.IconButton"); button.Click += (_, _) => SystemActions.Run(item.Target); grid.Children.Add(button);
                    }
                    if (grid.Children.Count == 0)
                    {
                        var empty = new TextBlock { Text = "Escolha até 6 atalhos em Editar widgets.", FontSize = 12, TextWrapping = TextWrapping.Wrap };
                        empty.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); body.Children.Add(empty);
                    }
                    else body.Children.Add(grid);
                }
            }
            tiles.ColumnDefinitions.Add(new ColumnDefinition());
            var card = new Border { Child = body, Padding = new Thickness(8), Margin = new Thickness(3), CornerRadius = new CornerRadius(14), MinHeight = 202 };
            card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard"); Grid.SetColumn(card, tiles.Children.Count); tiles.Children.Add(card);
        }
    }
    public void Dispose() => store.Changed -= SettingsChanged;
}
