using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using ThinkWind.Native;

namespace ThinkWind.Services;

// Approved installation of a fixed, read-only sensor action. The scheduled
// action must NEVER point into a user-writable installation or preview folder.
internal static class AdvancedSensorSetup
{
    internal static string UserSid { get { using var identity = WindowsIdentity.GetCurrent(); return identity.User!.Value; } }
    internal static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ThinkWind Sensors", UserSid);
    internal static string Executable => Path.Combine(DirectoryPath, "ThinkWind.exe");
    internal static string TaskName => "ThinkWind-Sensors-" + UserSid;
    internal static string PipeName => "ThinkWind.Sensors.v3." + UserSid;
    private static string Marker => Path.Combine(DirectoryPath, "sensor-task-v3.enabled");
    internal static bool IsConfigured
    {
        get
        {
            try { return File.Exists(Executable) && File.Exists(Marker) && File.ReadAllText(Marker) == AppBrand.Version; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
    internal static async Task<bool> Configure(bool remove = false)
    {
        try
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            info.ArgumentList.Add(remove ? "--remove-sensor-task" : "--configure-sensor-task");
            info.ArgumentList.Add(UserSid);
            using var process = Process.Start(info);
            if (process is null) return false;
            await process.WaitForExitAsync();
            if (process.ExitCode == 0) return true;
            if (process.ExitCode != 10)
                MessageBox.Show("A configuração do coletor terminou antes de concluir. Código: " + process.ExitCode + ". Tente novamente pelo botão Autorizar sensores.", "Think Wind");
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223) { }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        { MessageBox.Show("Configuração dos sensores: " + error.Message, "Think Wind"); }
        return false;
    }
    internal static bool RunSetup(string[] args, out int exitCode)
    {
        exitCode = 1;
        if (args.Length == 0 || args[0] is not ("--configure-sensor-task" or "--remove-sensor-task")) return false;
        object? service = null, folder = null, definition = null, principal = null, settings = null, triggers = null, trigger = null, actions = null, action = null, registered = null;
        string stage = "Verificação da autorização";
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (args.Length != 2) throw new InvalidOperationException("Argumentos de configuração inválidos.");
            if (args[1] != UserSid) throw new InvalidOperationException("A confirmação do UAC usou outra conta. Use a conta administradora que iniciou o Think Wind. Não é necessário elevar a interface inteira.");
            if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) throw new UnauthorizedAccessException("A configuração não recebeu a autorização administrativa do Windows.");
            stage = "Conexão com o Agendador de Tarefas";
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            ((dynamic)service!).Connect(); folder = ((dynamic)service).GetFolder(@"\");
            if (args[0] == "--remove-sensor-task")
            {
                registered = FindTask(folder, TaskName);
                if (registered is not null) { ((dynamic)registered).Stop(0); ((dynamic)folder).DeleteTask(TaskName, 0); }
                if (File.Exists(Marker)) File.Delete(Marker);
                // Leave the protected payload for a later authorized update;
                // removing the task revokes unattended privileged execution.
                exitCode = 0; return true;
            }
            // Stop only our own registered sensor action before replacing its
            // protected payload. Never terminate another hardware monitor.
            if (File.Exists(Marker)) File.Delete(Marker);
            stage = "Parada da tarefa anterior";
            registered = FindTask(folder, TaskName);
            if (registered is not null)
            {
                ((dynamic)registered).Stop(0);
                for (int wait = 0; wait < 25 && (int)((dynamic)registered).State == 4; wait++) System.Threading.Thread.Sleep(100);
                if ((int)((dynamic)registered).State == 4) throw new IOException("Coletor ainda está em uso.");
            }
            CoreAudio.Release(registered); registered = null;
            stage = "Cópia protegida do coletor em Program Files";
            ProtectAndCopy();
            stage = "Registro da tarefa de sensores";
            definition = ((dynamic)service).NewTask(0);
            principal = ((dynamic)definition).Principal;
            ((dynamic)principal).UserId = UserSid; ((dynamic)principal).LogonType = 3; ((dynamic)principal).RunLevel = 1;
            settings = ((dynamic)definition).Settings;
            ((dynamic)settings).Hidden = true; ((dynamic)settings).MultipleInstances = 2;
            ((dynamic)settings).DisallowStartIfOnBatteries = false; ((dynamic)settings).StopIfGoingOnBatteries = false;
            ((dynamic)settings).ExecutionTimeLimit = "PT0S";
            triggers = ((dynamic)definition).Triggers; trigger = ((dynamic)triggers).Create(9);
            ((dynamic)trigger).UserId = UserSid;
            actions = ((dynamic)definition).Actions; action = ((dynamic)actions).Create(0);
            ((dynamic)action).Path = Executable; ((dynamic)action).WorkingDirectory = DirectoryPath;
            ((dynamic)action).Arguments = "--sensor-worker \"$(Arg0)\"";
            string security = "O:BAG:BAD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;" + UserSid + ")";
            // DONT_ADD_PRINCIPAL_ACE prevents Scheduler from adding a broader
            // user ACE: normal-integrity clients may read/run, never edit it.
            registered = ((dynamic)folder).RegisterTaskDefinition(TaskName, definition, 6 | 16, UserSid, null, 3, security);
            File.WriteAllText(Marker, AppBrand.Version);
            exitCode = 0;
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            MessageBox.Show(stage + "\n\n" + error.Message + "\n\nCódigo: 0x" + error.HResult.ToString("X8"), "Think Wind — Configuração de sensores", MessageBoxButton.OK, MessageBoxImage.Error);
            exitCode = 10;
        }
        finally
        {
            foreach (var item in new[] { registered, action, actions, trigger, triggers, settings, principal, definition, folder, service }) CoreAudio.Release(item);
        }
        return true;
    }
    private static object? FindTask(object folder, string name)
    {
        object? tasks = null;
        try
        {
            // A missing task is normal on first installation. Enumerate instead
            // of treating GetTask's translated COM/FileNotFound error as failure.
            tasks = ((dynamic)folder).GetTasks(1);
            int count = (int)((dynamic)tasks).Count;
            for (int index = 1; index <= count; index++)
            {
                object item = ((dynamic)tasks)[index];
                if (string.Equals((string)((dynamic)item).Name, name, StringComparison.OrdinalIgnoreCase)) return item;
                CoreAudio.Release(item);
            }
            return null;
        }
        finally { CoreAudio.Release(tasks); }
    }
    private static void ProtectAndCopy()
    {
        string parent = Path.GetDirectoryName(DirectoryPath)!;
        foreach (string path in new[] { parent, DirectoryPath })
        {
            var directory = Directory.CreateDirectory(path);
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Pasta de sensores não pode ser um link.");
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(admins);
            foreach (var sid in new[] { admins, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null) })
                security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.SetAccessControl(security);
        }
        // Flat .NET distribution only. No scripts, user settings, plugins or
        // external executable paths become scheduled elevated actions.
        foreach (string source in Directory.EnumerateFiles(AppContext.BaseDirectory))
        {
            if (!new[] { ".exe", ".dll", ".json" }.Contains(Path.GetExtension(source), StringComparer.OrdinalIgnoreCase)) continue;
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("Arquivo de sensores não pode ser um link.");
            string destination = Path.Combine(DirectoryPath, Path.GetFileName(source));
            if (File.Exists(destination) && (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0) throw new IOException("Destino inválido.");
            File.Copy(source, destination, true);
            // Do not retain an ACL/owner inherited from a previous payload.
            var security = new FileSecurity(); security.SetAccessRuleProtection(false, false);
            security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
            new FileInfo(destination).SetAccessControl(security);
        }
        if (!File.Exists(Executable)) throw new IOException("Executável do coletor não encontrado.");
    }
    internal static void StartTask(bool frames = false)
    {
        object? service = null, folder = null, task = null, running = null;
        try
        {
            service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!);
            ((dynamic)service!).Connect(); folder = ((dynamic)service).GetFolder(@"\");
            task = ((dynamic)folder).GetTask(TaskName);
            // Only the fixed sensor mode is substituted. The worker rejects
            // all arguments except exactly 'sensors' or 'frames'.
            running = ((dynamic)task).Run(new object[] { frames ? "frames" : "sensors" });
        }
        finally { foreach (var item in new[] { running, task, folder, service }) CoreAudio.Release(item); }
    }
}
