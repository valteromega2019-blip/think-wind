using System.Collections.Generic;
using System.Windows.Controls;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private readonly List<HeaderIconMagnifier> headerMagnifiers = new();
    private void InitializeHeaderMagnification()
    {
        foreach (Panel row in new Panel[] { header, hoverQuick, statusRow })
            headerMagnifiers.Add(new HeaderIconMagnifier(row,
                () => !Store.IsEditing && !LayoutDrag.Active && !IsPositionAnimating && !ControlsBusy));
        Closed += (_, _) => { foreach (var effect in headerMagnifiers) effect.Dispose(); headerMagnifiers.Clear(); };
    }
}
