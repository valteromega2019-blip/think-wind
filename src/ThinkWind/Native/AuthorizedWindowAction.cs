using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32.SafeHandles;
using ThinkWind.Services;

namespace ThinkWind.Native;

// Per-click, bounded helper. Payload selects one fixed window operation, never
// a command/executable. The pipe carries only hello/ack/result, not commands.
internal static class AuthorizedWindowAction
{
    internal const string Switch = "--authorized-window-action";
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static bool authorizationDeclined;
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint process);
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint process);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint process);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll")] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    private static string PipeName(string nonce) => "ThinkWind.WindowAction.v1." + WindowActionSetup.UserSid + "." + nonce;

    internal static async Task<bool> Request(nint window, bool close, bool minimize, Window? owner)
    {
        if (!await gate.WaitAsync(0)) return false;
        try
        {
            if (!WindowActionSetup.IsConfigured)
            {
                if (authorizationDeclined || !WindowActionSetup.AutomaticAuthorizationEnabled) return false;
                if (!await WindowActionSetup.Configure()) { authorizationDeclined = true; return false; }
            }
            if (!Desktop.WindowExists(window)) return false;
            GetWindowThreadProcessId(window, out uint id);
            using var target = Process.GetProcessById((int)id);
            using var self = Process.GetCurrentProcess();
            string nonce = Guid.NewGuid().ToString("N");
            string action = close ? "close" : minimize ? "minimize" : "activate";
            string payload = string.Join("|", action, window.ToInt64().ToString(CultureInfo.InvariantCulture), id.ToString(CultureInfo.InvariantCulture),
                target.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), self.Id.ToString(CultureInfo.InvariantCulture),
                self.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture), nonce);
            var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WindowActionSetup.UserSid), PipeAccessRights.FullControl, AccessControlType.Allow));
            using var pipe = NamedPipeServerStreamAcl.Create(PipeName(nonce), PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 64, 64, security);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var connection = pipe.WaitForConnectionAsync(timeout.Token);
            if (owner?.IsVisible == true) owner.Activate();
            await Task.Run(() => WindowActionSetup.Start(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))));
            await connection;
            if (!GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint helper) || !TrustedHelper(helper)) throw new IOException("Identidade do auxiliar não confere.");
            byte[] message = new byte[1];
            await pipe.ReadExactlyAsync(message, timeout.Token);
            if (message[0] != 1) throw new IOException("Resposta de autorização inválida.");
            AllowSetForegroundWindow(helper);
            await pipe.WriteAsync(new byte[] { 0xA5 }, timeout.Token); await pipe.FlushAsync(timeout.Token);
            await pipe.ReadExactlyAsync(message, timeout.Token);
            if (message[0] == 0) return true;
            MessageBox.Show("O Windows não permitiu concluir a operação nesta janela pelo auxiliar autorizado.", "Think Wind");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException
            or InvalidOperationException or System.ComponentModel.Win32Exception or COMException)
        { MessageBox.Show("Controle automático de janelas: " + error.Message + "\nVocê pode atualizar a autorização em Personalizar > Sistema.", "Think Wind"); }
        finally { gate.Release(); }
        return false;
    }
    // The protected executable is not writable by normal users and checks its
    // elevated role before connecting. No token query against a high process is
    // needed from the medium UI (which Windows can deny).
    private static bool TrustedHelper(uint id)
    {
        using var process = Process.GetProcessById((int)id);
        using var self = Process.GetCurrentProcess();
        if (process.SessionId != self.SessionId) return false;
        nint handle = OpenProcess(0x1000, false, id);
        if (handle == 0) return false;
        try
        {
            var path = new StringBuilder(32768); uint size = (uint)path.Capacity;
            return QueryFullProcessImageName(handle, 0, path, ref size)
                && Path.GetFullPath(path.ToString()).Equals(Path.GetFullPath(WindowActionSetup.Executable), StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseHandle(handle); }
    }
    private static bool SameUser(Process process, SecurityIdentifier? user)
    {
        nint handle = OpenProcess(0x1000, false, (uint)process.Id);
        if (handle == 0) return false;
        try
        {
            if (!OpenProcessToken(handle, 8, out nint token)) return false;
            try { using var identity = new WindowsIdentity(token); return user is not null && identity.User?.Equals(user) == true; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(handle); }
    }
    internal static bool Run(string[] args, out int result)
    {
        result = 1;
        if (args.Length == 0 || args[0] != Switch) return false;
        if (args.Length != 2 || args[1].Length > 1024) return true;
        try { result = Execute(args[1]).GetAwaiter().GetResult(); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or ArgumentException
            or InvalidOperationException or FormatException or System.ComponentModel.Win32Exception) { }
        return true;
    }
    private static async Task<int> Execute(string encoded)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)
            || !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.GetFullPath(WindowActionSetup.Executable), StringComparison.OrdinalIgnoreCase)) return 1;
        var fields = Encoding.UTF8.GetString(Convert.FromBase64String(encoded)).Split('|');
        if (fields.Length != 7 || fields[0] is not ("activate" or "close" or "minimize")
            || !long.TryParse(fields[1], out long window) || !int.TryParse(fields[2], out int id)
            || !long.TryParse(fields[3], out long started) || !int.TryParse(fields[4], out int caller)
            || !long.TryParse(fields[5], out long callerStarted) || !Guid.TryParseExact(fields[6], "N", out _)
            || id <= 0 || caller <= 0 || started <= 0 || callerStarted <= 0) return 1;
        using var target = Process.GetProcessById(id); using var requester = Process.GetProcessById(caller); using var self = Process.GetCurrentProcess();
        GetWindowThreadProcessId((nint)window, out uint actual);
        if (actual != id || target.StartTime.ToUniversalTime().Ticks != started || requester.StartTime.ToUniversalTime().Ticks != callerStarted
            || target.SessionId != self.SessionId || requester.SessionId != self.SessionId
            || !SameUser(target, identity.User) || !SameUser(requester, identity.User)) return 1;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var pipe = new NamedPipeClientStream(".", PipeName(fields[6]), PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(timeout.Token).ConfigureAwait(false);
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint server) || server != caller) return 1;
        await pipe.WriteAsync(new byte[] { 1 }, timeout.Token).ConfigureAwait(false); await pipe.FlushAsync(timeout.Token).ConfigureAwait(false);
        byte[] ack = new byte[1]; await pipe.ReadExactlyAsync(ack, timeout.Token).ConfigureAwait(false);
        if (ack[0] != 0xA5) return 1;
        // Revalidate in the existing one-operation executor immediately before
        // sending WM_CLOSE/minimize/activate; no executable or raw message input.
        ElevatedWindowAction.Run(new[] { "--elevated-window-action", fields[0], fields[1], fields[2], fields[3] }, out int code);
        await pipe.WriteAsync(new byte[] { code == 0 ? (byte)0 : (byte)1 }, timeout.Token).ConfigureAwait(false);
        await pipe.FlushAsync(timeout.Token).ConfigureAwait(false); return code;
    }
}
