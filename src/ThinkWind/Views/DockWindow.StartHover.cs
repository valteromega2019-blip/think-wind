using System;
using System.Windows.Controls;

namespace OrbyLauncher.Views;

internal sealed partial class DockWindow
{
    private Button? startHoverButton;
    private DateTime startHoverSince;
    private bool startHoverConsumed;

    private void UpdateStartHover()
    {
        if (startHoverButton?.IsVisible != true || !startHoverButton.IsMouseOver)
        {
            startHoverSince = default; startHoverConsumed = false; return;
        }
        if (!Store.Value.StartHoverEnabled || hidden || IsPositionAnimating || Store.IsEditing
            || MenuOpen || fileDragging || LayoutDrag.Active || launcher?.IsVisible == true)
        { startHoverSince = default; return; }
        if (startHoverConsumed) return;
        if (startHoverSince == default) { startHoverSince = DateTime.UtcNow; return; }
        if (DateTime.UtcNow - startHoverSince < TimeSpan.FromSeconds(Store.Value.StartHoverSeconds)) return;
        startHoverConsumed = true; startHoverSince = default;
        // Open only; never toggle an already-visible menu closed. Reuse the
        // normal opening path and existing Dock timer (suspended for games).
        ShowPanel(LauncherPanelMode.Start);
    }
}
