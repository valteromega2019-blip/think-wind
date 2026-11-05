using System;
using System.IO;

namespace OrbyLauncher.Services;
internal static class AppVisibility
{
    // Hide administrative/development utilities from the drawer, without
    // uninstalling anything or suppressing everyday Windows applications.
    internal static bool IsAuxiliaryEntry(string name, string target)
    {
        string label = name.Trim();
        foreach (string prefix in new[] { "Uninstall", "Uninstaller", "Desinstalar", "Desinstalador", "Readme", "Leia-me", "Release notes", "Notas de versão" })
            if (label.Equals(prefix, StringComparison.CurrentCultureIgnoreCase)
                || label.StartsWith(prefix + " ", StringComparison.CurrentCultureIgnoreCase)
                || label.StartsWith(prefix + "-", StringComparison.CurrentCultureIgnoreCase)) return true;
        string extension = Path.GetExtension(target);
        if (extension.Equals(".chm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".htm", StringComparison.OrdinalIgnoreCase)) return true;
        if (label.EndsWith(" manual", StringComparison.CurrentCultureIgnoreCase)
            || label.EndsWith(" user guide", StringComparison.CurrentCultureIgnoreCase)
            || label.EndsWith(" help", StringComparison.CurrentCultureIgnoreCase)) return true;
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
        foreach (var text in new[] { "PowerShell", "Windows PowerShell", "Terminal", "Windows Terminal", "Prompt de Comando", "Command Prompt", "Editor do Registro", "Registry Editor", "Gerenciamento do Computador", "Computer Management", "Ferramentas do Windows", "Windows Tools", "Ferramentas Administrativas", "Administrative Tools",
            "Control Panel", "Painel de Controle", "Disk Cleanup", "Limpeza de Disco",
            "Defragment and Optimize Drives", "Desfragmentar e Otimizar Unidades", "dfrgui",
            "Windows Memory Diagnostic", "Diagnóstico de Memória do Windows",
            "ODBC Data Sources", "Fontes de Dados ODBC", "System Configuration", "Configuração do Sistema",
            "Event Viewer", "Visualizador de Eventos", "Resource Monitor", "Monitor de Recursos",
            "Performance Monitor", "Monitor de Desempenho", "Task Scheduler", "Agendador de Tarefas",
            "Services", "Serviços", "Component Services", "Serviços de Componentes", "Recovery Drive", "Unidade de Recuperação" })
            if (name.Equals(text, StringComparison.CurrentCultureIgnoreCase) || name.StartsWith(text + " ", StringComparison.CurrentCultureIgnoreCase)) return true;
        if (app.AppId?.Contains("Microsoft.WindowsTerminal", StringComparison.OrdinalIgnoreCase) == true) return true;
        string file = Path.GetFileNameWithoutExtension(app.Target);
        return file.Equals("powershell", StringComparison.OrdinalIgnoreCase) || file.Equals("pwsh", StringComparison.OrdinalIgnoreCase)
            || file.Equals("cmd", StringComparison.OrdinalIgnoreCase) || file.Equals("regedit", StringComparison.OrdinalIgnoreCase)
            || file.Equals("mmc", StringComparison.OrdinalIgnoreCase) || file.Equals("wt", StringComparison.OrdinalIgnoreCase)
            || file.Equals("control", StringComparison.OrdinalIgnoreCase) || file.Equals("cleanmgr", StringComparison.OrdinalIgnoreCase)
            || file.Equals("dfrgui", StringComparison.OrdinalIgnoreCase) || file.Equals("mdsched", StringComparison.OrdinalIgnoreCase)
            || file.Equals("odbcad32", StringComparison.OrdinalIgnoreCase) || file.Equals("msconfig", StringComparison.OrdinalIgnoreCase)
            || file.Equals("perfmon", StringComparison.OrdinalIgnoreCase) || file.Equals("recoverydrive", StringComparison.OrdinalIgnoreCase);
    }
}
