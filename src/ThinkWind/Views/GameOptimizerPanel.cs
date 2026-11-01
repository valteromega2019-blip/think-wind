using System;
using System.Windows;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class GameOptimizerPanel : StackPanel
{
    private readonly SettingsStore store;
    private readonly GameOptimizer optimizer;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 5), FontSize = 11 };
    private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12), FontSize = 11 };
    private string selected;
    private CancellationTokenSource? analysis;
    internal GameOptimizerPanel(SettingsStore settings)
    {
        store = settings; optimizer = ((App)Application.Current).Optimizer;
        Margin = new Thickness(3);
        Children.Add(new TextBlock { Text = "Otimizador de Jogos", FontSize = 16, FontWeight = FontWeights.SemiBold });
        selected = store.Value.OptimizerPowerPlan is "Performance" or "Economy" ? store.Value.OptimizerPowerPlan : "Balanced";
        var modes = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var mode in new[] { ("Balanced", "Equilibrado"), ("Performance", "Desempenho"), ("Economy", "Economia") })
        {
            var button = new RadioButton { Content = mode.Item2, GroupName = "OptimizerMode", IsChecked = mode.Item1 == selected, Margin = new Thickness(0, 4, 18, 4) };
            button.Checked += (_, _) => { selected = mode.Item1; Describe(); };
            modes.Children.Add(button);
        }
        var optimize = new Button { Content = "Otimizar", Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(0, 8, 0, 8) };
        optimize.Click += (_, _) =>
        {
            optimizer.Restore();
            if (optimizer.RestorationPending) { status.Text = optimizer.Status; return; }
            store.Update(p => { p.OptimizerPowerPlan = selected; p.OptimizerEnabled = true; });
            optimizer.Apply(optimizer.GameForeground, store.Value);
            status.Text = optimizer.GameForeground ? optimizer.Status : "Pronto · " + ModeName() + " será aplicado ao entrar em um jogo. O plano anterior será restaurado ao sair.";
        };
        Children.Add(modes);
        Children.Add(new ScrollViewer { Content = description, Height = 42, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var actions = new WrapPanel(); actions.Children.Add(optimize); Children.Add(actions);
        Children.Add(new ScrollViewer { Content = status, Height = 42, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var stop = new Button { Content = "Voltar ao normal", Margin = new Thickness(0, 4, 0, 4) };
        stop.Click += (_, _) => { store.Update(p => p.OptimizerEnabled = false); optimizer.Restore(); status.Text = optimizer.Status; };
        actions.Children.Add(stop);
        var analyze = new Button { Content = "Analisar", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 0, 8) };
        var report = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Text = "Clique em Analisar para revisar consumo e configurações." };
        report.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        actions.Children.Add(analyze);
        var approvals = new List<(CheckBox Check, OptimizerCloseCandidate Process)>();
        var choices = new StackPanel();
        var close = new Button { Content = "Fechar selecionados…", IsEnabled = false, Margin = new Thickness(0, 5, 0, 0) };
        var tabs = new WrapPanel { Margin = new Thickness(0, 4, 0, 6) };
        var reportScroll = new ScrollViewer { Content = report, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var apps = new DockPanel { LastChildFill = true, Visibility = Visibility.Collapsed };
        var appFooter = new StackPanel();
        var pager = new WrapPanel();
        var previous = new Button { Content = "‹" }; var next = new Button { Content = "›" };
        var pageLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        pager.Children.Add(previous); pager.Children.Add(pageLabel); pager.Children.Add(next);
        appFooter.Children.Add(pager); appFooter.Children.Add(close);
        DockPanel.SetDock(appFooter, Dock.Bottom); apps.Children.Add(appFooter);
        apps.Children.Add(new ScrollViewer { Content = choices, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var content = new Grid { Height = 205 }; content.Children.Add(reportScroll); content.Children.Add(apps);
        var cardBody = new StackPanel(); cardBody.Children.Add(tabs); cardBody.Children.Add(content);
        var card = new Border { Child = cardBody }; card.SetResourceReference(StyleProperty, "CardBorder"); Children.Add(card);
        int page = 0; string fullReport = report.Text; string currentTab = "Resumo";
        void UpdatePage()
        {
            int total = Math.Max(1, (approvals.Count + 3) / 4); page = Math.Clamp(page, 0, total - 1);
            for (int i = 0; i < approvals.Count; i++) approvals[i].Check.Visibility = i / 4 == page ? Visibility.Visible : Visibility.Collapsed;
            pageLabel.Text = approvals.Count == 0 ? "Nenhum aplicativo elegível" : $"{page + 1} / {total}";
            previous.IsEnabled = page > 0; next.IsEnabled = page < total - 1;
            close.Content = "Fechar selecionados (" + approvals.Count(item => item.Check.IsChecked == true) + ")…";
        }
        void ShowTab(string tab)
        {
            currentTab = tab; bool applicationTab = tab == "Aplicativos";
            apps.Visibility = applicationTab ? Visibility.Visible : Visibility.Collapsed;
            reportScroll.Visibility = applicationTab ? Visibility.Collapsed : Visibility.Visible;
            int processStart = fullReport.IndexOf("MAIOR CONSUMO OBSERVADO", StringComparison.Ordinal);
            int reviewStart = fullReport.IndexOf("APLICATIVOS PARA REVISAR", StringComparison.Ordinal);
            report.Text = processStart < 0 ? fullReport : tab == "Processos"
                ? fullReport.Substring(processStart, (reviewStart > processStart ? reviewStart : fullReport.Length) - processStart).Trim()
                    + "\n\nConsumo é uma amostra curta; RAM pode incluir páginas compartilhadas."
                : fullReport[..processStart].Trim();
            foreach (Button button in tabs.Children)
                button.SetResourceReference(Control.BackgroundProperty, (string)button.Content == tab ? "Think.AccentSurface" : "Think.Hover");
            reportScroll.ScrollToTop(); UpdatePage();
        }
        foreach (string name in new[] { "Resumo", "Processos", "Aplicativos" })
        {
            var tab = new Button { Content = name, Margin = new Thickness(0, 0, 4, 0) };
            tab.Click += (_, _) => ShowTab(name); tabs.Children.Add(tab);
        }
        previous.Click += (_, _) => { page--; UpdatePage(); }; next.Click += (_, _) => { page++; UpdatePage(); };
        foreach (Button button in actions.Children.Cast<UIElement>().Concat(tabs.Children.Cast<UIElement>()).Concat(new UIElement[] { previous, next, close }).OfType<Button>())
        {
            button.SetResourceReference(StyleProperty, "OutlineButton"); button.Margin = new Thickness(0, 3, 5, 3);
            button.Padding = new Thickness(9, 5, 9, 5);
        }
        description.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        status.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        pageLabel.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        ShowTab("Resumo");
        bool closing = false;
        close.Click += async (_, _) =>
        {
            if (closing || analysis is not null) return;
            var selectedProcesses = approvals.Where(item => item.Check.IsChecked == true).Select(item => item.Process).ToArray();
            if (selectedProcesses.Length == 0) { status.Text = "Selecione os aplicativos que deseja fechar."; return; }
            if (MessageBox.Show(Window.GetWindow(this), "Solicitar o fechamento de:\n" + string.Join("\n", selectedProcesses.Select(item => item.Name + " (PID " + item.Id + ")"))
                + "\n\nSalve seu trabalho. Fechar uma janela pode encerrar outras janelas do mesmo aplicativo. Nada será encerrado à força.",
                "Aprovar fechamento", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            closing = true; close.IsEnabled = false; analyze.IsEnabled = false; choices.IsEnabled = false;
            var outcomes = new List<string>();
            try
            {
                foreach (var process in selectedProcesses) outcomes.Add(await OptimizerProcessCloser.RequestClose(process));
                status.Text = string.Join("\n", outcomes);
            }
            finally { approvals.Clear(); choices.Children.Clear(); UpdatePage(); choices.IsEnabled = true; closing = false; analyze.IsEnabled = true; }
        };
        analyze.Click += async (_, _) =>
        {
            if (analysis is not null || closing) return;
            approvals.Clear(); choices.Children.Clear(); close.IsEnabled = false;
            using var cancellation = new CancellationTokenSource();
            analysis = cancellation; analyze.IsEnabled = false;
            fullReport = "Observando processos por alguns instantes…"; ShowTab("Resumo");
            var preferences = store.Value;
            try
            {
                var result = await OptimizerAnalysis.Analyze(((App)Application.Current).Telemetry.Latest,
                    preferences.GameModeEnabled, preferences.OptimizerEnabled,
                    preferences.OptimizerPowerPlan == "Performance" ? "Desempenho" : preferences.OptimizerPowerPlan == "Economy" ? "Economia" : preferences.OptimizerPowerPlan == "Keep" ? "Manter plano atual" : "Equilibrado",
                    preferences.HardwareOverlayEnabled, cancellation.Token);
                if (!cancellation.IsCancellationRequested)
                {
                    fullReport = result.Text;
                    foreach (var process in result.Candidates)
                    {
                        var check = new CheckBox { Content = new TextBlock { Text = process.Name, TextWrapping = TextWrapping.Wrap }, ToolTip = "PID " + process.Id, Margin = new Thickness(0, 7, 0, 7) }; check.SetResourceReference(Control.ForegroundProperty, "Think.Text"); check.Checked += (_, _) => UpdatePage(); check.Unchecked += (_, _) => UpdatePage();
                        choices.Children.Add(check); approvals.Add((check, process));
                    }
                    close.IsEnabled = approvals.Count > 0; page = 0; ShowTab(currentTab);
                }
            }
            catch (OperationCanceledException) { fullReport = "Análise cancelada."; ShowTab("Resumo"); }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
            { fullReport = "Não foi possível concluir a análise: " + error.Message; ShowTab("Resumo"); }
            finally { analysis = null; analyze.IsEnabled = true; }
        };
        Loaded += (_, _) => { optimizer.Changed += GameChanged; GameChanged(); };
        Unloaded += (_, _) => { optimizer.Changed -= GameChanged; analysis?.Cancel(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) analysis?.Cancel(); };
        Describe();
    }
    private string ModeName() => selected == "Performance" ? "Desempenho" : selected == "Economy" ? "Economia" : "Equilibrado";
    private void GameChanged()
    {
        if (optimizer.GameForeground) analysis?.Cancel();
        status.Text = store.Value.OptimizerEnabled && !optimizer.GameForeground && !optimizer.RestorationPending
            ? "Perfil automático habilitado · aguardando um jogo." : optimizer.Status;
    }
    private void Describe() => description.Text = (selected switch
    {
        "Performance" => "Maior desempenho, consumo e calor durante jogos.",
        "Economy" => "Menor consumo durante jogos; pode reduzir FPS.",
        _ => "Equilíbrio entre consumo e desempenho."
    }) + " Restaura o plano anterior ao sair do jogo.";
}

