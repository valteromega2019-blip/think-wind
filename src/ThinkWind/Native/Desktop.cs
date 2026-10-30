using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ThinkWind.Native;
internal static class Desktop
{
    private static nint lastIslandApplication;
    private static System.Drawing.Rectangle lastIslandMonitor;
    private static nint maximizedIslandWindow;
    private static System.Drawing.Rectangle maximizedIslandMonitor;
    private static DateTime nextMaximizedSearch;
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
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint hwnd);
    [DllImport("user32.dll")] private static extern bool IsWindowArranged(nint hwnd);
    private static bool arrangedAvailable = true;
    private static bool IsSnapped(nint window)
    {
        if (!arrangedAvailable) return false;
        try { return IsWindowArranged(window); }
        catch (EntryPointNotFoundException) { arrangedAvailable = false; return false; }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder name, int count);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] private static extern uint SendInput(uint count, INPUT[] input, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORY status);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder path, ref uint size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)] private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? id);
    [StructLayout(LayoutKind.Sequential)] private struct MEMORY
    { public uint Length, Load; public ulong TotalPhysical, AvailablePhysical, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint Type; public INPUTUNION Data; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION
    { [FieldOffset(0)] public KEYBOARD Keyboard; [FieldOffset(0)] public MOUSE Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBOARD { public ushort Key, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSE { public int X, Y; public uint Data, Flags, Time; public nuint Extra; }
    internal sealed record RunningWindow(nint Handle, string Title, string? Executable, bool Minimized, string? AppId = null);
    internal static List<RunningWindow> Windows()
    {
        var result = new List<RunningWindow>();
        var identities = new Dictionary<uint, string?>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4) != 0 || (GetWindowLong(hwnd, -20) & 0x80) != 0) return true;
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == Environment.ProcessId) return true;
            if (DwmGetWindowAttribute(hwnd, 14, out var cloaked, 4) == 0 && cloaked != 0) return true;
            var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
            if (text.Length == 0) return true;
            string? executable = null;
            try
            {
                // Limited-query access also works across process bitness and
                // avoids MainModule's stronger process-memory permissions.
                nint process = OpenProcess(0x1000, false, pid);
                if (process != 0) try
                {
                    var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
                    if (QueryFullProcessImageName(process, 0, path, ref length)) executable = path.ToString();
                }
                finally { CloseHandle(process); }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException) { }
            if (!identities.TryGetValue(pid, out var appId)) { appId = ReadAppId(pid); identities[pid] = appId; }
            result.Add(new(hwnd, text.ToString(), executable, IsIconic(hwnd), appId)); return true;
        }, 0);
        return result;
    }
    private static string? ReadAppId(uint processId)
    {
        nint process = OpenProcess(0x1000, false, processId); if (process == 0) return null;
        try
        {
            uint length = 0; if (GetApplicationUserModelId(process, ref length, null) != 122 || length is 0 or > 1024) return null;
            var id = new StringBuilder((int)length); return GetApplicationUserModelId(process, ref length, id) == 0 ? id.ToString() : null;
        }
        finally { CloseHandle(process); }
    }
    internal static void Activate(nint hwnd)
    {
        if (GetForegroundWindow() == hwnd) ShowWindow(hwnd, 6);
        else { if (IsIconic(hwnd)) ShowWindow(hwnd, 9); SetForegroundWindow(hwnd); }
    }
    internal static bool RestoreAndActivate(nint hwnd)
    { if (IsIconic(hwnd)) ShowWindow(hwnd, 9); return GetForegroundWindow() == hwnd || SetForegroundWindow(hwnd); }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    internal static bool LeftButtonDown => (GetAsyncKeyState(1) & 0x8000) != 0;
    internal static bool EscapeDown => (GetAsyncKeyState(0x1B) & 0x8000) != 0;
    internal static bool ForegroundIsOurs()
    { GetWindowThreadProcessId(GetForegroundWindow(), out var process); return process == Environment.ProcessId; }
    internal static bool? ForegroundHasApplication(OverlayChrome.Display display)
    {
        var window = GetForegroundWindow(); if (window == 0) return null;
        GetWindowThreadProcessId(window, out var process);
        if (process == Environment.ProcessId) return null;
        var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return null;
        if (name.ToString() is "Progman" or "WorkerW")
            return display.Bounds.Contains(System.Windows.Forms.Cursor.Position) ? false : null;
        if (System.Windows.Forms.Screen.FromHandle(window).Bounds != display.Bounds) return null;
        if (IsIconic(window) || !IsWindowVisible(window)) return null;
        return true;
    }
    internal static bool? ForegroundCoversDisplay(OverlayChrome.Display display)
    {
        var window = GetForegroundWindow();
        if (window == 0) return LastIslandCoverage(display);
        GetWindowThreadProcessId(window, out var process);
        if (process == Environment.ProcessId) return LastIslandCoverage(display);
        var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
        if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return LastIslandCoverage(display);
        if (name.ToString() is "Progman" or "WorkerW")
        {
            if (!display.Bounds.Contains(System.Windows.Forms.Cursor.Position)) return LastIslandCoverage(display);
            lastIslandApplication = 0; lastIslandMonitor = display.Bounds;
            return HasMaximizedApplication(display);
        }
        if (System.Windows.Forms.Screen.FromHandle(window).Bounds != display.Bounds) return LastIslandCoverage(display);
        lastIslandApplication = window; lastIslandMonitor = display.Bounds;
        return WindowCoversDisplay(window, display) == true || HasMaximizedApplication(display);
    }
    private static bool? LastIslandCoverage(OverlayChrome.Display display)
    {
        if (lastIslandMonitor == display.Bounds && lastIslandApplication != 0
            && WindowCoversDisplay(lastIslandApplication, display) == true) return true;
        return HasMaximizedApplication(display);
    }
    private static bool HasMaximizedApplication(OverlayChrome.Display display)
    {
        if (maximizedIslandMonitor != display.Bounds)
        { maximizedIslandMonitor = display.Bounds; maximizedIslandWindow = 0; nextMaximizedSearch = DateTime.MinValue; }
        bool Eligible(nint window)
        {
            // Borderless fullscreen windows are neither zoomed nor snapped.
            // Evaluate coverage as well so a small foreground window cannot
            // erase a fullscreen application still visible behind it.
            if (window == 0 || !IsWindowVisible(window) || IsIconic(window)) return false;
            if ((GetWindowLong(window, -20) & 0x80) != 0 || GetWindow(window, 4) != 0) return false;
            GetWindowThreadProcessId(window, out var pid); if (pid == Environment.ProcessId) return false;
            if (DwmGetWindowAttribute(window, 14, out var cloaked, 4) >= 0 && cloaked != 0) return false;
            return WindowCoversDisplay(window, display) == true;
        }
        // Reuse a confirmed maximized HWND even when a small window takes focus.
        if (Eligible(maximizedIslandWindow)) return true;
        maximizedIslandWindow = 0;
        if (DateTime.UtcNow < nextMaximizedSearch) return false;
        nextMaximizedSearch = DateTime.UtcNow.AddMilliseconds(250);
        EnumWindows((window, _) =>
        {
            if (!Eligible(window)) return true;
            maximizedIslandWindow = window; return false;
        }, 0);
        return maximizedIslandWindow != 0;
    }
    private static bool? WindowCoversDisplay(nint window, OverlayChrome.Display display)
    {
        if (!IsWindowVisible(window) || IsIconic(window)) return false;
        if (System.Windows.Forms.Screen.FromHandle(window).Bounds != display.Bounds) return false;
        // Windows Snap is a distinct state: arranged windows are not maximized,
        // but must keep the island in the same application/status-strip mode.
        if (IsSnapped(window)) return true;
        if (!OverlayChrome.GetWindowRect(window, out var rect)) return null;
        // The appbar changes the working area asynchronously. The native
        // maximized state remains authoritative during that resize.
        if (IsZoomed(window)) return true;
        var bounds = display.Bounds;
        bool fullscreen = rect.Left <= bounds.Left + 2 && rect.Right >= bounds.Right - 2 && rect.Top <= bounds.Top + 2 && rect.Bottom >= bounds.Bottom - 2;
        return fullscreen;
    }
    internal static void Key(ushort key)
    {
        var inputs = new[] { new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBOARD { Key = key } } }, new INPUT { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBOARD { Key = key, Flags = 2 } } } };
        if (SendInput(2, inputs, Marshal.SizeOf<INPUT>()) != 2) throw new InvalidOperationException("O Windows bloqueou o comando de teclado.");
    }
    internal static void Chord(ushort modifier, ushort key)
    {
        static INPUT Input(ushort value, uint flags) => new() { Type = 1, Data = new INPUTUNION { Keyboard = new KEYBOARD { Key = value, Flags = flags } } };
        var inputs = new[] { Input(modifier, 0), Input(key, 0), Input(key, 2), Input(modifier, 2) };
        if (SendInput(4, inputs, Marshal.SizeOf<INPUT>()) == 4) return;
        var release = new[] { Input(key, 2), Input(modifier, 2) };
        SendInput(2, release, Marshal.SizeOf<INPUT>());
        throw new InvalidOperationException("O Windows bloqueou o atalho solicitado.");
    }
    internal static uint MemoryPercent()
    { var status = new MEMORY { Length = (uint)Marshal.SizeOf<MEMORY>() }; return GlobalMemoryStatusEx(ref status) ? status.Load : 0; }
}
