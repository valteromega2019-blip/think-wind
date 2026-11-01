using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace OrbyLauncher.Services;
internal sealed record TaskbarBadge(string Name, string Status);
internal static class TaskbarBadges
{
    private static readonly object gate = new();
    private static Task<TaskbarBadge[]>? pending;
    private static TaskbarBadge[] cache = Array.Empty<TaskbarBadge>();
    private static DateTime nextRead, updated;
    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int length);
    internal static bool HasUnread(string value) => Regex.IsMatch(value,
        @"(?:[1-9]\d*\s+(?:(?:new|nov[ao]s?)\s+)?(?:notifications?|unread|notifica\w*|mensage\w*)|(?:badge|emblema|unread|notification\w*)\s*[:,(]?\s*[1-9]\d*|(?:new|unread)\s+(?:messages?|notifications?)|(?:mensagens?|notificações?)\s+(?:novas?|não lidas?))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    internal static bool HasCounter(string title) => Regex.IsMatch(title, @"^\s*\([1-9]\d*\)|\([1-9]\d*\)\s*$", RegexOptions.CultureInvariant);
    internal static TaskbarBadge[] Snapshot()
    {
        lock (gate)
        {
            if (pending?.IsCompleted == true)
            {
                if (pending.IsCompletedSuccessfully) { cache = pending.Result; updated = DateTime.UtcNow; }
                pending = null;
            }
            if (pending is null && DateTime.UtcNow >= nextRead)
            { nextRead = DateTime.UtcNow.AddSeconds(8); pending = Task.Run(Read); }
            return DateTime.UtcNow - updated < TimeSpan.FromSeconds(20) ? cache : Array.Empty<TaskbarBadge>();
        }
    }
    private static TaskbarBadge[] Read()
    {
        var result = new List<TaskbarBadge>(); var roots = new List<nint>();
        EnumWindows((window, _) =>
        {
            var name = new StringBuilder(128); GetClassName(window, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") roots.Add(window); return true;
        }, 0);
        var watch = Stopwatch.StartNew(); int nodes = 0;
        foreach (nint window in roots)
        {
            try
            {
                var queue = new Queue<(AutomationElement Element, int Depth)>(); queue.Enqueue((AutomationElement.FromHandle(window), 0));
                var walker = TreeWalker.RawViewWalker;
                while (queue.Count > 0 && nodes++ < 600 && watch.ElapsedMilliseconds < 350)
                {
                    var item = queue.Dequeue();
                    string name = item.Element.Current.Name ?? "";
                    if (item.Element.Current.ControlType == ControlType.Button)
                    {
                        string status = item.Element.GetCurrentPropertyValue(AutomationElement.ItemStatusProperty, true) as string ?? "";
                        if (HasUnread(name + " " + status) || HasCounter(name)) result.Add(new(name, status));
                    }
                    if (item.Depth >= 10) continue;
                    var child = walker.GetFirstChild(item.Element);
                    int siblings = 0;
                    while (child is not null && siblings++ < 100 && watch.ElapsedMilliseconds < 350)
                    { queue.Enqueue((child, item.Depth + 1)); child = walker.GetNextSibling(child); }
                }
            }
            catch (Exception e) when (e is ElementNotAvailableException or COMException or InvalidOperationException or UnauthorizedAccessException) { }
        }
        return result.ToArray();
    }
}
