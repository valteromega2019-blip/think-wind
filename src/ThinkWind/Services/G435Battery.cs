using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

// Protocol/curve adapted from portyporty/g435-battery-percentage, MIT.
// Copyright (c) 2026 Oğuz Kayra Bozkurt. See licenses/G435-MIT.txt.
internal static class G435Battery
{
    private static readonly object statusGate = new();
    private static bool charging;
    private static long statusAt;
    private static readonly ChargeRiseDetector riseDetector = new(120, 80);
    internal static int? LastMillivolts { get; private set; }
    // Passive Lightspeed status: 00 .. 50 49, state before 04 00 04 01.
    // Protocol evidence: gearhub LogitechHidppProvider (MIT). Separate from voltage heuristic.
    private static void ObserveStatus(byte[] packet)
    {
        if (packet.Length != 65 || packet[0] != 0 || packet[2] != 0x50 || packet[3] != 0x49) return;
        for (int i = 5; i + 3 < packet.Length; i++)
            if (packet[i] == 4 && packet[i + 1] == 0 && packet[i + 2] == 4 && packet[i + 3] == 1)
            {
                lock (statusGate) { charging = (packet[i - 1] & 2) != 0; statusAt = Environment.TickCount64; }
                return;
            }
    }
    private static bool IsCharging()
    {
        lock (statusGate) return statusAt != 0 && Environment.TickCount64 - statusAt <= 90000 && charging;
    }
    internal static Task<PeripheralBattery?> Read() => Task.Run(async () =>
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        PeripheralHid? hid = null;
        Task? receive = null;
        bool opened = false;
        var packets = new ConcurrentQueue<byte[]>();
        async Task Send(byte command, byte[] body, int wait, CancellationToken token)
        {
            var report = new byte[65]; Frame(command, body).CopyTo(report, 1);
            await hid!.Write(report, token).ConfigureAwait(false);
            if (wait > 0) await Task.Delay(wait, token).ConfigureAwait(false);
        }
        try
        {
            hid = PeripheralHid.Open(0x046D, 0x0ADF, 0xFF00, 1, true);
            if (hid is null || hid.OutputLength != 65 || hid.InputLength != 65) return null;
            receive = Task.Run(async () =>
            {
                try
                {
                    while (!deadline.IsCancellationRequested)
                    {
                        var packet = await hid.Read(deadline.Token).ConfigureAwait(false);
                        ObserveStatus(packet);
                        if (packets.Count < 32) packets.Enqueue(packet);
                    }
                }
                catch (OperationCanceledException) { }
                catch (IOException) { }
            });
            opened = true;
            await Send(0x84, new byte[] { 1 }, 50, deadline.Token);
            await Send(0x85, new byte[] { 1 }, 50, deadline.Token);
            await Send(0x80, Convert.FromHexString("504106010D002534125A5A01000000F0000000"), 450, deadline.Token);
            byte sequence = 2;
            Task Pa(byte category, byte length, byte[] payload, int wait)
            {
                var body = new byte[5 + payload.Length];
                body[0] = 0x50; body[1] = 0x41; body[2] = category; body[3] = sequence++; body[4] = length;
                payload.CopyTo(body, 5); return Send(0x80, body, wait, deadline.Token);
            }
            await Pa(0x0E, 1, new byte[] { 0xE0 }, 120);
            await Pa(0x0E, 2, new byte[] { 0xE1, 1 }, 120);
            await Pa(0x0E, 1, new byte[] { 0xE0 }, 120);
            while (packets.TryDequeue(out _)) { }
            await Pa(0x06, 1, new byte[] { 0, 0x31 }, 250);
            foreach (var packet in packets)
                for (int i = 0; i + 7 < packet.Length; i++)
                    if (packet[i] == 0 && packet[i + 1] == 7 && packet[i + 2] == 0 && packet[i + 3] == 6 && packet[i + 5] == 0)
                    {
                        int mv = packet[i + 6] | packet[i + 7] << 8;
                        if (mv >= 2500 && mv <= 4500)
                        {
                            LastMillivolts = mv;
                            bool inferred = riseDetector.Update(mv, Environment.TickCount64);
                            bool confirmed = IsCharging();
                            return new PeripheralBattery("Logitech G435 · estimativa por tensão",
                                Percentage(riseDetector.HoldBaseline && !confirmed ? riseDetector.Baseline : mv), "\uE7F6",
                                Estimated: true, Charging: confirmed || inferred, ChargingEstimated: inferred && !confirmed);
                        }
                    }
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { DiagnosticLog.Record("Bateria G435", error); }
        finally
        {
            if (opened && hid is not null)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(300);
                    await Send(0x85, new byte[] { 2 }, 0, cleanup.Token);
                }
                catch (Exception error) when (error is OperationCanceledException or IOException or InvalidOperationException) { }
            }
            deadline.Cancel();
            if (receive is not null) await receive.ConfigureAwait(false);
            hid?.Dispose();
        }
        return null;
    });
    private static byte[] Frame(byte command, byte[] body)
    {
        var frame = new byte[64]; frame[0] = command; frame[1] = (byte)body.Length; body.CopyTo(frame, 4);
        ushort crc = 0;
        foreach (byte value in frame)
        {
            crc ^= (ushort)(value << 8);
            for (int bit = 0; bit < 8; bit++) crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        frame[2] = (byte)crc; frame[3] = (byte)(crc >> 8); return frame;
    }
    private static int Percentage(int mv)
    {
        int[] volts = { 4200, 4150, 4100, 4050, 4000, 3950, 3900, 3850, 3800, 3750, 3700, 3650, 3600, 3550, 3500, 3400 };
        int[] levels = { 100, 95, 90, 85, 80, 75, 70, 62, 55, 45, 35, 25, 15, 10, 5, 0 };
        if (mv >= volts[0]) return 100;
        for (int i = 0; i < volts.Length - 1; i++)
            if (mv >= volts[i + 1]) return (int)Math.Round(levels[i + 1] + (mv - volts[i + 1]) * (levels[i] - levels[i + 1]) / (double)(volts[i] - volts[i + 1]));
        return 0;
    }
}
