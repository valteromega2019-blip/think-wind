using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;
using OrbyLauncher.Services;

namespace OrbyLauncher.Native;

// A fixed-purpose, same-user/session bridge. The only input is enable/disable;
// no executable, arbitrary window message or command is accepted by the helper.
// Reuses the existing Win-key implementation, never installs a second UI hook.
internal sealed class ShellInputBridge : IDisposable
{
    internal const string Switch = "--shell-input-bridge";
    private readonly Dispatcher dispatcher;
    private readonly Action open, changed;
    private readonly CancellationTokenSource stop = new();
    private readonly SemaphoreSlim writes = new(1, 1);
    private NamedPipeServerStream? pipe;
    private bool enabled, connecting, disposed;
    private DateTime retryAfter;
    private readonly DispatcherTimer retry = new() { Interval = TimeSpan.FromSeconds(15) };
    internal bool Active { get; private set; }
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint process);
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint process);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint process);
    private static string PipeName(string nonce) => "OrbyLauncher.ShellInput.v1." + WindowActionSetup.UserSid + "." + nonce;
    internal ShellInputBridge(Dispatcher ui, Action showStart, Action stateChanged)
    { dispatcher = ui; open = showStart; changed = stateChanged; retry.Tick += (_, _) => Configure(enabled); }
    internal void Configure(bool intercept)
    {
        bool different = enabled != intercept; enabled = intercept;
        if (intercept && !disposed) retry.Start(); else retry.Stop();
        if (!intercept && Active) { pipe?.Dispose(); return; }
        if (Active) { if (different) _ = SendState(); return; }
        if (!intercept || connecting || disposed || DateTime.UtcNow < retryAfter || !WindowActionSetup.IsConfigured) return;
        _ = Connect();
    }
    private async Task Connect()
    {
        connecting = true;
        try
        {
            using var self = Process.GetCurrentProcess();
            string nonce = Guid.NewGuid().ToString("N");
            var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WindowActionSetup.UserSid), PipeAccessRights.FullControl, AccessControlType.Allow));
            using var server = NamedPipeServerStreamAcl.Create(PipeName(nonce), PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 64, 64, security);
            pipe = server;
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(stop.Token); startup.CancelAfter(TimeSpan.FromSeconds(12));
            var connection = server.WaitForConnectionAsync(startup.Token);
            var request = self.Id + "|" + self.StartTime.ToUniversalTime().Ticks + "|" + nonce;
            await Task.Run(() => WindowActionSetup.Start(Convert.ToBase64String(Encoding.UTF8.GetBytes(request)), shell: true), stop.Token);
            await connection;
            if (!GetNamedPipeClientProcessId(server.SafePipeHandle, out uint helper) || !TrustedHelper(helper)) throw new IOException("Auxiliar não reconhecido.");
            var data = new byte[1]; await server.ReadExactlyAsync(data, startup.Token);
            if (data[0] != 1) throw new IOException("Resposta inválida.");
            Active = true; changed(); // UI removes its hook before enabling helper.
            await SendState();
            while (!stop.IsCancellationRequested)
            {
                await server.ReadExactlyAsync(data, stop.Token);
                if (data[0] == 2 && enabled) open();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or COMException) { }
        finally
        {
            pipe = null; Active = false; connecting = false; retryAfter = DateTime.UtcNow.AddSeconds(15);
            if (!disposed && !dispatcher.HasShutdownStarted) changed();
        }
    }
    private async Task SendState()
    {
        try
        {
            await writes.WaitAsync(stop.Token);
            try { if (pipe?.IsConnected == true) await pipe.WriteAsync(new byte[] { enabled ? (byte)1 : (byte)0 }, stop.Token); }
            finally { writes.Release(); }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or InvalidOperationException) { }
    }
    private static bool TrustedHelper(uint id)
    {
        using var helper = Process.GetProcessById((int)id); using var self = Process.GetCurrentProcess();
        if (helper.SessionId != self.SessionId) return false;
        nint handle = OpenProcess(0x1000, false, id); if (handle == 0) return false;
        try
        {
            var name = new StringBuilder(32768); uint size = (uint)name.Capacity;
            return QueryFullProcessImageName(handle, 0, name, ref size)
                && Path.GetFullPath(name.ToString()).Equals(Path.GetFullPath(WindowActionSetup.Executable), StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseHandle(handle); }
    }
    internal static bool Run(string[] args)
    {
        if (args.Length == 0 || args[0] != Switch) return false;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            if (args.Length != 2 || args[1].Length > 512 || !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)
                || !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.GetFullPath(WindowActionSetup.Executable), StringComparison.OrdinalIgnoreCase)) return true;
            var fields = Encoding.UTF8.GetString(Convert.FromBase64String(args[1])).Split('|');
            if (fields.Length != 3 || !int.TryParse(fields[0], out int pid) || !long.TryParse(fields[1], out long ticks) || !Guid.TryParseExact(fields[2], "N", out _)) return true;
            using var parent = Process.GetProcessById(pid); using var self = Process.GetCurrentProcess();
            if (parent.SessionId != self.SessionId || parent.StartTime.ToUniversalTime().Ticks != ticks) return true;
            using var client = new NamedPipeClientStream(".", PipeName(fields[2]), PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
            client.Connect(10000);
            if (!GetNamedPipeServerProcessId(client.SafePipeHandle, out uint server) || server != pid) return true;
            // Pipe ACL is restricted to the configuring user's SID. Verify the
            // parent token too; never trust just a supplied PID/session.
            if (!AuthorizedWindowAction.IsSameUser(parent, identity.User)) return true;
            client.WriteByte(1);
            var ui = Dispatcher.CurrentDispatcher;
            LauncherWindowsKey? key = null;
            using var life = new CancellationTokenSource();
            using var writeGate = new SemaphoreSlim(1, 1);
            async void Notify()
            {
                if (life.IsCancellationRequested) return;
                try
                {
                    await writeGate.WaitAsync(life.Token);
                    try { AllowSetForegroundWindow((uint)pid); await client.WriteAsync(new byte[] { 2 }, life.Token); }
                    finally { writeGate.Release(); }
                }
                catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException)
                { if (!ui.HasShutdownStarted) ui.BeginInvokeShutdown(DispatcherPriority.Normal); }
            }
            async Task Read()
            {
                try
                {
                    var value = new byte[1];
                    while (!life.IsCancellationRequested)
                    {
                        await client.ReadExactlyAsync(value, life.Token).ConfigureAwait(false);
                        if (value[0] > 1) throw new IOException("Estado inválido.");
                        bool on = value[0] == 1;
                        await ui.InvokeAsync(() => { if (on) key ??= new LauncherWindowsKey(ui, Notify); else { key?.Dispose(); key = null; } });
                    }
                }
                catch (Exception e) when (e is IOException or OperationCanceledException or InvalidOperationException) { }
                finally { if (!ui.HasShutdownStarted) ui.BeginInvokeShutdown(DispatcherPriority.Normal); }
            }
            var parentTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            parentTimer.Tick += (_, _) => { if (parent.HasExited || (key is not null && !key.IsActive)) ui.BeginInvokeShutdown(DispatcherPriority.Normal); };
            parentTimer.Start(); _ = Read();
            try { Dispatcher.Run(); }
            finally { parentTimer.Stop(); key?.Dispose(); life.Cancel(); }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception) { }
        return true;
    }
    public void Dispose() { disposed = true; enabled = false; retry.Stop(); stop.Cancel(); pipe?.Dispose(); }
}
