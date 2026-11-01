using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class TutorialWindow : Window
{
    private sealed record Step(string Module, string Title, string Text, string Target, string Action);
    private static readonly Step[] Steps =
    {
        new("1 · Barra de aplicativos", "Encontre sua barra", "Leve o mouse até a barra inferior. Ela aparece ao aproximar o cursor da borda. Os ícones ampliam se o efeito Dock estiver ativado.", "dock", "dock-hover"),
        new("1 · Barra de aplicativos", "Abra seu menu", "Clique no botão Iniciar indicado. Aqui ficam seus aplicativos fixados, a biblioteca e os recentes. Você também pode usar a tecla Windows.", "start", "start-open"),
        new("1 · Barra de aplicativos", "Pesquise um aplicativo", "Digite o nome de um aplicativo na pesquisa do menu ou da barra. O primeiro resultado fica selecionado: Enter abre o aplicativo. Ao sair da pesquisa, o texto é limpo.", "search", "search-typed"),
        new("2 · Central de controle", "Descubra a expansão", "Leve o mouse até a central no topo da tela. Na ilha, a primeira expansão mostra mídia e áudio. No modo barra, a seta abre os controles abaixo. Os ícones dependem da sua personalização.", "island", "island-hover"),
        new("2 · Central de controle", "Veja o painel completo", "Clique na seta de expansão. Aqui ficam os controles e widgets, como calendário, clima, notas rápidas e favoritos. Rede e áudio têm painéis próprios.", "expand", "island-full"),
        new("3 · Monitoramento e personalização", "Conheça o desempenho", "Clique no ícone de monitoramento. Ele mostra as leituras disponíveis do computador. O overlay é opcional e aparece em jogos; sensores avançados podem exigir autorização.", "monitor", "monitor-open"),
        new("3 · Monitoramento e personalização", "Deixe com a sua cara", "Clique no pincel de personalização. As abas organizam cores, transparência, ícones, widgets e overlay. As alterações são salvas automaticamente.", "customize", "settings-open"),
        new("3 · Monitoramento e personalização", "Explore a aparência", "Selecione a aba Aparência. Você pode mudar tema, cores e transparência em tempo real. Ao final das configurações, em Tutorial, é possível repetir este guia.", "appearance", "appearance-open")
    };
    public sealed class Progress
    {
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
    private Progress progress = new();
    private TargetAdorner? highlight;
    private AdornerLayer? highlightLayer;
    private FrameworkElement? highlighted;
    private bool success, finishing;
    private string? saveError;
    private Point? lastAnchor;
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
        follow.Tick += (_, _) => FollowTarget();
        advance.Tick += (_, _) => { advance.Stop(); progress.Step++; SaveProgress(); Render(); };
        TutorialGuide.Performed += Performed;
        Closed += (_, _) => { follow.Stop(); advance.Stop(); ClearHighlight(); TutorialGuide.Active = false; TutorialGuide.KeepDockOpen = TutorialGuide.KeepIslandOpen = false; TutorialGuide.Performed -= Performed; };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Pause(); e.Handled = true; } };
    }
    internal void OfferFirstRun()
    {
        // Never reopen onboarding merely because an existing installation updated.
        if ((!store.IsNewProfile && !store.Value.TutorialPending) || File.Exists(store.TutorialStatePath) || store.LoadWarning is not null) return;
        progress = new Progress(); SaveProgress();
        if (store.Value.TutorialPending) store.Update(p => p.TutorialPending = false);
        StartVisible();
    }
    internal void Repeat()
    {
        advance.Stop(); progress = new Progress();
        progress.Dismissed = false;
        SaveProgress(); StartVisible();
    }
    internal void Continue()
    {
        advance.Stop(); progress = ReadProgress();
        if (progress.Finished) progress = new Progress();
        progress.Dismissed = false; SaveProgress(); StartVisible();
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
            var value = File.Exists(store.TutorialStatePath) ? JsonSerializer.Deserialize<Progress>(File.ReadAllText(store.TutorialStatePath)) : null;
            if (value is not null && value.Step >= -1 && value.Step <= Steps.Length) return value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return new Progress();
    }
    private void SaveProgress()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(store.TutorialStatePath)!);
            File.WriteAllText(store.TutorialStatePath + ".tmp", JsonSerializer.Serialize(progress));
            File.Move(store.TutorialStatePath + ".tmp", store.TutorialStatePath, true); saveError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { saveError = "Não foi possível salvar o progresso do tutorial."; }
    }
    private void StartVisible()
    {
        finishing = false; TutorialGuide.Active = true; Render();
        if (!blocked()) Show();
        follow.Start(); FollowTarget();
    }
    private void Pause()
    {
        follow.Stop(); advance.Stop(); ClearHighlight(); TutorialGuide.Active = false;
        TutorialGuide.KeepDockOpen = TutorialGuide.KeepIslandOpen = false;
        progress.Dismissed = true; SaveProgress(); Hide();
    }
    private void AddAction(string label, Action action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 6, 0), Padding = new Thickness(8, 6, 8, 6), FontSize = 12 };
        button.Click += (_, _) => action(); actions.Children.Add(button);
    }
    private void Render()
    {
        success = false; finishing = false; lastAnchor = null; ClearHighlight(); actions.Children.Clear();
        TutorialGuide.KeepDockOpen = progress.Step >= 0 && progress.Step <= 2;
        TutorialGuide.KeepIslandOpen = progress.Step >= 4 && progress.Step <= 6;
        if (progress.Step < 0)
        {
            module.Text = "Bem-vindo ao Orby Launcher"; title.Text = "Vamos conhecer seu desktop?";
            description.Text = "Três módulos rápidos. Siga os destaques e use os controles de verdade: o guia reconhece suas ações e avança automaticamente.";
            hint.Text = "Você pode pular etapas ou sair a qualquer momento. O guia não altera suas configurações.";
            AddAction("Começar", () => { progress.Step = 0; SaveProgress(); Render(); });
            AddAction("Agora não", Pause);
        }
        else if (progress.Step >= Steps.Length)
        {
            finishing = true; progress.Finished = true; SaveProgress();
            module.Text = "Tutorial concluído"; title.Text = "Tudo pronto!";
            description.Text = "Você conheceu os principais recursos. Explore no seu ritmo e ajuste o launcher do seu jeito.";
            hint.Text = "Para rever: Personalizar → Tutorial → Repetir tutorial.";
            AddAction("Concluir", Pause);
        }
        else
        {
            var step = Steps[progress.Step]; module.Text = step.Module + $" · {progress.Step + 1}/{Steps.Length}";
            title.Text = step.Title; description.Text = step.Text;
            hint.Text = "Siga o destaque animado. A próxima etapa começa após sua ação.";
            AddAction("Voltar", () => { advance.Stop(); progress.Step--; SaveProgress(); Render(); });
            AddAction("Pular etapa", () => { advance.Stop(); progress.Step++; SaveProgress(); Render(); });
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
    private void FollowTarget()
    {
        if (!TutorialGuide.Active) return;
        if (blocked()) { advance.Stop(); success = false; ClearHighlight(); Hide(); return; }
        if (!IsVisible) Show();
        var target = progress.Step >= 0 && progress.Step < Steps.Length && !success ? TutorialGuide.Find(Steps[progress.Step].Target) : null;
        if (target != highlighted)
        {
            ClearHighlight(); highlighted = target;
            if (target is not null && AdornerLayer.GetAdornerLayer(target) is { } layer)
            { highlightLayer = layer; highlight = new TargetAdorner(target); layer.Add(highlight); }
        }
        if (!success && !finishing && progress.Step >= 0)
            hint.Text = saveError ?? (target is null ? "Abra a área indicada na explicação. Se o controle estiver oculto na sua configuração, use Pular etapa." : "Siga o contorno pulsante e realize a ação indicada.");
        var screen = System.Windows.Forms.Screen.PrimaryScreen ?? System.Windows.Forms.Screen.AllScreens[0];
        var point = target?.PointToScreen(new Point(target.ActualWidth / 2, target.ActualHeight / 2)) ?? (success ? lastAnchor : null);
        if (point.HasValue) lastAnchor = point;
        if (point is Point p) screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)p.X, (int)p.Y));
        var source = PresentationSource.FromVisual(this);
        var fromPixels = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var workStart = fromPixels.Transform(new Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var workEnd = fromPixels.Transform(new Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        Width = Math.Min(350, Math.Max(200, workEnd.X - workStart.X - 24));
        MaxHeight = Math.Max(120, workEnd.Y - workStart.Y - 24);
        double height = Math.Max(ActualHeight, 280);
        var anchor = point.HasValue ? fromPixels.Transform(point.Value) : new Point((workStart.X + workEnd.X) / 2, (workStart.Y + workEnd.Y) / 2);
        double x = point.HasValue ? anchor.X + 48 : anchor.X - Width / 2;
        double y = point.HasValue ? anchor.Y < (workStart.Y + workEnd.Y) / 2 ? anchor.Y + 65 : anchor.Y - height - 65 : anchor.Y - height / 2;
        Left = Math.Clamp(x, workStart.X + 12, Math.Max(workStart.X + 12, workEnd.X - Width - 12));
        Top = Math.Clamp(y, workStart.Y + 12, Math.Max(workStart.Y + 12, workEnd.Y - height - 12));
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
