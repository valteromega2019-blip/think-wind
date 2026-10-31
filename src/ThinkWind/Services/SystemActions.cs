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
    internal static bool CanRunAsAdministrator(string target)
    {
        target = Environment.ExpandEnvironmentVariables(target.Trim().Trim('"'));
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || target.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)) return false;
        string extension = Path.GetExtension(target);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".com", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase);
    }
    internal static void RunAsAdministrator(string target) => Launch(target, true);
    internal static bool Launch(string target, bool administrator = false)
    {
        try
        {
            target = Environment.ExpandEnvironmentVariables(target.Trim());
            if (target.Length >= 2 && target[0] == '"' && target[^1] == '"') target = target[1..^1];
            if (administrator && !CanRunAsAdministrator(target)) return false;
            if (target.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                var info = new ProcessStartInfo("explorer.exe") { UseShellExecute = true }; info.ArgumentList.Add(target); Process.Start(info);
            }
            else
            {
                var info = new ProcessStartInfo(target) { UseShellExecute = true };
                if (administrator) info.Verb = "runas";
                // Executables may depend on resources relative to their own
                // directory, rather than the launcher's current directory.
                if (Path.IsPathFullyQualified(target) && File.Exists(target)
                    && Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    info.WorkingDirectory = Path.GetDirectoryName(target)!;
                Process.Start(info);
            }
            return true;
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223)
        { return false; } // User cancelled the Windows elevation prompt.
        catch (System.ComponentModel.Win32Exception error) when (!administrator && error.NativeErrorCode == 740 && CanRunAsAdministrator(target))
        { return Launch(target, true); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        { MessageBox.Show("Não foi possível abrir: " + target + "\n" + e.Message, "Think Wind"); return false; }
    }
    internal static void Power(string action, bool confirm = true)
    {
        if (action is not ("Desligar" or "Reiniciar" or "Suspender")) return;
        if (confirm && MessageBox.Show(action + " o computador agora?", "Think Wind", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
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
    internal static void ApplyStartupDefault(SettingsStore store, bool installation = false)
    {
        if (!installation && store.Value.StartupDefaultsInitialized) return;
        SetStartup(store.Value.StartWithWindows);
        store.Value.StartupDefaultsInitialized = true;
        store.Save();
    }
}
