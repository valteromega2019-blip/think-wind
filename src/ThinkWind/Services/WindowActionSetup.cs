using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using ThinkWind.Native;

namespace ThinkWind.Services;

internal static class WindowActionSetup
{
    internal static bool AutomaticAuthorizationEnabled { get; set; } = true;
    internal static string UserSid => AdvancedSensorSetup.UserSid;
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ThinkWind Window Actions", UserSid);
    internal static string Executable => Path.Combine(DirectoryPath, "ThinkWind.exe");
    internal static string TaskName => "ThinkWind-WindowActions-" + UserSid;
    private static string Marker => Path.Combine(DirectoryPath, "window-actions.enabled");
    internal static bool IsConfigured
    {
        get
        {
            try { return File.Exists(Executable) && File.Exists(Marker) && File.ReadAllText(Marker) == AppBrand.Version + "|action-v1"; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
    internal static async Task<bool> Configure(bool remove = false)
    {
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(remove ? "--remove-window-action-task" : "--configure-window-action-task");
            info.ArgumentList.Add(UserSid);
            using var process = Process.Start(info);
            if (process is null) return false;
            await process.WaitForExitAsync(); return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223) { return false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show("Não foi possível configurar o controle autorizado de janelas: " + error.Message, "Think Wind"); return false; }
    }
    internal static bool RunSetup(string[] args, out int exitCode)
    {
        exitCode = 1;
        if (args.Length == 0 || args[0] is not ("--configure-window-action-task" or "--remove-window-action-task")) return false;
        object? service = null, folder = null, previous = null, definition = null, principal = null, settings = null, actions = null, action = null, registered = null;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (args.Length != 2 || args[1] != UserSid || !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                throw new UnauthorizedAccessException("Autorize com a mesma conta administradora que usa o Think Wind.");
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            ((dynamic)service!).Connect(); folder = ((dynamic)service).GetFolder(@"\");
            previous = AdvancedSensorSetup.FindTask(folder, TaskName);
            if (previous is not null)
            {
                ((dynamic)previous).Stop(0);
                for (int wait = 0; wait < 25 && (int)((dynamic)previous).State == 4; wait++) System.Threading.Thread.Sleep(100);
                if ((int)((dynamic)previous).State == 4) throw new IOException("O auxiliar ainda está encerrando. Tente novamente.");
            }
            if (File.Exists(Marker)) File.Delete(Marker);
            if (args[0] == "--remove-window-action-task")
            { if (previous is not null) ((dynamic)folder).DeleteTask(TaskName, 0); exitCode = 0; return true; }
            AdvancedSensorSetup.ProtectAndCopy(DirectoryPath);
            definition = ((dynamic)service).NewTask(0);
            principal = ((dynamic)definition).Principal;
            ((dynamic)principal).UserId = UserSid; ((dynamic)principal).LogonType = 3; ((dynamic)principal).RunLevel = 1;
            settings = ((dynamic)definition).Settings;
            ((dynamic)settings).Hidden = true; ((dynamic)settings).MultipleInstances = 2;
            ((dynamic)settings).AllowDemandStart = true; ((dynamic)settings).ExecutionTimeLimit = "PT30S";
            ((dynamic)settings).DisallowStartIfOnBatteries = false; ((dynamic)settings).StopIfGoingOnBatteries = false;
            actions = ((dynamic)definition).Actions; action = ((dynamic)actions).Create(0);
            ((dynamic)action).Path = Executable; ((dynamic)action).WorkingDirectory = DirectoryPath;
            ((dynamic)action).Arguments = "--authorized-window-action \"$(Arg0)\"";
            string security = "O:BAG:BAD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;" + UserSid + ")";
            registered = ((dynamic)folder).RegisterTaskDefinition(TaskName, definition, 6 | 16, UserSid, null, 3, security);
            File.WriteAllText(Marker, AppBrand.Version + "|action-v1"); exitCode = 0;
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { MessageBox.Show("Autorização do controle de janelas: " + error.Message, "Think Wind"); }
        finally { foreach (var item in new[] { registered, action, actions, settings, principal, definition, previous, folder, service }) CoreAudio.Release(item); }
        return true;
    }
    internal static void Start(string request)
    {
        object? service = null, folder = null, task = null, running = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            ((dynamic)service!).Connect(); folder = ((dynamic)service).GetFolder(@"\");
            task = ((dynamic)folder).GetTask(TaskName); running = ((dynamic)task).Run(request);
        }
        finally { foreach (var item in new[] { running, task, folder, service }) CoreAudio.Release(item); }
    }
}
