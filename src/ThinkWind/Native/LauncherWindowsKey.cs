using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace ThinkWind.Native;

// The hook owns a dedicated message pump, never the WPF thread. Only an isolated
// Windows key is consumed. Windows shortcuts are replayed together, in order.
internal sealed class LauncherWindowsKey : IDisposable
{
    private delegate nint KeyboardHook(int code, nint message, nint data);
    [StructLayout(LayoutKind.Sequential)] private struct Key { internal uint Virtual, Scan, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Message { internal nint Window; internal uint Id; internal nuint WParam; internal nint LParam; internal uint Time; internal int X, Y; internal uint Private; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { internal ushort Virtual, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] internal KeyboardInput Keyboard; [FieldOffset(0)] internal MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { internal uint Type; internal InputUnion Data; }
    [DllImport("user32.dll")] private static extern nint SetWindowsHookEx(int type, KeyboardHook callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern int GetMessage(out Message message, nint window, uint minimum, uint maximum);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out Message message, nint window, uint minimum, uint maximum, uint flags);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern short GetKeyState(int key);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, Input[] input, int size);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    private readonly Dispatcher ui;
    private readonly Action open;
    private readonly KeyboardHook callback;
    private volatile bool disposed;
    private uint threadId, pending, forwardedWindows;
    private nint hook;
    internal bool IsActive => hook != 0 && !disposed;
    internal LauncherWindowsKey(Dispatcher dispatcher, Action showStart)
    {
        ui = dispatcher; open = showStart; callback = Handle;
        new Thread(Run) { IsBackground = true, Name = "Think Wind — Windows key" }.Start();
    }
    private void Run()
    {
        threadId = GetCurrentThreadId(); PeekMessage(out _, 0, 0, 0, 0);
        if (disposed) return;
        hook = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (hook == 0) return;
        try
        {
            while (!disposed && GetMessage(out var message, 0, 0, 0) > 0)
                if (message.Id == 0x8001 && !ui.HasShutdownStarted)
                    ui.BeginInvoke(new Action(() => { if (!disposed) open(); }));
        }
        finally { UnhookWindowsHookEx(hook); hook = 0; }
    }
    private static Input InputKey(uint key, uint flags = 0, uint scan = 0) => new()
    { Type = 1, Data = new InputUnion { Keyboard = new KeyboardInput { Virtual = (ushort)key, Scan = (ushort)scan, Flags = flags } } };
    private nint Handle(int code, nint message, nint data)
    {
        if (code < 0 || disposed) return CallNextHookEx(hook, code, message, data);
        var key = Marshal.PtrToStructure<Key>(data);
        if ((key.Flags & 0x12) != 0) return CallNextHookEx(hook, code, message, data);
        bool down = message == 0x100 || message == 0x104;
        bool up = message == 0x101 || message == 0x105;
        bool windows = key.Virtual is 0x5B or 0x5C;
        uint bit = key.Virtual == 0x5B ? 1u : key.Virtual == 0x5C ? 2u : 0;
        if (windows && (forwardedWindows & bit) != 0)
        {
            if (up) forwardedWindows &= ~bit;
            return CallNextHookEx(hook, code, message, data);
        }
        if (windows && down && pending == 0)
        {
            if (GetKeyState(0x10) < 0 || GetKeyState(0x11) < 0 || GetKeyState(0x12) < 0)
            { forwardedWindows |= bit; return CallNextHookEx(hook, code, message, data); }
            pending = key.Virtual; return 1;
        }
        if (windows && pending == key.Virtual)
        {
            if (up) { pending = 0; PostThreadMessage(threadId, 0x8001, 0, 0); }
            return 1;
        }
        if (pending != 0 && down)
        {
            // Reproduce only the held Win modifier and this shortcut's keydown.
            // Its real keyup, and every ordinary key, continue through Windows.
            uint modifier = pending;
            var inputs = new[] { InputKey(modifier, 1), InputKey(key.Virtual, key.Flags & 1, key.Scan) };
            pending = 0;
            uint sent = SendInput(2, inputs, Marshal.SizeOf<Input>());
            if (sent > 0) forwardedWindows |= modifier == 0x5B ? 1u : 2u;
            if (sent == 2) { if (windows) forwardedWindows |= bit; return 1; }
        }
        return CallNextHookEx(hook, code, message, data);
    }
    public void Dispose() { disposed = true; if (threadId != 0) PostThreadMessage(threadId, 0x12, 0, 0); }
}
