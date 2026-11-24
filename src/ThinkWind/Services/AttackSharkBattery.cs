using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

internal static class AttackSharkBattery
{
    internal static Task<PeripheralBattery?> Read() => Task.Run(async () =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            using var device = PeripheralHid.Open(0x1D57, 0xFA60, 0x000A, 0, false);
            if (device is null) return null;
            int previous = -1;
            while (!deadline.IsCancellationRequested)
            {
                var packet = await device.Read(deadline.Token).ConfigureAwait(false);
                if (packet.Length < 5 || packet[0] != 3 || packet[1] != 0x55 || packet[2] != 0x40 || packet[3] is < 1 or > 3 || packet[4] > 100) continue;
                int percent = packet[4];
                if (previous == percent) return new PeripheralBattery("Attack Shark X11", percent, "\uE962", Charging: packet[3] == 3);
                previous = percent;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria Attack Shark X11", error); }
        return null;
    });
}