using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class DockWindow
{
    private readonly Dictionary<nint, long> windowUse = new();
    private long windowUseOrder;
    private nint lastUsedHandle;
    private DispatcherTimer? multiClickTimer;
    private Item? pendingMultiItem;
    private bool pendingMinimizeAll;
    private System.Drawing.Point firstMultiClickPoint;

    private void RememberWindowUse(nint handle)
    {
        if (lastUsedHandle == handle) return;
        lastUsedHandle = handle;
        if (windowUse.Count >= 256)
            foreach (var key in windowUse.OrderBy(pair => pair.Value).Take(64).Select(pair => pair.Key).ToArray()) windowUse.Remove(key);
        windowUse[handle] = ++windowUseOrder;
    }
    private Desktop.RunningWindow? LastUsedWindow(Item item) => item.Windows.Where(window => Desktop.WindowExists(window.Handle))
        .OrderByDescending(window => windowUse.TryGetValue(window.Handle, out var order) ? order : 0).FirstOrDefault();

    private void QueueMultiWindowClick(Item item, nint foreground)
    {
        var point = System.Windows.Forms.Cursor.Position;
        var range = System.Windows.Forms.SystemInformation.DoubleClickSize;
        if (multiClickTimer?.IsEnabled == true && pendingMultiItem?.Key == item.Key
            && Math.Abs(point.X - firstMultiClickPoint.X) <= range.Width / 2
            && Math.Abs(point.Y - firstMultiClickPoint.Y) <= range.Height / 2)
        {
            multiClickTimer.Stop(); pendingMultiItem = null;
            _ = ToggleWindowGroup(item, pendingMinimizeAll); return;
        }
        if (pendingMultiItem is Item previous)
        {
            multiClickTimer?.Stop(); pendingMultiItem = null;
            OpenDockItem(previous, LastUsedWindow(previous), false);
        }
        if (foreground != 0) RememberWindowUse(foreground);
        pendingMultiItem = item;
        pendingMinimizeAll = item.Windows.Any(window => Desktop.IsActiveWindow(window.Handle, foreground));
        firstMultiClickPoint = point;
        if (multiClickTimer is null)
        {
            multiClickTimer = new DispatcherTimer();
            multiClickTimer.Tick += (_, _) =>
            {
                multiClickTimer.Stop(); var pending = pendingMultiItem; pendingMultiItem = null;
                if (pending is not null && IsVisible && !Store.IsEditing && !LayoutDrag.Active)
                    OpenDockItem(pending, LastUsedWindow(pending), false);
            };
            Closed += (_, _) => { multiClickTimer.Stop(); pendingMultiItem = null; };
            IsVisibleChanged += (_, _) => { if (!IsVisible) { multiClickTimer.Stop(); pendingMultiItem = null; } };
        }
        multiClickTimer.Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime);
        multiClickTimer.Start();
    }
    private async Task ToggleWindowGroup(Item item, bool minimize)
    {
        if (openingDockItem) return;
        openingDockItem = true; StopPreviews();
        var targets = item.Windows.Where(window => Desktop.WindowExists(window.Handle)).DistinctBy(window => window.Handle)
            .OrderBy(window => windowUse.TryGetValue(window.Handle, out var order) ? order : 0).ToArray();
        try
        {
            foreach (var window in targets)
            {
                if (!Desktop.WindowExists(window.Handle)) continue;
                if (minimize && !Desktop.IsVisibleAndRestored(window.Handle)) continue;
                bool sent = minimize ? Desktop.MinimizeWindow(window.Handle) : await Desktop.ActivateFromDock(window.Handle, false);
                if (!sent && ElevatedWindowAction.NeedsAdminFallback(window.Handle))
                    await ElevatedWindowAction.Request(window.Handle, close: false, minimize: minimize, owner: this);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        { DiagnosticLog.Record("Dock: alternar todas as janelas", error); }
        finally { openingDockItem = false; nextAppRefresh = DateTime.MinValue; RefreshForegroundIcon(); }
    }
}
