using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OrbyLauncher.Services;

// Protocol and voltage curve adapted from portyporty/g435-battery-percentage (MIT).
// Copyright (c) 2026 Oğuz Kayra Bozkurt. See licenses/G435-MIT.txt.
// Whitelisted vendor collection only. Never probe keyboard or mouse input reports.
internal static class G435Battery
{
    internal static async Task<PeripheralBattery?> Read()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        HidDevice? hid = null;
        bool opened = false;
        var packets = new ConcurrentQueue<byte[]>();
        void Input(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            if (packets.Count >= 32) return;
            using var reader = DataReader.FromBuffer(args.Report.Data);
            if (reader.UnconsumedBufferLength > 65) return;
            var bytes = new byte[(int)reader.UnconsumedBufferLength]; reader.ReadBytes(bytes); packets.Enqueue(bytes);
        }
        async Task Send(byte command, byte[] body, int wait)
        {
            var frame = Frame(command, body);
            var report = hid!.CreateOutputReport(0);
            using var writer = new DataWriter(); writer.WriteByte(0); writer.WriteBytes(frame);
            report.Data = writer.DetachBuffer();
            await hid.SendOutputReportAsync(report).AsTask(deadline.Token).ConfigureAwait(false);
            if (wait > 0) await Task.Delay(wait, deadline.Token).ConfigureAwait(false);
        }
        try
        {
            var devices = await DeviceInformation.FindAllAsync(HidDevice.GetDeviceSelector(0xFF00, 1, 0x046D, 0x0ADF)).AsTask(deadline.Token).ConfigureAwait(false);
            if (devices.Count == 0) return null;
            hid = await HidDevice.FromIdAsync(devices[0].Id, FileAccessMode.ReadWrite).AsTask(deadline.Token).ConfigureAwait(false);
            if (hid is null) return null;
            if (hid.CreateOutputReport(0).Data.Length != 65) return null;
            hid.InputReportReceived += Input;
            opened = true;
            await Send(0x84, new byte[] { 1 }, 50);
            await Send(0x85, new byte[] { 1 }, 50);
            await Send(0x80, Convert.FromHexString("504106010D002534125A5A01000000F0000000"), 450);
            byte sequence = 2;
            Task Pa(byte category, byte length, byte[] payload, int wait)
            {
                var body = new byte[5 + payload.Length];
                body[0] = 0x50; body[1] = 0x41; body[2] = category; body[3] = sequence++; body[4] = length;
                payload.CopyTo(body, 5); return Send(0x80, body, wait);
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
                            return new("Logitech G435 · estimativa por tensão", Percentage(mv), "\uE7F6", Estimated: true);
                    }
        }
        catch (Exception error) when (error is OperationCanceledException or System.Runtime.InteropServices.COMException
            or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { DiagnosticLog.Record("Bateria G435", error); }
        finally
        {
            if (hid is not null)
            {
                if (opened)
                {
                    // Cleanup has a separate bound, even after the read deadline expires.
                    try
                    {
                        using var cleanup = new CancellationTokenSource(300);
                        var report = hid.CreateOutputReport(0);
                        using var writer = new DataWriter(); writer.WriteByte(0); writer.WriteBytes(Frame(0x85, new byte[] { 2 }));
                        report.Data = writer.DetachBuffer();
                        await hid.SendOutputReportAsync(report).AsTask(cleanup.Token).ConfigureAwait(false);
                    }
                    catch (Exception error) when (error is OperationCanceledException or System.Runtime.InteropServices.COMException or InvalidOperationException) { }
                    hid.InputReportReceived -= Input;
                }
                hid.Dispose();
            }
        }
        return null;
    }
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
