using System;
using System.Windows;
using System.Windows.Threading;

namespace ThinkWind.Views;
internal sealed partial class DockWindow
{
    private WindowPreview? preview;
    private Item? previewItem;
    private bool previewIconHovered;
    private readonly DispatcherTimer previewOpen = new();
    private readonly DispatcherTimer previewClose = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private void InitializePreviews()
    {
        previewOpen.Tick += (_, _) =>
        {
            previewOpen.Stop();
            if (!IsVisible || MenuOpen || fileDragging || Store.IsEditing || LayoutDrag.Active || !Store.Value.WindowPreviewsEnabled || previewItem is null) return;
            if (preview is null)
            {
                preview = new WindowPreview(Store, () => TargetBounds) { Owner = this };
                preview.MouseEnter += (_, _) => { lastHover = DateTime.UtcNow; previewClose.Stop(); };
                preview.MouseLeave += (_, _) => { lastHover = DateTime.UtcNow; previewClose.Start(); };
            }
            preview.Present(previewItem.Windows);
        };
        previewClose.Tick += (_, _) => { previewClose.Stop(); if (preview?.IsMouseOver != true && !previewIconHovered) preview?.Hide(); };
    }
    private void AttachPreview(FrameworkElement element, Item item)
    {
        element.MouseEnter += (_, _) =>
        {
            if (MenuOpen) return;
            if (item.Windows.Count == 0) { StopPreviews(); return; }
            previewIconHovered = true;
            previewClose.Stop(); previewOpen.Stop(); previewItem = item;
            previewOpen.Interval = TimeSpan.FromMilliseconds(Store.Value.WindowPreviewDelayMilliseconds);
            if (Store.Value.WindowPreviewsEnabled) previewOpen.Start();
        };
        element.MouseLeave += (_, _) => { previewIconHovered = false; previewOpen.Stop(); previewClose.Start(); };
    }
    private void StopPreviews() { previewIconHovered = false; previewOpen.Stop(); previewClose.Stop(); previewItem = null; preview?.Hide(); }
}
