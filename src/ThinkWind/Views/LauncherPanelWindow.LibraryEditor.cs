using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;
namespace OrbyLauncher.Views;
internal sealed partial class LauncherPanelWindow
{
    private bool editingLibrary;
    private readonly HashSet<string> pendingLibraryHides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Button editLibrary = new() { Content = "Editar", Margin = new Thickness(4), Padding = new Thickness(10, 5, 10, 5) };
    private readonly Button saveLibrary = new() { Margin = new Thickness(4), Padding = new Thickness(10, 5, 10, 5) };
    private readonly Button cancelLibrary = new() { Content = "Cancelar", Margin = new Thickness(4), Padding = new Thickness(10, 5, 10, 5) };
    private void AddLibraryEditorButtons(Panel parent)
    {
        editLibrary.ToolTip = "Selecione os aplicativos que deseja ocultar e clique em Salvar";
        cancelLibrary.ToolTip = "Descartar a seleção sem ocultar aplicativos";
        editLibrary.Click += (_, _) => { editingLibrary = true; pendingLibraryHides.Clear(); Reload(false); };
        cancelLibrary.Click += (_, _) => { CancelLibraryEditing(); Reload(false); };
        saveLibrary.Click += (_, _) =>
        {
            string[] selected = pendingLibraryHides.ToArray();
            CancelLibraryEditing();
            Store.Update(p => p.HiddenLibraryApps = p.HiddenLibraryApps.Concat(selected).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
            Reload(false);
        };
        saveLibrary.SetResourceReference(Control.BackgroundProperty, "Think.AccentSurface");
        saveLibrary.SetResourceReference(Control.ForegroundProperty, "Think.AccentInk");
        parent.Children.Add(editLibrary); parent.Children.Add(saveLibrary); parent.Children.Add(cancelLibrary);
    }
    private void CancelLibraryEditing() { editingLibrary = false; pendingLibraryHides.Clear(); }
    private void RefreshLibraryEditing()
    {
        bool available = section == "Biblioteca" && !HiddenLibraryView;
        if (!available) CancelLibraryEditing();
        editLibrary.Visibility = available && !editingLibrary ? Visibility.Visible : Visibility.Collapsed;
        saveLibrary.Visibility = cancelLibrary.Visibility = editingLibrary ? Visibility.Visible : Visibility.Collapsed;
        saveLibrary.Content = $"Salvar ({pendingLibraryHides.Count})";
        saveLibrary.IsEnabled = pendingLibraryHides.Count > 0;
        hiddenLibraryToggle.IsEnabled = !editingLibrary;
    }
    private void ToggleLibrarySelection(AppEntry app)
    {
        string key = AppVisibility.LibraryKey(app);
        if (!pendingLibraryHides.Remove(key))
        {
            if (Store.Value.HiddenLibraryApps.Count + pendingLibraryHides.Count >= 1000)
            {
                notice.Text = "Limite de aplicativos ocultos atingido. Restaure itens em Ocultos para continuar.";
                return;
            }
            pendingLibraryHides.Add(key);
        }
        RenderRows();
    }
}
