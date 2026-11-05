using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;
using Microsoft.Win32;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

// The visible UI uses the signed-in user's ordinary interactive token, never
// the elevated sensor task. No password, service, shell replacement or global
// StartupDelayInMSec change is needed.
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{B7408A21-58E2-4E3A-A842-84EF85965350}_is1";
    private static string UserSid { get { using var user = WindowsIdentity.GetCurrent(); return user.User!.Value; } }
    private static string TaskName => "OrbyLauncher-Startup-" + UserSid;
    private static bool? cachedEnabled;
    private static long cacheUntil;

    internal static bool IsInstalledExecutable
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(UninstallKey);
                return key?.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location)
                    && string.Equals(Path.GetFullPath(Path.Combine(location, "OrbyLauncher.exe")), Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception error) when (error is IOException or ArgumentException or NotSupportedException
                or UnauthorizedAccessException or System.Security.SecurityException) { return false; }
        }
    }

    internal static bool IsEnabled
    {
        get
        {
            if (cachedEnabled is { } cached && Environment.TickCount64 < cacheUntil) return cached;
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            bool enabled = key?.GetValue("OrbyLauncher") is string;
            try { enabled |= WithScheduler(folder =>
            {
                object? task = null;
                try { task = ((dynamic)folder).GetTask(TaskName); return (bool)((dynamic)task).Enabled; }
                catch (Exception error) when (MissingTask(error)) { return false; }
                finally { CoreAudio.Release(task); }
            }); }
            catch (Exception error) when (SchedulerUnavailable(error)) { }
            Remember(enabled); return enabled;
        }
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string executable = Environment.ProcessPath ?? throw new IOException("Executável de inicialização não encontrado.");
            bool scheduled;
            try
            {
                scheduled = WithScheduler(folder =>
                {
                    string sid = UserSid;
                    object? registered = null;
                    try
                    {
                        registered = ((dynamic)folder).RegisterTask(TaskName, Definition(executable, sid), 6, sid, null, 3, null);
                        return registered is not null;
                    }
                    finally { CoreAudio.Release(registered); }
                });
            }
            catch (Exception error) when (SchedulerUnavailable(error)) { scheduled = false; }
            if (scheduled) key.DeleteValue("OrbyLauncher", false);
            else key.SetValue("OrbyLauncher", "\"" + executable + "\" --startup");
        }
        else
        {
            try { WithScheduler(folder =>
            {
                try { ((dynamic)folder).DeleteTask(TaskName, 0); }
                catch (Exception error) when (MissingTask(error)) { }
                return true;
            }); }
            catch (Exception error) when (SchedulerUnavailable(error))
            { throw new IOException("Não foi possível remover a inicialização do Agendador de Tarefas.", error); }
            finally { key.DeleteValue("OrbyLauncher", false); cachedEnabled = null; }
        }
        Remember(enabled);
    }

    // Pure definition: startup policy can be validated without registering or
    // running a task, altering HKCU, or requesting elevated permissions.
    internal static string Definition(string executable, string sid)
    {
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, object value) => new(ns + name, value);
        return new XElement(ns + "Task", new XAttribute("version", "1.2"),
            E("RegistrationInfo", E("Description", "Inicia o Orby Launcher no logon, sem atraso adicional e sem elevação.")),
            E("Triggers", E("LogonTrigger", new object[] { E("Enabled", true), E("UserId", sid), E("Delay", "PT0S") })),
            E("Principals", new XElement(ns + "Principal", new XAttribute("id", "OrbyUser"),
                E("UserId", sid), E("LogonType", "InteractiveToken"), E("RunLevel", "LeastPrivilege"))),
            E("Settings", new object[] { E("MultipleInstancesPolicy", "IgnoreNew"),
                E("DisallowStartIfOnBatteries", false), E("StopIfGoingOnBatteries", false),
                E("AllowHardTerminate", false), E("StartWhenAvailable", true),
                E("Enabled", true), E("ExecutionTimeLimit", "PT0S"), E("Priority", 4) }),
            new XElement(ns + "Actions", new XAttribute("Context", "OrbyUser"),
                E("Exec", new object[] { E("Command", executable), E("Arguments", "--startup"), E("WorkingDirectory", Path.GetDirectoryName(executable)!) }))).ToString();
    }

    private static T WithScheduler<T>(Func<object, T> operation)
    {
        object? service = null, folder = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service") ?? throw new COMException("Agendador indisponível."));
            ((dynamic)service!).Connect(); folder = ((dynamic)service).GetFolder(@"\");
            return operation(folder!);
        }
        finally { CoreAudio.Release(folder); CoreAudio.Release(service); }
    }
    private static bool MissingTask(Exception error) => error is FileNotFoundException
        || error is COMException && error.HResult == unchecked((int)0x80070002);
    private static bool SchedulerUnavailable(Exception error) => error is COMException or IOException or UnauthorizedAccessException
        or InvalidOperationException or ArgumentException or NotSupportedException or System.Security.SecurityException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException;
    private static void Remember(bool enabled) { cachedEnabled = enabled; cacheUntil = Environment.TickCount64 + 3000; }

    internal static bool Run(string[] args, out int code)
    {
        code = 0;
        if (args.Length != 1 || args[0] != "--remove-startup") return false;
        try { SetEnabled(false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { code = 1; }
        return true;
    }
}
