using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ThinkWind.Native;

// Observe no text and retain no key history. Only standalone Windows presses
// are redirected; combinations keep the shell's original key-down sequence.
internal sealed class WindowsStartKey : IDisposable
{
    private delegate nint Hook(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Key { internal uint VirtualKey, Scan, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { internal uint Type; internal InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] internal Keyboard Keyboard; [FieldOffset(0)] internal Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { internal ushort Key, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int kind, Hook callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    private readonly Hook callback;
    private readonly Dispatcher dispatcher;
    private readonly Action open;
    private nint handle;
    private uint pressed;
    private bool combination;
    internal WindowsStartKey(Dispatcher ui, Action showStart)
    {
        dispatcher = ui; open = showStart; callback = Process;
        handle = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    private static Input Stroke(ushort key, uint flags) => new() { Type = 1, Data = new InputUnion { Keyboard = new Keyboard { Key = key, Flags = flags } } };
    private nint Process(int code, nint message, nint data)
    {
        if (code < 0) return CallNextHookEx(handle, code, message, data);
        var key = Marshal.PtrToStructure<Key>(data);
        if ((key.Flags & 0x10) != 0) return CallNextHookEx(handle, code, message, data);
        bool down = message == 0x100 || message == 0x104, up = message == 0x101 || message == 0x105;
        if (key.VirtualKey is 0x5B or 0x5C)
        {
            if (down && pressed == 0)
            { pressed = key.VirtualKey; combination = (GetAsyncKeyState(0x10) & 0x8000) != 0 || (GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0; }
            else if (down && pressed != key.VirtualKey) combination = true;
            if (up && pressed == key.VirtualKey)
            {
                pressed = 0;
                if (!combination && !dispatcher.HasShutdownStarted)
                {
                    // A neutral modifier marks the shell's Win sequence as used.
                    // Send the release in the same batch; never leave Win held.
                    var strokes = new[] { Stroke(0x11, 0), Stroke(0x11, 2), Stroke((ushort)key.VirtualKey, 3) };
                    if (SendInput((uint)strokes.Length, strokes, Marshal.SizeOf<Input>()) == strokes.Length)
                    { dispatcher.BeginInvoke(DispatcherPriority.Input, open); return 1; }
                }
            }
        }
        else if (down && pressed != 0) combination = true;
        return CallNextHookEx(handle, code, message, data);
    }
    public void Dispose() { if (handle != 0) { UnhookWindowsHookEx(handle); handle = 0; } }
}
