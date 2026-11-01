using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace OrbyLauncher.Native;
// On-demand injection only. No hooks, timers, key-state polling or elevated UI.
internal static class VirtualKeyboardInput
{
    [StructLayout(LayoutKind.Sequential)] private struct Input { internal uint Type; internal Union Data; }
    [StructLayout(LayoutKind.Explicit)] private struct Union
    { [FieldOffset(0)] internal Keyboard Keyboard; [FieldOffset(0)] internal Mouse Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct Keyboard { internal ushort Virtual, Scan; internal uint Flags, Time; internal nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Mouse { internal int X, Y; internal uint Data, Flags, Time; internal nuint Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern short VkKeyScanEx(char character, nint layout);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    internal static nint LastExternalWindow { get; private set; }
    internal static void RememberTarget()
    {
        nint window = GetForegroundWindow(); GetWindowThreadProcessId(window, out uint process);
        if (window != 0 && process != Environment.ProcessId) LastExternalWindow = window;
    }
    internal static void RestoreTarget()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint process);
        if (process == Environment.ProcessId && IsWindow(LastExternalWindow)) SetForegroundWindow(LastExternalWindow);
    }
    private static Input Key(ushort key, bool up = false, ushort scan = 0, bool unicode = false) => new()
    { Type = 1, Data = new Union { Keyboard = new Keyboard { Virtual = key, Scan = scan, Flags = (up ? 2u : 0u) | (unicode ? 4u : key is >= 0x21 and <= 0x2E or 0x5B or 0x5C or 0x6F ? 1u : 0u) } } };
    internal static bool Send(string? text, ushort virtualKey, bool control, bool alt, bool windows, bool shift)
    {
        var input = new List<Input>(); var modifiers = new List<ushort>();
        ushort key = virtualKey;
        if (text is { Length: 1 } && (control || alt || windows))
        {
            uint thread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
            short mapped = VkKeyScanEx(text[0], GetKeyboardLayout(thread)); if (mapped == -1) return false;
            key = (ushort)(mapped & 255); shift |= (mapped & 0x100) != 0;
            control |= (mapped & 0x200) != 0; alt |= (mapped & 0x400) != 0; text = null;
        }
        if (control) modifiers.Add(0x11); if (alt) modifiers.Add(0x12); if (windows) modifiers.Add(0x5B); if (shift && text is null) modifiers.Add(0x10);
        // One snapshot per explicit click, never a polling loop. Do not release
        // a modifier that the user is already holding on the physical keyboard.
        modifiers.RemoveAll(modifier => GetAsyncKeyState(modifier) < 0);
        foreach (ushort modifier in modifiers) input.Add(Key(modifier));
        if (text is not null)
            foreach (char character in text) { input.Add(Key(0, scan: character, unicode: true)); input.Add(Key(0, true, character, true)); }
        else { input.Add(Key(key)); input.Add(Key(key, true)); }
        for (int i = modifiers.Count - 1; i >= 0; i--) input.Add(Key(modifiers[i], true));
        var events = input.ToArray(); uint sent = SendInput((uint)events.Length, events, Marshal.SizeOf<Input>());
        if (sent == events.Length) return true;
        // Release only synthetic keys that actually entered the input stream.
        var held = new List<Input>();
        for (int i = 0; i < sent; i++)
        {
            var item = events[i];
            if ((item.Data.Keyboard.Flags & 2) == 0) held.Add(item);
            else held.RemoveAll(x => x.Data.Keyboard.Virtual == item.Data.Keyboard.Virtual && x.Data.Keyboard.Scan == item.Data.Keyboard.Scan);
        }
        held.Reverse();
        for (int i = 0; i < held.Count; i++) { var item = held[i]; item.Data.Keyboard.Flags |= 2; held[i] = item; }
        if (held.Count > 0) SendInput((uint)held.Count, held.ToArray(), Marshal.SizeOf<Input>());
        return false;
    }
}
