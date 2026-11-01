using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class GameOptimizerPanel : StackPanel
{
    private readonly SettingsStore store;
    private readonly GameOptimizer optimizer;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 10), FontSize = 13 };
    private readonly TextBlock description = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12), FontSize = 12 };
    private readonly Button optimize = new() { Content = "Otimizar", Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(0, 8, 0, 8) };
    private CancellationTokenSource? request;
    private string selected;
    private const string Model = "qwen2.5:1.5b";
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
        Children.Add(modes); Children.Add(description); Children.Add(optimize); Children.Add(status);
        var stop = new Button { Content = "Voltar ao normal", Margin = new Thickness(0, 4, 0, 4) };
        stop.Click += (_, _) => { request?.Cancel(); store.Update(p => p.OptimizerEnabled = false); optimizer.Restore(); status.Text = optimizer.Status; }; Children.Add(stop);
        optimize.Click += async (_, _) =>
        {
            if (request is not null) return;
            var app = (App)Application.Current;
            if (optimizer.GameForeground) { status.Text = "Prepare a otimização fora do jogo para a IA não disputar recursos com ele."; return; }
            using var cancellation = new CancellationTokenSource(); request = cancellation; optimize.IsEnabled = false;
            try
            {
                optimizer.Restore(); store.Update(p => { p.OptimizerPowerPlan = selected; p.OptimizerEnabled = true; });
                status.Text = "Perfil preparado para o próximo jogo. Preparando análise local…";
                await LocalOptimizerAi.StartRuntime(cancellation.Token);
                var models = await LocalOptimizerAi.Models(cancellation.Token);
                if (!models.Contains(Model, StringComparer.Ordinal))
                {
                    if (MessageBox.Show(Window.GetWindow(this), "Preparar a IA local agora? O modelo predefinido ocupa aproximadamente 1 GB e será baixado uma única vez pelo Ollama.", "Preparar otimizador", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                    { status.Text = "Perfil preparado. A análise com IA ficou pendente do download do modelo."; return; }
                    await LocalOptimizerAi.DownloadModel(Model, new Progress<string>(text => { if (!cancellation.IsCancellationRequested) status.Text = "Preparando IA · " + text; }), cancellation.Token);
                }
                var sample = app.Telemetry.Latest;
                if (sample is null || DateTime.Now - sample.Time > TimeSpan.FromSeconds(30)) { status.Text = "Perfil preparado. Aguarde sensores recentes e clique em Otimizar novamente para analisar."; return; }
                status.Text = "Analisando seu computador localmente…";
                status.Text = "Perfil preparado para os jogos. Sugestões da IA:\n\n" + await LocalOptimizerAi.Suggest(Model, sample, store.Value, cancellation.Token);
            }
            catch (OperationCanceledException) { status.Text = "Análise cancelada. O perfil de energia permanece como configurado; Voltar ao normal o desativa."; }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or System.IO.IOException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or System.Collections.Generic.KeyNotFoundException)
            { status.Text = "Perfil preparado, mas a IA não ficou disponível: " + e.Message; }
            finally { request = null; optimize.IsEnabled = true; }
        };
        Loaded += (_, _) => optimizer.Changed += GameChanged;
        Unloaded += (_, _) => { optimizer.Changed -= GameChanged; request?.Cancel(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) request?.Cancel(); };
        Describe(); status.Text = optimizer.Status;
    }
    private void GameChanged() { if (optimizer.GameForeground) request?.Cancel(); if (request is null) status.Text = optimizer.Status; }
    private void Describe() => description.Text = (selected switch
    {
        "Performance" => "Prioriza desempenho com maior consumo e calor. O plano anterior retorna ao sair do jogo.",
        "Economy" => "Prioriza economia de energia e pode reduzir o desempenho. O plano anterior retorna ao sair do jogo.",
        _ => "Equilibra consumo e desempenho. O plano anterior retorna ao sair do jogo."
    }) + " As configurações do launcher são preservadas. Aplicativos não são fechados automaticamente; a IA apenas sugere.";
}

