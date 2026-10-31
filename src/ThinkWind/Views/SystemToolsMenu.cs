using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal static class SystemToolsMenu
{
    internal static ContextMenu Create(Button anchor)
    {
        var menu = new ContextMenu { PlacementTarget = anchor, Placement = PlacementMode.Bottom };
        void Add(string label, Action action)
        {
            var item = new MenuItem { Header = label };
            item.Click += (_, _) => action(); menu.Items.Add(item);
        }
        string SystemFile(string name) => Path.Combine(Environment.SystemDirectory, name);
        void Console(string name) => SystemActions.Run(SystemFile(name));
        Add("Aplicativos instalados", () => SystemActions.Run("ms-settings:appsfeatures"));
        Add("Opções de Energia", () => SystemActions.Run("ms-settings:powersleep"));
        Add("Visualizador de Eventos", () => Console("eventvwr.msc"));
        Add("Sistema", () => SystemActions.Run("ms-settings:about"));
        Add("Gerenciador de Dispositivos", () => Console("devmgmt.msc"));
        Add("Conexões de Rede", () => Console("ncpa.cpl"));
        Add("Gerenciamento de Disco", () => Console("diskmgmt.msc"));
        Add("Gerenciamento do Computador", () => Console("compmgmt.msc"));
        string terminal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (!File.Exists(terminal)) terminal = SystemFile(@"WindowsPowerShell\v1.0\powershell.exe");
        Add("Terminal", () => SystemActions.Run(terminal));
        Add("Terminal (Administrador)", () => SystemActions.RunAsAdministrator(terminal));
        menu.Items.Add(new Separator());
        Add("Gerenciador de Tarefas", () => Console("Taskmgr.exe"));
        Add("Configurações", () => SystemActions.Run("ms-settings:"));
        Add("Explorador de Arquivos", () => SystemActions.Run("explorer.exe"));
        Add("Pesquisar", () => Desktop.Chord(0x5B, 0x53));
        Add("Executar", () => ShellAction("FileRun"));
        menu.Items.Add(new Separator());
        var power = new MenuItem { Header = "Desligar ou sair" };
        var signOut = new MenuItem { Header = "Sair" };
        signOut.Click += (_, _) =>
        {
            if (MessageBox.Show("Sair da conta do Windows agora? Salve seus documentos antes de continuar.", "Think Wind", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            try
            {
                var info = new ProcessStartInfo(SystemFile("shutdown.exe")) { UseShellExecute = false, CreateNoWindow = true };
                info.ArgumentList.Add("/l"); Process.Start(info);
            }
            catch (System.ComponentModel.Win32Exception error) { MessageBox.Show(error.Message, "Think Wind"); }
        };
        power.Items.Add(signOut);
        foreach (string action in new[] { "Suspender", "Desligar", "Reiniciar" })
        {
            var item = new MenuItem { Header = action };
            item.Click += (_, _) => SystemActions.Power(action); power.Items.Add(item);
        }
        menu.Items.Add(power);
        Add("Área de Trabalho", ShowDesktop);
        return menu;
    }
    internal static void ShowDesktop() => ShellAction("MinimizeAll");
    private static void ShellAction(string action)
    {
        object? shell = null;
        try
        {
            var type = Type.GetTypeFromProgID("Shell.Application");
            if (type is null) return;
            shell = Activator.CreateInstance(type);
            if (shell is null) return;
            if (action == "FileRun") ((dynamic)shell).FileRun();
            else if (action == "MinimizeAll") ((dynamic)shell).MinimizeAll();
        }
        catch (Exception error) when (error is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { MessageBox.Show("Não foi possível abrir este recurso do Windows: " + error.Message, "Think Wind"); }
        finally { if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell); }
    }
}
