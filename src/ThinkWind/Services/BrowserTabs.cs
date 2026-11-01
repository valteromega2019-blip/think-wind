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

internal sealed record BrowserTab(nint Window, int ProcessId, string Title, int[] RuntimeId, bool Selected)
{
    internal bool IsMeet => Title.StartsWith("Meet -", StringComparison.OrdinalIgnoreCase) || Title.Contains("Google Meet", StringComparison.OrdinalIgnoreCase);
}
internal static class BrowserTabs
{
    private static readonly SemaphoreSlim gate = new(1, 1);
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
                foreach (var window in windows.Where(IsBrowser).Take(4))
                {
                    cancellation.ThrowIfCancellationRequested();
                    try
                    {
                        var root = AutomationElement.FromHandle(window.Handle);
                        foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)))
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (!IsBrowserTab(element) || !element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)) continue;
                            result.Add(new(window.Handle, root.Current.ProcessId, element.Current.Name, element.GetRuntimeId(), ((SelectionItemPattern)pattern).Current.IsSelected));
                        }
                    }
                    catch (Exception e) when (Unavailable(e)) { }
                }
                // Keep the active tab and Meet discoverable, then fill the four slots.
                return result.OrderByDescending(tab => tab.Selected).ThenByDescending(tab => tab.IsMeet).Take(4).OrderBy(tab => tab.Selected).ToArray();
            }, cancellation);
        }
        finally { gate.Release(); }
    }
    internal static async Task<bool> SelectAsync(BrowserTab tab, CancellationToken cancellation)
    {
        await gate.WaitAsync(cancellation);
        try
        {
            return await Task.Run(() =>
            {
                var root = AutomationElement.FromHandle(tab.Window);
                if (root.Current.ProcessId != tab.ProcessId) return false;
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)))
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (!element.GetRuntimeId().SequenceEqual(tab.RuntimeId) || !IsBrowserTab(element)) continue;
                    if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern))
                    { ((SelectionItemPattern)pattern).Select(); return true; }
                }
                return false;
            }, cancellation);
        }
        catch (Exception e) when (Unavailable(e)) { return false; }
        finally { gate.Release(); }
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
