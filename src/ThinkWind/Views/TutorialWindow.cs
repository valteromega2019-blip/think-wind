using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class TutorialWindow : Window
{
    private sealed record Step(string Module, string Title, string Text, string Target, string Action);
    private static readonly Step[] MainSteps =
    {
        new("1 · Barra de aplicativos", "Encontre sua barra", "Leve o mouse até a barra inferior. Ela aparece ao aproximar o cursor da borda. Os ícones ampliam se o efeito Dock estiver ativado.", "dock", "dock-hover"),
        new("1 · Barra de aplicativos", "Abra seu menu", "Clique no botão Iniciar indicado. Aqui ficam seus aplicativos fixados, a biblioteca e os recentes. Você também pode usar a tecla Windows.", "start", "start-open"),
        new("1 · Barra de aplicativos", "Pesquise um aplicativo", "Digite o nome de um aplicativo na pesquisa do menu ou da barra. O primeiro resultado fica selecionado: Enter abre o aplicativo. Ao sair da pesquisa, o texto é limpo.", "search", "search-typed"),
        new("2 · Central de controle", "Descubra a expansão", "Leve o mouse até a central no topo da tela. Na ilha, a primeira expansão mostra mídia e áudio. No modo barra, a seta abre os controles abaixo. Os ícones dependem da sua personalização.", "island", "island-hover"),
        new("2 · Central de controle", "Veja o painel completo", "Clique na seta de expansão. Aqui ficam os controles e widgets, como calendário, clima, notas rápidas e favoritos. Rede e áudio têm painéis próprios.", "expand", "island-full"),
        new("2 · Central de controle", "Organize seus widgets", "No painel completo, clique no lápis de edição. Os controles internos ficam bloqueados e o fundo recebe desfoque para destacar o editor.", "widget-editor", "widget-edit-open"),
        new("2 · Central de controle", "Ajuste pelas bordas", "Arraste uma borda lateral de um widget. A largura encaixa em três, dois ou um por linha; os outros blocos são redistribuídos. A borda inferior ajusta a altura sem cortar o conteúdo. Para manter o layout atual, use Pular etapa.", "widget-resize", "widget-resized"),
        new("2 · Central de controle", "Conclua a edição", "Clique em Concluir edição. Seu layout é salvo automaticamente, o desfoque desaparece e os controles voltam a funcionar.", "widget-edit-finish", "widget-edit-finished"),
        new("3 · Monitoramento e personalização", "Conheça o desempenho", "Clique no ícone de monitoramento. Ele mostra as leituras disponíveis do computador. O overlay é opcional e aparece em jogos; sensores avançados podem exigir autorização.", "monitor", "monitor-open"),
        new("3 · Monitoramento e personalização", "Prepare seus jogos", "Clique na aba Otimizador. Escolha Economia, Equilibrado ou Desempenho para preparar o perfil de energia. O plano anterior retorna ao sair do jogo.", "optimizer", "optimizer-open"),
        new("3 · Monitoramento e personalização", "Revise os aplicativos", "Clique em Analisar. A primeira aba mostra aplicativos elegíveis para fechamento. Marque somente o que estiver sem uso e confirme a ação. Processos protegidos ficam fora da seleção; nada é fechado durante esta etapa do tutorial.", "optimizer-analyze", "optimizer-analyzed"),
        new("3 · Monitoramento e personalização", "Deixe com a sua cara", "Clique no pincel de personalização. As abas organizam cores, transparência, ícones, widgets e overlay. As alterações são salvas automaticamente.", "customize", "settings-open"),
        new("3 · Monitoramento e personalização", "Explore a aparência", "Selecione Aparência para tema, cores e cantos. No grupo Visual, Aplicativos e Central de controle organizam cada barra; Transparência cuida dos materiais e Tipografia das fontes. As mudanças aparecem na hora.", "appearance", "appearance-open"),
        new("3 · Monitoramento e personalização", "Dock flutuante ou barra fixa", "Em Aparência, passe o mouse sobre Barra inferior fixa de ponta a ponta. É opcional: mantém os controles centralizados e reserva espaço para os aplicativos. Desativada, volta ao Dock flutuante. Não é necessário mudar a opção para avançar. Para rever o guia, use Personalizar → Tutorial.", "fixed-bar", "fixed-bar-discovered")
    };
    private static readonly Step[] UpdateSteps =
    {
        new("Novidades · Agenda experimental", "Abra o calendário", "A agenda é experimental. Clique no calendário para conhecer eventos, notas coloridas e lembretes. Você também pode usar o calendário do widget expandido.", "calendar", "agenda-calendar-open"),
        new("Novidades · Agenda experimental", "Escolha um dia", "Um clique no dia consulta seus itens; dois cliques abrem a criação. A janela aparece ao lado do calendário, pode ser movida e minimizada na barra. Os lembretes funcionam enquanto o Orby estiver aberto.", "agenda-calendar", "agenda-open"),
        new("Novidades · Agenda experimental", "Eventos e notas no mesmo lugar", "O seletor com ícones alterna Evento e Nota. Escolha uma cor para marcar o dia. O backup da agenda e das configurações fica em Personalizar → Backup, em arquivos separados. Explore e use Pular etapa para continuar sem criar um item.", "agenda-form", "agenda-saved"),
        new("Novidades · Widgets e barras", "Abra a central", "Leve o mouse ao topo para acessar os controles da central.", "island", "island-hover"),
        new("Novidades · Widgets e barras", "Veja os widgets", "Abra o painel completo pela seta. O editor aparece somente nesta área expandida.", "expand", "island-full"),
        new("Novidades · Widgets e barras", "Novo editor pelas bordas", "Clique no lápis. O fundo desfocado indica o modo de edição; os controles dos widgets ficam bloqueados enquanto você organiza o layout.", "widget-editor", "widget-edit-open"),
        new("Novidades · Widgets e barras", "Encaixe automático", "Arraste uma borda lateral: três, dois ou um widget por linha. Os vizinhos mudam de posição automaticamente. A borda inferior controla a altura com limites para proteger o conteúdo. Pule esta etapa se não quiser alterar seu layout.", "widget-resize", "widget-resized"),
        new("Novidades · Widgets e barras", "Salve e volte a usar", "Clique em Concluir edição. O layout já fica salvo e os controles são liberados novamente.", "widget-edit-finish", "widget-edit-finished"),
        new("Novidades · Widgets e barras", "Personalização organizada", "Abra Personalizar para conhecer a nova organização das opções visuais.", "customize", "settings-open"),
        new("Novidades · Widgets e barras", "Aparência em um só lugar", "Selecione Aparência. Cores e arredondamento ficam aqui; as abas Aplicativos, Central, Transparência e Tipografia ficam juntas no grupo Visual.", "appearance", "appearance-open"),
        new("Novidades · Widgets e barras", "Nova barra inferior fixa", "Passe o mouse sobre a opção Barra inferior fixa de ponta a ponta. Ela é opcional, centraliza os controles e reserva espaço para os aplicativos. Não é preciso ativar para concluir.", "fixed-bar", "fixed-bar-discovered"),
        new("Novidades · Diagnóstico e suporte", "Relate um problema", "No grupo Aplicativo de Personalizar, clique em Diagnóstico e suporte. Ali você pode consultar erros locais, descrever um problema e revisar um relatório. Abrir relato no GitHub prepara um rascunho no navegador; só você confirma a publicação. Nada é enviado durante o tutorial.", "diagnostics", "diagnostics-open")
    };
    private bool updateTour;
    private Step[] Steps => updateTour ? UpdateSteps : MainSteps;
    private string ProgressPath => updateTour
        ? Path.Combine(Path.GetDirectoryName(store.TutorialStatePath)!, "update-tour-0.31.0-agenda-v1.json") : store.TutorialStatePath;
    private string? CurrentTarget => progress.Step >= 0 && progress.Step < Steps.Length ? Steps[progress.Step].Target : null;
    public sealed class Progress
    {
        public int Schema { get; set; }
        public int Step { get; set; } = -1;
        public bool Finished { get; set; }
        public bool Dismissed { get; set; }
    }
    private readonly SettingsStore store;
    private readonly Func<bool> blocked;
    private readonly DispatcherTimer follow = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly DispatcherTimer advance = new() { Interval = TimeSpan.FromMilliseconds(1100) };
    private readonly TextBlock module = new() { FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
    private readonly TextBlock title = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock description = new() { FontSize = 14, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 8) };
    private readonly TextBlock hint = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12) };
    private readonly StackPanel actions = new() { Orientation = Orientation.Horizontal };
    private readonly Border card;
    private BackdropHost? backdrop;
    private Progress progress = new();
    private TargetAdorner? highlight;
    private AdornerLayer? highlightLayer;
    private FrameworkElement? highlighted;
    private bool success, finishing;
    private string? saveError;
    private Rect? lastGuideBounds;
    internal TutorialWindow(SettingsStore settings, Func<bool> isBlocked)
    {
        store = settings; blocked = isBlocked;
        Width = 350; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        Title = "Orby Launcher · Tutorial";
        var content = new StackPanel { Margin = new Thickness(20) };
        content.Children.Add(module); content.Children.Add(title); content.Children.Add(description);
        content.Children.Add(hint); content.Children.Add(actions);
        card = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Child = content };
        card.SetResourceReference(Border.BackgroundProperty, "Think.Popup");
        card.SetResourceReference(Border.BorderBrushProperty, "Think.Accent");
        // The standard popup surface keeps guidance readable over any wallpaper.
        SetResourceReference(ForegroundProperty, "Think.Text");
        module.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        Content = card;
        SourceInitialized += (_, _) =>
        {
            backdrop = new BackdropHost(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook((nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
            { if (message == 0x0047) SyncBackdrop(); return 0; });
            SyncBackdrop();
        };
        IsVisibleChanged += (_, _) => SyncBackdrop();
        SizeChanged += (_, _) => SyncBackdrop();
        Closed += (_, _) => backdrop?.Dispose();
        follow.Tick += (_, _) => FollowTarget();
        advance.Tick += (_, _) => AdvanceStep();
        TutorialGuide.Performed += Performed;
        Closed += (_, _) => { follow.Stop(); advance.Stop(); ClearHighlight(); TutorialGuide.Active = false; TutorialGuide.KeepDockOpen = TutorialGuide.KeepIslandOpen = TutorialGuide.CollapseDockOnDesktop = false; TutorialGuide.Performed -= Performed; };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Pause(); e.Handled = true; } };
    }
    private void SyncBackdrop()
    {
        if (backdrop is null) return;
        backdrop.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
        if (OverlayChrome.GetWindowRect(new WindowInteropHelper(this).Handle, out var rectangle))
            backdrop.Synchronize(rectangle, 20 * (PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1), false, IsVisible);
        if (!backdrop.MaterialAvailable) card.SetResourceReference(Border.BackgroundProperty, "Think.Background");
    }
    internal void OfferFirstRun()
    {
        // Never reopen onboarding merely because an existing installation updated.
        if ((!store.IsNewProfile && !store.Value.TutorialPending) || File.Exists(store.TutorialStatePath) || store.LoadWarning is not null)
        {
            if (!store.IsNewProfile && !store.Value.TutorialPending && store.LoadWarning is null) OfferUpdateTour();
            return;
        }
        progress = new Progress(); SaveProgress();
        if (store.Value.TutorialPending) store.Update(p => p.TutorialPending = false);
        StartVisible();
    }
    internal void Repeat()
    {
        updateTour = false;
        advance.Stop(); progress = new Progress();
        progress.Dismissed = false;
        SaveProgress(); StartVisible();
    }
    internal void Continue()
    {
        updateTour = false;
        advance.Stop(); progress = ReadProgress();
        if (progress.Finished) progress = new Progress();
        progress.Dismissed = false; SaveProgress(); StartVisible();
    }
    private void OfferUpdateTour()
    {
        updateTour = true; progress = ReadProgress();
        if (progress.Finished || progress.Dismissed) { updateTour = false; return; }
        SaveProgress(); StartVisible();
    }
    internal void RepeatUpdateTour()
    {
        advance.Stop(); updateTour = true; progress = new Progress();
        SaveProgress(); StartVisible();
    }
    internal void Suspend(bool active)
    {
        if (!TutorialGuide.Active) return;
        if (active) { follow.Stop(); advance.Stop(); success = false; ClearHighlight(); Hide(); }
        else { Render(); follow.Start(); }
    }
    private Progress ReadProgress()
    {
        try
        {
            var value = File.Exists(ProgressPath) ? JsonSerializer.Deserialize<Progress>(File.ReadAllText(ProgressPath)) : null;
            if (value is not null && value.Step >= -1 && value.Step <= Steps.Length)
            {
                if (!updateTour && value.Schema < 1 && value.Step >= 6) value.Step += 2;
                if (!updateTour && value.Schema < 2 && value.Step >= 5) value.Step += 3;
                value.Schema = 2;
                return value;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return new Progress();
    }
    private void SaveProgress()
    {
        progress.Schema = 2;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProgressPath)!);
            File.WriteAllText(ProgressPath + ".tmp", JsonSerializer.Serialize(progress));
            File.Move(ProgressPath + ".tmp", ProgressPath, true); saveError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { saveError = "Não foi possível salvar o progresso do tutorial."; }
    }
    private void StartVisible()
    {
        if (!blocked() && !updateTour) SystemToolsMenu.ShowDesktop();
        finishing = false; TutorialGuide.Active = true; Render();
        if (!blocked()) Show();
        follow.Start(); FollowTarget();
    }
    private void Pause()
    {
        if (CurrentTarget is "widget-editor" or "widget-resize" or "widget-edit-finish" && store.IsEditing) store.SetEditing(false);
        follow.Stop(); advance.Stop(); ClearHighlight(); TutorialGuide.Active = false;
        TutorialGuide.KeepDockOpen = TutorialGuide.KeepIslandOpen = TutorialGuide.CollapseDockOnDesktop = false;
        progress.Dismissed = true; SaveProgress(); Hide();
    }
    private void AddAction(string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 6, 8, 6), FontSize = 12 };
        button.Click += (_, _) => action(); actions.Children.Add(button);
    }
    private void Render()
    {
        FontSizing.Apply(this, store.Value.FontScale);
        success = false; finishing = false; lastGuideBounds = null; ClearHighlight(); actions.Children.Clear();
        TutorialGuide.KeepDockOpen = CurrentTarget is "dock" or "start" or "search";
        TutorialGuide.KeepIslandOpen = CurrentTarget is "expand" or "widget-editor" or "widget-resize" or "widget-edit-finish" or "monitor" or "optimizer" or "optimizer-analyze" or "customize" or "calendar" or "agenda-calendar" or "agenda-form";
        TutorialGuide.CollapseDockOnDesktop = CurrentTarget is not null && !TutorialGuide.KeepDockOpen;
        if (progress.Step < 0)
        {
            module.Text = "Bem-vindo ao Orby Launcher"; title.Text = "Vamos conhecer seu desktop?";
            description.Text = "Três módulos rápidos. Siga os destaques e use os controles de verdade: o guia reconhece suas ações e avança automaticamente.";
            if (updateTour)
            {
                module.Text = "Novidades desta atualização"; title.Text = "Conheça o novo editor";
                description.Text = "Um guia curto para edição pelas bordas, encaixe dos widgets, personalização organizada, barra inferior fixa e diagnóstico de problemas. Comece quando quiser.";
            }
            hint.Text = "Você pode pular etapas ou sair a qualquer momento. Ajustes feitos por você durante o guia são salvos normalmente.";
            AddAction("Começar", () => { if (!blocked()) SystemToolsMenu.ShowDesktop(); progress.Step = 0; SaveProgress(); Render(); });
            AddAction("Agora não", Pause);
        }
        else if (progress.Step >= Steps.Length)
        {
            finishing = true; progress.Finished = true; SaveProgress();
            module.Text = "Tutorial concluído"; title.Text = "Tudo pronto!";
            description.Text = "Você conheceu os principais recursos. Explore no seu ritmo e ajuste o launcher do seu jeito.";
            hint.Text = "Para rever: Personalizar → Tutorial → Repetir tutorial.";
            if (updateTour) hint.Text = "Para rever: Personalizar → Tutorial → Ver novidades desta atualização.";
            AddAction("Concluir", Pause);
        }
        else
        {
            var step = Steps[progress.Step]; module.Text = step.Module + $" · {progress.Step + 1}/{Steps.Length}";
            title.Text = step.Title; description.Text = step.Text;
            hint.Text = "Siga o destaque animado. A próxima etapa começa após sua ação.";
            AddAction("Voltar", () => { advance.Stop(); progress.Step--; SaveProgress(); Render(); });
            AddAction("Pular etapa", AdvanceStep);
            AddAction("Sair", Pause);
        }
        if (saveError is not null) hint.Text = saveError;
        if (IsVisible && SystemParameters.ClientAreaAnimation && store.Value.AnimationMilliseconds > 0)
            card.BeginAnimation(OpacityProperty, new DoubleAnimation(.75, 1, TimeSpan.FromMilliseconds(Math.Min(180, store.Value.AnimationMilliseconds))));
        FollowTarget();
    }
    private void Performed(string action)
    {
        if (!TutorialGuide.Active || success || blocked() || progress.Step < 0 || progress.Step >= Steps.Length || Steps[progress.Step].Action != action) return;
        success = true; hint.Text = "Parabéns! Vamos para a próxima etapa…";
        ClearHighlight(); advance.Start();
    }
    private void AdvanceStep()
    {
        advance.Stop();
        if (CurrentTarget == "widget-edit-finish" && store.IsEditing) store.SetEditing(false);
        bool leavingSearch = CurrentTarget == "search";
        progress.Step++; SaveProgress(); Render();
        if (leavingSearch) ((App)Application.Current).CompleteTutorialSearch();
    }
    private void FollowTarget()
    {
        if (!TutorialGuide.Active) return;
        if (blocked()) { advance.Stop(); success = false; ClearHighlight(); Hide(); return; }
        if (!IsVisible) Show();
        var target = progress.Step >= 0 && progress.Step < Steps.Length && !success ? TutorialGuide.Find(Steps[progress.Step].Target) : null;
        if (target != highlighted)
        {
            if (target is not null && progress.Step >= 0 && progress.Step < Steps.Length && (Steps[progress.Step].Target is "fixed-bar" or "diagnostics"))
                target.BringIntoView();
            ClearHighlight(); highlighted = target;
            if (target is not null && AdornerLayer.GetAdornerLayer(target) is { } layer)
            { highlightLayer = layer; highlight = new TargetAdorner(target); layer.Add(highlight); }
        }
        if (!success && !finishing && progress.Step >= 0)
            hint.Text = saveError ?? (target is null ? "Abra a área indicada na explicação. Se o controle estiver oculto na sua configuração, use Pular etapa." : "Siga o contorno pulsante e realize a ação indicada.");
        PlaceBeside(target);
        EnsureAbovePanels();
    }
    private void ClearHighlight()
    {
        if (highlight is not null) { highlight.BeginAnimation(OpacityProperty, null); highlightLayer?.Remove(highlight); }
        highlight = null; highlighted = null; highlightLayer = null;
    }
    private sealed class TargetAdorner : Adorner
    {
        internal TargetAdorner(UIElement target) : base(target)
        {
            IsHitTestVisible = false;
            if (SystemParameters.ClientAreaAnimation)
                BeginAnimation(OpacityProperty, new DoubleAnimation(.4, 1, TimeSpan.FromMilliseconds(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }
        protected override void OnRender(DrawingContext dc)
        {
            var brush = Application.Current.TryFindResource("Think.Accent") as Brush ?? Brushes.DeepSkyBlue;
            var size = AdornedElement.RenderSize;
            dc.DrawRoundedRectangle(null, new Pen(brush, 2), new Rect(1, 1, Math.Max(0, size.Width - 2), Math.Max(0, size.Height - 2)), 9, 9);
        }
    }
}
