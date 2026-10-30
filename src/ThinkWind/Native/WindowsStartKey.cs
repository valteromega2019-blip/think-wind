using System;
using System.ComponentModel;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace ThinkWind.Native;
// RegisterHotKey is opt-in. No ordinary input is intercepted or re-injected.
internal sealed class WindowsStartKey : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    private readonly HwndSource source;
    private readonly Action open;
    internal WindowsStartKey(Action showStart)
    {
        open = showStart;
        source = new HwndSource(new HwndSourceParameters("Think Wind — Hotkey") { ParentWindow = new nint(-3), Width = 0, Height = 0 });
        source.AddHook(Message);
        if (!RegisterHotKey(source.Handle, 1, 0x4003, 0x20)) { int error = Marshal.GetLastWin32Error(); source.Dispose(); throw new Win32Exception(error); }
    }
    private nint Message(nint window, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x312 && wParam == 1) { handled = true; source.Dispatcher.BeginInvoke(open); } return 0; }
    public void Dispose() { UnregisterHotKey(source.Handle, 1); source.RemoveHook(Message); source.Dispose(); }
}
