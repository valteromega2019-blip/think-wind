using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.HumanInterfaceDevice;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OrbyLauncher.Services;

// X11 receiver 1D57:FA60, input-only collection 000A:0000.
// Protocol reference: vasfvitor/attackshark-x11/docs/PROTOCOL.md.
// No feature/output reports, configuration changes or keyboard/mouse input reads.
internal static class AttackSharkBattery
{
    internal static async Task<PeripheralBattery?> Read()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        HidDevice? device = null;
        var result = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        int previous = -1;
        void Input(HidDevice sender, HidInputReportReceivedEventArgs args)
        {
            try
            {
                using var reader = DataReader.FromBuffer(args.Report.Data);
                if (reader.UnconsumedBufferLength < 5 || reader.UnconsumedBufferLength > 64) return;
                byte report = reader.ReadByte(), model = reader.ReadByte(), kind = reader.ReadByte();
                byte state = reader.ReadByte(), percent = reader.ReadByte();
                if (report != 3 || model != 0x55 || kind != 0x40 || percent > 100 || state is < 1 or > 3) return;
                if (Interlocked.Exchange(ref previous, percent) == percent) result.TrySetResult(percent);
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
        }
        try
        {
            var devices = await DeviceInformation.FindAllAsync(HidDevice.GetDeviceSelector(0x000A, 0, 0x1D57, 0xFA60)).AsTask(deadline.Token).ConfigureAwait(false);
            if (devices.Count == 0) return null;
            device = await HidDevice.FromIdAsync(devices[0].Id, FileAccessMode.Read).AsTask(deadline.Token).ConfigureAwait(false);
            if (device is null) return null;
            device.InputReportReceived += Input;
            int percent = await result.Task.WaitAsync(deadline.Token).ConfigureAwait(false);
            return new("Attack Shark X11", percent, "\uE962");
        }
        catch (OperationCanceledException) { return null; } // Sleeping/offline mouse is normal.
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { DiagnosticLog.Record("Bateria Attack Shark X11", error); return null; }
        finally
        {
            if (device is not null) { device.InputReportReceived -= Input; device.Dispose(); }
        }
    }
}
