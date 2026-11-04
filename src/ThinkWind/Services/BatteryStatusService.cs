using System;
using System.Runtime.InteropServices;

namespace OrbyLauncher.Services;

internal readonly record struct BatteryReading(bool Available, int? Percentage, bool Charging, bool PluggedIn, bool Saving, int? RemainingMinutes);

// One inexpensive native read shared by all monitors. No extra timer, process,
// WMI query, global hook or permanently subscribed system event.
internal static class BatteryStatusService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        internal byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        internal uint BatteryLifeTime, BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    private static readonly object Gate = new();
    private static long nextRead;
    private static BatteryReading reading;
    internal static bool IsSimulation { get; } = Array.IndexOf(Environment.GetCommandLineArgs(), "--simulate-battery") >= 0;
    private static readonly long simulationStarted = Environment.TickCount64;

    internal static BatteryReading Read()
    {
        lock (Gate)
        {
            long now = Environment.TickCount64;
            if (now < nextRead) return reading;
            nextRead = now + 5000;
            if (IsSimulation)
            {
                // Explicit preview argument only. Never written to preferences
                // or the OS; all normal launches continue using native readings.
                reading = ((now - simulationStarted) / 15000 % 4) switch
                {
                    0 => new(true, 72, false, false, false, 185),
                    1 => new(true, 18, false, false, true, 38),
                    2 => new(true, 45, true, true, false, null),
                    _ => new(true, 100, false, true, false, null)
                };
                return reading;
            }
            // Preserve the last successful result on transient API failure.
            if (!GetSystemPowerStatus(out var power)) return reading;
            int? percentage = power.BatteryLifePercent <= 100 ? power.BatteryLifePercent : null;
            bool available = power.BatteryFlag == 255 ? percentage.HasValue : (power.BatteryFlag & 128) == 0;
            reading = new BatteryReading(available, available ? percentage : null,
                available && power.BatteryFlag != 255 && (power.BatteryFlag & 8) != 0,
                power.AcLineStatus == 1, power.SystemStatusFlag == 1,
                available && power.AcLineStatus == 0 && power.BatteryLifeTime != uint.MaxValue
                    ? (int)(power.BatteryLifeTime / 60) : null);
            return reading;
        }
    }
}
