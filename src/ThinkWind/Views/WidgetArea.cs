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
    private readonly Grid notesWidget = new() { MinHeight = 180 };
    private readonly StackPanel writingHint = new() { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16) };
    private readonly Button exportNote = new() { Content = "Salvar TXT", Padding = new Thickness(10, 6, 10, 6), ToolTip = "Salvar uma cópia como arquivo .txt" };
    private readonly TextBlock noteStatus = new() { Text = "Salvamento automático", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 8, 0) };
    private bool exportingNote;
    private readonly TextBox notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000,
        MinHeight = 110, VerticalAlignment = VerticalAlignment.Stretch, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(12), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly Grid favorites = new() { MinHeight = 148 };
    private readonly ShellIcons icons = new();
    private string signature = "";
    internal FrameworkElement Notes => notesWidget;
    internal FrameworkElement Favorites => favorites;
    internal bool IsInteracting => exportingNote || notesWidget.IsKeyboardFocusWithin || favorites.IsKeyboardFocusWithin || notes.IsMouseCaptureWithin;
    internal WidgetArea(SettingsStore settings)
    {
        store = settings;
        notes.SetResourceReference(Control.ForegroundProperty, "Think.Text"); notes.Text = store.Value.WidgetNote;
        notes.SetResourceReference(TextBox.CaretBrushProperty, "Think.Accent");
        notes.ToolTip = "Notas rápidas · salvamento automático";
        System.Windows.Automation.AutomationProperties.SetName(notes, "Notas rápidas, salvas automaticamente");
        notesWidget.RowDefinitions.Add(new RowDefinition()); notesWidget.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var writingArea = new Grid(); writingArea.Children.Add(notes);
        var pencil = new TextBlock { Text = "\uE70F", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 8) };
        pencil.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); writingHint.Children.Add(pencil);
        var prompt = new TextBlock { Text = "Escreva uma nota…", FontSize = 14, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
        prompt.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); writingHint.Children.Add(prompt);
        writingArea.Children.Add(writingHint); notesWidget.Children.Add(writingArea);
        var footer = new DockPanel { Margin = new Thickness(12, 0, 12, 8), LastChildFill = true };
        exportNote.SetResourceReference(FrameworkElement.StyleProperty, "Think.IconButton");
        DockPanel.SetDock(exportNote, Dock.Right); footer.Children.Add(exportNote);
        noteStatus.ToolTip = "A nota é salva automaticamente no aplicativo. Salvar TXT exporta uma cópia para uma pasta escolhida.";
        noteStatus.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); footer.Children.Add(noteStatus);
        Grid.SetRow(footer, 1); notesWidget.Children.Add(footer);
        exportNote.Click += async (_, _) => await ExportNote();
        notes.TextChanged += (_, _) =>
        {
            UpdateNoteAppearance();
            if (notes.Text != store.Value.WidgetNote) store.Update(p => p.WidgetNote = notes.Text);
        };
        UpdateNoteAppearance();
        store.Changed += SettingsChanged; Refresh();
    }
    private void UpdateNoteAppearance()
    {
        writingHint.Visibility = notes.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        exportNote.IsEnabled = !exportingNote && !string.IsNullOrWhiteSpace(notes.Text);
        noteStatus.Text = "Salvamento automático";
    }
    private async System.Threading.Tasks.Task ExportNote()
    {
        if (exportingNote || string.IsNullOrWhiteSpace(notes.Text)) return;
        exportingNote = true; exportNote.IsEnabled = false;
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Salvar nota como TXT", Filter = "Arquivo de texto (*.txt)|*.txt", DefaultExt = ".txt",
                AddExtension = true, OverwritePrompt = true, FileName = "Nota-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt" };
            var owner = Window.GetWindow(notes);
            bool? accepted = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            if (accepted != true) return;
            await System.IO.File.WriteAllTextAsync(dialog.FileName, notes.Text, new System.Text.UTF8Encoding(false));
            noteStatus.Text = "TXT salvo";
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException or ArgumentException or NotSupportedException)
        { MessageBox.Show("Não foi possível salvar a nota: " + error.Message, "Salvar nota", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { exportingNote = false; exportNote.IsEnabled = !string.IsNullOrWhiteSpace(notes.Text); }
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
