using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

internal static class RevisionPerformanceChecks
{
    internal static int Run()
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int passed = 0, failed = 0;
        void Check(string name, Action action)
        {
            try { action(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
        }
        Check("Worker: pedido já cancelado não executa trabalho", () =>
        {
            using var worker = new StaWorker("Orby check — cancelado");
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            bool ran = false;
            var pending = worker.Run(() => { ran = true; return 1; }, cancellation.Token);
            Canceled(pending); Require(!ran, "Trabalho cancelado foi executado.");
        });
        Check("Worker: cancelamento dentro da ação não vira falha", () =>
        {
            using var worker = new StaWorker("Orby check — cancelamento interno");
            var pending = worker.Run<int>(() => throw new OperationCanceledException());
            Canceled(pending);
        });
        Check("Worker: descarte libera fila sem executar ações pendentes", () =>
        {
            using var started = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var worker = new StaWorker("Orby check — descarte");
            var running = worker.Run(() =>
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Ação bloqueada não foi liberada.");
                return 1;
            });
            bool queuedRan = false;
            Task<int>? queued = null;
            try
            {
                Require(started.Wait(TimeSpan.FromSeconds(5)), "Worker não iniciou.");
                queued = worker.Run(() => { queuedRan = true; return 2; });
                worker.Dispose(); worker.Dispose();
            }
            finally { release.Set(); }
            Require(running.GetAwaiter().GetResult() == 1, "Ação em andamento perdeu o resultado.");
            Canceled(queued!); Require(!queuedRan, "Fila executou após descarte.");
            Canceled(worker.Run(() => 3));
        });
        Check("Catálogo: suspensão cancela identidades e ícones sem publicar", () =>
        {
            using var catalog = new ApplicationCatalog(Dispatcher.CurrentDispatcher);
            bool published = false; catalog.Changed += () => published = true;
            catalog.SetSuspended(true);
            Canceled(catalog.ResolvePinsAsync(new[] { new Shortcut("Exemplo", @"C:\OrbyChecks\missing.exe") }));
            Canceled(catalog.ReadDockIconAsync(@"C:\OrbyChecks\missing.exe", 128));
            Require(!published && catalog.Items.Count == 0, "Catálogo suspenso publicou dados.");
        });
        Check("Tema: alterar só opacidade geral atualiza o popup em ambos os temas", () =>
        {
            foreach (string theme in new[] { "Dark", "Light" })
            {
                var preferences = new Preferences { Theme = theme, IslandOpacity = null, TintOpacity = .25 };
                ThemeService.Apply(preferences);
                byte before = ((SolidColorBrush)application.Resources["Think.Popup"]).Color.A;
                preferences.TintOpacity = .75; ThemeService.Apply(preferences);
                byte after = ((SolidColorBrush)application.Resources["Think.Popup"]).Color.A;
                Require(SystemParameters.HighContrast || before == 64 && after == 191, "Opacidade do tema permaneceu desatualizada.");
            }
        });
        Check("Popup: fallback restaura brush local, recurso dinâmico e binding", () =>
        {
            ThemeService.Apply(new Preferences());
            var original = new SolidColorBrush(Color.FromArgb(80, 20, 40, 60));
            var changed = new SolidColorBrush(Color.FromArgb(110, 80, 60, 40));
            var local = new Border { Background = original };
            FallbackAndRestore(local, original);
            var dynamic = new Border(); dynamic.Resources["Check.Background"] = original;
            dynamic.SetResourceReference(Border.BackgroundProperty, "Check.Background");
            FallbackAndRestore(dynamic, original);
            dynamic.Resources["Check.Background"] = changed;
            Require(ReferenceEquals(dynamic.Background, changed), "Recurso dinâmico perdeu a atualização.");
            var source = new Border { Background = original };
            var bound = new Border();
            BindingOperations.SetBinding(bound, Border.BackgroundProperty, new Binding(nameof(Border.Background)) { Source = source });
            FallbackAndRestore(bound, original);
            Require(BindingOperations.IsDataBound(bound, Border.BackgroundProperty), "Fallback removeu binding.");
            source.Background = changed;
            Require(ReferenceEquals(bound.Background, changed), "Binding perdeu a atualização.");
        });
        Check("Logon: definição sem atraso, sem elevação e com prioridade normal", () =>
        {
            const string executable = @"C:\Program Files\Orby & Tools\OrbyLauncher.exe";
            const string sid = "S-1-5-21-1-2-3-1001";
            var root = XElement.Parse(StartupRegistration.Definition(executable, sid));
            XNamespace ns = root.Name.Namespace;
            var trigger = root.Element(ns + "Triggers")!.Elements().Single();
            Require(trigger.Name == ns + "LogonTrigger" && trigger.Element(ns + "UserId")!.Value == sid
                && trigger.Element(ns + "Delay")!.Value == "PT0S", "Gatilho de logon incorreto.");
            var principal = root.Element(ns + "Principals")!.Elements().Single();
            Require(principal.Element(ns + "UserId")!.Value == sid && principal.Element(ns + "LogonType")!.Value == "InteractiveToken"
                && principal.Element(ns + "RunLevel")!.Value == "LeastPrivilege", "Tarefa não usa token interativo limitado.");
            var settings = root.Element(ns + "Settings")!;
            Require(settings.Element(ns + "Priority")!.Value == "4" && settings.Element(ns + "ExecutionTimeLimit")!.Value == "PT0S"
                && settings.Element(ns + "DisallowStartIfOnBatteries")!.Value == "false", "Prioridade/duração/bateria incorretas.");
            var action = root.Element(ns + "Actions")!.Elements().Single();
            Require(action.Name == ns + "Exec" && action.Element(ns + "Command")!.Value == executable
                && action.Element(ns + "Arguments")!.Value == "--startup"
                && action.Element(ns + "WorkingDirectory")!.Value == Path.GetDirectoryName(executable), "Caminho/argumentos escapados incorretamente.");
        });
        Check("Logon: escolha desativada e versão do registro persistem", () =>
        {
            string path = Path.Combine(Path.GetTempPath(), "OrbyRevisionChecks", Guid.NewGuid().ToString("N"), "settings.json");
            var store = new SettingsStore(path);
            store.Value.StartWithWindows = false; store.Value.StartupDefaultsInitialized = true;
            store.Value.StartupRegistrationVersion = 1; store.Save();
            var loaded = new SettingsStore(path).Value;
            Require(!loaded.StartWithWindows && loaded.StartupDefaultsInitialized && loaded.StartupRegistrationVersion == 1,
                "Preferência de inicialização não persistiu.");
        });
        Check("Fontes: limites gerais e compactos sem crescimento acumulado", () =>
        {
            var normal = new TextBlock { FontSize = 14, Text = "Texto" };
            var compact = new TextBlock { FontSize = 14, Text = "Texto" };
            var compactRoot = new StackPanel(); compactRoot.Children.Add(compact);
            FontSizing.LimitScale(compactRoot, 1.35);
            var panel = new StackPanel(); panel.Children.Add(normal); panel.Children.Add(compactRoot);
            var window = new Window { Content = panel };
            foreach (double scale in new[] { 2.5, 1d, 1.5, 2.5 })
            {
                FontSizing.Apply(window, scale); FontSizing.PrepareForMeasure(panel);
                Require(Math.Abs(normal.FontSize - 14 * Math.Min(scale, 1.4)) < .001, "Limite geral incorreto.");
                Require(Math.Abs(compact.FontSize - 14 * Math.Min(scale, 1.35)) < .001, "Limite compacto incorreto.");
            }
            var card = new HardwareSummaryCard("cpu", compact: true);
            Require(card.Padding.Left >= 12 && card.Padding.Top >= 12, "Cartão sem margem interna segura.");
        });
        Check("Brilho: indisponível sem bloco vazio e suporte recuperável", () =>
        {
            var control = new BrightnessControl(() => new System.Drawing.Rectangle(0, 0, 1920, 1080));
            var content = (StackPanel)control.Child;
            Require(content.Visibility == Visibility.Collapsed && control.Padding == new Thickness(0), "Bloco aparece antes da detecção.");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var render = typeof(BrightnessControl).GetMethod("Render", flags)!;
            var state = typeof(BrightnessControl).GetField("state", flags)!;
            state.SetValue(control, new BrightnessState("Tela", 0, 100, 50)); render.Invoke(control, null);
            Require(content.Visibility == Visibility.Visible && control.Padding.Left == 12, "Controle suportado não reapareceu.");
            state.SetValue(control, null); render.Invoke(control, null);
            Require(content.Visibility == Visibility.Collapsed && control.Padding == new Thickness(0)
                && control.Margin == new Thickness(0) && control.Visibility == Visibility.Visible, "Bloco indisponível ocupa espaço ou impede nova consulta.");
        });
        Console.WriteLine($"Revisão: {passed} aprovados, {failed} falhas. Nenhuma tarefa/registro de inicialização alterado.");
        return failed == 0 ? 0 : 1;
    }
    private static void FallbackAndRestore(Border target, Brush original)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        var fallback = typeof(PopupBackdrop).GetMethod("Fallback", flags)!;
        var restore = typeof(PopupBackdrop).GetMethod("RestoreBackground", flags)!;
        fallback.Invoke(null, new object[] { target });
        Require(ReferenceEquals(target.Background, Application.Current.Resources["Think.Background"]), "Fallback não foi aplicado.");
        restore.Invoke(null, new object[] { target });
        Require(ReferenceEquals(target.Background, original), "Fundo original não foi restaurado.");
    }
    private static void Canceled(Task task)
    {
        try { task.GetAwaiter().GetResult(); throw new InvalidOperationException("Cancelamento não foi observado."); }
        catch (OperationCanceledException) { Require(task.IsCanceled && !task.IsFaulted, "Cancelamento foi publicado como falha."); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
