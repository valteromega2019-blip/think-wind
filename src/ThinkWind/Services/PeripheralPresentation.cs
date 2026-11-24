using System;
using System.Linq;
namespace OrbyLauncher.Services;
internal static class PeripheralPresentation
{
    internal static Preferences? Preferences { get; set; }
    internal static bool Hidden(string name) => Preferences?.HiddenBatteryDevices.Contains(name) == true;
    internal static string Glyph(PeripheralBattery device)
    {
        var preferences = Preferences;
        return preferences is not null && preferences.BatteryDeviceIcons.TryGetValue(device.Name, out var kind) ? Icon(kind) : device.Glyph;
    }
    internal static string Icon(string kind) => kind switch { "mouse" => "\uE962", "keyboard" => "\uE765", "controller" => "\uE7FC", "phone" => "\uE8EA", _ => "\uE7F6" };
}
