using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private bool editingPanel;
    private double? editingWidth, editingHeight;
    private void RememberEditingPanel()
    {
        if (Store.IsEditing == editingPanel) return;
        editingPanel = Store.IsEditing;
        if (editingPanel && Stage == IslandStage.Full && IsVisible)
        {
            var bounds = TargetBounds;
            editingWidth = (bounds.Right - bounds.Left) / Display.Scale;
            editingHeight = (bounds.Bottom - bounds.Top) / Display.Scale;
        }
        else { editingWidth = editingHeight = null; }
    }
    private Button WidgetEditorButton(bool strip)
    {
        var button = ActionButton(Store.IsEditing ? "Concluir edição dos widgets" : "Editar tamanho e posição dos widgets", () =>
        {
            CancelCollapse();
            Store.SetEditing(!Store.IsEditing);
        });
        button.Style = (Style)FindResource("Think.IconButton");
        button.Content = Glyph("\uE70F", strip ? 13 : 17 * Store.Value.IslandScale);
        button.Width = strip ? 28 : 32 * Store.Value.IslandScale;
        button.Height = strip ? 24 : 32 * Store.Value.IslandScale;
        button.Padding = new Thickness(3);
        button.SetResourceReference(Control.ForegroundProperty, Store.IsEditing ? "Think.Accent" : "Think.Text");
        button.SetResourceReference(Control.BackgroundProperty, Store.IsEditing ? "Think.AccentSurface" : "Think.Control");
        return button;
    }

    private Thumb HeightGrip(string id)
    {
        var grip = new Thumb { Width = 32, Height = 28, Cursor = Cursors.SizeNS,
            ToolTip = "Arraste para ajustar a altura da linha. O conteúdo define a altura mínima.", Margin = new Thickness(4, 0, 0, 0) };
        var surface = new FrameworkElementFactory(typeof(Border));
        surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        surface.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("Think.AccentSurface"));
        surface.SetValue(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"));
        surface.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, "↕");
        text.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("Think.Accent"));
        text.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        surface.AppendChild(text); grip.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = surface };
        double distance = 0;
        grip.DragStarted += (_, _) => distance = 0;
        grip.DragDelta += (_, e) => { distance += e.VerticalChange; e.Handled = true; };
        grip.DragCompleted += (_, e) =>
        {
            if (e.Canceled || Math.Abs(distance) < 2) return;
            // A line shares one height: shrink/expand its peers together rather
            // than allowing an older minimum on a neighbor to block the change.
            var peers = cards.TryGetValue(id, out var card) ? modules.RowPeers(card) : Array.Empty<string>();
            Store.Update(p =>
            {
                foreach (string peer in peers)
                    p.ModuleExtraHeight[peer] = Math.Clamp(p.ModuleExtraHeight.GetValueOrDefault(peer) + distance, 0, 240);
            });
        };
        return grip;
    }
}
