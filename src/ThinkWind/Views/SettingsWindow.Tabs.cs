using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ThinkWind.Views;

internal sealed partial class SettingsWindow
{
    private readonly WrapPanel tabNavigation = new() { Margin = new Thickness(16, 0, 16, 8) };
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
            "Barra inferior" => "Barra", "Dynamic Island" => "Ilha", "Ícones da ilha" => "Ícones",
            "Overlay e sensores" => "Overlay", "Editor de layout" => "Layout", "Sobre e segurança" => "Sobre", _ => name
        };
        var tab = Button(label, () => SelectSettingsTab(name));
        tab.Padding = new Thickness(10, 6, 10, 6); tab.Margin = new Thickness(2);
        tab.FontSize = 11; tab.ToolTip = name;
        AutomationProperties.SetName(tab, "Configurações: " + name);
        page.Visibility = Visibility.Collapsed;
        settingsPages[name] = (page, tab);
    }
    private void FinishSettingsTabs()
    {
        string[] order = { "Aparência", "Barra inferior", "Dynamic Island", "Ícones da ilha", "Clima", "Overlay e sensores", "Editor de layout", "Jogos", "Monitores", "Sistema", "Sobre e segurança" };
        foreach (string name in order.Concat(settingsPages.Keys).Distinct())
            if (settingsPages.TryGetValue(name, out var page)) tabNavigation.Children.Add(page.Tab);
        if (!settingsPages.ContainsKey(selectedSettingsTab)) selectedSettingsTab = settingsPages.Keys.First();
        SelectSettingsTab(selectedSettingsTab);
    }
    private void SelectSettingsTab(string name)
    {
        if (!settingsPages.ContainsKey(name)) return;
        if (settingsPages.TryGetValue(selectedSettingsTab, out var old) && old.Page.Visibility == Visibility.Visible)
            tabOffsets[selectedSettingsTab] = settingsScroll.VerticalOffset;
        selectedSettingsTab = name;
        foreach (var entry in settingsPages)
        {
            bool selected = entry.Key == name;
            entry.Value.Page.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
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
