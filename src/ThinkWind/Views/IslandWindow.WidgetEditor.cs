using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private bool editingPanel;
    private double? editingWidth, editingHeight;
    private double editingBoardWidth;
    private double? stableFullPanelWidth, stableFullPanelHeight;
    private bool IsPrimaryEditor => System.Windows.Forms.Screen.AllScreens.Any(screen => screen.Primary && screen.Bounds == Display.Bounds);
    private (double Width, double Height, double Limit, double Maximum, string Structure)? widgetWidthFitKey;
    private double fittedWidgetBoardWidth, fittedWidgetInnerWidth;
    private BackdropHost? editorBackdrop;
    private void SyncEditorBackdrop()
    {
        if (!IsPrimaryEditor || !Store.IsEditing || !IsVisible || Stage != IslandStage.Full)
        {
            editorBackdrop?.Dispose(); editorBackdrop = null; return;
        }
        var handle = BackdropHandle != 0 ? BackdropHandle : new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        try
        {
            // A passive composition surface below the editor: no keyboard or
            // mouse hook, focus change, screenshot loop or persistent service.
            editorBackdrop ??= new BackdropHost(handle, false, 0, "Edição dos widgets");
            editorBackdrop.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
            var bounds = Display.Bounds;
            editorBackdrop.Synchronize(new OverlayChrome.PixelRect
            { Left = bounds.Left, Top = bounds.Top, Right = bounds.Right, Bottom = bounds.Bottom }, 0, false, true);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or System.Runtime.InteropServices.ExternalException or InvalidOperationException)
        { editorBackdrop?.Dispose(); editorBackdrop = null; }
    }
    private void RememberEditingPanel()
    {
        if (Store.IsEditing == editingPanel) return;
        editingPanel = Store.IsEditing;
        if (editingPanel && Stage == IslandStage.Full && IsVisible)
        {
            var bounds = TargetBounds;
            editingWidth = (bounds.Right - bounds.Left) / Display.Scale;
            editingHeight = (bounds.Bottom - bounds.Top) / Display.Scale;
            editingBoardWidth = double.IsFinite(modules.Width) ? modules.Width : Math.Max(760, editingWidth.Value - 24);
        }
        else { editingWidth = editingHeight = null; }
    }
    private Button WidgetEditorButton(bool strip)
    {
        var button = ActionButton(Store.IsEditing ? "Concluir edição dos widgets" : "Editar tamanho e posição dos widgets", () =>
        {
            CancelCollapse();
            SetWidgetEditing(!Store.IsEditing);
        });
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = Glyph("\uE70F", strip ? 13 : 17 * Store.Value.IslandScale);
        button.Width = strip ? 28 : 32 * Store.Value.IslandScale;
        button.Height = strip ? 24 : 32 * Store.Value.IslandScale;
        button.Padding = new Thickness(3);
        button.SetResourceReference(Control.ForegroundProperty, Store.IsEditing ? "Think.Accent" : "Think.Text");
        button.SetResourceReference(Control.BackgroundProperty, Store.IsEditing ? "Think.AccentSurface" : "Think.Control");
        TutorialGuide.Mark(button, "widget-editor");
        return button;
    }
    private void SetWidgetEditing(bool enabled)
    {
        if (enabled && !IsPrimaryEditor) return;
        Store.SetEditing(enabled);
        TutorialGuide.Notify(enabled ? "widget-edit-open" : "widget-edit-finished");
    }

    private void AddWidgetResizeEdges(Grid content, string id)
    {
        foreach (var edge in new[] { ResizeEdge(id, false, true), ResizeEdge(id, false), ResizeEdge(id, true) })
        {
            Grid.SetRowSpan(edge, 3); Panel.SetZIndex(edge, 20); content.Children.Add(edge);
            if (edge.Cursor == Cursors.SizeWE) TutorialGuide.Mark(edge, "widget-resize");
        }
    }
    private Thumb ResizeEdge(string id, bool vertical, bool left = false)
    {
        var grip = new Thumb
        {
            Width = vertical ? double.NaN : 10, Height = vertical ? 10 : double.NaN,
            HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : left ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            VerticalAlignment = vertical ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
            Cursor = vertical ? Cursors.SizeNS : Cursors.SizeWE,
            ToolTip = vertical ? "Arraste a borda para ajustar a altura da linha, respeitando o conteúdo."
                : "Arraste a borda para encaixar 3, 2 ou 1 widget por linha.",
            Margin = vertical ? new Thickness(18, 0, 18, -3) : left ? new Thickness(-3, 12, 0, 12) : new Thickness(0, 12, -3, 12)
        };
        var surface = new FrameworkElementFactory(typeof(Border));
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        surface.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("Think.AccentSurface"));
        surface.SetValue(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"));
        surface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = surface };
        double distance = 0;
        double startWidth = 0, unitsPerPixel = 1;
        System.Drawing.Point start = default;
        string[] peers = Array.Empty<string>();
        grip.DragStarted += (_, _) =>
        {
            distance = 0; start = System.Windows.Forms.Cursor.Position;
            if (!cards.TryGetValue(id, out var card)) return;
            startWidth = card.ActualWidth; peers = modules.RowPeers(card);
            double pixels = card.PointToScreen(new Point(card.ActualWidth, 0)).X - card.PointToScreen(new Point()).X;
            unitsPerPixel = pixels > 0 ? card.ActualWidth / pixels : 1;
        };
        grip.DragDelta += (_, e) =>
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            distance = (vertical ? cursor.Y - start.Y : (cursor.X - start.X) * (left ? -1 : 1)) * unitsPerPixel;
            if (vertical)
            {
                foreach (string peer in peers)
                    if (cards.TryGetValue(peer, out var peerCard)) peerCard.SetValue(IslandModuleBoard.ExtraHeightProperty,
                        Math.Clamp(Store.Value.ModuleExtraHeight.GetValueOrDefault(peer) + distance, 0, 240));
            }
            else if (cards.TryGetValue(id, out var resizedCard)) resizedCard.SetValue(IslandModuleBoard.ColumnsProperty, ColumnsForWidth(startWidth + distance));
            ModuleSizeChanged(); e.Handled = true;
        };
        grip.DragCompleted += (_, e) =>
        {
            if (e.Canceled || Math.Abs(distance) < 2) { RefreshSettings(); return; }
            Store.Update(p =>
            {
                if (!vertical) p.ModuleColumns[id] = ColumnsForWidth(startWidth + distance);
                else foreach (string peer in peers)
                    p.ModuleExtraHeight[peer] = Math.Clamp(p.ModuleExtraHeight.GetValueOrDefault(peer) + distance, 0, 240);
            });
            TutorialGuide.Notify("widget-resized");
        };
        return grip;
    }
    private int ColumnsForWidth(double width)
    {
        double available = Math.Max(1, modules.ActualWidth);
        return width >= available * .75 ? 1 : width >= available * .42 ? 2 : 3;
    }
    private void FitCalendarWidth()
    {
        if (calendar.ActualWidth <= 0 || calendar.Template is null) return;
        if (calendar.Template.FindName("PART_Root", calendar) is not Viewbox viewport
            || viewport.Child is not FrameworkElement month) return;
        // The native calendar template needs 280 DIP for seven readable days.
        // Its Viewbox scales down below that size and distributes columns when
        // a wider widget is available, instead of leaving content at the left.
        double width = Math.Max(280, Math.Round(calendar.ActualWidth));
        if (double.IsNaN(month.Width) || Math.Abs(month.Width - width) > .5) month.Width = width;
        viewport.HorizontalAlignment = HorizontalAlignment.Stretch;
    }
}
