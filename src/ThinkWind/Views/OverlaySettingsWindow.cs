using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class OverlaySettingsWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override string? MaterialOverride => "Acrylic";
    protected override bool HideOnClose => false;
    private readonly SettingsStore settings;
    private readonly OverlayAppearancePanel appearance = new();
    private readonly StackPanel metricGroups = new();
    private bool loading;
    internal OverlaySettingsWindow(SettingsStore store) : base(store)
    {
        settings = store; Title = "Think Wind — Personalização do overlay"; ShowActivated = true;
        MonitoringDesign.Install(Resources, store.Value);
        var root = new DockPanel();
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = ActionButton("Fechar personalização do overlay", Close); close.Content = Glyph("\uE711", 13); close.SetResourceReference(StyleProperty, "Think.IconButton"); close.Width = close.Height = 30; DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        var title = new TextBlock { Text = "Overlay", FontSize = 21, FontWeight = FontWeights.SemiBold };
        title.SetResourceReference(TextBlock.ForegroundProperty, "Fg"); heading.Children.Add(title);
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var content = new StackPanel();
        appearance.IslandAppearance = store.Value;
        appearance.SetOptions(store.Value.OverlayAppearance);
        appearance.OptionsChanged += (_, _) => settings.Update(p => p.OverlayAppearance = appearance.GetOptions());
        var appearanceSection = new Expander { Header = "Aparência e prévia", Content = appearance, IsExpanded = false, Margin = new Thickness(0, 8, 0, 8) };
        appearanceSection.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        content.Children.Add(appearanceSection);
        var enabled = new OverlayToggleButton(store) { Margin = new Thickness(0, 8, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
        content.Children.Add(enabled);
        var automatic = new TextBlock { Text = "O overlay aparece somente quando um jogo reconhecido está em primeiro plano. O botão e o atalho habilitam/desabilitam essa exibição; não mostram o overlay no desktop ou em vídeos.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        automatic.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); content.Children.Add(automatic);
        var hotkey = new CheckBox { Content = "Ctrl + Shift + O liga/desliga o overlay", IsChecked = store.Value.OverlayHotkeyEnabled, Margin = new Thickness(0, 0, 0, 12) };
        hotkey.SetResourceReference(Control.ForegroundProperty, "Fg");
        hotkey.Checked += (_, _) => settings.Update(p => p.OverlayHotkeyEnabled = true);
        hotkey.Unchecked += (_, _) => settings.Update(p => p.OverlayHotkeyEnabled = false); content.Children.Add(hotkey);
        var positionTitle = new TextBlock { Text = "Posição na tela", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 10) };
        positionTitle.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); content.Children.Add(positionTitle);
        var choices = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var item in new[] { ("TL", "Superior esquerdo"), ("TR", "Superior direito"), ("BL", "Inferior esquerdo"), ("BR", "Inferior direito") })
        {
            var radio = new RadioButton { Content = item.Item2, GroupName = "Corner", IsChecked = store.Value.OverlayCorner == item.Item1, Margin = new Thickness(0, 0, 12, 8) };
            radio.SetResourceReference(Control.ForegroundProperty, "Fg");
            radio.Checked += (_, _) => settings.Update(p => p.OverlayCorner = item.Item1); choices.Children.Add(radio);
        }
        content.Children.Add(choices);
        var sizeTitle = new TextBlock { Text = $"Tamanho do overlay · {store.Value.OverlayScale:P0}", FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 8) };
        sizeTitle.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); content.Children.Add(sizeTitle);
        var size = new Slider { Minimum = 70, Maximum = 150, Value = store.Value.OverlayScale * 100,
            TickFrequency = 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 16),
            ToolTip = "Tamanho de 70% a 150%, aplicado imediatamente" };
        System.Windows.Automation.AutomationProperties.SetName(size, "Tamanho do overlay em porcentagem");
        size.ValueChanged += (_, _) =>
        {
            sizeTitle.Text = $"Tamanho do overlay · {size.Value:0}%";
            settings.Update(p => p.OverlayScale = size.Value / 100);
        };
        content.Children.Add(size);
        var presets = new WrapPanel();
        foreach (var preset in new[] { "SÓ FPS", "PADRÃO", "TUDO" })
        {
            var button = ActionButton(preset, () =>
            {
                settings.Update(p => p.OverlayVisibleMetrics = preset == "SÓ FPS" ? new() { "fps" } : preset == "PADRÃO" ? OverlayMetrics.Defaults().ToList() : OverlayMetrics.All.Select(m => m.Key).ToList());
                BuildMetrics();
            }); button.SetResourceReference(StyleProperty, "OutlineButton"); presets.Children.Add(button);
        }
        content.Children.Add(presets);
        var metricsSection = new Expander { Header = "Métricas exibidas", Content = metricGroups, IsExpanded = true, Margin = new Thickness(0, 10, 0, 0) };
        metricsSection.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        content.Children.Add(metricsSection); BuildMetrics();
        var notice = new TextBlock { Text = "Métricas indisponíveis aparecem como —. FPS/frametime usam eventos de apresentação DXGI/D3D9 (não equivalem a todos os quadros exibidos). Usam coleta local quando o launcher está como administrador ou o coletor avançado atualizado quando está com permissões normais. Hotspot, ventoinha e sensores extras dependem do hardware; PerfCap não está disponível nesta implementação. Sem injeção no jogo; tela cheia exclusiva pode não exibir este overlay. Ctrl+Shift+O pode estar indisponível se outro aplicativo já usar o atalho.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 12, 0, 0) };
        notice.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        var help = new Expander { Header = "Informações e compatibilidade", Content = notice, IsExpanded = false, Margin = new Thickness(0, 10, 0, 0) };
        help.SetResourceReference(Control.ForegroundProperty, "Think.Muted"); content.Children.Add(help);
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        root.Children.Add(scroll); Surface.Child = root;
        settings.Changed += RefreshTheme;
        Closed += (_, _) => settings.Changed -= RefreshTheme;
    }
    private void RefreshTheme(object? sender, EventArgs e) { MonitoringDesign.Install(Resources, settings.Value); appearance.IslandAppearance = settings.Value; appearance.RefreshPreview(); RefreshAppearance(); }
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
                var check = new CheckBox { Content = metric.Label, IsChecked = settings.Value.OverlayVisibleMetrics.Contains(metric.Key), Margin = new Thickness(4, 5, 12, 5), MinWidth = 125, FontSize = 11 };
                check.SetResourceReference(Control.ForegroundProperty, "Fg");
                void Change() { if (loading) return; settings.Update(p => { if (check.IsChecked == true) { if (!p.OverlayVisibleMetrics.Contains(metric.Key)) p.OverlayVisibleMetrics.Add(metric.Key); } else p.OverlayVisibleMetrics.Remove(metric.Key); }); }
                check.Checked += (_, _) => Change(); check.Unchecked += (_, _) => Change(); row.Children.Add(check);
            }
            metricGroups.Children.Add(groupSection);
        }
        loading = false;
    }
    public override void Position()
    {
        var display = OverlayChrome.GetDisplay(settings.Value.MonitorIndex, settings.Value.MonitorDeviceName);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        int gap = (int)Math.Round(16 * display.Scale);
        int width = (int)Math.Round(Math.Min(620, display.Width - 32) * display.Scale);
        int height = (int)Math.Round(Math.Min(640, display.Height - 48) * display.Scale);
        OverlayChrome.Move(handle, new OverlayChrome.PixelRect
        {
            Left = display.Work.Left + gap, Top = display.Work.Top + gap,
            Right = display.Work.Left + gap + width, Bottom = display.Work.Top + gap + height
        });
        RefreshAppearance();
    }
}
