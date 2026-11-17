using System;
using System.Collections.Generic;
using System.Linq;
using OrbyLauncher.Native;

namespace OrbyLauncher.Services;

internal static class DockGroupAction
{
    // The Dock or its preview can own focus by the time Click arrives.
    // Group state, not foreground focus, determines the double-click action.
    internal static bool ShouldMinimize(IEnumerable<nint> handles)
    {
        var windows = handles.Where(Desktop.WindowExists).Distinct().ToArray();
        return windows.Length > 0 && windows.All(Desktop.IsVisibleAndRestored);
    }
}
