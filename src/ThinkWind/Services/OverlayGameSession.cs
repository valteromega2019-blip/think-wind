using System;
using System.Runtime.InteropServices;

namespace OrbyLauncher.Services;

// Retain a process handle, not just its PID (which Windows can reuse).
internal sealed class OverlayGameSession : IDisposable
{
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(nint handle, uint milliseconds);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    private nint process, window;
    internal int ProcessId { get; private set; }
    internal bool Update(ForegroundGame game, bool recognized)
    {
        int before = ProcessId;
        if (ProcessId != 0)
        {
            GetWindowThreadProcessId(window, out uint owner);
            bool alive = process != 0 ? WaitForSingleObject(process, 0) == 258
                : IsWindow(window) && owner == ProcessId;
            if (!alive) Dispose();
        }
        if (recognized && game.Handle != 0)
        {
            GetWindowThreadProcessId(game.Handle, out uint pid);
            if (pid != 0 && pid != ProcessId)
            {
                Dispose();
                process = OpenProcess(0x100000, false, pid); // SYNCHRONIZE only; never read game memory.
                ProcessId = (int)pid; window = game.Handle;
            }
        }
        return before != ProcessId;
    }
    public void Dispose()
    {
        if (process != 0) CloseHandle(process);
        process = 0; window = 0; ProcessId = 0;
    }
}
