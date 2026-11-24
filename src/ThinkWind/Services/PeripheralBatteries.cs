using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Devices.Enumeration;

namespace OrbyLauncher.Services;

internal sealed record PeripheralBattery(string Name, int? Percent, string Glyph, string? Level = null, bool Estimated = false, bool Charging = false, bool Stale = false, bool ChargingEstimated = false)
{
    internal int VisualCharge => Percent ?? (Level switch { "Vazia" => 0, "Baixa" => 20, "Média" => 50, "Cheia" => 100, _ => -1 });
    internal string Display => ChargingEstimated ? "Possível carregamento · estimado" : Percent is int percent ? (Estimated ? "≈" : "") + percent + "%" : Level ?? "Indisponível";
}
internal static class PeripheralBatteries
{
    private static readonly object gate = new();
    private static readonly List<DeviceWatcher> watchers = new();
    private static int revision;
    private static readonly Dictionary<string, PeripheralBattery[]> sources = new();
    private static readonly Dictionary<string, long> lastSuccess = new();
    private static void UpdateSource(string key, IEnumerable<PeripheralBattery> values, int expected)
    {
        var incoming = values.ToArray();
        bool? present = key switch
        {
            "g435" => OrbyLauncher.Native.PeripheralHid.Present(0x046D, 0x0ADF),
            "x11" => OrbyLauncher.Native.PeripheralHid.Present(0x1D57, 0xFA60),
            "ultimate" => OrbyLauncher.Native.PeripheralHid.Present(0x2DC8, 0x3106),
            _ => null
        };
        lock (gate)
        {
            if (expected != revision) return;
            if (present == false) { sources.Remove(key); lastSuccess.Remove(key); }
            else if (incoming.Length > 0)
            {
                bool keptPrevious = false;
                if (sources.TryGetValue(key, out var old) && lastSuccess.TryGetValue(key, out long last) && Environment.TickCount64 - last <= 120000)
                    incoming = incoming.Select(device =>
                    {
                        var known = old.FirstOrDefault(value => value.Name == device.Name && value.Percent.HasValue);
                        if (known is not null && device.Percent is null && device.Level?.Contains("informad", StringComparison.OrdinalIgnoreCase) == true)
                        { keptPrevious = true; return known with { Stale = true }; }
                        return device;
                    }).ToArray();
                sources[key] = incoming;
                if (!keptPrevious) lastSuccess[key] = Environment.TickCount64;
            }
            else if (sources.TryGetValue(key, out var previous) && lastSuccess.TryGetValue(key, out long success))
            {
                long age = Environment.TickCount64 - success;
                if (age <= 120000) sources[key] = previous.Select(device => device with { Stale = true }).ToArray();
                else if (present == true) sources[key] = previous.Select(device => device with { Percent = null, Level = "Sem leitura recente", Charging = false, ChargingEstimated = false, Stale = true }).ToArray();
                else { sources.Remove(key); lastSuccess.Remove(key); }
            }
            Publish(sources.Values.SelectMany(value => value));
        }
    }
    internal static event Action? Changed;
    private static bool notificationPending;
    private static void Publish(IEnumerable<PeripheralBattery> readings)
    {
        var next = readings.GroupBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group.OrderByDescending(device => device.Percent.HasValue).ThenBy(device => device.Stale).First())
            .OrderBy(device => device.Name).ToArray();
        if (Current.SequenceEqual(next)) return;
        Current = next;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted && !notificationPending)
        {
            notificationPending = true;
            dispatcher.BeginInvoke(new Action(() =>
            {
                lock (gate) notificationPending = false;
                if (!stopped) Changed?.Invoke();
            }));
        }
    }
    private static void Invalidate(string? removedId = null, string? removedName = null)
    {
        lock (gate)
        {
            foreach (string key in nextReads.Keys.ToArray()) nextReads[key] = 0;
            revision++;
            // A USB receiver exposes several interfaces. Retire only its source;
            // unrelated HID events must never blank the entire indicator strip.
            string? source = removedId is null ? null : removedId.Contains("vid_046d&pid_0adf", StringComparison.OrdinalIgnoreCase) ? "g435"
                : removedId.Contains("vid_1d57&pid_fa60", StringComparison.OrdinalIgnoreCase) ? "x11"
                : removedId.Contains("vid_2dc8&pid_3106", StringComparison.OrdinalIgnoreCase) ? "ultimate"
                : removedId.Contains("vid_1ea7&pid_0002", StringComparison.OrdinalIgnoreCase) ? "keyboards" : null;
            bool removed = source switch
            {
                "g435" => !OrbyLauncher.Native.PeripheralHid.Present(0x046D, 0x0ADF),
                "x11" => !OrbyLauncher.Native.PeripheralHid.Present(0x1D57, 0xFA60),
                "ultimate" => !OrbyLauncher.Native.PeripheralHid.Present(0x2DC8, 0x3106),
                "keyboards" => !OrbyLauncher.Native.PeripheralHid.Present(0x1EA7, 0x0002),
                _ => false
            };
            if (source is not null && removed) { sources.Remove(source); lastSuccess.Remove(source); }
            if (source == "ultimate" && removed && sources.TryGetValue("controllers", out var controllers))
                sources["controllers"] = controllers.Where(device => device.Name != "8BitDo Ultimate").ToArray();
            if (!string.IsNullOrWhiteSpace(removedName))
                foreach (string key in sources.Keys.ToArray()) sources[key] = sources[key].Where(device => device.Name != removedName).ToArray();
            Publish(sources.Values.SelectMany(value => value));
        }
    }
    private static void WatchDevices()
    {
        if (watchers.Count != 0) return;
        foreach (var kind in new[] { DeviceInformationKind.DeviceInterface, DeviceInformationKind.AssociationEndpoint })
        {
            bool ready = false;
            var names = new Dictionary<string, string>();
            var namesGate = new object();
            string selector = kind == DeviceInformationKind.DeviceInterface
                ? "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\""
                : "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
            try
            {
                var watcher = DeviceInformation.CreateWatcher(selector, new[] { "System.Devices.Aep.IsConnected" }, kind);
                watcher.Added += (_, device) => { lock (namesGate) names[device.Id] = device.Name; if (ready) Invalidate(); };
                watcher.Removed += (_, device) =>
                {
                    string? name; lock (namesGate) { names.Remove(device.Id, out name); }
                    if (ready) Invalidate(device.Id, kind == DeviceInformationKind.AssociationEndpoint ? name : null);
                };
                watcher.Updated += (_, change) =>
                {
                    if (!ready || !change.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var connected)) return;
                    string? name; lock (namesGate) names.TryGetValue(change.Id, out name);
                    Invalidate(connected is false ? change.Id : null, connected is false ? name : null);
                };
                watcher.EnumerationCompleted += (_, _) => ready = true;
                watchers.Add(watcher); watcher.Start();
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException)
            { DiagnosticLog.Record("Observar conexão de periféricos", error); }
        }
    }
    internal static void Stop()
    {
        lock (gate) { stopped = true; revision++; }
        foreach (var watcher in watchers)
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) watcher.Stop();
        watchers.Clear();
    }
    private static readonly Dictionary<string, long> nextReads = new();
    private static readonly HashSet<string> reading = new();
    private static bool stopped;
    internal static PeripheralBattery[] Current { get; private set; } = Array.Empty<PeripheralBattery>();
    internal static void Refresh()
    {
        if (stopped) return;
        WatchDevices();
        Schedule("local", 2000, ReadLocal);
        Schedule("controllers", 5000, started =>
        {
            UpdateSource("controllers", ReceiverBatterySources.ReadControllers(), started);
            return Task.CompletedTask;
        });
        Schedule("g435", 5000, async started =>
        {
            var value = await G435Battery.Read().ConfigureAwait(false);
            UpdateSource("g435", value is null ? Array.Empty<PeripheralBattery>() : new[] { value }, started);
        });
        Schedule("ultimate", 5000, async started =>
        {
            var value = await UltimateBattery.Read().ConfigureAwait(false);
            UpdateSource("ultimate", value is null ? Array.Empty<PeripheralBattery>() : new[] { value }, started);
        });
        Schedule("x11", 5000, async started =>
        {
            var value = await AttackSharkBattery.Read().ConfigureAwait(false);
            UpdateSource("x11", value is null ? Array.Empty<PeripheralBattery>() : new[] { value }, started);
        });
        Schedule("windows", 60000, async started => UpdateSource("windows", await ReceiverBatterySources.ReadWindowsDevices().ConfigureAwait(false), started));
        Schedule("bluetooth", 60000, ReadBluetooth);
    }
    private static void Schedule(string key, int interval, Func<int, Task> read)
    {
        int started;
        lock (gate)
        {
            if (stopped || reading.Contains(key) || nextReads.TryGetValue(key, out long due) && Environment.TickCount64 < due) return;
            reading.Add(key); nextReads[key] = Environment.TickCount64 + interval; started = revision;
        }
        _ = Task.Run(async () =>
        {
            try { await read(started).ConfigureAwait(false); }
            catch (OperationCanceledException) { UpdateSource(key, Array.Empty<PeripheralBattery>(), started); }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.IO.IOException)
            { DiagnosticLog.Record("Bateria de periféricos: " + key, error); }
            finally { lock (gate) reading.Remove(key); }
        });
    }
    private static Task ReadLocal(int started)
    {
            var driverBatteries = BluetoothDriverBattery.Read();
            UpdateSource("bluetooth-driver", driverBatteries, started);
            if (driverBatteries.Any(device => device.Name.Equals("BT+2.4G KB", StringComparison.OrdinalIgnoreCase)))
            {
                lock (gate)
                {
                    if (started == revision && sources.Remove("keyboards"))
                    { lastSuccess.Remove("keyboards"); Publish(sources.Values.SelectMany(value => value)); }
                }
            }
            else UpdateSource("keyboards", ReceiverBatterySources.ReadCustomKeyboard(), started);
            return Task.CompletedTask;
    }
    private static async Task ReadBluetooth(int started)
    {
            var result = new List<PeripheralBattery>();
            using var deadline = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            string[] protocols = { "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}", "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}" };
            foreach (string protocol in protocols)
            {
                var devices = await DeviceInformation.FindAllAsync("System.Devices.Aep.ProtocolId:=\"" + protocol + "\" AND System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True",
                    new[] { "System.Devices.BatteryLife", "System.Devices.Aep.IsConnected" }, DeviceInformationKind.AssociationEndpoint).AsTask(deadline.Token).ConfigureAwait(false);
                foreach (var device in devices)
                {
                    if (!device.Properties.TryGetValue("System.Devices.BatteryLife", out var charge)
                        || charge is not (byte or ushort or int or uint)) continue;
                    long percent = Convert.ToInt64(charge);
                    if (percent < 0 || percent > 100 || string.IsNullOrWhiteSpace(device.Name)) continue;
                    string name = device.Name.ToLowerInvariant();
                    string glyph = name.Contains("mouse") ? "\uE962" : name.Contains("keyboard") || name.Contains("teclado") ? "\uE765"
                        : name.Contains("controller") || name.Contains("gamepad") || name.Contains("controle") || name.Contains("xbox") ? "\uE7FC" : "\uE7F6";
                    result.Add(new(device.Name, (int)percent, glyph));
                }
            }
            UpdateSource("bluetooth", result, started);
    }
}
