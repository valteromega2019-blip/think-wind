using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ThinkWind.Native;

internal sealed class OverlayHotkey : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    private readonly HwndSource source;
    private readonly Action toggle;
    internal OverlayHotkey(Action action)
    {
        toggle = action;
        source = new HwndSource(new HwndSourceParameters("Think Wind — Overlay hotkey") { ParentWindow = new nint(-3), Width = 0, Height = 0 });
        source.AddHook(Message);
        if (!RegisterHotKey(source.Handle, 2, 0x4006, 0x4F)) { int error = Marshal.GetLastWin32Error(); source.Dispose(); throw new Win32Exception(error); }
    }
    private nint Message(nint window, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x312 && wParam == 2) { handled = true; source.Dispatcher.BeginInvoke(toggle); } return 0; }
    public void Dispose() { UnregisterHotKey(source.Handle, 2); source.RemoveHook(Message); source.Dispose(); }
}
