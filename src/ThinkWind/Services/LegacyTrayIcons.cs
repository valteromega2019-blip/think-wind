using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ThinkWind.Native;

namespace ThinkWind.Services;

internal static class LegacyTrayIcons
{
    [StructLayout(LayoutKind.Sequential)] private struct Identifier { internal uint Size; internal nint Window; internal uint Id; internal Guid Guid; }
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? type, string? title);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);
    [DllImport("shell32.dll")] private static extern int Shell_NotifyIconGetRect(ref Identifier identifier, out OverlayChrome.PixelRect rectangle);

    internal static List<(TrayIconMetadata Icon, nint Window, uint Process)> Read(IEnumerable<TrayIconMetadata> source)
    {
        // Shell metadata can carry a generated GUID even when the application
        // registered the icon by HWND + UID. Try that identity independently.
        var candidates = source.Where(icon => icon.Executable.Length > 0)
            .GroupBy(icon => icon.Executable, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var result = new List<(TrayIconMetadata, nint, uint)>();
        if (candidates.Count == 0) return result;
        var paths = new Dictionary<uint, string>(); var seen = new HashSet<(nint, uint)>();
        bool Visit(nint window, nint parameter)
        {
            GetWindowThreadProcessId(window, out var pid); if (pid == 0) return true;
            if (!paths.TryGetValue(pid, out var path))
            {
                path = ""; var process = OpenProcess(0x1000, false, pid);
                if (process != 0)
                {
                    try { uint size = 32768; var name = new StringBuilder((int)size); if (QueryFullProcessImageName(process, 0, name, ref size)) path = name.ToString(); }
                    finally { CloseHandle(process); }
                }
                paths[pid] = path;
            }
            if (!candidates.TryGetValue(path, out var icons)) return true;
            foreach (var icon in icons)
            {
                // When UID is absent, make a small, bounded set of documented
                // identity queries. Only a successful shell lookup creates an item.
                var ids = icon.Id.HasValue ? new[] { icon.Id.Value } : Enumerable.Range(0, 9).Select(value => (uint)value);
                foreach (uint id in ids)
                {
                    if (!seen.Add((window, id))) continue;
                    var identifier = new Identifier { Size = (uint)Marshal.SizeOf<Identifier>(), Window = window, Id = id };
                    // A running process alone is not enough: verify the HWND + UID
                    // pair against the shell before showing this notification icon.
                    if (Shell_NotifyIconGetRect(ref identifier, out _) >= 0)
                        result.Add((icon with { Id = id }, window, pid));
                }
            }
            return true;
        }
        EnumWindows(Visit, 0);
        nint after = 0;
        for (int count = 0; count < 1024; count++)
        {
            after = FindWindowEx(new nint(-3), after, null, null); if (after == 0) break;
            Visit(after, 0);
        }
        return result;
    }
}
