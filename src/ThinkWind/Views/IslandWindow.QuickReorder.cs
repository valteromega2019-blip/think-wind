using System.Windows;
using System.Windows.Controls.Primitives;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private void EnableQuickReorder(FrameworkElement element, string id, bool mirrored = false)
    {
        LayoutDrag.Attach(element, "quick", id, () => true,
            (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, mirrored ? !after : after)));
        element.PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (LayoutDrag.SuppressClick) e.Handled = true;
        };
    }
}
