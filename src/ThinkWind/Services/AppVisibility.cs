using System;
using System.IO;

namespace OrbyLauncher.Services;
internal static class AppVisibility
{
    private static readonly string[] auxiliaryPrefixes = { "Uninstall", "Uninstaller", "Desinstalar", "Desinstalador", "Readme", "Leia-me", "Release notes", "Notas de versão",
        "Repair installation", "Reparar instalação", "Installation guide", "Guia de instalação", "License agreement", "Contrato de licença" };
    private static readonly string[] systemNames = { "PowerShell", "Windows PowerShell", "Terminal", "Windows Terminal", "Prompt de Comando", "Command Prompt", "Editor do Registro", "Registry Editor", "Gerenciamento do Computador", "Computer Management", "Ferramentas do Windows", "Windows Tools", "Ferramentas Administrativas", "Administrative Tools",
        "Control Panel", "Painel de Controle", "Disk Cleanup", "Limpeza de Disco", "Defragment and Optimize Drives", "Desfragmentar e Otimizar Unidades", "dfrgui",
        "Windows Memory Diagnostic", "Diagnóstico de Memória do Windows", "ODBC Data Sources", "Fontes de Dados ODBC", "System Configuration", "Configuração do Sistema",
        "Event Viewer", "Visualizador de Eventos", "Resource Monitor", "Monitor de Recursos", "Performance Monitor", "Monitor de Desempenho", "Task Scheduler", "Agendador de Tarefas",
        "Services", "Serviços", "Component Services", "Serviços de Componentes", "Recovery Drive", "Unidade de Recuperação", "Unidade de Recuperacao",
        "System Information", "Informações do Sistema", "Informacoes do Sistema", "Print Management", "Gerenciamento de Impressão", "Gerenciamento de Impressao",
        "Device Manager", "Gerenciador de Dispositivos", "Disk Management", "Gerenciamento de Disco", "Task Manager", "Gerenciador de Tarefas",
        "Windows Defender Firewall with Advanced Security", "Firewall do Windows Defender com Segurança Avançada", "Local Security Policy", "Política de Segurança Local",
        "Local Group Policy Editor", "Editor de Política de Grupo Local", "iSCSI Initiator", "Iniciador iSCSI", "Windows Installer" };
    private static readonly string[] systemFiles = { "powershell", "pwsh", "cmd", "regedit", "mmc", "wt", "control", "cleanmgr", "dfrgui", "mdsched", "odbcad32", "msconfig", "perfmon", "recoverydrive",
        "msinfo32", "taskmgr", "iscsicpl", "certmgr", "optionalfeatures", "systempropertiesadvanced", "systempropertiescomputername", "systempropertieshardware", "systempropertiesperformance", "systempropertiesremote",
        "msiexec", "compmgmtlauncher", "windowsfeatures", "werfault", "wermgr", "ctfmon", "runtimebroker", "dllhost", "conhost", "backgroundtaskhost" };
    private static readonly string[] systemPackages = { "Microsoft.WindowsTerminal", "Microsoft.AAD.BrokerPlugin", "Microsoft.AccountsControl", "Microsoft.AsyncTextService", "Microsoft.CredDialogHost",
        "Microsoft.LockApp", "Microsoft.Windows.ShellExperienceHost", "Microsoft.Windows.StartMenuExperienceHost", "Microsoft.Windows.CloudExperienceHost", "Microsoft.Windows.ContentDeliveryManager",
        "Microsoft.Windows.PeopleExperienceHost", "Microsoft.Windows.OOBENetwork" };

    internal static string LibraryKey(AppEntry app) => !string.IsNullOrWhiteSpace(app.AppId)
        ? "id:" + app.AppId.Trim()
        : "target:" + (app.ResolvedTarget ?? app.Target).Trim().Replace('/', '\\').TrimEnd('\\');

    // Hide administrative/development utilities from the drawer, without
    // uninstalling anything or suppressing everyday Windows applications.
    internal static bool IsAuxiliaryEntry(string name, string target)
    {
        string label = name.Trim();
        foreach (string prefix in auxiliaryPrefixes)
            if (label.Equals(prefix, StringComparison.CurrentCultureIgnoreCase)
                || label.StartsWith(prefix + " ", StringComparison.CurrentCultureIgnoreCase)
                || label.StartsWith(prefix + "-", StringComparison.CurrentCultureIgnoreCase)
                || label.StartsWith(prefix + " (", StringComparison.CurrentCultureIgnoreCase)) return true;
        string extension = Path.GetExtension(target);
        if (extension.Equals(".chm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cpl", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".msc", StringComparison.OrdinalIgnoreCase)) return true;
        if (label.EndsWith(" manual", StringComparison.CurrentCultureIgnoreCase)
            || label.EndsWith(" user guide", StringComparison.CurrentCultureIgnoreCase)
            || label.EndsWith(" help", StringComparison.CurrentCultureIgnoreCase)
            || label.EndsWith(" documentation", StringComparison.CurrentCultureIgnoreCase)
            || label.EndsWith(" documentação", StringComparison.CurrentCultureIgnoreCase)) return true;
        string file = Path.GetFileNameWithoutExtension(target);
        if (file.Equals("uninstall", StringComparison.OrdinalIgnoreCase)
            || file.Equals("uninstaller", StringComparison.OrdinalIgnoreCase)
            || file.Equals("unins000", StringComparison.OrdinalIgnoreCase)
            || file.Equals("unins001", StringComparison.OrdinalIgnoreCase)) return true;
        return IsSystemTool(new AppEntry(label, target, "", null));
    }
    internal static bool IsSystemTool(AppEntry app)
    {
        string name = app.Name.Trim();
        foreach (var text in systemNames)
            if (name.Equals(text, StringComparison.CurrentCultureIgnoreCase) || name.StartsWith(text + " ", StringComparison.CurrentCultureIgnoreCase)) return true;
        foreach (string package in systemPackages)
            if (app.AppId?.StartsWith(package + "_", StringComparison.OrdinalIgnoreCase) == true
                || app.AppId?.StartsWith(package + ".", StringComparison.OrdinalIgnoreCase) == true) return true;
        string target = app.ResolvedTarget ?? app.Target;
        string extension = Path.GetExtension(target);
        if (extension.Equals(".msc", StringComparison.OrdinalIgnoreCase) || extension.Equals(".cpl", StringComparison.OrdinalIgnoreCase)) return true;
        string file = Path.GetFileNameWithoutExtension(target);
        foreach (string tool in systemFiles)
            if (file.Equals(tool, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
