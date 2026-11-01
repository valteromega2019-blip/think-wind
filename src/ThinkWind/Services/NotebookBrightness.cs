using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace OrbyLauncher.Services;
internal static class NotebookBrightness
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { internal uint Size; internal RectangleNative Bounds, Work; internal uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device; }
    [StructLayout(LayoutKind.Sequential)] private struct RectangleNative { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayDevice
    {
        internal uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Description;
        internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Key;
    }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice display, uint flags);
    internal static string Identity(string value)
    {
        string[] parts = value.Split('#');
        if (parts.Length >= 3) value = "DISPLAY\\" + parts[1] + "\\" + parts[2];
        value = value.Replace("MONITOR\\", "DISPLAY\\", StringComparison.OrdinalIgnoreCase);
        return Regex.Replace(value, @"_\d+$", "").ToUpperInvariant();
    }
    private static HashSet<string> SelectedIds(Rectangle bounds)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        nint monitor = MonitorFromPoint(new Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 }, 0);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) return ids;
        for (uint i = 0; i < 16; i++)
        {
            var device = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(info.Device, i, ref device, 1)) break; // EDD_GET_DEVICE_INTERFACE_NAME
            if ((device.Flags & 1) != 0 && !string.IsNullOrWhiteSpace(device.Id)) ids.Add(Identity(device.Id));
        }
        return ids;
    }
    private static ManagementObjectSearcher Search(string className) => new(new ManagementScope(@"\\.\root\wmi", new ConnectionOptions { Timeout = TimeSpan.FromSeconds(2) }),
        new ObjectQuery("SELECT * FROM " + className), new EnumerationOptions { Timeout = TimeSpan.FromSeconds(2), ReturnImmediately = true });
    internal static BrightnessState? Access(Rectangle bounds, BrightnessState? target, double? percent)
    {
        try
        {
            var ids = SelectedIds(bounds); if (ids.Count == 0) return null;
            if (target is not null && !ids.Contains(Identity(target.Id))) return null;
            using var search = Search("WmiMonitorBrightness"); using var results = search.Get();
            BrightnessState? selected = null;
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    string id = Convert.ToString(item["InstanceName"]) ?? "";
                    if (item["Active"] is not true || !ids.Contains(Identity(id))) continue;
                    if (target is not null && id != target.Id) continue;
                    if (selected is not null) return null; // Mirrored/ambiguous panels: do not guess.
                    var levels = (item["Level"] as byte[] ?? Array.Empty<byte>()).Distinct().OrderBy(level => level).ToArray();
                    if (levels.Length < 2) return null;
                    selected = new("Painel do notebook", levels[0], levels[^1], Convert.ToByte(item["CurrentBrightness"]), "WMI", id, levels);
                }
            }
            if (selected is null || percent is null) return selected;
            if (!double.IsFinite(percent.Value)) return null;
            // WMI brightness is already an absolute percentage, not DDC's raw range.
            byte requested = selected.Levels!.OrderBy(level => Math.Abs(level - Math.Clamp(percent.Value, 0, 100))).First();
            using var methods = Search("WmiMonitorBrightnessMethods"); using var candidates = methods.Get();
            foreach (ManagementObject method in candidates)
            {
                using (method)
                {
                    if (method["Active"] is not true || Convert.ToString(method["InstanceName"]) != selected.Id) continue;
                    using var input = method.GetMethodParameters("WmiSetBrightness"); input["Timeout"] = 0u; input["Brightness"] = requested;
                    using var output = method.InvokeMethod("WmiSetBrightness", input, new InvokeMethodOptions { Timeout = TimeSpan.FromSeconds(2) });
                    if (output is null || Convert.ToUInt32(output["ReturnValue"]) != 0) return null;
                    // Read back from the provider; never report the requested value as confirmed.
                    return Access(bounds, selected, null);
                }
            }
            return null;
        }
        catch (Exception e) when (e is ManagementException or COMException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException or ArgumentException or OverflowException or FormatException or TimeoutException)
        { return null; }
    }
}
