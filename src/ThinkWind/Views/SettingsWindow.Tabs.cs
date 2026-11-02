using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Data;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private readonly StackPanel tabNavigation = new() { Margin = new Thickness(8) };
    private readonly ComboBox compactNavigation = new() { Margin = new Thickness(16, 0, 16, 8), DisplayMemberPath = "Name", Visibility = Visibility.Collapsed };
    private readonly TextBlock sectionTrail = new() { FontSize = 11, Margin = new Thickness(24, 4, 24, 0), TextWrapping = TextWrapping.Wrap };
    private bool changingNavigation;
    private Action? adaptSettingsNavigation;
    private static readonly ControlTemplate navigationTemplate = CreateNavigationTemplate();
    private static ControlTemplate CreateNavigationTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "NavigationSurface");
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty)); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
        presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left); border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Think.Hover"), "NavigationSurface")); template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusWithinProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"), "NavigationSurface"));
        focus.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(1), "NavigationSurface")); template.Triggers.Add(focus); return template;
    }
    private static readonly (string Name, string[] Pages)[] navigationGroups =
    {
        ("Visual", new[] { "Aparência", "Transparência", "Tipografia", "Animações" }),
        ("Seu desktop", new[] { "Barra de aplicativos", "Central de controle", "Ícones da central de controle", "Widgets", "Clima", "Teclado", "Editor de layout" }),
        ("Desempenho", new[] { "Monitoramento compacto", "Overlay", "Overlay e sensores", "Jogos", "Monitores" }),
        ("Aplicativo", new[] { "Sistema", "Atualizações", "Tutorial", "Sobre e segurança" })
    };
    private FrameworkElement CreateSettingsNavigation(FrameworkElement body)
    {
        var shell = new Grid(); shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); shell.RowDefinitions.Add(new RowDefinition());
        var navigationColumn = new ColumnDefinition { Width = new GridLength(174) }; shell.ColumnDefinitions.Add(navigationColumn); shell.ColumnDefinitions.Add(new ColumnDefinition());
        var scroll = new ScrollViewer { Content = tabNavigation, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        var sidebar = new Border { Child = scroll, CornerRadius = new CornerRadius(14), Margin = new Thickness(12, 0, 0, 8) };
        sidebar.SetResourceReference(Border.BackgroundProperty, "Think.Surface"); Grid.SetRow(sidebar, 1); shell.Children.Add(sidebar);
        Grid.SetColumnSpan(compactNavigation, 2); shell.Children.Add(compactNavigation);
        var right = new DockPanel(); sectionTrail.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); DockPanel.SetDock(sectionTrail, Dock.Top); right.Children.Add(sectionTrail); right.Children.Add(body);
        Grid.SetColumn(right, 1); Grid.SetRow(right, 1); shell.Children.Add(right);
        AutomationProperties.SetName(compactNavigation, "Categoria da personalização");
        compactNavigation.SelectionChanged += (_, _) => { if (!changingNavigation && compactNavigation.SelectedItem is Choice choice) SelectSettingsTab(choice.Id); };
        void Adapt()
        {
            bool compact = ActualWidth > 0 && ActualWidth / Math.Max(1, store.Value.FontScale) < 640;
            sidebar.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            compactNavigation.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            navigationColumn.Width = new GridLength(compact ? 0 : 174);
        }
        adaptSettingsNavigation = Adapt;
        SizeChanged += (_, _) => Adapt(); Loaded += (_, _) => Adapt(); return shell;
    }
    private readonly ScrollViewer settingsScroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        PanningMode = PanningMode.VerticalOnly
    };
    private readonly Dictionary<string, (Border Page, Button Tab)> settingsPages = new();
    private readonly Dictionary<string, double> tabOffsets = new();
    private string selectedSettingsTab = "Aparência";
    private void ClearSettingsTabs() { settingsPages.Clear(); tabNavigation.Children.Clear(); }
    private void RegisterSettingsTab(string name, Border page)
    {
        string label = name switch
        {
            "Barra de aplicativos" => "Aplicativos", "Central de controle" => "Central de controle", "Ícones da central de controle" => "Ícones",
            "Overlay e sensores" => "Sensores", "Editor de layout" => "Layout", "Sobre e segurança" => "Sobre", _ => name
        };
        var tab = Button(label, () => SelectSettingsTab(name));
        var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
        text.SetBinding(TextBlock.ForegroundProperty, new Binding("Foreground") { Source = tab });
        tab.Content = text; tab.HorizontalContentAlignment = HorizontalAlignment.Left;
        tab.Template = navigationTemplate;
        tab.Padding = new Thickness(10, 8, 10, 8); tab.Margin = new Thickness(0, 2, 0, 2);
        tab.FontSize = 12; tab.ToolTip = name;
        AutomationProperties.SetName(tab, "Configurações: " + name);
        if (name == "Aparência") TutorialGuide.Mark(tab, "appearance");
        page.Visibility = Visibility.Collapsed;
        settingsPages[name] = (page, tab);
    }
    private void FinishSettingsTabs()
    {
        var added = new HashSet<string>(); var choices = new List<Choice>();
        foreach (var group in navigationGroups)
        {
            if (!group.Pages.Any(settingsPages.ContainsKey)) continue;
            var heading = new TextBlock { Text = group.Name, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 12, 8, 6) };
            heading.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); tabNavigation.Children.Add(heading);
            foreach (string name in group.Pages)
                if (settingsPages.TryGetValue(name, out var page)) { tabNavigation.Children.Add(page.Tab); choices.Add(new(name, name)); added.Add(name); }
        }
        foreach (var entry in settingsPages.Where(entry => !added.Contains(entry.Key))) { tabNavigation.Children.Add(entry.Value.Tab); choices.Add(new(entry.Key, entry.Key)); }
        changingNavigation = true; compactNavigation.ItemsSource = choices; changingNavigation = false;
        if (!settingsPages.ContainsKey(selectedSettingsTab)) selectedSettingsTab = settingsPages.Keys.First();
        SelectSettingsTab(selectedSettingsTab);
    }
    private void SelectSettingsTab(string name)
    {
        if (!settingsPages.ContainsKey(name)) return;
        bool searching = searchResults.Visibility == Visibility.Visible;
        if (settingsQuery.Text.Length > 0) settingsQuery.Clear();
        searchResults.Visibility = Visibility.Collapsed;
        sectionTrail.Text = (navigationGroups.FirstOrDefault(group => group.Pages.Contains(name)).Name ?? "Personalização") + "  /  " + name;
        changingNavigation = true;
        compactNavigation.SelectedItem = (compactNavigation.ItemsSource as IEnumerable<Choice>)?.FirstOrDefault(choice => choice.Id == name);
        changingNavigation = false;
        if (name == "Aparência") TutorialGuide.Notify("appearance-open");
        bool changedTab = selectedSettingsTab != name;
        if (!searching && settingsPages.TryGetValue(selectedSettingsTab, out var old) && old.Page.Visibility == Visibility.Visible)
            tabOffsets[selectedSettingsTab] = settingsScroll.VerticalOffset;
        selectedSettingsTab = name;
        foreach (var entry in settingsPages)
        {
            bool selected = entry.Key == name;
            entry.Value.Page.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            entry.Value.Page.BeginAnimation(OpacityProperty, null);
            if (selected && changedTab && IsVisible && SystemParameters.ClientAreaAnimation && store.Value.AnimationMilliseconds > 0)
                entry.Value.Page.BeginAnimation(OpacityProperty, new DoubleAnimation(.9, 1, TimeSpan.FromMilliseconds(Math.Min(180, store.Value.AnimationMilliseconds)))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
            entry.Value.Tab.SetResourceReference(Control.BackgroundProperty, selected ? "Think.AccentSurface" : "Think.Control");
            entry.Value.Tab.SetResourceReference(Control.ForegroundProperty, selected ? "Think.AccentInk" : "Think.Text");
            entry.Value.Tab.FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
            AutomationProperties.SetHelpText(entry.Value.Tab, selected ? "Aba selecionada" : "Abrir esta aba");
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (selectedSettingsTab == name && IsVisible) settingsScroll.ScrollToVerticalOffset(tabOffsets.GetValueOrDefault(name));
        }));
    }
}
