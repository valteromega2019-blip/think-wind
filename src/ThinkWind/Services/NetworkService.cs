using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace ThinkWind.Services;
internal sealed record NetworkState(bool Connected, string Name, string Kind, string? Address, uint? Signal);
internal static class NetworkService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WlanInterface
    {
        internal Guid Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Description;
        internal int State;
    }
    [DllImport("wlanapi.dll")] private static extern uint WlanOpenHandle(uint version, nint reserved, out uint negotiated, out nint client);
    [DllImport("wlanapi.dll")] private static extern uint WlanCloseHandle(nint client, nint reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanEnumInterfaces(nint client, nint reserved, out nint list);
    [DllImport("wlanapi.dll")] private static extern uint WlanQueryInterface(nint client, ref Guid id, int opcode, nint reserved, out uint size, out nint data, out int type);
    [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(nint memory);
    internal static NetworkState Read()
    {
        var active = NetworkInterface.GetAllNetworkInterfaces()
            .Where(i => i.OperationalStatus == OperationalStatus.Up && i.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .OrderByDescending(i => i.GetIPProperties().GatewayAddresses.Count > 0).ThenByDescending(i => i.Speed).FirstOrDefault();
        if (active is null) return new(false, "Sem conexão ativa", "Rede", null, null);
        bool wifi = active.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
        var ip = active.GetIPProperties().UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
        var wireless = wifi ? WirelessName() : (null, (uint?)null);
        return new(true, wireless.Item1 ?? active.Name, wifi ? "Wi-Fi" : "Ethernet / rede", ip, wireless.Item2);
    }
    private static (string?, uint?) WirelessName()
    {
        nint client = 0, list = 0;
        try
        {
            if (WlanOpenHandle(2, 0, out _, out client) != 0 || WlanEnumInterfaces(client, 0, out list) != 0) return (null, null);
            int count = Math.Clamp(Marshal.ReadInt32(list), 0, 64);
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<WlanInterface>(list + 8 + i * Marshal.SizeOf<WlanInterface>());
                if (info.State != 1) continue;
                nint data = 0;
                try
                {
                    // wlan_intf_opcode_current_connection. Access-denied on newer
                    // Windows/location policies is a normal fallback, never a prompt.
                    if (WlanQueryInterface(client, ref info.Id, 7, 0, out var size, out data, out _) != 0 || size < 580) continue;
                    int length = Math.Clamp(Marshal.ReadInt32(data, 520), 0, 32);
                    var bytes = new byte[length]; Marshal.Copy(data + 524, bytes, 0, length);
                    string ssid = Encoding.UTF8.GetString(bytes).Replace('\0', ' ');
                    return (string.IsNullOrWhiteSpace(ssid) ? info.Description : ssid, (uint)Math.Clamp(Marshal.ReadInt32(data, 576), 0, 100));
                }
                finally { if (data != 0) WlanFreeMemory(data); }
            }
        }
        finally { if (list != 0) WlanFreeMemory(list); if (client != 0) WlanCloseHandle(client, 0); }
        return (null, null);
    }
}
