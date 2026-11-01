using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class LocalOptimizerAiPanel : StackPanel
{
    private readonly ComboBox models = new() { MinHeight = 32, Margin = new Thickness(0, 4, 0, 8) };
    private readonly TextBlock result = new() { Text = "A IA analisa somente quando você solicita. Requer Ollama local com um modelo já instalado. Não baixa modelos nem aplica ações.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12), FontSize = 12 };
    private readonly Button refresh = new() { Content = "Localizar modelos instalados" }, analyze = new() { Content = "Analisar com IA local", IsEnabled = false }, cancel = new() { Content = "Cancelar", IsEnabled = false };
    private CancellationTokenSource? request;
    private readonly SettingsStore settings;
    private readonly Button start = new() { Content = "Iniciar mecanismo local" }, download = new() { Content = "Baixar modelo" };
    private readonly TextBox modelName = new() { Text = "", MinHeight = 32, Margin = new Thickness(0, 4, 0, 6), ToolTip = "Nome exato de um modelo local do catálogo Ollama" };
    internal LocalOptimizerAiPanel(SettingsStore store)
    {
        settings = store;
        Children.Add(new TextBlock { Text = "Sugestões com IA local", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 6) });
        Children.Add(result); Children.Add(models);
        Children.Add(new TextBlock { Text = "Preparar IA neste computador", FontWeight = FontWeights.SemiBold });
        Children.Add(new TextBlock { Text = "Nome do modelo Ollama para baixar (download pode ocupar vários GB)", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        Children.Add(modelName);
        var preparation = new WrapPanel(); preparation.Children.Add(start); preparation.Children.Add(download); Children.Add(preparation);
        System.Windows.Automation.AutomationProperties.SetName(modelName, "Nome do modelo local a baixar");
        start.Click += async (_, _) =>
        {
            if (request is not null) return;
            using var cancellation = new CancellationTokenSource(); request = cancellation; SetBusy(true);
            try { await LocalOptimizerAi.StartRuntime(cancellation.Token); result.Text = "Mecanismo local pronto. Use Localizar modelos instalados ou baixe um modelo abaixo."; }
            catch (Exception e) when (Expected(e) || e is System.ComponentModel.Win32Exception) { result.Text = "Não foi possível preparar o mecanismo: " + e.Message; }
            finally { request = null; SetBusy(false); }
        };
        download.Click += async (_, _) =>
        {
            if (request is not null) return;
            string name = modelName.Text.Trim();
            if (name.Length == 0) { result.Text = "Informe o nome exato de um modelo local do catálogo Ollama."; return; }
            if (((App)Application.Current).Optimizer.GameForeground) { result.Text = "Saia do jogo antes de baixar ou preparar a IA."; return; }
            if (MessageBox.Show(Window.GetWindow(this), "Baixar “" + name + "” pelo Ollama? O download usa internet e pode ocupar vários GB. Os pesos ficarão no armazenamento gerenciado pelo Ollama.", "Preparar IA local", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            using var cancellation = new CancellationTokenSource(); request = cancellation; SetBusy(true); result.Text = "Preparando download autorizado…";
            try
            {
                await LocalOptimizerAi.StartRuntime(cancellation.Token);
                await LocalOptimizerAi.DownloadModel(name, new Progress<string>(value => { if (!cancellation.IsCancellationRequested) result.Text = "Download do modelo · " + value; }), cancellation.Token);
                var installed = await LocalOptimizerAi.Models(cancellation.Token); models.ItemsSource = installed; models.SelectedItem = name;
                result.Text = "Modelo baixado. Selecione-o na lista para solicitar a análise.";
            }
            catch (Exception e) when (Expected(e) || e is System.IO.IOException or System.ComponentModel.Win32Exception) { result.Text = "Preparação interrompida: " + e.Message; }
            finally { request = null; SetBusy(false); }
        };
        var actions = new WrapPanel(); actions.Children.Add(refresh); actions.Children.Add(analyze); actions.Children.Add(cancel); Children.Add(actions);
        System.Windows.Automation.AutomationProperties.SetName(models, "Modelo de IA instalado localmente");
        refresh.Click += async (_, _) =>
        {
            if (request is not null) return;
            using var cancellation = new CancellationTokenSource(); request = cancellation; SetBusy(true);
            try
            {
                var names = await LocalOptimizerAi.Models(cancellation.Token); cancellation.Token.ThrowIfCancellationRequested();
                models.ItemsSource = names; if (names.Length > 0) models.SelectedIndex = 0;
                result.Text = names.Length == 0 ? "Nenhum modelo local encontrado. Instale um modelo no Ollama antes de analisar." : "Escolha um modelo e solicite a análise. Serão enviados ao serviço local apenas hardware, temperaturas, uso de CPU/GPU/RAM e preferências do otimizador.";
            }
            catch (Exception e) when (Expected(e)) { result.Text = "Ollama local indisponível, operação cancelada ou resposta inválida. Abra o Ollama com um modelo local instalado e tente novamente."; }
            finally { request = null; SetBusy(false); }
        };
        analyze.Click += async (_, _) =>
        {
            if (request is not null || models.SelectedItem is not string model) return;
            var app = (App)Application.Current;
            if (app.Optimizer.GameForeground) { result.Text = "Saia do jogo antes de solicitar a análise, para evitar competição por CPU/GPU."; return; }
            using var cancellation = new CancellationTokenSource(); request = cancellation; SetBusy(true); result.Text = "Analisando localmente… Nenhuma ação será executada.";
            try
            {
                var sample = app.Telemetry.Latest;
                if (sample is null || DateTime.Now - sample.Time > TimeSpan.FromSeconds(30)) throw new InvalidOperationException("Aguarde uma leitura recente dos sensores antes de analisar.");
                result.Text = "Sugestões geradas por IA · confira antes de agir\n\n" + await LocalOptimizerAi.Suggest(model, sample, settings.Value, cancellation.Token);
            }
            catch (Exception e) when (Expected(e)) { result.Text = e is InvalidOperationException ? e.Message : "Análise interrompida ou modelo local indisponível. Tente novamente fora do jogo."; }
            finally { request = null; SetBusy(false); }
        };
        cancel.Click += (_, _) => request?.Cancel();
        IsVisibleChanged += (_, _) => { if (!IsVisible) request?.Cancel(); };
        Loaded += (_, _) => ((App)Application.Current).Optimizer.Changed += GameChanged;
        Unloaded += (_, _) => { request?.Cancel(); ((App)Application.Current).Optimizer.Changed -= GameChanged; };
    }
    private void GameChanged() { if (((App)Application.Current).Optimizer.GameForeground) request?.Cancel(); }
    private void SetBusy(bool busy) { refresh.IsEnabled = !busy; start.IsEnabled = download.IsEnabled = modelName.IsEnabled = !busy; analyze.IsEnabled = !busy && models.SelectedItem is string; models.IsEnabled = !busy; cancel.IsEnabled = busy; }
    private static bool Expected(Exception e) => e is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException;
}
