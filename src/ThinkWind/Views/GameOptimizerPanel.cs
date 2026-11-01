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
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 10), FontSize = 13 };
    private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12), FontSize = 12 };
    private string selected;
    private CancellationTokenSource? analysis;
    internal GameOptimizerPanel(SettingsStore settings)
    {
        store = settings; optimizer = ((App)Application.Current).Optimizer;
        Margin = new Thickness(10);
        Children.Add(new TextBlock { Text = "Otimizador de Jogos", FontSize = 22, FontWeight = FontWeights.SemiBold });
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
        Children.Add(modes); Children.Add(description); Children.Add(optimize); Children.Add(status);
        var stop = new Button { Content = "Voltar ao normal", Margin = new Thickness(0, 4, 0, 4) };
        stop.Click += (_, _) => { store.Update(p => p.OptimizerEnabled = false); optimizer.Restore(); status.Text = optimizer.Status; };
        Children.Add(stop);
        var analyze = new Button { Content = "Analisar processos e configurações", Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 8, 0, 8) };
        var report = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        Children.Add(analyze);
        Children.Add(new ScrollViewer { Content = report, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var choices = new StackPanel();
        var close = new Button { Content = "Fechar selecionados…", IsEnabled = false, Margin = new Thickness(0, 8, 0, 8) };
        Children.Add(new ScrollViewer { Content = choices, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Children.Add(close);
        var approvals = new List<(CheckBox Check, OptimizerCloseCandidate Process)>();
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
            finally { approvals.Clear(); choices.Children.Clear(); choices.IsEnabled = true; closing = false; analyze.IsEnabled = true; }
        };
        analyze.Click += async (_, _) =>
        {
            if (analysis is not null || closing) return;
            approvals.Clear(); choices.Children.Clear(); close.IsEnabled = false;
            using var cancellation = new CancellationTokenSource();
            analysis = cancellation; analyze.IsEnabled = false;
            report.Text = "Observando processos por alguns instantes…";
            var preferences = store.Value;
            try
            {
                var result = await OptimizerAnalysis.Analyze(((App)Application.Current).Telemetry.Latest,
                    preferences.GameModeEnabled, preferences.OptimizerEnabled,
                    preferences.OptimizerPowerPlan == "Performance" ? "Desempenho" : preferences.OptimizerPowerPlan == "Economy" ? "Economia" : preferences.OptimizerPowerPlan == "Keep" ? "Manter plano atual" : "Equilibrado",
                    preferences.HardwareOverlayEnabled, cancellation.Token);
                if (!cancellation.IsCancellationRequested)
                {
                    report.Text = result.Text;
                    foreach (var process in result.Candidates)
                    {
                        var check = new CheckBox { Content = process.Name + " · PID " + process.Id, Margin = new Thickness(0, 5, 0, 5) };
                        choices.Children.Add(check); approvals.Add((check, process));
                    }
                    close.IsEnabled = approvals.Count > 0;
                }
            }
            catch (OperationCanceledException) { report.Text = "Análise cancelada."; }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
            { report.Text = "Não foi possível concluir a análise: " + error.Message; }
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
        "Performance" => "Usa o plano Alto desempenho do Windows durante jogos, com maior consumo e calor.",
        "Economy" => "Usa o plano Economia de energia durante jogos, priorizando consumo menor. Pode reduzir o desempenho.",
        _ => "Usa o plano Equilibrado durante jogos, conciliando consumo e desempenho."
    }) + " O plano anterior retorna ao sair do jogo. As configurações e os recursos do launcher são preservados.";
}
