using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using ThinkWind.Native;

namespace ThinkWind.Services;

internal static class AppUninstall
{
    private sealed record Candidate(string Name, string Command);
    internal static bool CanOffer(string target)
    {
        const string prefix = @"shell:AppsFolder\";
        if (target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return !target[prefix.Length..].Contains("://", StringComparison.Ordinal);
        return Path.GetExtension(target).ToLowerInvariant() is ".exe" or ".lnk" or ".appref-ms";
    }
    internal static async Task Open(string name, string target)
    {
        try
        {
            using var worker = new StaWorker("Think Wind — desinstalação");
            var identity = await worker.Run(() => ShellAppIdentity.ReadLaunch(target, new ShellIcons()));
            if (identity.AppId is string id && id.Contains('!'))
            {
                SystemActions.Run("ms-settings:appsfeatures-app?PFN=" + Uri.EscapeDataString(id.Split('!')[0]));
                return;
            }
            var candidates = await Task.Run(() => Find(name, identity.Path));
            if (candidates.Count != 1) { SystemActions.Run("ms-settings:appsfeatures"); return; }
            var app = candidates[0];
            if (MessageBox.Show("Abrir o desinstalador de " + app.Name + "?", "Think Wind", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (!SplitCommand(app.Command, out string executable, out string arguments))
            { SystemActions.Run("ms-settings:appsfeatures"); return; }
            Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = true });
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { MessageBox.Show("Não foi possível abrir a desinstalação. " + error.Message, "Think Wind"); }
    }
    private static List<Candidate> Find(string name, string target)
    {
        var result = new List<Candidate>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var list = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (list is null) continue;
            foreach (string keyName in list.GetSubKeyNames())
            {
                using var key = list.OpenSubKey(keyName);
                if (key?.GetValue("DisplayName") is not string display || key.GetValue("UninstallString") is not string command
                    || key.GetValue("SystemComponent") is int component && component != 0) continue;
                // Exact name or the original executable icon. Do not uninstall
                // another product just because it shares an installation folder.
                string icon = (key.GetValue("DisplayIcon") as string ?? "").Trim();
                int comma = icon.LastIndexOf(','); if (comma >= 0) icon = icon[..comma];
                icon = Environment.ExpandEnvironmentVariables(icon.Trim().Trim('"'));
                if (display.Equals(name, StringComparison.CurrentCultureIgnoreCase)
                    || Path.IsPathFullyQualified(target) && target.Equals(icon, StringComparison.OrdinalIgnoreCase))
                    result.Add(new(display, command));
            }
        }
        return result.DistinctBy(item => item.Command, StringComparer.OrdinalIgnoreCase).ToList();
    }
    private static bool SplitCommand(string command, out string executable, out string arguments)
    {
        command = Environment.ExpandEnvironmentVariables(command.Trim());
        executable = arguments = "";
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1); if (end <= 1) return false;
            executable = command[1..end]; arguments = command[(end + 1)..].TrimStart();
        }
        else
        {
            // Registered commands may contain an unquoted path with spaces.
            // Accept only an existing .exe prefix, never pass it to a shell.
            int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (end < 0) return false;
            executable = command[..(end + 4)]; arguments = command[(end + 4)..].TrimStart();
        }
        if (executable.Equals("msiexec.exe", StringComparison.OrdinalIgnoreCase)) executable = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
        return Path.IsPathFullyQualified(executable) && File.Exists(executable);
    }
}
