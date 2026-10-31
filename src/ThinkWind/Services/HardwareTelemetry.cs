using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using LibreHardwareMonitor.Hardware;
using Microsoft.Win32;
using ThinkWind.Native;

namespace ThinkWind.Services;

internal sealed record DeviceReading(string Name, double? Temperature = null, double? Load = null, double? Clock = null, double? Power = null, double? MemoryUsed = null, double? MemoryTotal = null);
internal sealed record GpuReading(string Id, DeviceReading Device, bool? Integrated, ulong DedicatedMemory, Dictionary<string, double?> Extra);
internal sealed record HardwareSample(DateTime Time, DeviceReading Cpu, DeviceReading Gpu, double? RamUsed, double? RamTotal, double? DiskFree, double? DiskTotal, string Disk, string Status,
    Dictionary<string, double?>? Extra = null, FrameStats? Frames = null, GpuReading[]? Gpus = null);

// Independent collector: no IPC, files or processes from Think Monitor.
// One background loop, activated only by a visible panel or opt-in overlay.
internal sealed class HardwareTelemetry : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Memory
    { internal uint Length, Load; internal ulong Total, Available, PageTotal, PageAvailable, VirtualTotal, VirtualAvailable, Extended; }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref Memory memory);
    [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    private readonly Dispatcher dispatcher;
    private readonly HashSet<object> consumers = new();
    private readonly SemaphoreSlim wake = new(0, 1);
    private readonly CancellationTokenSource stop = new();
    private readonly object sync = new();
    private bool disposed;
    private readonly AdvancedSensorClient? advanced;
    private readonly bool localFrameAccess;
    private volatile string preferredGpu = "";
    private IReadOnlyList<GpuAdapterInfo> adapterInfo = Array.Empty<GpuAdapterInfo>();
    internal volatile bool CaptureFrames;
    private long lastIdle, lastTotal;
    private int authorizationChanged;
    internal void RefreshAuthorizedSensors()
    {
        Interlocked.Exchange(ref authorizationChanged, 1);
        lock (sync) { if (!disposed && wake.CurrentCount == 0) wake.Release(); }
    }
    internal HardwareSample? Latest { get; private set; }
    internal System.Collections.Generic.Queue<HardwareSample> History { get; } = new();
    internal event Action<HardwareSample>? Changed;
    internal void SelectGpu(string id)
    {
        if (preferredGpu == id) return;
        preferredGpu = id;
        // UI-owned history must not mix samples from different adapters.
        History.Clear();
        if (Latest is { } sample) { Latest = SelectGpuSample(sample); Changed?.Invoke(Latest); }
    }
    private HardwareSample SelectGpuSample(HardwareSample sample)
    {
        if (sample.Gpus is not { Length: > 0 } gpus) return sample;
        var gpu = gpus.FirstOrDefault(gpu => gpu.Id == preferredGpu) ?? gpus
            .OrderBy(gpu => gpu.Integrated == false ? 0 : gpu.Integrated is null ? 1 : 2)
            .ThenByDescending(gpu => gpu.DedicatedMemory).First();
        var extra = sample.Extra is null ? new Dictionary<string, double?>() : new Dictionary<string, double?>(sample.Extra);
        foreach (var pair in gpu.Extra) extra[pair.Key] = pair.Value;
        return sample with { Gpu = gpu.Device, Extra = extra };
    }
    internal HardwareTelemetry(Dispatcher ui, bool worker = false)
    {
        dispatcher = ui;
        using var identity = WindowsIdentity.GetCurrent();
        localFrameAccess = !worker && new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        if (!worker && !localFrameAccess) advanced = new AdvancedSensorClient();
        _ = Task.Run(Run);
    }
    internal void SetActive(object consumer, bool active)
    {
        lock (sync)
        {
            if (disposed) return;
            if (active) consumers.Add(consumer); else consumers.Remove(consumer);
            if (wake.CurrentCount == 0) wake.Release();
        }
    }
    private bool Needed { get { lock (sync) return !disposed && consumers.Count > 0; } }
    private async Task Run()
    {
        Computer? computer = null;
        using var localFrames = new FrameMonitor();
        DateTime retryFrames = DateTime.MinValue;
        bool attempted = false;
        string sensorStatus = "";
        try
        {
            while (!stop.IsCancellationRequested)
            {
                if (!Needed)
                {
                    localFrames.Stop();
                    try { computer?.Close(); } catch (Exception error) when (error is not OutOfMemoryException) { }
                    computer = null; attempted = false;
                    advanced?.Disconnect();
                    lastIdle = lastTotal = 0;
                    await wake.WaitAsync(stop.Token).ConfigureAwait(false);
                    continue;
                }
                // The setup may have completed while a local-only read or an
                // earlier connection backoff was active. Retry immediately.
                if (Interlocked.Exchange(ref authorizationChanged, 0) != 0) advanced?.ResetAuthorization();
                HardwareSample? remote = advanced is null ? null : await advanced.Read(stop.Token, CaptureFrames).ConfigureAwait(false);
                if (localFrameAccess && CaptureFrames)
                {
                    if (!localFrames.Running && DateTime.UtcNow >= retryFrames)
                    { localFrames.Start(); retryFrames = DateTime.UtcNow.AddSeconds(15); }
                }
                else if (localFrames.Running) localFrames.Stop();
                if (remote is not null && computer is not null)
                {
                    try { computer.Close(); } catch (Exception error) when (error is not OutOfMemoryException) { }
                    computer = null; attempted = false;
                }
                if (remote is null && !attempted)
                {
                    attempted = true;
                    try
                    {
                        using var identity = WindowsIdentity.GetCurrent();
                        bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                        // CPU low-level access needs elevation/PawnIO. Do not
                        // install drivers or elevate the launcher automatically.
                        computer = new Computer { IsCpuEnabled = elevated, IsGpuEnabled = true };
                        computer.Open();
                        adapterInfo = GpuTopology.Read();
                        sensorStatus = elevated ? "" : "Temperatura/consumo da CPU podem exigir permissão e PawnIO. Uso e RAM vêm do Windows.";
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        try { computer?.Close(); } catch (Exception cleanup) when (cleanup is not OutOfMemoryException) { }
                        computer = null; sensorStatus = "Sensores indisponíveis; exibindo os dados disponíveis do Windows.";
                    }
                }
                try
                {
                    var sample = remote ?? Read(computer, sensorStatus + (advanced?.LastError is { } error ? " " + error : ""));
                    if (localFrameAccess && CaptureFrames)
                        sample = sample with { Frames = localFrames.GetForegroundStats(),
                            Status = sample.Status + (localFrames.Error is null ? "" : " FPS/ETW indisponível: " + localFrames.Error) };
                    sample = SelectGpuSample(sample);
                    if (Needed && !dispatcher.HasShutdownStarted)
                        _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            if (!Needed) return;
                            // A manual selection may have changed while this
                            // background sample was waiting in the UI queue.
                            var current = SelectGpuSample(sample);
                            Latest = current; History.Enqueue(current); while (History.Count > 450) History.Dequeue(); Changed?.Invoke(current);
                        }));
                }
                catch (Exception error) when (error is not OutOfMemoryException) { sensorStatus = "Falha temporária na leitura dos sensores."; }
                // No frame loop, keyboard hook or per-control polling.
                using var delay = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                var pause = Task.Delay(TimeSpan.FromSeconds(2), delay.Token);
                var signal = wake.WaitAsync(delay.Token);
                await Task.WhenAny(pause, signal).ConfigureAwait(false);
                delay.Cancel();
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        finally { try { computer?.Close(); } catch (Exception error) when (error is not OutOfMemoryException) { } }
    }
    private HardwareSample Read(Computer? computer, string status)
    {
        var hardware = new List<IHardware>();
        void Walk(IHardware item) { item.Update(); hardware.Add(item); foreach (var child in item.SubHardware) Walk(child); }
        if (computer is not null) foreach (var item in computer.Hardware) Walk(item);
        double? cpuLoad = null;
        if (GetSystemTimes(out long idle, out long kernel, out long user))
        {
            long total = kernel + user;
            if (lastTotal > 0 && total > lastTotal) cpuLoad = Math.Clamp(100d * (1 - (idle - lastIdle) / (double)(total - lastTotal)), 0, 100);
            lastIdle = idle; lastTotal = total;
        }
        DeviceReading Device(IHardware? item, string fallback, bool cpu)
        {
            var sensors = item?.Sensors ?? Array.Empty<ISensor>();
            double? Find(SensorType type, params string[] preferred)
            {
                var valid = sensors.Where(s => s.SensorType == type && s.Value.HasValue && float.IsFinite(s.Value.Value)).ToArray();
                foreach (string name in preferred)
                {
                    var match = valid.FirstOrDefault(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                    if (match is not null) return match.Value;
                }
                return valid.Length > 0 ? valid.Max(s => (double?)s.Value) : null;
            }
            double? temperature = Find(SensorType.Temperature, "Package", "Tctl", "Tdie", "GPU Core");
            if (temperature <= 0) temperature = null;
            double? used = sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name.Equals("GPU Memory Used", StringComparison.OrdinalIgnoreCase))?.Value;
            double? total = sensors.FirstOrDefault(s => s.SensorType == SensorType.SmallData && s.Name.Equals("GPU Memory Total", StringComparison.OrdinalIgnoreCase))?.Value;
            return new(item?.Name.TrimEnd('\0') ?? fallback, temperature,
                cpu ? cpuLoad ?? Find(SensorType.Load, "CPU Total") : Find(SensorType.Load, "GPU Core", "D3D 3D"),
                Find(SensorType.Clock, cpu ? "CPU Core" : "GPU Core"), Find(SensorType.Power, "Package", "GPU Package", "GPU Power"),
                cpu ? null : used / 1024, cpu ? null : total / 1024);
        }
        string cpuName = "Processador";
        using (var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
            cpuName = (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? cpuName;
        var cpu = Device(hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu), cpuName, true);
        string GpuName(string name) => new(name.Replace("(TM)", "", StringComparison.OrdinalIgnoreCase)
            .Replace("(R)", "", StringComparison.OrdinalIgnoreCase).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var gpus = hardware.Where(h => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel).Take(16).Select(item =>
        {
            var device = Device(item, item.Name, false);
            var native = adapterInfo.FirstOrDefault(adapter => GpuName(adapter.Name) == GpuName(device.Name));
            bool? integrated = native?.Integrated;
            if (integrated is null && item.GetType().Name.Contains("Integrated", StringComparison.OrdinalIgnoreCase)) integrated = true;
            if (integrated is null && new[] { "GeForce", "Quadro", "Radeon RX", "Intel Arc" }.Any(name => device.Name.Contains(name, StringComparison.OrdinalIgnoreCase))) integrated = false;
            double? G(SensorType type, string name) => item.Sensors.FirstOrDefault(sensor => sensor.SensorType == type && sensor.Name.Contains(name, StringComparison.OrdinalIgnoreCase))?.Value;
            var gpuExtra = new Dictionary<string, double?>
            {
                ["gpu_hotspot"] = G(SensorType.Temperature, "Hot Spot"), ["gpu_memclock"] = G(SensorType.Clock, "Memory"),
                ["gpu_volt"] = G(SensorType.Voltage, "Core"), ["gpu_fan"] = G(SensorType.Fan, "Fan"), ["vram_temp"] = G(SensorType.Temperature, "Memory")
            };
            return new GpuReading(item.Identifier.ToString(), device, integrated, native?.DedicatedMemory ?? 0, gpuExtra);
        }).ToArray();
        var gpu = gpus.FirstOrDefault()?.Device ?? new DeviceReading("GPU não detectada");
        const double gb = 1024d * 1024 * 1024;
        var memory = new Memory { Length = (uint)Marshal.SizeOf<Memory>() };
        double? ramUsed = null, ramTotal = null;
        if (GlobalMemoryStatusEx(ref memory)) { ramTotal = memory.Total / gb; ramUsed = (memory.Total - memory.Available) / gb; }
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        double? diskFree = null, diskTotal = null;
        try { var drive = new DriveInfo(root); if (drive.IsReady) { diskFree = drive.AvailableFreeSpace / gb; diskTotal = drive.TotalSize / gb; } }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        var extras = new Dictionary<string, double?>();
        var cpuSensors = hardware.Where(h => h.HardwareType == HardwareType.Cpu).SelectMany(h => h.Sensors).Where(s => s.Value.HasValue && float.IsFinite(s.Value.Value)).ToArray();
        extras["cpu_maxcore"] = cpuSensors.Where(s => s.SensorType == SensorType.Load && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)).Select(s => (double?)s.Value).DefaultIfEmpty(null).Max();
        extras["cpu_clockmax"] = cpuSensors.Where(s => s.SensorType == SensorType.Clock).Select(s => (double?)s.Value).DefaultIfEmpty(null).Max();
        var clocks = cpuSensors.Where(s => s.SensorType == SensorType.Clock && s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase)).Select(s => (double?)s.Value).ToArray();
        if (clocks.Length > 0) cpu = cpu with { Clock = clocks.Average() };
        return new(DateTime.Now, cpu, gpu, ramUsed, ramTotal, diskFree, diskTotal, root.TrimEnd('\\'), status, extras, Gpus: gpus);
    }
    public void Dispose()
    {
        lock (sync) { if (disposed) return; disposed = true; consumers.Clear(); }
        stop.Cancel(); // Sensor cleanup stays on the worker, never the UI thread.
        advanced?.Disconnect();
    }
}
