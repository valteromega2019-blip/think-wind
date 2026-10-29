using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ThinkWind.Services;
internal sealed record WifiAdapterInfo(Guid Id, string Name, bool? RadioOn, bool CanToggle, string? Error);
internal sealed record WifiNetwork(Guid Adapter, string Name, byte[] Ssid, string Profile, uint Signal, bool Secure, uint Auth, uint Cipher, bool Connected, bool Connectable)
{
    internal string Key => Adapter + ":" + Convert.ToHexString(Ssid) + ":" + Auth + ":" + Cipher;
    internal bool CanCreateProfile => (!Secure && Auth == 1) || ((Auth is 7 or 9) && Cipher == 4);
}
internal sealed record WifiSnapshot(IReadOnlyList<WifiAdapterInfo> Adapters, IReadOnlyList<WifiNetwork> Networks);
internal sealed class WifiUnavailableException : Exception { internal WifiUnavailableException(string message) : base(message) { } }
internal sealed class WifiService : IDisposable
{
    private readonly StaWorker worker = new("Think Wind — WLAN");
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Interface
    { internal Guid Id; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Name; internal uint State; }
    [StructLayout(LayoutKind.Sequential)] private struct Ssid
    { internal uint Length; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] internal byte[] Bytes; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Available
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] internal string Profile;
        internal Ssid Ssid; internal uint BssType, Bssids; internal int Connectable; internal uint Reason, PhyCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] internal uint[] Phys;
        internal int MorePhys; internal uint Signal; internal int Secure; internal uint Auth, Cipher, Flags, Reserved;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Connection
    { internal uint Mode; internal nint Profile, Ssid, Bssid; internal uint BssType, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Radio { internal uint Phy, Software, Hardware; }
    [StructLayout(LayoutKind.Sequential)] private struct Notification
    { internal uint Source, Code; internal Guid Interface; internal uint Size; internal nint Data; }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Notify(ref Notification info, nint context);
    [DllImport("wlanapi.dll")] private static extern uint WlanOpenHandle(uint version, nint reserved, out uint negotiated, out nint client);
    [DllImport("wlanapi.dll")] private static extern uint WlanCloseHandle(nint client, nint reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanEnumInterfaces(nint client, nint reserved, out nint list);
    [DllImport("wlanapi.dll")] private static extern uint WlanGetAvailableNetworkList(nint client, ref Guid adapter, uint flags, nint reserved, out nint list);
    [DllImport("wlanapi.dll")] private static extern uint WlanQueryInterface(nint client, ref Guid adapter, uint opcode, nint reserved, out uint size, out nint data, out uint kind);
    [DllImport("wlanapi.dll")] private static extern uint WlanSetInterface(nint client, ref Guid adapter, uint opcode, uint size, ref Radio state, nint reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanScan(nint client, ref Guid adapter, nint ssid, nint ies, nint reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanConnect(nint client, ref Guid adapter, ref Connection connection, nint reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanDisconnect(nint client, ref Guid adapter, nint reserved);
    [DllImport("wlanapi.dll")] private static extern uint WlanRegisterNotification(nint client, uint source, [MarshalAs(UnmanagedType.Bool)] bool ignoreDuplicates, Notify? callback, nint context, nint reserved, out uint previous);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)] private static extern uint WlanReasonCodeToString(uint reason, uint capacity, StringBuilder text, nint reserved);
    [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(nint data);
    private static void Check(uint error)
    {
        if (error == 0) return;
        throw new WifiUnavailableException(error == 5 ? "O Windows bloqueou o acesso ao Wi-Fi. Confira a permissão de localização e as políticas da conta." : new Win32Exception(unchecked((int)error)).Message);
    }
    private static T WithClient<T>(Func<nint, T> action)
    {
        nint client = 0;
        try { Check(WlanOpenHandle(2, 0, out _, out client)); return action(client); }
        finally { if (client != 0) WlanCloseHandle(client, 0); }
    }
    internal Task<WifiSnapshot> ReadAsync(bool scan) => worker.Run(() => WithClient(client => Read(client, scan)));
    private static WifiSnapshot Read(nint client, bool scan)
    {
        var adapters = new List<WifiAdapterInfo>(); var networks = new List<WifiNetwork>(); nint interfaces = 0;
        try
        {
            Check(WlanEnumInterfaces(client, 0, out interfaces)); int count = Math.Clamp(Marshal.ReadInt32(interfaces), 0, 64);
            for (int i = 0; i < count; i++)
            {
                var adapter = Marshal.PtrToStructure<Interface>(interfaces + 8 + i * Marshal.SizeOf<Interface>());
                bool? radioOn = null; bool canToggle = false; string? error = null;
                try
                {
                    var radios = ReadRadio(client, adapter.Id); radioOn = radios.Count > 0 ? radios.Any(radio => radio.Software == 1 && radio.Hardware == 1) : null;
                    canToggle = radios.Count > 0 && radios.All(radio => radio.Hardware == 1);
                }
                catch (WifiUnavailableException) { }
                try
                {
                    if (scan && radioOn != false)
                        Wait(client, adapter.Id, new uint[] { 7, 8 }, () => Check(WlanScan(client, ref adapter.Id, 0, 0, 0)), 4000, false);
                    if (radioOn != false) ReadNetworks(client, adapter.Id, networks);
                }
                catch (WifiUnavailableException failure) { error = failure.Message; }
                adapters.Add(new(adapter.Id, adapter.Name, radioOn, canToggle, error));
            }
            return new(adapters, networks.OrderByDescending(network => network.Connected).ThenByDescending(network => network.Signal).ToArray());
        }
        finally { if (interfaces != 0) WlanFreeMemory(interfaces); }
    }
    private static List<Radio> ReadRadio(nint client, Guid adapter)
    {
        nint data = 0;
        try
        {
            Check(WlanQueryInterface(client, ref adapter, 4, 0, out var size, out data, out _));
            var result = new List<Radio>(); int count = size >= 4 ? Math.Clamp(Marshal.ReadInt32(data), 0, 64) : 0;
            for (int i = 0; i < count && 4 + (i + 1) * 12 <= size; i++) result.Add(Marshal.PtrToStructure<Radio>(data + 4 + i * 12));
            return result;
        }
        finally { if (data != 0) WlanFreeMemory(data); }
    }
    private static void ReadNetworks(nint client, Guid adapter, List<WifiNetwork> result)
    {
        nint data = 0;
        try
        {
            Check(WlanGetAvailableNetworkList(client, ref adapter, 0, 0, out data)); int count = Math.Clamp(Marshal.ReadInt32(data), 0, 512);
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<Available>(data + 8 + i * Marshal.SizeOf<Available>());
                if (info.Ssid.Length is 0 or > 32 || info.BssType != 1) continue;
                var bytes = info.Ssid.Bytes.Take((int)info.Ssid.Length).ToArray(); string name = Encoding.UTF8.GetString(bytes).Replace('\0', ' ');
                var network = new WifiNetwork(adapter, name, bytes, info.Profile, Math.Min(100, info.Signal), info.Secure != 0, info.Auth, info.Cipher, (info.Flags & 1) != 0, info.Connectable != 0);
                int existing = result.FindIndex(item => item.Key == network.Key);
                if (existing < 0) result.Add(network); else if (network.Connected || (result[existing].Profile.Length == 0 && network.Profile.Length > 0)) result[existing] = network;
            }
        }
        finally { if (data != 0) WlanFreeMemory(data); }
    }
    private static void Wait(nint client, Guid adapter, uint[] codes, Action begin, int milliseconds, bool requireConfirmation)
    {
        using var signal = new ManualResetEventSlim(); uint reason = 0; uint received = 0;
        Notify callback = (ref Notification notification, nint context) =>
        {
            if (notification.Interface != adapter || !codes.Contains(notification.Code)) return;
            received = notification.Code;
            if ((notification.Code is 10 or 11) && notification.Size >= 564 && notification.Data != 0) reason = unchecked((uint)Marshal.ReadInt32(notification.Data, 560));
            signal.Set();
        };
        Check(WlanRegisterNotification(client, 8, true, callback, 0, 0, out _));
        try
        {
            begin(); bool completed = signal.Wait(milliseconds);
            if (requireConfirmation && !completed) throw new WifiUnavailableException("O Windows ainda não confirmou a operação. Consulte o estado da rede antes de tentar novamente.");
            if (received == 11 || reason != 0)
            {
                var text = new StringBuilder(1024); WlanReasonCodeToString(reason, 1024, text, 0);
                throw new WifiUnavailableException(text.Length > 0 ? text.ToString() : "Não foi possível conectar. Confira a senha e a segurança da rede.");
            }
        }
        finally { WlanRegisterNotification(client, 0, true, null, 0, 0, out _); GC.KeepAlive(callback); }
    }
    internal Task<WifiSnapshot> ConnectAsync(WifiNetwork network, SecureString password) => worker.Run(() => WithClient(client =>
    {
        Guid adapter = network.Adapter; nint profile = 0; int bytes = 0;
        try
        {
            uint mode;
            if (network.Profile.Length > 0) { profile = Marshal.StringToHGlobalUni(network.Profile); bytes = (network.Profile.Length + 1) * 2; mode = 0; }
            else { if (!network.CanCreateProfile) throw new WifiUnavailableException("Esta rede exige um perfil corporativo ou uma segurança não suportada pelo editor. Use um perfil já configurado no Windows."); (profile, bytes) = TemporaryProfile(network, password); mode = 1; }
            var connection = new Connection { Mode = mode, Profile = profile, BssType = 1 };
            Wait(client, adapter, new uint[] { 10, 11 }, () => Check(WlanConnect(client, ref adapter, ref connection, 0)), 22000, true);
            var state = Read(client, false);
            if (!state.Networks.Any(item => item.Key == network.Key && item.Connected)) throw new WifiUnavailableException("A conexão não foi confirmada para a rede selecionada.");
            return state;
        }
        finally { if (profile != 0) { for (int i = 0; i < bytes; i++) Marshal.WriteByte(profile, i, 0); Marshal.FreeHGlobal(profile); } password.Dispose(); }
    }));
    private static (nint, int) TemporaryProfile(WifiNetwork network, SecureString secret)
    {
        bool key64 = secret.Length == 64 && network.Auth == 7; nint password = Marshal.SecureStringToCoTaskMemUnicode(secret);
        char[]? buffer = null;
        try
        {
            if (network.Secure)
            {
                if (secret.Length is < 8 or > 64 || (secret.Length == 64 && !key64)) throw new WifiUnavailableException("A senha deve conter de 8 a 63 caracteres, ou 64 dígitos hexadecimais no WPA2.");
                if (key64) for (int i = 0; i < secret.Length; i++) if (!Uri.IsHexDigit((char)Marshal.ReadInt16(password, i * 2))) throw new WifiUnavailableException("A chave WPA2 de 64 caracteres deve ser hexadecimal.");
            }
            string auth = network.Secure ? network.Auth == 9 ? "WPA3SAE" : "WPA2PSK" : "open";
            string prefix = "<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>ThinkWind-" + Convert.ToHexString(network.Ssid) + "</name><SSIDConfig><SSID><hex>" + Convert.ToHexString(network.Ssid) + "</hex></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>manual</connectionMode><MSM><security><authEncryption><authentication>" + auth + "</authentication><encryption>" + (network.Secure ? "AES" : "none") + "</encryption><useOneX>false</useOneX></authEncryption>";
            if (network.Secure) prefix += "<sharedKey><keyType>" + (key64 ? "networkKey" : "passPhrase") + "</keyType><protected>false</protected><keyMaterial>";
            string suffix = (network.Secure ? "</keyMaterial></sharedKey>" : "") + "</security></MSM></WLANProfile>";
            buffer = new char[prefix.Length + secret.Length * 6 + suffix.Length + 1]; prefix.CopyTo(0, buffer, 0, prefix.Length); int at = prefix.Length;
            if (network.Secure) for (int i = 0; i < secret.Length; i++)
            {
                char character = (char)Marshal.ReadInt16(password, i * 2);
                string? escaped = character switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", '\"' => "&quot;", '\'' => "&apos;", _ => null };
                if (escaped is null) buffer[at++] = character; else { escaped.CopyTo(0, buffer, at, escaped.Length); at += escaped.Length; }
            }
            suffix.CopyTo(0, buffer, at, suffix.Length); at += suffix.Length;
            nint native = Marshal.AllocHGlobal((at + 1) * 2); Marshal.Copy(buffer, 0, native, at + 1); return (native, (at + 1) * 2);
        }
        finally { if (buffer is not null) Array.Clear(buffer); Marshal.ZeroFreeCoTaskMemUnicode(password); }
    }
    internal Task<WifiSnapshot> DisconnectAsync(Guid adapter) => worker.Run(() => WithClient(client =>
    {
        Wait(client, adapter, new uint[] { 21 }, () => Check(WlanDisconnect(client, ref adapter, 0)), 8000, false);
        var result = Read(client, false);
        if (result.Networks.Any(network => network.Adapter == adapter && network.Connected)) throw new WifiUnavailableException("O Windows ainda informa uma conexão ativa neste adaptador.");
        return result;
    }));
    internal Task<WifiSnapshot> SetRadioAsync(Guid adapter, bool enabled) => worker.Run(() => WithClient(client =>
    {
        var radios = ReadRadio(client, adapter); if (radios.Count == 0 || radios.Any(radio => radio.Hardware != 1)) throw new WifiUnavailableException("O adaptador está bloqueado por hardware, modo avião ou política do Windows.");
        foreach (var item in radios) { var state = item; state.Software = enabled ? 1u : 2u; Check(WlanSetInterface(client, ref adapter, 4, 12, ref state, 0)); }
        var actual = ReadRadio(client, adapter);
        if (actual.Count == 0 || actual.Any(state => state.Software != (enabled ? 1u : 2u))) throw new WifiUnavailableException("O Windows não confirmou a alteração do rádio Wi-Fi.");
        return Read(client, false);
    }));
    public void Dispose() => worker.Dispose();
}
