using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

internal static class DockGroupChecks
{
    internal static int Run()
    {
        int passed = 0, result = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) =>
        {
            var a = new Window { Title = "Orby test A", Width = 180, Height = 100, ShowActivated = false };
            var b = new Window { Title = "Orby test B", Width = 180, Height = 100, ShowActivated = false };
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException(name);
                Console.WriteLine("PASS " + name); passed++;
            }
            try
            {
                a.Show(); b.Show();
                nint ha = new WindowInteropHelper(a).Handle, hb = new WindowInteropHelper(b).Handle;
                var group = new[] { ha, hb };
                Check(DockGroupAction.ShouldMinimize(group), "Grupo restaurado minimiza sem depender do foco");
                b.WindowState = WindowState.Minimized;
                Check(!DockGroupAction.ShouldMinimize(group), "Grupo parcialmente minimizado restaura todas");
                b.WindowState = WindowState.Normal;
                Check(await Desktop.ActivateFromDock(ha, true), "Windows confirma minimizar primeira janela");
                Check(await Desktop.ActivateFromDock(hb, true), "Windows confirma minimizar segunda janela");
                Check(!Desktop.IsVisibleAndRestored(ha) && !Desktop.IsVisibleAndRestored(hb), "Todas permanecem minimizadas");
                Check(await Desktop.ActivateFromDock(ha, true) && !Desktop.IsVisibleAndRestored(ha), "Minimizar novamente nao restaura");
                Check(!DockGroupAction.ShouldMinimize(group), "Proximo duplo clique solicita restauracao");
                a.WindowState = b.WindowState = WindowState.Normal;
                Check(DockGroupAction.ShouldMinimize(group), "Grupo restaurado volta a solicitar minimizar");
                b.Close();
                Check(DockGroupAction.ShouldMinimize(group), "Janela fechada nao invalida grupo restante");
                Check(!DockGroupAction.ShouldMinimize(Array.Empty<nint>()), "Grupo vazio nao minimiza");
                result = 0;
            }
            catch (Exception error) { Console.WriteLine("FAIL " + error); }
            finally { a.Close(); b.Close(); Console.WriteLine($"Dock: {passed} aprovados; resultado={result}"); app.Shutdown(result); }
        };
        app.Run(); return result;
    }
}
