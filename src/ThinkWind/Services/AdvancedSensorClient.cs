using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace ThinkWind.Services;

internal sealed class AdvancedSensorClient
{
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint process);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint length);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    private NamedPipeClientStream? pipe;
    private DateTime retryAfter;
    private bool frameMode;
    internal string? LastError { get; private set; }
    internal async Task<HardwareSample?> Read(CancellationToken stop, bool frames = false)
    {
        if (pipe is not null && frameMode != frames) { Disconnect(); retryAfter = DateTime.UtcNow.AddSeconds(3); }
        if (!AdvancedSensorSetup.IsConfigured)
        { LastError = "Coletor administrativo: autorize ou atualize os sensores em Personalizar."; return null; }
        if (DateTime.UtcNow < retryAfter) return null;
        string stage = "iniciar a tarefa";
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            if (pipe is null)
            {
                AdvancedSensorSetup.StartTask(frames);
                stage = "conectar ao coletor";
                var next = new NamedPipeClientStream(".", AdvancedSensorSetup.PipeName + (frames ? ".frames" : ".sensors"), PipeDirection.In, PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);
                try
                {
                    await next.ConnectAsync(timeout.Token).ConfigureAwait(false);
                    if (!Trusted(next)) throw new IOException("Identidade do coletor não confere.");
                    frameMode = frames;
                    pipe = next;
                }
                catch { next.Dispose(); throw; }
            }
            byte[] lengthBytes = new byte[4];
            stage = "receber as leituras";
            var stream = pipe ?? throw new IOException("Coletor desconectado.");
            await stream.ReadExactlyAsync(lengthBytes, timeout.Token).ConfigureAwait(false);
            int length = BitConverter.ToInt32(lengthBytes);
            if (length is < 1 or > 65536) throw new IOException("Resposta do coletor inválida.");
            byte[] bytes = new byte[length];
            await stream.ReadExactlyAsync(bytes, timeout.Token).ConfigureAwait(false);
            var sample = JsonSerializer.Deserialize<HardwareSample>(bytes);
            if (sample is null || (DateTime.Now - sample.Time).Duration() > TimeSpan.FromSeconds(30)) throw new IOException("Leitura expirada.");
            LastError = null; return sample;
        }
        catch (OperationCanceledException) when (!stop.IsCancellationRequested)
        { LastError = "Coletor administrativo: tempo esgotado ao " + stage + "."; Disconnect(); retryAfter = DateTime.UtcNow.AddSeconds(15); return null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or COMException or System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException)
        { LastError = "Coletor administrativo: falha ao " + stage + " (0x" + error.HResult.ToString("X8") + ")."; Disconnect(); retryAfter = DateTime.UtcNow.AddSeconds(15); return null; }
    }
    private static bool Trusted(NamedPipeClientStream pipe)
    {
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint id)) return false;
        using var target = Process.GetProcessById((int)id);
        if (target.SessionId != Process.GetCurrentProcess().SessionId) return false;
        nint process = OpenProcess(0x1000, false, id);
        if (process == 0) return false;
        try
        {
            var path = new StringBuilder(32768); uint size = (uint)path.Capacity;
            // Authenticate the actual pipe server by its session and immutable,
            // SID-specific executable, as for the window-action helper. A medium
            // integrity client cannot reliably query an elevated process token.
            // The protected worker checks its own elevation before creating IPC
            // and validates the connecting user's SID and session independently.
            return QueryFullProcessImageName(process, 0, path, ref size)
                && Path.GetFullPath(path.ToString()).Equals(Path.GetFullPath(AdvancedSensorSetup.Executable), StringComparison.OrdinalIgnoreCase);
        }
        finally { CloseHandle(process); }
    }
    internal void Disconnect() { Interlocked.Exchange(ref pipe, null)?.Dispose(); }
    // Called only by the telemetry loop, never concurrently with Read.
    internal void ResetAuthorization() { Disconnect(); retryAfter = DateTime.MinValue; LastError = null; }
}
