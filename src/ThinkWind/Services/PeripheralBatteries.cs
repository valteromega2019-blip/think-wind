using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Devices.Enumeration;

namespace OrbyLauncher.Services;

internal sealed record PeripheralBattery(string Name, int? Percent, string Glyph, string? Level = null, bool Estimated = false, bool Charging = false)
{
    internal int VisualCharge => Percent ?? (Level switch { "Vazia" => 0, "Baixa" => 20, "Média" => 50, "Cheia" => 100, _ => -1 });
    internal string Display => Percent is int percent ? (Estimated ? "≈" : "") + percent + "%" : Level ?? "Indisponível";
}
internal static class PeripheralBatteries
{
    private static readonly object gate = new();
    private static readonly List<DeviceWatcher> watchers = new();
    private static int revision;
    private static readonly Dictionary<string, PeripheralBattery[]> sources = new();
    private static void UpdateSource(string key, IEnumerable<PeripheralBattery> values, int expected)
    {
        lock (gate)
        {
            if (expected != revision) return;
            sources[key] = values.ToArray(); Publish(sources.Values.SelectMany(value => value));
        }
    }
    internal static event Action? Changed;
    private static void Publish(IEnumerable<PeripheralBattery> readings)
    {
        Current = readings.GroupBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(group => group.OrderByDescending(device => device.Percent.HasValue).First())
            .OrderBy(device => device.Name).ToArray();
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.HasShutdownStarted)
            dispatcher.BeginInvoke(new Action(() => Changed?.Invoke()));
    }
    private static void Invalidate()
    {
        lock (gate) { nextRead = 0; revision++; sources.Clear(); Publish(Array.Empty<PeripheralBattery>()); }
    }
    private static void WatchDevices()
    {
        if (watchers.Count != 0) return;
        foreach (var kind in new[] { DeviceInformationKind.DeviceInterface, DeviceInformationKind.AssociationEndpoint })
        {
            bool ready = false;
            string selector = kind == DeviceInformationKind.DeviceInterface
                ? "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\""
                : "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\"";
            try
            {
                var watcher = DeviceInformation.CreateWatcher(selector, new[] { "System.Devices.Aep.IsConnected" }, kind);
                watcher.Added += (_, _) => { if (ready) Invalidate(); };
                watcher.Removed += (_, _) => { if (ready) Invalidate(); };
                watcher.Updated += (_, change) => { if (ready && change.Properties.ContainsKey("System.Devices.Aep.IsConnected")) Invalidate(); };
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
    private static long nextRead;
    private static bool busy;
    private static bool stopped;
    internal static PeripheralBattery[] Current { get; private set; } = Array.Empty<PeripheralBattery>();
    internal static void Refresh()
    {
        if (stopped) return;
        WatchDevices();
        lock (gate)
        {
            if (busy || Environment.TickCount64 < nextRead) return;
            busy = true; nextRead = Environment.TickCount64 + 10000;
        }
        _ = Task.Run(Read);
    }
    private static async Task Read()
    {
        int started;
        lock (gate) started = revision;
        try
        {
            UpdateSource("controllers", ReceiverBatterySources.ReadControllers(), started);
            UpdateSource("keyboards", ReceiverBatterySources.ReadCustomKeyboard(), started);
            var sharkRead = AttackSharkBattery.Read();
            var ultimateRead = UltimateBattery.Read();
            var g435 = await G435Battery.Read();
            UpdateSource("g435", g435 is null ? Array.Empty<PeripheralBattery>() : new[] { g435 }, started);
            var ultimate = await ultimateRead;
            UpdateSource("ultimate", ultimate is null ? Array.Empty<PeripheralBattery>() : new[] { ultimate }, started);
            var shark = await sharkRead;
            UpdateSource("x11", shark is null ? Array.Empty<PeripheralBattery>() : new[] { shark }, started);
            UpdateSource("windows", await ReceiverBatterySources.ReadWindowsDevices(), started);
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
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria de periféricos", error); /* Preserve independently successful sources. */ }
        finally { lock (gate) busy = false; }
    }
}
