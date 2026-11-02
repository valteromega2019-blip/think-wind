using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

// One shared editor for the settings page and the standalone overlay window.
internal sealed class OverlaySettingsContent : UserControl, IDisposable
{
    private readonly SettingsStore settings;
    private readonly OverlayAppearancePanel appearance = new();
    private readonly StackPanel metricGroups = new();
    private bool loading;
    private bool syncingPosition;
    private readonly Slider positionX = new(), positionY = new();
    private readonly TextBlock positionXLabel = new(), positionYLabel = new();
    private readonly System.Collections.Generic.Dictionary<string, Button> cornerButtons = new();
    internal OverlaySettingsContent(SettingsStore store)
    {
        settings = store;
        appearance.HideInlinePreview();
        Loaded += (_, _) => UpdateScreenPreview();
        IsVisibleChanged += (_, _) => UpdateScreenPreview();
        Unloaded += (_, _) => ((App)Application.Current).SetOverlayEditorPreview(this, false);
        MonitoringDesign.Install(Resources, store.Value);
        Resources[typeof(CheckBox)] = SelectionCircleStyle.Create();
        var content = new StackPanel();
        var navigation = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        var generalPage = new StackPanel();
        var positionPage = new StackPanel { Visibility = Visibility.Collapsed };
        var metricsPage = new StackPanel { Visibility = Visibility.Collapsed };
        var pages = new[] { generalPage, positionPage, metricsPage };
        var tabs = new System.Collections.Generic.List<Button>();
        foreach (var item in new[] { ("Geral e aparência", generalPage), ("Posição e tamanho", positionPage), ("Métricas", metricsPage) })
        {
            var tab = ActionButton(item.Item1, () =>
            {
                foreach (var page in pages) page.Visibility = ReferenceEquals(page, item.Item2) ? Visibility.Visible : Visibility.Collapsed;
                foreach (var button in tabs)
                {
                    button.SetResourceReference(Control.BackgroundProperty, ReferenceEquals(button.Tag, item.Item2) ? "Think.AccentSurface" : "Think.Control");
                    button.SetResourceReference(Control.ForegroundProperty, ReferenceEquals(button.Tag, item.Item2) ? "Think.Accent" : "Think.Text");
                }
                ClockDesign.Reveal(item.Item2);
            });
            tab.Tag = item.Item2;
            tab.Content = ClockDesign.Label(ReferenceEquals(item.Item2, generalPage) ? "\uE790" : ReferenceEquals(item.Item2, positionPage) ? "\uE9E9" : "\uE9D9", item.Item1);
            tab.SetResourceReference(Control.ForegroundProperty, ReferenceEquals(item.Item2, generalPage) ? "Think.Accent" : "Think.Text");
            tab.SetResourceReference(Control.BackgroundProperty, ReferenceEquals(item.Item2, generalPage) ? "Think.AccentSurface" : "Think.Control");
            tabs.Add(tab); navigation.Children.Add(tab);
        }
        content.Children.Add(navigation);
        foreach (var page in pages) content.Children.Add(page);
        appearance.IslandAppearance = store.Value;
        appearance.SetOptions(store.Value.OverlayAppearance);
        appearance.OptionsChanged += (_, _) => settings.Update(p => p.OverlayAppearance = appearance.GetOptions());
        var appearanceSection = new Expander { Header = "Aparência do overlay na tela", Content = appearance, IsExpanded = true, Margin = new Thickness(0, 8, 0, 8) };
        appearanceSection.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        var enabled = new OverlayToggleButton(store) { Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
        var enabledRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(enabled, Dock.Left); enabledRow.Children.Add(enabled);
        var enabledLabel = new TextBlock { Text = "Exibir overlay nos jogos", FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        enabledLabel.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); enabledRow.Children.Add(enabledLabel); generalPage.Children.Add(enabledRow);
        var automatic = new TextBlock { Text = "Durante a edição, o overlay aparece diretamente na tela e acompanha suas alterações. Ao fechar, volta ao funcionamento nos jogos. Se as leituras estiverem desligadas, ative-as no monitor de desempenho para visualizar. FPS exige um jogo reconhecido em execução.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), FontSize = 12 };
        automatic.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); generalPage.Children.Add(automatic);
        var hotkey = new CheckBox { Content = "Ctrl + Shift + O liga/desliga o overlay", IsChecked = store.Value.OverlayHotkeyEnabled, Margin = new Thickness(0, 0, 0, 12) };
        hotkey.SetResourceReference(Control.ForegroundProperty, "Fg");
        hotkey.Checked += (_, _) => settings.Update(p => p.OverlayHotkeyEnabled = true);
        hotkey.Unchecked += (_, _) => settings.Update(p => p.OverlayHotkeyEnabled = false); generalPage.Children.Add(hotkey);
        var appearanceCard = ClockDesign.Card(appearanceSection, 14); appearanceCard.Margin = new Thickness(0, 4, 0, 8);
        generalPage.Children.Add(appearanceCard);
        var positionTitle = new TextBlock { Text = "Posição na tela", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 10) };
        positionTitle.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); positionPage.Children.Add(positionTitle);
        var choices = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var item in new[] { ("TL", "Superior esquerdo"), ("TR", "Superior direito"), ("BL", "Inferior esquerdo"), ("BR", "Inferior direito") })
        {
            var radio = ActionButton(item.Item2, () => { if (!syncingPosition) settings.Update(p => { p.OverlayCorner = item.Item1; p.OverlayCustomPosition = false; }); });
            radio.Content = ClockDesign.Label(item.Item1 switch { "TL" => "\uE74A", "TR" => "\uE742", "BL" => "\uE74B", _ => "\uE741" }, item.Item2);
            cornerButtons.Add(item.Item1, radio); choices.Children.Add(radio);
        }
        positionPage.Children.Add(choices);
        AddPositionSlider(positionPage, positionX, positionXLabel, true);
        AddPositionSlider(positionPage, positionY, positionYLabel, false);
        SyncPositionControls();
        var sizeTitle = new TextBlock { Text = $"Tamanho do overlay · {store.Value.OverlayScale:P0}", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) };
        sizeTitle.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); positionPage.Children.Add(sizeTitle);
        var size = new Slider { Minimum = 70, Maximum = 150, Value = store.Value.OverlayScale * 100,
            TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 16),
            ToolTip = "Tamanho de 70% a 150%, aplicado imediatamente" };
        System.Windows.Automation.AutomationProperties.SetName(size, "Tamanho do overlay em porcentagem");
        size.ValueChanged += (_, _) =>
        {
            sizeTitle.Text = $"Tamanho do overlay · {size.Value:0}%";
            settings.Update(p => p.OverlayScale = size.Value / 100);
        };
        size.SetResourceReference(StyleProperty, typeof(Slider)); positionPage.Children.Add(size);
        var presets = new WrapPanel();
        foreach (var preset in new[] { "SÓ FPS", "PADRÃO", "TUDO" })
        {
            var button = ActionButton(preset, () =>
            {
                settings.Update(p => p.OverlayVisibleMetrics = preset == "SÓ FPS" ? new() { "fps" } : preset == "PADRÃO" ? OverlayMetrics.Defaults().ToList() : OverlayMetrics.All.Select(m => m.Key).ToList());
                BuildMetrics();
            }); button.Content = ClockDesign.Label(preset == "SÓ FPS" ? "\uE9D9" : preset == "PADRÃO" ? "\uE73E" : "\uE8B3", preset); presets.Children.Add(button);
        }
        metricsPage.Children.Add(presets);
        var metricsSection = new Expander { Header = "Métricas exibidas", Content = metricGroups, IsExpanded = true, Margin = new Thickness(0, 10, 0, 0) };
        metricsSection.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        metricsPage.Children.Add(metricsSection); BuildMetrics();
        var notice = new TextBlock { Text = "Métricas indisponíveis aparecem como —. FPS/frametime usam eventos de apresentação DXGI/D3D9 (não equivalem a todos os quadros exibidos). Usam coleta local quando o launcher está como administrador ou o coletor avançado atualizado quando está com permissões normais. Hotspot, ventoinha e sensores extras dependem do hardware; PerfCap não está disponível nesta implementação. Sem injeção no jogo; tela cheia exclusiva pode não exibir este overlay. Ctrl+Shift+O pode estar indisponível se outro aplicativo já usar o atalho.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) };
        notice.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var help = new Expander { Header = "Informações e compatibilidade", Content = notice, IsExpanded = false, Margin = new Thickness(0, 10, 0, 0) };
        help.SetResourceReference(Control.ForegroundProperty, "Think.Muted"); metricsPage.Children.Add(help);
        Content = content;
        settings.Changed += RefreshTheme;
    }
    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(3, 3, 3, 6), MinHeight = 36, Padding = new Thickness(12, 8, 12, 8), FontSize = 12 };
        button.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        button.SetResourceReference(Control.BackgroundProperty, "Think.Control");
        System.Windows.Automation.AutomationProperties.SetName(button, text);
        button.Click += (_, _) => action(); return button;
    }
    private void UpdateScreenPreview() => ((App)Application.Current).SetOverlayEditorPreview(this, IsLoaded && IsVisible);
    public void Dispose()
    {
        settings.Changed -= RefreshTheme;
        ((App)Application.Current).SetOverlayEditorPreview(this, false);
    }
    private void RefreshTheme(object? sender, EventArgs e) { MonitoringDesign.Install(Resources, settings.Value); appearance.IslandAppearance = settings.Value; appearance.RefreshPreview(); SyncPositionControls(); }
    private void AddPositionSlider(Panel parent, Slider slider, TextBlock label, bool horizontal)
    {
        label.FontSize = 12; label.Margin = new Thickness(0, 2, 0, 6);
        label.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); parent.Children.Add(label);
        slider.Minimum = 0; slider.Maximum = 100; slider.SmallChange = .1; slider.LargeChange = 5;
        slider.SetResourceReference(StyleProperty, typeof(Slider));
        slider.TickFrequency = 1; slider.IsMoveToPointEnabled = true;
        slider.Margin = new Thickness(0, 0, 0, 14);
        slider.ToolTip = horizontal ? "0%: esquerda · 50%: centro · 100%: direita" : "0%: topo · 50%: centro · 100%: base";
        System.Windows.Automation.AutomationProperties.SetName(slider, horizontal ? "Posição horizontal do overlay" : "Posição vertical do overlay");
        slider.ValueChanged += (_, _) =>
        {
            if (syncingPosition) return;
            // Read both sliders before changing mode so the other axis keeps
            // the current preset's coordinate on the first adjustment.
            double x = positionX.Value, y = positionY.Value;
            settings.Update(p => { p.OverlayCustomPosition = true; p.OverlayPositionX = x; p.OverlayPositionY = y; });
        };
        parent.Children.Add(slider);
    }
    private void SyncPositionControls()
    {
        syncingPosition = true;
        try
        {
            var p = settings.Value;
            positionX.Value = p.OverlayCustomPosition ? p.OverlayPositionX : p.OverlayCorner.EndsWith("R") ? 100 : 0;
            positionY.Value = p.OverlayCustomPosition ? p.OverlayPositionY : p.OverlayCorner.StartsWith("B") ? 100 : 0;
            positionXLabel.Text = $"Posição horizontal · {positionX.Value:0.0}%";
            positionYLabel.Text = $"Posição vertical · {positionY.Value:0.0}%";
            foreach (var pair in cornerButtons)
            {
                bool selected = !p.OverlayCustomPosition && pair.Key == p.OverlayCorner;
                pair.Value.SetResourceReference(Control.BackgroundProperty, selected ? "Think.AccentSurface" : "Think.Control");
                pair.Value.SetResourceReference(Control.ForegroundProperty, selected ? "Think.Accent" : "Think.Text");
                System.Windows.Automation.AutomationProperties.SetHelpText(pair.Value, selected ? "Posição selecionada" : "Usar esta posição");
            }
        }
        finally { syncingPosition = false; }
    }
    private void BuildMetrics()
    {
        loading = true;
        metricGroups.Children.Clear();
        foreach (var group in OverlayMetrics.All.GroupBy(m => m.Group))
        {
            var row = new WrapPanel();
            var groupSection = new Expander { Header = group.Key, Content = row, IsExpanded = group.Key is "FPS" or "CPU" or "GPU", Margin = new Thickness(0, 6, 0, 0) };
            groupSection.SetResourceReference(Control.ForegroundProperty, "Think.Text");
            foreach (var metric in group)
            {
                var check = new CheckBox { Content = metric.Label, IsChecked = settings.Value.OverlayVisibleMetrics.Contains(metric.Key), Margin = new Thickness(4, 7, 12, 7), MinWidth = 210, FontSize = 12 };
                check.SetResourceReference(Control.ForegroundProperty, "Fg");
                void Change() { if (loading) return; settings.Update(p => { if (check.IsChecked == true) { if (!p.OverlayVisibleMetrics.Contains(metric.Key)) p.OverlayVisibleMetrics.Add(metric.Key); } else p.OverlayVisibleMetrics.Remove(metric.Key); }); }
                check.Checked += (_, _) => Change(); check.Unchecked += (_, _) => Change(); row.Children.Add(check);
            }
            var card = new Border { Child = groupSection, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6, 12, 10), Margin = new Thickness(0, 4, 0, 4) };
            card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
            metricGroups.Children.Add(card);
        }
        loading = false;
    }
}
