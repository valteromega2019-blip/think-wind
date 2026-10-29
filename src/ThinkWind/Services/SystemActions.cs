using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ThinkWind.Services;
internal static class SystemActions
{
    internal static void Run(string target)
    { Launch(target); }
    internal static bool Launch(string target)
    {
        try
        {
            if (target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; info.ArgumentList.Add(target); Process.Start(info);
            }
            else Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { MessageBox.Show("Não foi possível abrir: " + target + "\n" + e.Message, "Think Wind"); return false; }
    }
    internal static void Power(string action)
    {
        if (action is not ("Desligar" or "Reiniciar" or "Suspender")) return;
        if (MessageBox.Show(action + " o computador agora?", "Think Wind", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (action == "Suspender")
        {
            if (!System.Windows.Forms.Application.SetSuspendState(System.Windows.Forms.PowerState.Suspend, false, false)) MessageBox.Show("O Windows não permitiu suspender o computador.", "Think Wind");
            return;
        }
        var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe")) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(action == "Reiniciar" ? "/r" : "/s"); info.ArgumentList.Add("/t"); info.ArgumentList.Add("0");
        try { Process.Start(info); } catch (System.ComponentModel.Win32Exception error) { MessageBox.Show(error.Message, "Think Wind"); }
    }
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static bool StartupEnabled()
    { using var key = Registry.CurrentUser.OpenSubKey(RunKey); return key?.GetValue("ThinkWind") is string; }
    internal static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("ThinkWind", "\"" + Environment.ProcessPath + "\"");
        else key.DeleteValue("ThinkWind", false);
    }
}
