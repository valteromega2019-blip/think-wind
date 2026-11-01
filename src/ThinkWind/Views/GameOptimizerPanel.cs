using System;
using System.Windows;
using System.Threading;
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
        analyze.Click += async (_, _) =>
        {
            if (analysis is not null) return;
            using var cancellation = new CancellationTokenSource();
            analysis = cancellation; analyze.IsEnabled = false;
            report.Text = "Observando processos por alguns instantes…";
            var preferences = store.Value;
            try
            {
                string result = await OptimizerAnalysis.Analyze(((App)Application.Current).Telemetry.Latest,
                    preferences.GameModeEnabled, preferences.OptimizerEnabled,
                    preferences.OptimizerPowerPlan == "Performance" ? "Desempenho" : preferences.OptimizerPowerPlan == "Economy" ? "Economia" : preferences.OptimizerPowerPlan == "Keep" ? "Manter plano atual" : "Equilibrado",
                    preferences.HardwareOverlayEnabled, cancellation.Token);
                if (!cancellation.IsCancellationRequested) report.Text = result;
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
