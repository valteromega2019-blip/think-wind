using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OrbyLauncher.Native;

internal sealed record BluetoothDisconnectResult(bool Requested, string Message);

// On-demand request to the Bluetooth Classic stack. No radio/profile changes,
// unpairing, elevation or driver installation. Not a universal BLE API.
internal static class BluetoothConnection
{
    private const uint DisconnectDevice = 0x0041000C; // IOCTL_BTH_DISCONNECT_DEVICE, bthioctl.h
    [StructLayout(LayoutKind.Sequential)] private struct FindRadioParameters { internal uint Size; }
    [DllImport("bthprops.cpl", SetLastError = true)]
    private static extern nint BluetoothFindFirstRadio(ref FindRadioParameters parameters, out SafeFileHandle radio);
    [DllImport("bthprops.cpl", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindNextRadio(nint find, out SafeFileHandle radio);
    [DllImport("bthprops.cpl")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BluetoothFindRadioClose(nint find);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint control, ref ulong input, uint inputSize,
        nint output, uint outputSize, out uint returned, nint overlapped);

    internal static bool TryAddress(string? value, out ulong address)
    {
        string text = (value ?? "").Replace(":", "").Replace("-", "").Trim();
        address = 0;
        return text.Length == 12 && ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address)
            && address is > 0 and <= 0xFFFFFFFFFFFF;
    }

    internal static BluetoothDisconnectResult Disconnect(ulong address)
    {
        if (address is 0 or > 0xFFFFFFFFFFFF) throw new ArgumentOutOfRangeException(nameof(address));
        var parameters = new FindRadioParameters { Size = (uint)Marshal.SizeOf<FindRadioParameters>() };
        nint find = BluetoothFindFirstRadio(ref parameters, out var radio);
        if (find == 0)
        {
            int error = Marshal.GetLastWin32Error(); radio?.Dispose();
            throw new Win32Exception(error, "O Windows não disponibilizou um rádio para solicitar a desconexão.");
        }
        int failure = 1167; // ERROR_DEVICE_NOT_CONNECTED
        try
        {
            while (true)
            {
                using (radio)
                {
                    if (radio is not null && !radio.IsInvalid)
                    {
                        if (DeviceIoControl(radio, DisconnectDevice, ref address, sizeof(ulong), 0, 0, out _, 0))
                            return new(true, "Desconexão solicitada. Aguardando o estado informado pelo Windows; o pareamento foi mantido.");
                        int error = Marshal.GetLastWin32Error();
                        if (error != 1167) failure = error;
                    }
                }
                if (!BluetoothFindNextRadio(find, out radio)) { radio?.Dispose(); break; }
            }
        }
        finally { BluetoothFindRadioClose(find); }
        if (failure == 1167) return new(false, "O Windows não encontrou uma conexão ativa neste dispositivo. O pareamento foi mantido.");
        throw new Win32Exception(failure, "O Windows ou o driver não permitiu esta desconexão. Use o gerenciamento nativo; o pareamento foi mantido.");
    }
}
