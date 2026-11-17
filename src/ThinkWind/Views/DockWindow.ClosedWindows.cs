using System;
using System.Linq;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class DockWindow
{
    private void PruneClosedWindows()
    {
        if (rebuilding || LayoutDrag.Active || fileDragging) return;
        preview?.PruneClosedWindows();
        bool changed = false;
        foreach (var entry in entries)
            changed |= entry.Windows.RemoveAll(window => !Desktop.WindowExists(window.Handle)) > 0;
        if (!changed) return;
        // Keep pins and launcher actions; discard only entries whose real
        // windows are gone. No process scan or shortcut resolution is needed.
        entries.RemoveAll(entry => entry.Windows.Count == 0 && entry.Pin is null && entry.Action is null);
        signature = ""; nextAppRefresh = DateTime.MinValue; iconForeground = -1;
        AppIconTheme.UpdateWindowApps(entries.SelectMany(entry => entry.Windows)
            .Select(window => (window.Executable ?? "", window.AppId, !window.Minimized)));
        if (!MenuOpen) { RenderPage(); Position(); }
    }
}
