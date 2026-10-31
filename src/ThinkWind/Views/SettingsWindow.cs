using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed partial class SettingsWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool HideOnClose => false;
    protected override string? MaterialOverride => "Acrylic";
    private sealed record Choice(string Id, string Name) { public override string ToString() => Name; }
    private readonly SettingsStore store;
    private readonly WeatherWidget weatherSettings;
    private FrameworkElement? weatherSectionTarget;
    private CheckBox? weatherVisibilityCheck;
    private readonly StackPanel content = new() { Margin = new Thickness(24, 8, 24, 24) };
    private readonly StackPanel quickRows = new(), moduleRows = new();
    private readonly TextBlock status = new() { Margin = new Thickness(24, 12, 24, 16), FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly Button editing = new() { Margin = new Thickness(0, 12, 0, 8) };
    private bool building;
    private string layoutSignature = "";
    public SettingsWindow(SettingsStore settings) : base(settings)
    {
        store = settings;
        weatherSettings = new WeatherWidget(store, true);
        Title = "Think Wind · Personalização";
        SizeToContent = SizeToContent.Manual;
        var display = OverlayChrome.GetDisplay(store.Value.MonitorIndex, store.Value.MonitorDeviceName);
        Width = Math.Min(440, display.Width - 24); Height = Math.Min(580, display.Height - 24);
        MinWidth = Math.Min(380, Width); MinHeight = Math.Min(360, Height);
        WindowStartupLocation = WindowStartupLocation.Manual;
        Topmost = true;
        SourceInitialized += (_, _) => Position();
        ShowActivated = true;
        Surface.Padding = new Thickness(0);
        Surface.BorderThickness = new Thickness(1);
        Surface.SetResourceReference(Border.BorderBrushProperty, "Think.Hover");
        SetResourceReference(ForegroundProperty, "Think.Text");
        var root = new DockPanel();
        DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var heading = new StackPanel { Margin = new Thickness(20, 16, 20, 10) };
        var titleBar = new DockPanel { Margin = new Thickness(0, 0, 0, 8), Background = Brushes.Transparent };
        var close = ActionButton("Fechar personalização", Close); close.Content = Glyph("\uE711", 13); close.Width = close.Height = 30; close.Padding = new Thickness(4);
        close.SetResourceReference(StyleProperty, "Think.IconButton"); DockPanel.SetDock(close, Dock.Right); titleBar.Children.Add(close);
        titleBar.Children.Add(new Image { Source = AppBrand.WindowIcon, Width = 28, Height = 28, HorizontalAlignment = HorizontalAlignment.Left });
        titleBar.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 1 && !close.IsMouseOver) DragMove(); };
        heading.Children.Add(titleBar);
        heading.Children.Add(new TextBlock { Text = "Personalizar", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 4) });
        heading.Children.Add(new TextBlock { Text = "Alterações salvas automaticamente.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Opacity = .8 });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var tabScroll = new ScrollViewer { Content = tabNavigation, MaxHeight = 124, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        tabScroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        DockPanel.SetDock(tabScroll, Dock.Top); root.Children.Add(tabScroll);
        var scroll = settingsScroll;
        scroll.Content = content;
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        root.Children.Add(new AdornerDecoratorHost(scroll));
        Surface.Child = root;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && store.IsEditing) { store.SetEditing(false); e.Handled = true; } };
        editing.Click += (_, _) => store.SetEditing(!store.IsEditing);
        store.Changed += StoreChanged; store.PersistenceChanged += PersistenceChanged;
        Closed += (_, _) => { weatherSettings.Dispose(); store.Changed -= StoreChanged; store.PersistenceChanged -= PersistenceChanged; store.Flush(); };
        Build();
    }
    internal void ShowWeatherSection()
    {
        SelectSettingsTab("Clima");
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (!IsVisible || weatherSectionTarget is null) return;
            weatherSectionTarget.BringIntoView(new Rect(0, 0, weatherSectionTarget.ActualWidth, Math.Min(80, weatherSectionTarget.ActualHeight)));
        }));
    }
    private void StoreChanged(object? sender, EventArgs e)
    {
        if (weatherVisibilityCheck is not null && weatherVisibilityCheck.IsChecked != store.Value.ShowWeatherInHeader)
        {
            bool previous = building; building = true;
            try { weatherVisibilityCheck.IsChecked = store.Value.ShowWeatherInHeader; }
            finally { building = previous; }
        }
        if (weatherVisibilityCheck is not null) weatherVisibilityCheck.IsEnabled = !store.Value.DisabledQuickControls.Contains("weather");
        weatherSettings.Configure();
        RefreshAppearance();
        editing.Content = store.IsEditing ? "Concluir edição visual" : "Editar layout na tela";
        if (!building && layoutSignature != LayoutSignature()) Dispatcher.BeginInvoke(new Action(RebuildLayoutRows));
        UpdateStatus();
    }
    public override void Position()
    {
        var screen = Display; int gap = (int)Math.Round(12 * screen.Scale);
        var handle = new WindowInteropHelper(this).Handle; if (handle == 0) return;
        OverlayChrome.Move(handle, new OverlayChrome.PixelRect
        {
            Left = screen.Work.Left + gap, Top = screen.Work.Top + gap,
            Right = screen.Work.Left + gap + (int)Math.Round(Width * screen.Scale),
            Bottom = screen.Work.Top + gap + (int)Math.Round(Height * screen.Scale)
        });
    }
    private string LayoutSignature() => string.Join('|', store.Value.QuickOrder) + "/" + string.Join('|', store.Value.HiddenQuickControls) + "/" + string.Join('|', store.Value.DisabledQuickControls) + "/" + string.Join('|', store.Value.ModuleOrder) + "/" + string.Join('|', store.Value.HiddenModules) + store.Value.ShowAudio + store.Value.ShowWeatherInHeader;
    private void PersistenceChanged(object? sender, EventArgs e) => UpdateStatus();
    private void UpdateStatus()
    {
        status.Text = store.SaveError ?? (store.HasPendingChanges ? "Salvando suas preferências…" : "Preferências salvas neste computador.");
        status.SetResourceReference(TextBlock.ForegroundProperty, store.SaveError is null ? "Think.Muted" : "Think.Accent");
    }
    private void Change(Action<Preferences> change) { if (!building) store.Update(change); }
    private void Build()
    {
        building = true; content.Children.Clear(); ClearSettingsTabs();
        var general = Section("Aparência", "Grafite, ciano e superfícies limpas — identidade Think.");
        Slider(general, "Tamanho das fontes", .8, 2.5, store.Value.FontScale, value => Change(p => p.FontScale = value), "P0", .05);
        Select(general, "Tema do launcher e Windows", store.Value.Theme, new[] { new Choice("Auto", "Automático · acompanhar Windows"), new Choice("Dark", "Escuro"), new Choice("Light", "Claro") }, async value => { if (!building) await ThemeService.SelectTheme(store, value); });
        Slider(general, "Opacidade dos blocos e controles", .1, .9, store.Value.InnerSurfaceOpacity, v => Change(p => p.InnerSurfaceOpacity = v), "P0", .05);
        var accent = new DockPanel { Margin = new Thickness(0, 10, 0, 14) };
        var color = new TextBox { Text = store.Value.AccentColor, Width = 100, Padding = new Thickness(10, 7, 10, 7), VerticalContentAlignment = VerticalAlignment.Center };
        color.SetResourceReference(Control.BackgroundProperty, "Think.Control"); color.SetResourceReference(Control.ForegroundProperty, "Think.Text");
        color.ToolTip = "Cor de destaque em hexadecimal, por exemplo #60CDFF";
        color.TextChanged += (_, _) =>
        {
            if (building) return;
            string text = color.Text.Trim();
            if (text.Length != 7 || !text.StartsWith('#') || !text.Skip(1).All(Uri.IsHexDigit)) return;
            Change(p => p.AccentColor = text.ToUpperInvariant());
        };
        DockPanel.SetDock(color, Dock.Right); accent.Children.Add(color);
        var swatches = new WrapPanel();
        foreach (var hex in new[] { "#60CDFF", "#89A5FF", "#B69CFF", "#5ED6B3", "#FFAE6D", "#FA819A" })
        {
            var swatch = new Button { Width = 30, Height = 30, Padding = new Thickness(0), Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)), ToolTip = hex };
            swatch.Click += (_, _) => color.Text = hex; swatches.Children.Add(swatch);
        }
        accent.Children.Add(swatches); general.Children.Add(new TextBlock { Text = "Cor de destaque" }); general.Children.Add(accent);
        Slider(general, "Duração das animações", 0, 600, store.Value.AnimationMilliseconds, v => Change(p => p.AnimationMilliseconds = v), "0 'ms'");

        var dock = Section("Barra inferior", "Ícones originais, sem fundos individuais. Ajustes independentes da ilha.");
        Select(dock, "Material e desfoque", store.Value.DockMaterial, Materials(), v => Change(p => p.DockMaterial = v));
        Slider(dock, "Intensidade da tonalidade / opacidade", .05, 1, store.Value.DockOpacity ?? store.Value.TintOpacity, v => Change(p => p.DockOpacity = v), "P0");
        Slider(dock, "Escala da barra", .75, 1.5, store.Value.DockScale, v => Change(p => p.DockScale = v), "P0");
        Slider(dock, "Tamanho dos ícones", 20, 48, store.Value.IconSize, v => Change(p => p.IconSize = v));
        Check(dock, "Ampliação dos ícones estilo Dock", store.Value.DockMagnification, v => Change(p => p.DockMagnification = v));
        Check(dock, "Miniaturas de janelas ao passar o mouse", store.Value.WindowPreviewsEnabled, v => Change(p => p.WindowPreviewsEnabled = v));
        Slider(dock, "Espera para abrir miniaturas", 150, 1500, store.Value.WindowPreviewDelayMilliseconds, v => Change(p => p.WindowPreviewDelayMilliseconds = v), "0 'ms'", 50);
        Slider(dock, "Intensidade da ampliação", .4, .6, store.Value.DockMagnificationAmount, v => Change(p => p.DockMagnificationAmount = v), "P0", .01);
        Slider(dock, "Velocidade da mola", .5, 2, store.Value.DockMagnificationSpeed, v => Change(p => p.DockMagnificationSpeed = v), "0.0 '×'", .1);
        Slider(dock, "Ícones vizinhos de cada lado", 0, 4, store.Value.DockMagnificationNeighbors, v => Change(p => p.DockMagnificationNeighbors = (int)Math.Round(v)), "0", 1);
        Slider(dock, "Espaço entre os ícones", 0, 24, store.Value.DockSpacing, v => Change(p => p.DockSpacing = v));
        Slider(dock, "Arredondamento", 0, 48, store.Value.DockRadius, v => Change(p => p.DockRadius = v));
        Slider(dock, "Distância da borda inferior", 4, 80, store.Value.DockBottomOffset, v => Change(p => p.DockBottomOffset = v));
        Slider(dock, "Posição horizontal", -4000, 4000, store.Value.DockHorizontalOffset, v => Change(p => p.DockHorizontalOffset = v));
        Check(dock, "Recolher automaticamente quando estiver ociosa", store.Value.AutoHide, v => Change(p => p.AutoHide = v));
        Check(dock, "Manter a barra visível quando nenhum app ocupa sua área", store.Value.DockVisibleOnDesktop, v => Change(p => p.DockVisibleOnDesktop = v));
        Slider(dock, "Espera para recolher a barra (segundos)", 0, 10, store.Value.DockCollapseSeconds, v => Change(p => p.DockCollapseSeconds = v), "0.0 's'", .1);
        Select(dock, "Pesquisa na barra", store.Value.DockSearchMode, new[] { new Choice("Bar", "Barra compacta"), new Choice("Icon", "Apenas lupa") }, v => Change(p => p.DockSearchMode = v));
        Note(dock, "Recolhida, a barra é uma cápsula central arredondada de 96 × 6 DIP, a 4 pixels da borda inferior. Os ajustes de tamanho e posição acima afetam o estado expandido.");

        var island = Section("Dynamic Island", "Integrada à borda superior, com cantos inferiores arredondados.");
        Select(island, "Material e desfoque", store.Value.IslandMaterial, Materials(), v => Change(p => p.IslandMaterial = v));
        Slider(island, "Intensidade da tonalidade / opacidade", .05, 1, store.Value.IslandOpacity ?? store.Value.TintOpacity, v => Change(p => p.IslandOpacity = v), "P0");
        Slider(island, "Escala dos acessos rápidos", .8, 1.4, store.Value.IslandScale, v => Change(p => p.IslandScale = v), "P0");
        Slider(island, "Largura expandida", 400, 900, store.Value.IslandExpandedWidth, v => Change(p => p.IslandExpandedWidth = v));
        Select(island, "Lado da expansão com apps maximizados ou em tela dividida", store.Value.IslandPanelSide, new[] { new Choice("Right", "Direita"), new Choice("Left", "Esquerda") }, v => Change(p => p.IslandPanelSide = v));
        Note(island, "No desktop e com janelas restauradas, a expansão fica centralizada. Com aplicativo maximizado ou encaixado pelo Windows Snap, abre no lado escolhido (direita por padrão).");
        Note(island, "Sobre aplicativos, o painel abre abaixo da barra de status. O recolhimento segue o tempo configurado, preservando controles em uso e a passagem entre os painéis.");
        Check(island, "Recolher a ilha automaticamente após sair com o mouse", store.Value.IslandAutoCollapse, v => Change(p => p.IslandAutoCollapse = v));
        Check(island, "Reduzir a ilha sobre apps maximizados ou em tela dividida", store.Value.IslandReduceOverMaximized, v => Change(p => p.IslandReduceOverMaximized = v));
        Check(island, "Reservar sempre a faixa superior para a ilha", store.Value.IslandReserveTop, v => Change(p => p.IslandReserveTop = v));
        Note(island, "A reserva usa a área útil nativa do Windows e pode reorganizar ícones do desktop ao ativar ou desativar. Permanece estável entre os modos da ilha.");
        var overlay = Section("Overlay e sensores", "Monitoramento opcional e sensores avançados. As permissões do coletor são independentes da interface.");
        Check(overlay, "Overlay de monitoramento do PC", store.Value.HardwareOverlayEnabled, v => Change(p => p.HardwareOverlayEnabled = v));
        overlay.Children.Add(Button("Personalizar overlay · métricas, posição e aparência", () => ((App)Application.Current).OpenOverlaySettings()));
        overlay.Children.Add(Button("Autorizar / atualizar coletor de sensores (UAC uma vez)", async () =>
        {
            store.Update(p => { p.HardwareSensorsSetupAttempted = true; p.HardwareSensorsAutoSetup = true; });
            await AdvancedSensorSetup.Configure();
        }));
        overlay.Children.Add(Button("Desativar início elevado dos sensores", async () =>
        {
            store.Update(p => p.HardwareSensorsSetupAttempted = true);
            if (await AdvancedSensorSetup.Configure(remove: true)) store.Update(p => p.HardwareSensorsAutoSetup = false);
        }));
        Note(overlay, "Overlay opcional: aparece somente em jogo reconhecido em primeiro plano, quando habilitado. Não aparece no desktop nem em vídeos. Temperaturas dependem de suporte e permissões; dados ausentes aparecem como —.");
        Check(island, "Barra superior acompanha a cor do aplicativo abaixo", store.Value.IslandMatchAppColor, v => Change(p => p.IslandMatchAppColor = v));
        Slider(island, "Espera para recolher (segundos)", 0, 10, store.Value.IslandCollapseSeconds, v => Change(p => p.IslandCollapseSeconds = v), "0.0 's'", .1);
        Slider(island, "Espaçamento", 0, 20, store.Value.IslandSpacing, v => Change(p => p.IslandSpacing = v));
        Slider(island, "Arredondamento inferior", 0, 48, store.Value.IslandRadius, v => Change(p => p.IslandRadius = v));
        Slider(island, "Posição horizontal", -4000, 4000, store.Value.IslandHorizontalOffset, v => Change(p => p.IslandHorizontalOffset = v));
        Note(island, "Acrylic usa o fundo real através da composição do Windows. Mica é um material opaco baseado no papel de parede. A intensidade acima controla a tonalidade; o raio do desfoque é definido pelo sistema.");
        var weatherSection = Section("Clima", "Após a primeira escolha no widget, altere a cidade somente aqui. A seleção é salva automaticamente.");
        weatherSectionTarget = weatherSection;
        weatherVisibilityCheck = Check(weatherSection, "Mostrar clima ao lado do relógio", store.Value.ShowWeatherInHeader, value => Change(p => p.ShowWeatherInHeader = value));
        if (weatherSettings.Parent is Panel oldWeather) oldWeather.Children.Remove(weatherSettings);
        weatherSection.Children.Add(weatherSettings);

        var layout = Section("Editor de layout", "Arraste pelos puxadores no desktop. As linhas ciano indicam onde o item será inserido.");
        // Detach reusable controls from their previous section when resetting.
        if (editing.Parent is Panel oldEditing) oldEditing.Children.Remove(editing);
        layout.Children.Add(editing);
        Note(layout, "Na barra, arraste os ícones. Na ilha, arraste os acessos rápidos ou o título de cada módulo. O puxador lateral ajusta a posição horizontal. Esc conclui a edição.");
        Note(layout, "Os três primeiros módulos visíveis ficam em destaque. Calendário e bandeja têm áreas detalhadas. A engrenagem do Windows fica no compacto; a personalização do Think Wind aparece apenas no painel completo.");
        var islandIcons = Section("Ícones da ilha", "Marque para mostrar na barra principal. Desmarque para mover aos acessos adicionais da área expandida. Use Ocultar nas duas áreas para desativar completamente.");
        Note(islandIcons, "No modo barra, relógio e clima ficam à esquerda, seguidos por desempenho e ferramentas. Os demais ícones aparecem à direita na ordem escolhida.");
        islandIcons.Children.Add(Button("Configurar ícone e cidade do clima", ShowWeatherSection));
        if (quickRows.Parent is Panel oldQuick) oldQuick.Children.Remove(quickRows);
        islandIcons.Children.Add(quickRows);
        layout.Children.Add(new TextBlock { Text = "MÓDULOS DA ILHA", FontSize = 11, Margin = new Thickness(0, 18, 0, 8) });
        if (moduleRows.Parent is Panel oldModules) oldModules.Children.Remove(moduleRows);
        layout.Children.Add(moduleRows);
        RebuildLayoutRows();
        var resets = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        resets.Children.Add(Button("Restaurar barra", () => { store.ResetDock(); Build(); }));
        resets.Children.Add(Button("Restaurar ilha", () => { store.ResetIsland(); Build(); }));
        resets.Children.Add(Button("Restaurar tudo", () => { store.ResetAll(); Build(); }));
        layout.Children.Add(resets);

        var system = Section("Sistema", "Os recursos nativos do Windows permanecem disponíveis.");
        system.Children.Add(Button("Autorizar / atualizar controle automático de janelas", async () =>
        {
            if (await WindowActionSetup.Configure()) store.Update(p => p.AutoAuthorizeWindowActions = true);
        }));
        system.Children.Add(Button("Revogar controle automático de janelas", async () =>
        {
            if (await WindowActionSetup.Configure(remove: true)) store.Update(p => p.AutoAuthorizeWindowActions = false);
        }));
        Note(system, "Autorize o auxiliar uma vez pelo UAC para controlar suas janelas administrativas sem confirmar a cada clique. A autorização pode ser solicitada novamente ao atualizar o auxiliar. A interface continua normal para arrastar arquivos. Revogar remove a tarefa e desativa novas solicitações automáticas.");
        var games = Section("Jogos", "Proteção de tela cheia, detecção de jogos e exceções.");
        Check(games, "Modo Jogo automático: ocultar launcher e suspender tarefas", store.Value.GameModeEnabled, value => Change(p => p.GameModeEnabled = value));
        Check(games, "Ocultar launcher em vídeos e apps em tela cheia", store.Value.HideOverFullscreen, v => Change(p => p.HideOverFullscreen = v));
        Check(system, "Abrir Iniciar com Ctrl+Alt+Espaço (desativado durante jogos)", store.Value.StartHotkeyEnabled, value => Change(p => p.StartHotkeyEnabled = value));
        Check(system, "Tecla Windows abre o Iniciar do Think Wind (desativado no Modo Jogo)", store.Value.WindowsKeyOpensLauncher, value => Change(p => p.WindowsKeyOpensLauncher = value));
        Note(games, "Detecção automática por tela cheia e pastas de instalação Steam, Epic e GOG, inclusive jogos em janela. A lista abaixo é apenas complementar. Aplicativos instalados nessas pastas também podem ser reconhecidos; use as exceções quando necessário.");
        Note(games, "Aplicativos comuns em janela não acionam o Modo Jogo apenas por receberem foco. Para jogos não reconhecidos, use a lista complementar. Think Monitor é tratado como ferramenta de desktop. No Modo Jogo, a interceptação da tecla Windows é desativada.");
        void ProcessList(string label, string value, Action<string> change)
        {
            games.Children.Add(new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 8, 4, 4) });
            var field = new TextBox { Text = value, Padding = new Thickness(8), Margin = new Thickness(4) };
            field.SetResourceReference(Control.BackgroundProperty, "Think.Control"); field.SetResourceReference(Control.ForegroundProperty, "Think.Text");
            field.TextChanged += (_, _) => { if (!building) change(field.Text); }; games.Children.Add(field);
        }
        ProcessList("Jogos adicionais (opcional): executáveis separados por ;", store.Value.GameModeInclude, value => Change(p => p.GameModeInclude = value));
        ProcessList("Exceções do Modo Jogo (separadas por ;)", store.Value.GameModeExclude, value => Change(p => p.GameModeExclude = value));
        Check(system, "Usar somente a barra do Think Wind (ocultar a barra do Windows)", store.Value.HideWindowsTaskbar, value => Change(p => p.HideWindowsTaskbar = value));
        Note(system, "Oculta a barra original em todos os monitores enquanto o Think Wind estiver aberto. Ao desativar ou sair, ela é restaurada. O Explorador e a tecla Windows continuam funcionando.");
        var screens = System.Windows.Forms.Screen.AllScreens;
        var monitors = Section("Monitores", "Escolha onde exibir a barra e a ilha, em uma ou várias telas.");
        int monitorIndex = Array.FindIndex(screens, s => s.DeviceName.Equals(store.Value.MonitorDeviceName, StringComparison.OrdinalIgnoreCase));
        if (monitorIndex < 0) monitorIndex = Math.Clamp(store.Value.MonitorIndex, 0, screens.Length - 1);
        Select(monitors, "Monitor da barra e da ilha", monitorIndex.ToString(), screens.Select((s, i) => new Choice(i.ToString(), $"{s.DeviceName} · {s.Bounds.Width} × {s.Bounds.Height}")).ToArray(), value => Change(p =>
        { p.MonitorIndex = int.Parse(value); p.MonitorDeviceName = screens[p.MonitorIndex].DeviceName; }));
        Check(monitors, "Ativar em múltiplos monitores", store.Value.MultiMonitorEnabled, value => Change(p => p.MultiMonitorEnabled = value));
        Check(monitors, "Usar todos os monitores conectados", store.Value.AllMonitors, value => Change(p => p.AllMonitors = value));
        Check(monitors, "Mostrar Dynamic Island nas telas secundárias", store.Value.SecondaryIslandsEnabled, value => Change(p => p.SecondaryIslandsEnabled = value));
        Note(monitors, "Por padrão, a ilha aparece somente na tela principal do Windows. A barra inferior continua nas telas selecionadas. Ative esta opção para mostrar também a ilha nas telas secundárias selecionadas.");
        foreach (var screen in screens)
        {
            string device = screen.DeviceName;
            Check(monitors, $"Tela selecionada: {device}", store.Value.ActiveMonitors.Contains(device), value => Change(p =>
            {
                if (value && !p.ActiveMonitors.Contains(device)) p.ActiveMonitors.Add(device);
                else if (!value) p.ActiveMonitors.Remove(device);
            }));
        }
        Note(monitors, "Para escolher telas individuais, ative múltiplos monitores e desmarque usar todos. Sem seleção, será usado o monitor principal configurado acima.");
        Check(system, "Mostrar uso de RAM", store.Value.ShowMemory, value => Change(p => p.ShowMemory = value));
        Check(system, "Mostrar bateria quando disponível", store.Value.ShowBattery, value => Change(p => p.ShowBattery = value));
        try
        {
            Check(system, "Iniciar junto com o Windows", SystemActions.StartupEnabled(), value =>
            {
                if (building) return;
                try
                {
                    SystemActions.SetStartup(value);
                    Change(p => { p.StartWithWindows = value; p.StartupDefaultsInitialized = true; });
                }
                catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException) { MessageBox.Show(e.Message, "Não foi possível alterar a inicialização"); Build(); }
            });
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException) { Note(system, "A opção de inicialização não está disponível para esta conta."); }
        Note(system, "A ocultação da barra original pode ser desativada nesta seção. O launcher não substitui os arquivos do Explorer.");
        var about = Section("Sobre e segurança", "Think Wind · " + AppBrand.Version);
        Note(about, AppBrand.Copyright);
        Note(about, "Baixe instaladores somente da página oficial ou das releases do projeto no GitHub. Compare o SHA-256 com o arquivo publicado na mesma release. O instalador Alfa ainda não possui assinatura digital; o hash confirma a correspondência do arquivo, não garante ausência de vulnerabilidades.");
        Note(about, "As preferências ficam neste usuário do Windows. Alterações de tema e ocultação automática afetam as preferências do sistema. Desinstalação de aplicativos usa os mecanismos registrados no Windows. Rede, áudio e Bluetooth dependem das permissões do sistema.");
        about.Children.Add(Button("Downloads oficiais", () => SystemActions.Run("https://github.com/valteromega2019-blip/think-wind/releases")));
        about.Children.Add(Button("Informações de segurança", () => SystemActions.Run("https://github.com/valteromega2019-blip/think-wind/blob/main/SECURITY.md")));
        building = false; FinishSettingsTabs(); StoreChanged(this, EventArgs.Empty);
    }
    private void RebuildLayoutRows()
    {
        layoutSignature = LayoutSignature();
        quickRows.Children.Clear(); moduleRows.Children.Clear();
        var weatherPrimary = Check(quickRows, "Clima na barra principal", store.Value.ShowWeatherInHeader && !store.Value.DisabledQuickControls.Contains("weather"), value => Change(p => p.ShowWeatherInHeader = value));
        weatherPrimary.IsEnabled = !store.Value.DisabledQuickControls.Contains("weather");
        AddDisabledIconOption(quickRows, "weather", "Clima");
        foreach (var id in store.Value.QuickOrder.Where(id => id != "settings").ToArray())
        {
            string label = LayoutCatalog.QuickControls.First(q => q.Id == id).Name;
            bool disabled = store.Value.DisabledQuickControls.Contains(id);
            LayoutRow(quickRows, "quick-settings", id, label, !store.Value.HiddenQuickControls.Contains(id) && !disabled, store.Value.QuickOrder,
                enabled => store.Update(p => { if (enabled) p.HiddenQuickControls.Remove(id); else p.HiddenQuickControls.Add(id); }),
                (from, to, after) => store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, after)));
            ((FrameworkElement)quickRows.Children[quickRows.Children.Count - 1]).IsEnabled = !disabled;
            AddDisabledIconOption(quickRows, id, label);
        }
        foreach (var id in store.Value.ModuleOrder.ToArray())
        {
            LayoutRow(moduleRows, "modules-settings", id, LayoutCatalog.ModuleName(id), store.ModuleVisible(id), store.Value.ModuleOrder,
                enabled => store.Update(p => { if (enabled) { p.HiddenModules.Remove(id); if (id == "audio") p.ShowAudio = true; } else p.HiddenModules.Add(id); }),
                (from, to, after) => store.Update(p => LayoutCatalog.Move(p.ModuleOrder, from, to, after)));
        }
    }
    private void LayoutRow(StackPanel parent, string scope, string id, string name, bool enabled, List<string> order, Action<bool> toggle, Action<string, string, bool> reorder)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent };
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (int direction in new[] { -1, 1 })
        {
            var move = Button(direction < 0 ? "↑" : "↓", () =>
            {
                int index = order.IndexOf(id), target = index + direction;
                if (target < 0 || target >= order.Count) return;
                reorder(id, order[target], direction > 0); RebuildLayoutRows();
            });
            move.Padding = new Thickness(9, 5, 9, 5); move.ToolTip = direction < 0 ? "Mover para cima" : "Mover para baixo";
            actions.Children.Add(move);
        }
        DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
        var grip = new TextBlock { Text = "⋮⋮", Width = 26, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.SizeAll };
        grip.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); row.Children.Add(grip);
        var visible = new CheckBox { Content = name, IsChecked = enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
        visible.Checked += (_, _) => { if (!building) toggle(true); };
        visible.Unchecked += (_, _) => { if (!building) toggle(false); };
        row.Children.Add(visible); parent.Children.Add(row);
        LayoutDrag.Attach(row, scope, id, () => true, (from, to, after) => { reorder(from, to, after); Dispatcher.BeginInvoke(new Action(RebuildLayoutRows)); }, true, grip);
    }
    private StackPanel Section(string title, string description)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        Note(panel, description);
        var card = new Border { Child = panel, CornerRadius = new CornerRadius(16), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 16) };
        card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
        card.BorderThickness = new Thickness(1); card.SetResourceReference(Border.BorderBrushProperty, "Think.GlassCard");
        content.Children.Add(card); RegisterSettingsTab(title, card); return panel;
    }
    private static void Note(Panel parent, string text)
    { var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 0, 0, 14) }; note.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); parent.Children.Add(note); }
    private static Choice[] Materials() => new[] { new Choice("Acrylic", "Acrylic · desfoque nativo"), new Choice("Mica", "Mica · material do Windows"), new Choice("Solid", "Sólido · sem desfoque") };
    private static void Slider(Panel parent, string label, double min, double max, double value, Action<double> changed, string format = "0 'px'", double? smallChange = null)
    {
        var heading = new DockPanel { Margin = new Thickness(0, 10, 0, 6) };
        var readout = new TextBlock { Text = value.ToString(format, System.Globalization.CultureInfo.CurrentCulture), HorizontalAlignment = HorizontalAlignment.Right };
        readout.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        DockPanel.SetDock(readout, Dock.Right); heading.Children.Add(readout); heading.Children.Add(new TextBlock { Text = label }); parent.Children.Add(heading);
        var slider = new System.Windows.Controls.Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), SmallChange = smallChange ?? (format == "P0" ? .01 : 1), LargeChange = (max - min) / 10, Margin = new Thickness(0, 4, 0, 8) };
        System.Windows.Automation.AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { readout.Text = slider.Value.ToString(format, System.Globalization.CultureInfo.CurrentCulture); changed(slider.Value); };
        parent.Children.Add(slider);
    }
    private static void Select(Panel parent, string label, string selected, Choice[] choices, Action<string> changed)
    {
        parent.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 6) });
        var combo = new ComboBox { ItemsSource = choices, SelectedItem = choices.FirstOrDefault(c => c.Id == selected), Margin = new Thickness(0, 0, 0, 10) };
        System.Windows.Automation.AutomationProperties.SetName(combo, label);
        combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is Choice choice) changed(choice.Id); }; parent.Children.Add(combo);
    }
    private static CheckBox Check(Panel parent, string label, bool value, Action<bool> changed)
    {
        var check = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap }, IsChecked = value, Margin = new Thickness(0, 9, 0, 9) };
        System.Windows.Automation.AutomationProperties.SetName(check, label);
        check.Checked += (_, _) => changed(true); check.Unchecked += (_, _) => changed(false); parent.Children.Add(check); return check;
    }
    private static Button Button(string label, Action action)
    { var button = new Button { Content = label }; button.Click += (_, _) => action(); return button; }
}
