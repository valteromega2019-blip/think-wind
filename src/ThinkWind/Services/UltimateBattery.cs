using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

internal static class UltimateBattery
{
    // Ultimate V1 receiver present on this PC, not an Ultimate 2 PID.
    // Attempt telemetry only if the exact vendor collection is actually exposed.
    internal static Task<PeripheralBattery?> Read() => Task.Run(async () =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            using var device = PeripheralHid.Open(0x2DC8, 0x3106, 0xFF7A, 1, true)
                ?? PeripheralHid.Open(0x2DC8, 0x3106, 0xFFA0, 1, true);
            if (device is null || device.InputLength < 64 || device.OutputLength < 64) return null;
            async Task<byte[]?> Query(byte command, bool battery)
            {
                using var responseDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                responseDeadline.CancelAfter(350);
                var report = new byte[device.OutputLength];
                report[0] = 0x81; report[1] = 4; report[2] = command; report[3] = 1;
                if (battery) report[6] = 6;
                await device.Write(report, responseDeadline.Token).ConfigureAwait(false);
                for (int count = 0; count < 12; count++)
                {
                    var reply = await device.Read(responseDeadline.Token).ConfigureAwait(false);
                    if (reply.Length >= 19 && reply[0] == 2 && reply[1] == 4 && reply[4] == command && reply[5] == 1)
                        return reply;
                }
                return null;
            }
            var link = await Query(0x20, false);
            if (link is null || link[18] == 0) return null;
            var data = await Query(0x21, true);
            if (data is null || data[6] != 6) return null;
            int percent = data[18] & 0x7F;
            if (percent > 100) return null;
            return new PeripheralBattery("8BitDo Ultimate", percent, "\uE7FC");
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria 8BitDo Ultimate", error); }
        return null;
    });
}
