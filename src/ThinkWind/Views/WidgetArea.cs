using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

// Reusable widget bodies; the main board owns their independent layout slots.
internal sealed class WidgetArea : IDisposable
{
    private readonly SettingsStore store;
    private readonly TextBox notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000,
        MinHeight = 148, VerticalAlignment = VerticalAlignment.Stretch, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(12), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly Grid favorites = new() { MinHeight = 148 };
    private readonly ShellIcons icons = new();
    private string signature = "";
    internal FrameworkElement Notes => notes;
    internal FrameworkElement Favorites => favorites;
    internal bool IsInteracting => notes.IsKeyboardFocusWithin || favorites.IsKeyboardFocusWithin || notes.IsMouseCaptureWithin;
    internal WidgetArea(SettingsStore settings)
    {
        store = settings;
        notes.SetResourceReference(Control.ForegroundProperty, "Think.Text"); notes.Text = store.Value.WidgetNote;
        notes.ToolTip = "Notas rápidas · salvamento automático";
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
        string next = System.Text.Json.JsonSerializer.Serialize(store.Value.WidgetFavorites);
        if (next == signature) return;
        signature = next; favorites.Children.Clear();
        var grid = new UniformGrid { Columns = 3, VerticalAlignment = VerticalAlignment.Center };
        foreach (var item in store.Value.WidgetFavorites)
        {
            var content = new StackPanel();
            content.Children.Add(new Image { Source = icons.Get(item.Target), Width = 30, Height = 30, Stretch = Stretch.Uniform });
            var name = new TextBlock { Text = item.Name, FontSize = 11, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) };
            name.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); content.Children.Add(name);
            var button = new Button { Content = content, ToolTip = item.Name, Padding = new Thickness(5), Margin = new Thickness(2) };
            button.SetResourceReference(FrameworkElement.StyleProperty, "Think.IconButton"); button.Click += (_, _) => SystemActions.Run(item.Target); grid.Children.Add(button);
        }
        if (grid.Children.Count > 0) favorites.Children.Add(grid);
        else
        {
            var edit = new Button { Content = "Adicionar atalhos favoritos", Padding = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            edit.SetResourceReference(FrameworkElement.StyleProperty, "Think.IconButton");
            edit.Click += (_, _) => ((App)Application.Current).OpenWidgetSettings(); favorites.Children.Add(edit);
        }
    }
    public void Dispose() => store.Changed -= SettingsChanged;
}