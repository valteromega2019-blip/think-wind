using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32.SafeHandles;

namespace ThinkWind.Services;

internal static class AdvancedSensorWorker
{
    private static void RecordStatus(string status)
    {
        try { File.WriteAllText(Path.Combine(AdvancedSensorSetup.DirectoryPath, "sensor-worker-status.log"), DateTime.UtcNow.ToString("O") + "\n" + status); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
    [DllImport("kernel32.dll")] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint process);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll")] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    private static bool SameUser(uint id, SecurityIdentifier user)
    {
        using var client = Process.GetProcessById((int)id);
        if (client.SessionId != Process.GetCurrentProcess().SessionId) return false;
        nint process = OpenProcess(0x1000, false, id);
        if (process == 0) return false;
        try
        {
            if (!OpenProcessToken(process, 8, out nint token)) return false;
            try { using var identity = new WindowsIdentity(token); return identity.User?.Equals(user) == true; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }
    internal static async Task<int> Run(Dispatcher dispatcher, bool captureFrames)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)
            || !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.GetFullPath(AdvancedSensorSetup.Executable), StringComparison.OrdinalIgnoreCase)) return 1;
        RecordStatus("Coletor elevado iniciado; aguardando conexão. Modo: " + (captureFrames ? "frames" : "sensors"));
        var sid = identity.User!;
        var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        // Read-only pipe: the normal launcher never requests write access to
        // an elevated object. No impersonation or integrity-label changes.
        // The task creates no hardware monitor until a validated client arrives.
        while (true)
        {
            using var pipe = NamedPipeServerStreamAcl.Create(AdvancedSensorSetup.PipeName + (captureFrames ? ".frames" : ".sensors"), PipeDirection.Out, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.FirstPipeInstance, 4096, 65536, security);
            using var idle = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try { await pipe.WaitForConnectionAsync(idle.Token); }
            catch (OperationCanceledException) { return 0; }
            bool accepted = false;
            if (GetNamedPipeClientProcessId(pipe.SafePipeHandle, out uint clientId))
                accepted = SameUser(clientId, sid);
            if (!accepted) continue;
            RecordStatus("Cliente validado; inicializando leitura de hardware.");
            using var frames = new FrameMonitor();
            if (captureFrames) frames.Start();
            using var telemetry = new HardwareTelemetry(dispatcher, worker: true);
            var samples = Channel.CreateBounded<HardwareSample>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });
            telemetry.Changed += sample => samples.Writer.TryWrite(sample);
            telemetry.SetActive(pipe, true);
            bool firstSample = true;
            try
            {
                // Writes detect a disconnected client; the 15 s deadline also
                // ends the worker if a native sensor stops publishing samples.
                while (pipe.IsConnected)
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var reading = samples.Reader.ReadAsync(deadline.Token).AsTask();
                    var sample = await reading;
                    if (captureFrames) sample = sample with { Frames = frames.GetForegroundStats(), Status = sample.Status + (frames.Error is null ? "" : " FPS/ETW indisponível: " + frames.Error) };
                    if (firstSample)
                    {
                        firstSample = false;
                        RecordStatus("Primeira amostra: CPU=" + sample.Cpu.Name + "; temperatura=" + (sample.Cpu.Temperature?.ToString() ?? "indisponível") + "; " + sample.Status);
                    }
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(sample);
                    await pipe.WriteAsync(BitConverter.GetBytes(bytes.Length), deadline.Token);
                    await pipe.WriteAsync(bytes, deadline.Token);
                    await pipe.FlushAsync(deadline.Token);
                }
            }
            catch (OperationCanceledException) { RecordStatus("Tempo esgotado aguardando ou enviando amostra de hardware."); }
            catch (Exception error) when (error is IOException or ObjectDisposedException) { }
            // Exit after a client session; a later demand starts the task again.
            // This also guarantees native libraries are unloaded on disconnect.
            return 0;
        }
    }
}
