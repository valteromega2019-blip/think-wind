using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ThinkWind.Services;
internal sealed record BrightnessState(string Monitor, uint Minimum, uint Maximum, uint Value);
internal static class BrightnessService
{
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct PhysicalMonitor
    { internal nint Handle; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] internal string Description; }
    [DllImport("user32.dll")] private static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("dxva2.dll")] private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);
    [DllImport("dxva2.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, [Out] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll")] private static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] physical);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorCapabilities(nint monitor, out uint capabilities, out uint temperatures);
    [DllImport("dxva2.dll")] private static extern bool GetMonitorBrightness(nint monitor, out uint minimum, out uint value, out uint maximum);
    [DllImport("dxva2.dll")] private static extern bool SetMonitorBrightness(nint monitor, uint value);
    internal static Task<BrightnessState?> ReadAsync(Rectangle bounds) => Task.Run(() => WithMonitor(bounds, null, null));
    internal static Task<BrightnessState?> SetAsync(Rectangle bounds, BrightnessState target, double percent) => Task.Run(() => WithMonitor(bounds, target, percent));
    private static BrightnessState? WithMonitor(Rectangle bounds, BrightnessState? target, double? percent)
    {
        nint monitor = MonitorFromPoint(new Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 }, 0);
        if (monitor == 0 || !GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count is 0 or > 16) return null;
        var physical = new PhysicalMonitor[checked((int)count)];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, physical)) return null;
        try
        {
            foreach (var item in physical)
            {
                if (target is not null && target.Monitor != item.Description) continue;
                if (!GetMonitorCapabilities(item.Handle, out var flags, out _) || (flags & 2) == 0 || !GetMonitorBrightness(item.Handle, out var min, out var value, out var max) || max <= min) continue;
                if (percent is double requested)
                {
                    uint next = (uint)Math.Round(min + (max - min) * Math.Clamp(requested, 0, 100) / 100);
                    if (!SetMonitorBrightness(item.Handle, next) || !GetMonitorBrightness(item.Handle, out min, out value, out max) || max <= min) return null;
                }
                return new(item.Description, min, max, value);
            }
            return null;
        }
        finally { DestroyPhysicalMonitors(count, physical); }
    }
}
