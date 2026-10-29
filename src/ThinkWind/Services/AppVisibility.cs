using System;
using System.IO;

namespace ThinkWind.Services;
internal static class AppVisibility
{
    // Hide administrative/development utilities from the drawer, without
    // uninstalling anything or suppressing everyday Windows applications.
    internal static bool IsSystemTool(AppEntry app)
    {
        string name = app.Name.Trim();
        foreach (var text in new[] { "PowerShell", "Windows PowerShell", "Terminal", "Windows Terminal", "Prompt de Comando", "Command Prompt", "Editor do Registro", "Registry Editor", "Gerenciamento do Computador", "Computer Management", "Ferramentas do Windows", "Windows Tools", "Ferramentas Administrativas", "Administrative Tools" })
            if (name.Equals(text, StringComparison.CurrentCultureIgnoreCase) || name.StartsWith(text + " ", StringComparison.CurrentCultureIgnoreCase)) return true;
        if (app.AppId?.Contains("Microsoft.WindowsTerminal", StringComparison.OrdinalIgnoreCase) == true) return true;
        string file = Path.GetFileNameWithoutExtension(app.Target);
        return file.Equals("powershell", StringComparison.OrdinalIgnoreCase) || file.Equals("pwsh", StringComparison.OrdinalIgnoreCase)
            || file.Equals("cmd", StringComparison.OrdinalIgnoreCase) || file.Equals("regedit", StringComparison.OrdinalIgnoreCase)
            || file.Equals("mmc", StringComparison.OrdinalIgnoreCase) || file.Equals("wt", StringComparison.OrdinalIgnoreCase);
    }
}
