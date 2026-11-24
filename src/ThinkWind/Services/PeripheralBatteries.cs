using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Devices.Enumeration;

namespace OrbyLauncher.Services;

internal sealed record PeripheralBattery(string Name, int? Percent, string Glyph, string? Level = null, bool Estimated = false)
{
    internal string Display => Percent is int percent ? (Estimated ? "≈" : "") + percent + "%" : Level ?? "Indisponível";
}
internal static class PeripheralBatteries
{
    private static long nextRead;
    private static bool busy;
    internal static PeripheralBattery[] Current { get; private set; } = Array.Empty<PeripheralBattery>();
    internal static void Refresh()
    {
        if (busy || Environment.TickCount64 < nextRead) return;
        busy = true; nextRead = Environment.TickCount64 + 60000;
        _ = Read();
    }
    private static async Task Read()
    {
        try
        {
            var result = new List<PeripheralBattery>();
            var sharkRead = AttackSharkBattery.Read();
            var g435 = await G435Battery.Read();
            if (g435 is not null) result.Add(g435);
            var shark = await sharkRead;
            if (shark is not null) result.Add(shark);
            result.AddRange(ReceiverBatterySources.ReadControllers());
            result.AddRange(await ReceiverBatterySources.ReadWindowsDevices());
            string[] protocols = { "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}", "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}" };
            foreach (string protocol in protocols)
            {
                var devices = await DeviceInformation.FindAllAsync("System.Devices.Aep.ProtocolId:=\"" + protocol + "\" AND System.Devices.Aep.IsConnected:=System.StructuredQueryType.Boolean#True",
                    new[] { "System.Devices.BatteryLife", "System.Devices.Aep.IsConnected" }, DeviceInformationKind.AssociationEndpoint);
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
            Current = result.DistinctBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase).OrderBy(device => device.Name).ToArray();
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria de periféricos", error); Current = Array.Empty<PeripheralBattery>(); }
        finally { busy = false; }
    }
}
