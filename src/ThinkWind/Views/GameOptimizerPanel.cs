using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class GameOptimizerPanel : StackPanel
{
    private readonly SettingsStore store;
    private readonly GameOptimizer optimizer;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };
    private readonly StackPanel apps = new();
    internal GameOptimizerPanel(SettingsStore settings)
    {
        store = settings; optimizer = ((App)Application.Current).Optimizer;
        Margin = new Thickness(8);
        Children.Add(new TextBlock { Text = "Otimizador de Jogos", FontSize = 22, FontWeight = FontWeights.SemiBold });
        Children.Add(new LocalOptimizerAiPanel(store));
        Note("Perfil local · sem envio de dados para IA externa nesta etapa.");
        var enabled = new CheckBox { Content = "Aplicar perfil ao reconhecer um jogo", IsChecked = store.Value.OptimizerEnabled, Margin = new Thickness(0, 8, 0, 10) };
        enabled.Checked += (_, _) => store.Update(p => p.OptimizerEnabled = true);
        enabled.Unchecked += (_, _) => store.Update(p => p.OptimizerEnabled = false); Children.Add(enabled);
        var plans = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id", ItemsSource = new[] { new Plan("Keep", "Manter plano atual"), new Plan("Balanced", "Equilibrado"), new Plan("Performance", "Alto desempenho · maior consumo e calor") }, SelectedValue = store.Value.OptimizerPowerPlan, Margin = new Thickness(0, 0, 0, 8) };
        plans.SelectionChanged += (_, _) => { if (plans.SelectedValue is string value) { optimizer.Restore(); store.Update(p => p.OptimizerPowerPlan = value); } }; Children.Add(plans);
        Children.Add(status);
        Button("Restaurar perfil agora e desativar", () => { store.Update(p => p.OptimizerEnabled = false); enabled.IsChecked = false; optimizer.Restore(); });
        Note("O plano anterior é restaurado ao sair do jogo ou encerrar o launcher. Se você trocar o plano pelo Windows durante o jogo, sua escolha será preservada. Planos ausentes não são criados. Nenhuma configuração de overlay, sensores ou barras é redefinida.");
        Note("Aplicativos dispensáveis · seleção manual");
        Button("Listar janelas para encerrar normalmente", LoadApps); Children.Add(apps);
        Note("Fechar aplicativos pode liberar recursos. O encerramento respeita avisos de documentos não salvos. Aplicativos não são reabertos automaticamente e seu estado anterior não é garantido. Nenhum processo é terminado à força.");
        Note("Memória: sem esvaziar standby ou working sets. O gerenciamento permanece com o Windows; não há promessa de ganho de FPS nem limpeza artificial de RAM. A redução de tarefas do launcher usa o Modo Jogo existente.");
        Loaded += (_, _) => { optimizer.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => optimizer.Changed -= Refresh;
    }
    private sealed record Plan(string Id, string Name);
    private void Note(string text) { var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 6, 0, 8) }; label.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); Children.Add(label); }
    private void Button(string text, Action action) { var button = new Button { Content = text, Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(10, 7, 10, 7) }; button.Click += (_, _) => action(); Children.Add(button); }
    private void Refresh() => status.Text = optimizer.Status;
    private async void LoadApps()
    {
        apps.Children.Clear();
        System.Collections.Generic.List<OrbyLauncher.Native.Desktop.RunningWindow> windows;
        try { windows = await System.Threading.Tasks.Task.Run(OrbyLauncher.Native.Desktop.Windows); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { status.Text = "Não foi possível listar janelas: " + e.Message; return; }
        if (!IsVisible) return;
        foreach (var window in windows.Where(window => window.Executable is not null).Take(30))
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(window.Executable!);
            if (new[] { "explorer", "SystemSettings", "ApplicationFrameHost", "OrbyLauncher", "ThinkMonitor", "ThinkExplode", "steam", "steamwebhelper", "Discord", "WhatsApp.Root" }.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;
            var button = new Button { Content = "Fechar: " + window.Title, ToolTip = window.Executable, Margin = new Thickness(0, 3, 0, 3) };
            button.Click += (_, _) =>
            {
                if (MessageBox.Show(Window.GetWindow(this), "Solicitar fechamento de “" + window.Title + "”?", "Otimizador", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                try
                {
                    using var process = FindProcess(window.Handle);
                    if (process is not null && process.MainWindowHandle == window.Handle) { process.CloseMainWindow(); button.IsEnabled = false; }
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { status.Text = "O Windows não confirmou o fechamento: " + e.Message; }
            };
            apps.Children.Add(button);
        }
    }
    private static Process? FindProcess(nint handle)
    {
        foreach (var process in Process.GetProcesses())
        { try { if (process.MainWindowHandle == handle) return process; } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { } process.Dispose(); }
        return null;
    }
}
