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

    internal static BatteryReading Read()
    {
        lock (Gate)
        {
            long now = Environment.TickCount64;
            if (now < nextRead) return reading;
            nextRead = now + 5000;
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
