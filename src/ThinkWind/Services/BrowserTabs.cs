using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

internal sealed record BrowserTab(nint Window, int ProcessId, string Title, int[] RuntimeId, bool Selected, bool Cached = false);
internal static class BrowserTabs
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private sealed record CachedTabs(uint Process, IReadOnlyList<BrowserTab> Tabs, DateTime Updated);
    private static readonly Dictionary<nint, CachedTabs> cache = new();
    private static BrowserTab[] previewSnapshot = Array.Empty<BrowserTab>();
    internal static Task<IReadOnlyList<BrowserTab>> ReadForPreviewAsync(IEnumerable<Desktop.RunningWindow> source, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var windows = source.Where(IsBrowser).Take(6).ToArray();
        var snapshot = Volatile.Read(ref previewSnapshot);
        var result = new List<BrowserTab>();
        foreach (var window in windows)
        {
            GetWindowThreadProcessId(window.Handle, out uint process);
            var known = snapshot.Where(tab => tab.Window == window.Handle && tab.ProcessId == process).ToArray();
            if (known.Length == 0) return ReadAsync(windows, cancellation);
            result.AddRange(known.Select(tab => tab with { Cached = true }));
        }
        return Task.FromResult<IReadOnlyList<BrowserTab>>(result);
    }
    private static int observing;
    private static string observed = "";
    private static DateTime observedAt;
    private static DateTime callObservedAt;
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    // Reuse the dock's existing window refresh, only on a browser focus/title
    // change. No additional timer or browser extension is needed.
    internal static async void Observe(IReadOnlyList<Desktop.RunningWindow> windows)
    {
        nint foreground = GetForegroundWindow();
        var active = windows.FirstOrDefault(window => window.Handle == foreground && IsBrowser(window));
        if (active is null) { observed = ""; return; }
        if (Interlocked.CompareExchange(ref observing, 1, 0) != 0) return;
        try
        {
            string key = active.Handle + "|" + active.Title;
            bool changed = key != observed;
            bool readTabs = changed || DateTime.UtcNow - observedAt >= TimeSpan.FromMinutes(2);
            bool inspectCall = changed || DateTime.UtcNow - callObservedAt >= TimeSpan.FromSeconds(10);
            if (!readTabs && !inspectCall) return;
            observed = key;
            if (readTabs)
            {
                observedAt = DateTime.UtcNow;
                var tabs = await ReadAsync(new[] { active }, CancellationToken.None);
                var selected = tabs.FirstOrDefault(tab => tab.Window == active.Handle && tab.Selected && !tab.Cached);
                if (changed && selected is not null) await BrowserTabImages.CaptureAsync(selected);
            }
            // Custom meeting names need not contain "Meet". Probe the native
            // URL + controls at a bounded cadence using the existing dock tick.
            if (inspectCall) { callObservedAt = DateTime.UtcNow; await CallIntegration.FindAsync(new[] { active }, CancellationToken.None); }
        }
        catch (OperationCanceledException) { }
        finally { Interlocked.Exchange(ref observing, 0); }
    }
    internal static bool IsBrowser(Desktop.RunningWindow window) =>
        Path.GetFileNameWithoutExtension(window.Executable ?? "").ToLowerInvariant() is "msedge" or "chrome" or "firefox" or "brave" or "chromium" or "opera" or "vivaldi";
    internal static async Task<IReadOnlyList<BrowserTab>> ReadAsync(IEnumerable<Desktop.RunningWindow> windows, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            return await Task.Run<IReadOnlyList<BrowserTab>>(() =>
            {
                var result = new List<BrowserTab>();
                foreach (var handle in cache.Keys.Where(handle => !Desktop.WindowExists(handle)).ToArray()) cache.Remove(handle);
                foreach (var window in windows.Where(IsBrowser).Take(6))
                {
                    cancellation.ThrowIfCancellationRequested();
                    GetWindowThreadProcessId(window.Handle, out uint process);
                    if (process == 0) { cache.Remove(window.Handle); continue; }
                    if (cache.TryGetValue(window.Handle, out var previous) && previous.Process != process) { cache.Remove(window.Handle); previous = null; }
                    if (previous is not null && IsIconic(window.Handle))
                    { result.AddRange(previous.Tabs.Select(tab => tab with { Cached = true })); continue; }
                    var found = new List<BrowserTab>();
                    try
                    {
                        var root = AutomationElement.FromHandle(window.Handle);
                        foreach (var element in ReadTabElements(root, cancellation))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (!element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)) continue;
                            found.Add(new(window.Handle, root.Current.ProcessId, element.Current.Name, element.GetRuntimeId(), ((SelectionItemPattern)pattern).Current.IsSelected));
                        }
                    }
                    catch (Exception e) when (Unavailable(e)) { }
                    if (found.Count > 0)
                    {
                        // Keep metadata beyond the four visual slots so music in
                        // another tab remains associated with this browser.
                        var saved = found.OrderByDescending(tab => tab.Selected).Take(64).ToArray();
                        cache[window.Handle] = new(process, saved, DateTime.UtcNow); result.AddRange(saved);
                    }
                    else if (previous is not null) result.AddRange(previous.Tabs.Select(tab => tab with { Cached = true }));
                }
                foreach (var handle in cache.OrderByDescending(entry => entry.Value.Updated).Skip(16).Select(entry => entry.Key).ToArray()) cache.Remove(handle);
                Volatile.Write(ref previewSnapshot, cache.Values.SelectMany(entry => entry.Tabs).ToArray());
                // Six actual windows, keeping their tab metadata for the menu.
                // Titles never establish whether a call is active.
                return result.Take(384).ToArray();
            }, cancellation);
        }
        finally { gate.Release(); }
    }
    internal static async Task<IReadOnlyList<string>> KnownTitlesAsync(IEnumerable<Desktop.RunningWindow> windows, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            return windows.Where(IsBrowser).SelectMany(window =>
            {
                GetWindowThreadProcessId(window.Handle, out uint process);
                return cache.TryGetValue(window.Handle, out var saved) && saved.Process == process
                    ? saved.Tabs.Select(tab => tab.Title) : Enumerable.Empty<string>();
            }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        finally { gate.Release(); }
    }
    internal static async Task<BrowserTab?> ConfirmedSelectedAsync(Desktop.RunningWindow window, CancellationToken cancellation)
    {
        var tabs = await ReadAsync(new[] { window }, cancellation);
        var selected = tabs.Where(tab => tab.Window == window.Handle && tab.Selected && !tab.Cached).ToArray();
        return selected.Length == 1 ? selected[0] : null;
    }
    internal static async Task<BrowserTab?> FindKnownAsync(Desktop.RunningWindow window, BrowserTab previous, CancellationToken cancellation)
    {
        var observedTabs = await ReadAsync(new[] { window }, cancellation);
        await gate.WaitAsync(cancellation);
        try
        {
            if (!cache.TryGetValue(window.Handle, out var saved) || saved.Process != (uint)previous.ProcessId) return null;
            var found = saved.Tabs.FirstOrDefault(tab => tab.RuntimeId.SequenceEqual(previous.RuntimeId));
            return found is null ? null : found with { Cached = observedTabs.All(tab => tab.Cached) };
        }
        finally { gate.Release(); }
    }
    internal static async Task<bool> SelectAsync(BrowserTab tab, CancellationToken cancellation, bool strict = false)
    {
        GetWindowThreadProcessId(tab.Window, out uint process);
        if (process != tab.ProcessId || !Desktop.WindowExists(tab.Window)) return false;
        // This path is entered only after an explicit click. Restoring here lets
        // the browser expose its tabs again; hover never restores a window.
        if (!await Desktop.ActivateFromDock(tab.Window, false)) return false;
        await Task.Delay(120, cancellation);
        await gate.WaitAsync(cancellation);
        try
        {
            return await Task.Run(() =>
            {
                var root = AutomationElement.FromHandle(tab.Window);
                if (root.Current.ProcessId != tab.ProcessId) return false;
                var elements = ReadTabElements(root, cancellation);
                var selected = elements.FirstOrDefault(element => element.GetRuntimeId().SequenceEqual(tab.RuntimeId));
                if (selected is null && !strict)
                {
                    var named = elements.Where(element => element.Current.Name == tab.Title).ToArray();
                    if (named.Length == 1) selected = named[0];
                }
                cancellation.ThrowIfCancellationRequested();
                if (selected is not null && selected.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
                    { ((SelectionItemPattern)pattern).Select(); return true; }
                return false;
            }, cancellation);
        }
        catch (Exception e) when (Unavailable(e)) { return false; }
        finally { gate.Release(); }
    }
    internal static IReadOnlyList<AutomationElement> ReadTabElements(AutomationElement root, CancellationToken cancellation = default)
    {
        var result = new List<AutomationElement>();
        cancellation.ThrowIfCancellationRequested();
        if (root.Current.ControlType == ControlType.Document) return result;
        var walker = TreeWalker.ControlViewWalker;
        var pending = new Stack<(AutomationElement Element, int Depth)>();
        var first = walker.GetFirstChild(root);
        if (first is not null) pending.Push((first, 1));
        int visited = 0;
        while (pending.Count > 0 && visited < 4096)
        {
            cancellation.ThrowIfCancellationRequested();
            var (element, depth) = pending.Pop();
            visited++;
            var type = element.Current.ControlType;
            if (type == ControlType.TabItem && IsBrowserTab(element)) result.Add(element);
            if (visited == 4096) break;
            // Queue only one sibling per visited node, so a wide toolbar cannot
            // enumerate unbounded siblings before the node budget is applied.
            var sibling = walker.GetNextSibling(element);
            if (sibling is not null) pending.Push((sibling, depth));
            if (type == ControlType.Document || depth >= 24) continue;
            var child = walker.GetFirstChild(element);
            if (child is not null) pending.Push((child, depth + 1));
        }
        return result;
    }
    private static bool IsBrowserTab(AutomationElement element)
    {
        // Web pages can have their own ARIA tabs; never treat those as browser tabs.
        var walker = TreeWalker.ControlViewWalker; var parent = walker.GetParent(element);
        for (int depth = 0; parent is not null && depth < 24; depth++, parent = walker.GetParent(parent))
        {
            if (parent.Current.ControlType == ControlType.Document) return false;
            if (parent.Current.ControlType == ControlType.Window) return true;
        }
        return false;
    }
    private static bool Unavailable(Exception e) => e is ElementNotAvailableException or InvalidOperationException or COMException or UnauthorizedAccessException or TimeoutException or NotSupportedException;
}
