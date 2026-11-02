using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OrbyLauncher.Native;

// Observe button presses only while the menu is visible. No hooks, capture,
// NOLEGACY flag or input suppression; normal mouse delivery stays with Windows.
internal sealed class OutsideClickObserver : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct Device { internal ushort Page, Usage; internal uint Flags; internal nint Target; }
    [StructLayout(LayoutKind.Sequential)] private struct Header { internal uint Type, Size; internal nint Device, Parameter; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { internal int X, Y; }
    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices(Device[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(nint input, uint command, nint buffer, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    private readonly HwndSource source;
    private readonly Action outside;
    private readonly Func<bool>? inside;
    private static readonly List<OutsideClickObserver> observers = new();
    private readonly nint buffer = Marshal.AllocHGlobal(64);
    private bool active, disposed, queued;
    internal OutsideClickObserver(Window window, Action onOutside, Func<bool>? isInside = null)
    { source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); outside = onOutside; inside = isInside; source.AddHook(Message); }
    internal void Start()
    {
        if (disposed || active) return;
        active = RegisterRawInputDevices(new[] { new Device { Page = 1, Usage = 2, Flags = 0x100, Target = source.Handle } }, 1, (uint)Marshal.SizeOf<Device>());
        if (active) observers.Add(this);
    }
    internal void Stop()
    {
        if (!active) return;
        observers.Remove(this);
        var remaining = observers.LastOrDefault();
        RegisterRawInputDevices(new[] { new Device { Page = 1, Usage = 2, Flags = remaining is null ? 1u : 0x100u, Target = remaining?.source.Handle ?? 0 } }, 1, (uint)Marshal.SizeOf<Device>());
        active = false;
    }
    private nint Message(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0xFF || !active || disposed || queued) return 0;
        uint size = 64, header = (uint)Marshal.SizeOf<Header>();
        uint copied = GetRawInputData(lParam, 0x10000003, buffer, ref size, header);
        if (copied == uint.MaxValue || copied < header + 8 || Marshal.ReadInt32(buffer) != 0) return 0;
        // RAWMOUSE.usButtonFlags follows the aligned button union at offset 4.
        if ((Marshal.ReadInt16(buffer, (int)header + 4) & 0x155) == 0 || !GetCursorPos(out var point)) return 0;
        GetWindowThreadProcessId(WindowFromPoint(point), out uint process);
        if (process == 0) return 0;
        queued = true;
        source.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            queued = false;
            // One native mouse subscription shared by the visible panels.
            foreach (var observer in observers.ToArray())
                if (observer.active && !observer.disposed && (observer.inside is not null ? !observer.inside() : process != Environment.ProcessId)) observer.outside();
        }));
        return 0; // Let the native default procedure clean up WM_INPUT.
    }
    public void Dispose()
    {
        if (disposed) return;
        Stop(); disposed = true; source.RemoveHook(Message); Marshal.FreeHGlobal(buffer);
    }
}
