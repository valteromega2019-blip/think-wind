using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;

namespace OrbyLauncher.Services;

// Read-only Windows interfaces. No input polling, vibration, firmware commands,
// exclusive HID handles, vendor application memory access or administrator task.
internal static class ReceiverBatterySources
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Battery { internal byte Type, Level; }
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern uint GetBattery(uint index, byte deviceType, out Battery battery);

    internal static IEnumerable<PeripheralBattery> ReadControllers()
    {
        var result = new List<PeripheralBattery>();
        try
        {
            foreach (var controller in Windows.Gaming.Input.RawGameController.RawGameControllers)
            {
                if (controller.HardwareVendorId != 0x2DC8 || controller.HardwareProductId != 0x3106) continue;
                var report = controller.TryGetBatteryReport();
                int? percent = null;
                if (report?.RemainingCapacityInMilliwattHours is int remaining && report.FullChargeCapacityInMilliwattHours is int full
                    && full > 0 && remaining >= 0 && remaining <= full)
                    percent = (int)Math.Round(remaining * 100d / full);
                result.Add(new("8BitDo Ultimate", percent, "\uE7FC", "Nível não informado", Charging: report?.Status == Windows.System.Power.BatteryStatus.Charging));
            }
            if (!result.Exists(device => device.Name == "8BitDo Ultimate") && OrbyLauncher.Native.PeripheralHid.Present(0x2DC8, 0x3106))
                result.Add(new("8BitDo Ultimate", null, "\uE7FC", "Receptor · carga não informada"));
            for (uint slot = 0; slot < 4; slot++)
            {
                if (GetBattery(slot, 0, out var battery) != 0) continue;
                // Disconnected, wired and unknown do not provide a valid battery level.
                if (battery.Type is not (2 or 3) || battery.Level > 3) continue;
                string level = battery.Level switch { 0 => "Vazia", 1 => "Baixa", 2 => "Média", _ => "Cheia" };
                result.Add(new("Controle XInput " + (slot + 1), null, "\uE7FC", level));
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or COMException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria XInput", error); }
        if (!result.Exists(device => device.Name == "8BitDo Ultimate") && OrbyLauncher.Native.PeripheralHid.Present(0x2DC8, 0x3106))
            result.Add(new("8BitDo Ultimate", null, "\uE7FC", "Receptor · carga não informada"));
        return result;
    }

    internal static IEnumerable<PeripheralBattery> ReadCustomKeyboard()
    {
        // Identified USB receiver; never open the keyboard collection or inspect keystrokes.
        if (OrbyLauncher.Native.PeripheralHid.Present(0x1EA7, 0x0002))
            return new[] { new PeripheralBattery("Teclado 2.4G RF Keyboard & Mouse · 1EA7:0002", null, "\uE765", "Bateria não informada pelo receptor") };
        return Array.Empty<PeripheralBattery>();
    }
    internal static async Task<IEnumerable<PeripheralBattery>> ReadWindowsDevices()
    {
        var result = new List<PeripheralBattery>();
        try
        {
            // Query only present devices for which the driver publishes battery.
            // Do not open keyboard/mouse input collections or send guessed reports.
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            var devices = await DeviceInformation.FindAllAsync(
                "System.Devices.Present:=System.StructuredQueryType.Boolean#True AND System.Devices.BatteryLife:>=0",
                new[] { "System.Devices.BatteryLife", "System.Devices.HardwareIds" }, DeviceInformationKind.Device).AsTask(timeout.Token).ConfigureAwait(false);
            foreach (var device in devices)
            {
                if (!device.Properties.TryGetValue("System.Devices.HardwareIds", out var ids) || ids is not string[] hardware
                    || !Array.Exists(hardware, id => id.Contains("VID_", StringComparison.OrdinalIgnoreCase))) continue;
                if (!device.Properties.TryGetValue("System.Devices.BatteryLife", out var charge) || charge is not (byte or ushort or int or uint)) continue;
                long percent = Convert.ToInt64(charge);
                if (percent < 0 || percent > 100 || string.IsNullOrWhiteSpace(device.Name)) continue;
                string name = device.Name.ToLowerInvariant();
                string glyph = name.Contains("mouse") ? "\uE962" : name.Contains("keyboard") || name.Contains("teclado") ? "\uE765"
                    : name.Contains("controller") || name.Contains("controle") || name.Contains("8bitdo") ? "\uE7FC" : "\uE7F6";
                result.Add(new(device.Name, (int)percent, glyph));
            }
        }
        catch (Exception error) when (error is COMException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria USB informada pelo driver", error); }
        return result;
    }
}
