using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ThinkWind.Native;
internal static class Desktop
{
    private delegate bool EnumProc(nint hwnd, nint param);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint param);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint hwnd, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, INPUT[] input, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint hwnd, ref MARGINS margins);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORY status);
    [StructLayout(LayoutKind.Sequential)] private struct MARGINS { public int Left, Right, Top, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MEMORY
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION
    { [FieldOffset(0)] public KEYBOARD Keyboard; [FieldOffset(0)] public MOUSE Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBOARD { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSE { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    internal sealed record RunningWindow(nint Handle, string Title, string? Executable);
    internal static List<RunningWindow> Windows()
    {
        var result = new List<RunningWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4) != 0 || (GetWindowLong(hwnd, -20) & 0x80) != 0) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == Environment.ProcessId) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out var cloaked, 4) == 0 && cloaked != 0) return true;
            var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
            if (text.Length == 0) return true;
            string? executable = null;
            try { using var process = Process.GetProcessById((int)pid); executable = process.MainModule?.FileName; }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException) { }
            result.Add(new(hwnd, text.ToString(), executable)); return true;
        }, 0);
        return result;
    }
    internal static void Activate(nint hwnd)
    {
        if (GetForegroundWindow() == hwnd) ShowWindow(hwnd, 6);
        else { if (IsIconic(hwnd)) ShowWindow(hwnd, 9); SetForegroundWindow(hwnd); }
    }
    internal static void Key(ushort key)
    {
        var inputs = new[] { new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBOARD { Key = key } } }, new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBOARD { Key = key, Flags = 2 } } } };
        if (SendInput(2, inputs, Marshal.SizeOf<INPUT>()) != 2) throw new InvalidOperationException("O Windows bloqueou o comando de teclado.");
    }
    internal static uint MemoryPercent()
    { var status = new MEMORY { Length = (uint)Marshal.SizeOf<MEMORY>() }; return GlobalMemoryStatusEx(ref status) ? status.Load : 0; }
    internal static bool Acrylic(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        HwndSource.FromHwnd(hwnd).CompositionTarget.BackgroundColor = Colors.Transparent;
        var margins = new MARGINS { Left = -1 }; DwmExtendFrameIntoClientArea(hwnd, ref margins);
        int dark = 1, round = 2, material = 3;
        DwmSetWindowAttribute(hwnd, 20, ref dark, 4); DwmSetWindowAttribute(hwnd, 33, ref round, 4);
        return DwmSetWindowAttribute(hwnd, 38, ref material, 4) == 0;
    }
}
