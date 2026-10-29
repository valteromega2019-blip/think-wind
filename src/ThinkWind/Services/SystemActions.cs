using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace ThinkWind.Services;
internal static class SystemActions
{
    internal static void Run(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { MessageBox.Show("Não foi possível abrir: " + target + "\n" + e.Message, "Think Wind"); }
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
