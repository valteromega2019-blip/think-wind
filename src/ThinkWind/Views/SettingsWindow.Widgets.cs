using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed partial class SettingsWindow
{
    private readonly StackPanel widgetRows = new();
    private readonly WrapPanel widgetPreview = new();
    private readonly StackPanel favoriteRows = new();
    private readonly TextBox widgetNoteEditor = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
        Height = 100, MaxLength = 4000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8) };
    private string widgetEditorSignature = "";
    private bool widgetEditorQueued;
    internal void ShowWidgetsSection() => SelectSettingsTab("Widgets");
    private string WidgetEditorSignature() => JsonSerializer.Serialize(new { store.Value.ModuleOrder, store.Value.HiddenModules, store.Value.ShowAudio, store.Value.WidgetFavorites });
    private void BuildWidgetEditor()
    {
        var page = Section("Widgets", "Arraste pelos puxadores ou use as setas. Mostre, oculte e organize os cartões com atualização imediata na central de controle.");
        foreach (var element in new FrameworkElement[] { widgetPreview, widgetRows, widgetNoteEditor, favoriteRows })
            if (element.Parent is Panel old) old.Children.Remove(element);
        page.Children.Add(new TextBlock { Text = "Prévia da ordem", FontWeight = FontWeights.SemiBold });
        widgetPreview.Margin = new Thickness(0, 8, 0, 12); page.Children.Add(widgetPreview); page.Children.Add(widgetRows);
        page.Children.Add(Button("Restaurar ordem e visibilidade", () => store.Update(p => { p.ModuleOrder = LayoutCatalog.Modules.ToList(); p.HiddenModules.Clear(); p.ShowAudio = true; })));
        page.Children.Add(Button("Configurar cidade do clima", ShowWeatherSection));
        page.Children.Add(new TextBlock { Text = "Notas rápidas", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6) });
        widgetNoteEditor.SetResourceReference(Control.BackgroundProperty, "Think.Control"); widgetNoteEditor.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        widgetNoteEditor.Text = store.Value.WidgetNote; page.Children.Add(widgetNoteEditor);
        widgetNoteEditor.TextChanged -= WidgetNoteEdited; widgetNoteEditor.TextChanged += WidgetNoteEdited;
        page.Children.Add(new TextBlock { Text = "Atalhos favoritos · até 6", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 6) });
        page.Children.Add(favoriteRows);
        page.Children.Add(Button("Usar atalhos fixados na barra", () => store.Update(p => p.WidgetFavorites = p.WidgetFavorites.Concat(p.Shortcuts)
            .DistinctBy(item => item.Target, StringComparer.OrdinalIgnoreCase).Take(6).ToList())));
        var name = new TextBox { MaxLength = 60, Margin = new Thickness(0, 6, 0, 4), Padding = new Thickness(8), ToolTip = "Nome do atalho" };
        var target = new TextBox { MaxLength = 2048, Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(8), ToolTip = "Aplicativo, pasta ou endereço" };
        foreach (var field in new[] { name, target }) { field.SetResourceReference(Control.BackgroundProperty, "Think.Control"); field.SetResourceReference(Control.ForegroundProperty, "Think.Text"); }
        System.Windows.Automation.AutomationProperties.SetName(name, "Nome do atalho favorito");
        System.Windows.Automation.AutomationProperties.SetName(target, "Destino do atalho favorito");
        page.Children.Add(new TextBlock { Text = "Nome e destino do novo atalho", Margin = new Thickness(0, 8, 0, 0) }); page.Children.Add(name); page.Children.Add(target);
        page.Children.Add(Button("Escolher programa ou atalho…", () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Aplicativos e atalhos|*.exe;*.lnk;*.url|Todos os arquivos|*.*" };
            if (dialog.ShowDialog(this) == true) { target.Text = dialog.FileName; if (string.IsNullOrWhiteSpace(name.Text)) name.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName); }
        }));
        page.Children.Add(Button("Adicionar favorito", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || string.IsNullOrWhiteSpace(target.Text)) { status.Text = "Preencha o nome e o destino do atalho."; return; }
            if (store.Value.WidgetFavorites.Count >= 6) { status.Text = "Remova um favorito para adicionar outro. O limite é 6."; return; }
            store.Update(p => p.WidgetFavorites.Add(new(name.Text.Trim(), target.Text.Trim()))); name.Clear(); target.Clear();
        }));
        RebuildWidgetRows();
    }
    private void WidgetNoteEdited(object sender, TextChangedEventArgs e)
    { if (!building && widgetNoteEditor.Text != store.Value.WidgetNote) store.Update(p => p.WidgetNote = widgetNoteEditor.Text); }
    private void RefreshWidgetEditor()
    {
        if (!widgetNoteEditor.IsKeyboardFocusWithin && widgetNoteEditor.Text != store.Value.WidgetNote) widgetNoteEditor.Text = store.Value.WidgetNote;
        if (building || widgetEditorQueued || widgetEditorSignature == WidgetEditorSignature()) return;
        widgetEditorQueued = true;
        Dispatcher.BeginInvoke(new Action(() => { widgetEditorQueued = false; if (IsVisible) RebuildWidgetRows(); }));
    }
    private void RebuildWidgetRows()
    {
        widgetEditorSignature = WidgetEditorSignature(); widgetRows.Children.Clear(); widgetPreview.Children.Clear(); favoriteRows.Children.Clear();
        foreach (string id in store.Value.ModuleOrder)
        {
            string label = LayoutCatalog.ModuleName(id);
            bool visible = store.ModuleVisible(id);
            LayoutRow(widgetRows, "widgets-settings", id, label, visible, store.Value.ModuleOrder,
                enabled => store.Update(p => { if (enabled) { p.HiddenModules.Remove(id); if (id == "audio") p.ShowAudio = true; } else p.HiddenModules.Add(id); }),
                (from, to, after) => store.Update(p => LayoutCatalog.Move(p.ModuleOrder, from, to, after)));
            if (visible)
            {
                var tile = new Border { Child = new TextBlock { Text = label, FontSize = 11 }, CornerRadius = new CornerRadius(10), Padding = new Thickness(8), Margin = new Thickness(0, 0, 5, 5) };
                tile.SetResourceReference(Border.BackgroundProperty, "Think.Control"); widgetPreview.Children.Add(tile);
            }
        }
        foreach (var item in store.Value.WidgetFavorites)
        {
            var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
            var remove = Button("Remover", () => store.Update(p => p.WidgetFavorites.Remove(item))); DockPanel.SetDock(remove, Dock.Right); row.Children.Add(remove);
            foreach (int direction in new[] { 1, -1 })
            {
                var move = Button(direction < 0 ? "↑" : "↓", () => store.Update(p =>
                {
                    int index = p.WidgetFavorites.IndexOf(item), destination = index + direction;
                    if (destination < 0 || destination >= p.WidgetFavorites.Count) return;
                    p.WidgetFavorites.RemoveAt(index); p.WidgetFavorites.Insert(destination, item);
                }));
                DockPanel.SetDock(move, Dock.Right); row.Children.Add(move);
            }
            row.Children.Add(new TextBlock { Text = item.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = item.Target }); favoriteRows.Children.Add(row);
        }
    }
}
