using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private readonly TextBox settingsQuery = new() { MaxLength = 100, MinHeight = 34, Padding = new Thickness(8), BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly StackPanel searchResults = new() { Visibility = Visibility.Collapsed };
    private Border CreateSettingsSearch()
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(settingsQuery);
        var placeholder = new TextBlock { Text = "Buscar uma opção…  Ctrl+F", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        placeholder.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); grid.Children.Add(placeholder);
        var clear = Button("×", () => { settingsQuery.Clear(); settingsQuery.Focus(); }); clear.ToolTip = "Limpar busca"; clear.Padding = new Thickness(10, 4, 10, 4); clear.Margin = new Thickness(0);
        AutomationProperties.SetName(clear, "Limpar busca de personalização");
        Grid.SetColumn(clear, 1); grid.Children.Add(clear);
        AutomationProperties.SetName(settingsQuery, "Buscar opções de personalização");
        settingsQuery.TextChanged += (_, _) => { placeholder.Visibility = settingsQuery.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; ShowSettingsSearch(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { settingsQuery.Focus(); settingsQuery.SelectAll(); e.Handled = true; }
            else if (!e.Handled && e.Key == Key.Escape && settingsQuery.Text.Length > 0) { settingsQuery.Clear(); e.Handled = true; }
        };
        var frame = new Border { Child = grid, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 12, 0, 0), BorderThickness = new Thickness(1) };
        frame.SetResourceReference(Border.BackgroundProperty, "Think.Control"); frame.SetResourceReference(Border.BorderBrushProperty, "Think.Hover"); return frame;
    }
    private static IEnumerable<(FrameworkElement Element, string Text)> SearchableSettings(DependencyObject node)
    {
        if (node is TextBlock text && !string.IsNullOrWhiteSpace(text.Text)) yield return (text, text.Text);
        if (node is Control control)
        {
            string label = AutomationProperties.GetName(control);
            if (label.Length > 0) yield return (control, label);
            if (control is ContentControl contentControl && contentControl.Content is string caption && caption.Length > 0) yield return (control, caption);
        }
        // Search labels, not editable values or thousands of installed-font items.
        if (node is TextBox or ComboBox) yield break;
        foreach (object child in LogicalTreeHelper.GetChildren(node))
            if (child is DependencyObject dependency)
                foreach (var result in SearchableSettings(dependency)) yield return result;
    }
    private void ShowSettingsSearch()
    {
        if (building || settingsPages.Count == 0) return;
        if (searchResults.Parent is null) content.Children.Insert(0, searchResults);
        string query = settingsQuery.Text.Trim(); searchResults.Children.Clear();
        if (query.Length == 0)
        {
            searchResults.Visibility = Visibility.Collapsed;
            foreach (var page in settingsPages) page.Value.Page.Visibility = page.Key == selectedSettingsTab ? Visibility.Visible : Visibility.Collapsed;
            string selected = selectedSettingsTab;
            sectionTrail.Text = (navigationGroups.FirstOrDefault(group => group.Pages.Contains(selected)).Name ?? "Personalização") + "  /  " + selected;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { if (selectedSettingsTab == selected && settingsQuery.Text.Length == 0) settingsScroll.ScrollToVerticalOffset(tabOffsets.GetValueOrDefault(selected)); }));
            return;
        }
        if (searchResults.Visibility != Visibility.Visible) tabOffsets[selectedSettingsTab] = settingsScroll.VerticalOffset;
        foreach (var page in settingsPages.Values) page.Page.Visibility = Visibility.Collapsed;
        searchResults.Visibility = Visibility.Visible; sectionTrail.Text = "Resultados da busca";
        var compare = CultureInfo.GetCultureInfo("pt-BR").CompareInfo;
        string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int count = 0;
        foreach (var entry in settingsPages)
        {
            var matches = SearchableSettings(entry.Value.Page).Where(item => words.All(word => compare.IndexOf(item.Text + " " + entry.Key, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).DistinctBy(item => item.Text).Take(3).ToArray();
            foreach (var match in matches)
            {
                string section = entry.Key; FrameworkElement target = match.Element;
                var result = Button(section + "\n" + match.Text, () =>
                {
                    SelectSettingsTab(section);
                    for (DependencyObject? parent = target; parent is not null; parent = LogicalTreeHelper.GetParent(parent))
                        if (parent is Expander expander) expander.IsExpanded = true;
                    Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
                    {
                        if (IsVisible && selectedSettingsTab == section)
                            target.BringIntoView(new Rect(0, 0, Math.Max(1, target.ActualWidth), Math.Max(36, target.ActualHeight)));
                    }));
                });
                result.Content = new TextBlock { Text = section + "\n" + match.Text, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
                result.Margin = new Thickness(0, 0, 0, 8); result.ToolTip = "Abrir " + section;
                searchResults.Children.Add(result); count++;
            }
        }
        searchResults.Children.Insert(0, new TextBlock { Text = count == 0 ? "Nenhuma opção encontrada. Tente “fonte”, “opacidade” ou “teclado”." : $"{count} atalhos encontrados", TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) });
        settingsScroll.ScrollToTop();
    }
}
