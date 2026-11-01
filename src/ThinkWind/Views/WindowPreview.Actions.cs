using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class WindowPreview
{
    private bool windowActionBusy;
    private ContextMenu? windowActionMenu;
    private bool WindowActionInteracting => windowActionBusy || windowActionMenu?.IsOpen == true;

    private FrameworkElement WithWindowActions(FrameworkElement content, IEnumerable<Desktop.RunningWindow> source, nint preferred = 0)
    {
        if (content.Parent is Panel panel) panel.Children.Remove(content);
        else if (content.Parent is Viewbox box) box.Child = null;
        var candidates = source.Where(w => WindowThumbnail.IsWindow(w.Handle)).DistinctBy(w => w.Handle).ToArray();
        var targets = preferred != 0 ? candidates.Where(w => w.Handle == preferred).ToArray() : candidates;
        var layout = new DockPanel();
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(4, 2, 4, 2) };
        DockPanel.SetDock(controls, Dock.Top); layout.Children.Add(controls);
        Add("Minimizar janela do aplicativo", "\uE921", false);
        Add("Fechar janela do aplicativo", "\uE711", true);
        layout.Children.Add(content);
        return layout;

        void Add(string label, string glyph, bool close)
        {
            var button = PreviewControl(label, glyph, () => ChooseWindow(close));
            button.SetResourceReference(StyleProperty, "Think.IconButton");
            button.IsEnabled = targets.Length > 0;
            if (targets.Length == 0) button.ToolTip = "O aplicativo não disponibiliza uma janela para esta ação.";
            controls.Children.Add(button);
            void ChooseWindow(bool requestClose)
            {
                if (windowActionBusy) return;
                var available = targets.Where(w => WindowThumbnail.IsWindow(w.Handle)).ToArray();
                if (available.Length == 0) { Hide(); return; }
                if (available.Length == 1) { _ = ApplyWindowAction(available[0].Handle, requestClose); return; }
                // Never close an arbitrary window when one music session has several windows.
                var menu = new ContextMenu { PlacementTarget = button };
                foreach (var window in available)
                {
                    var item = new MenuItem { Header = window.Title };
                    item.Click += async (_, _) => await ApplyWindowAction(window.Handle, requestClose);
                    menu.Items.Add(item);
                }
                windowActionMenu = menu;
                menu.Closed += (_, _) => { if (ReferenceEquals(windowActionMenu, menu)) windowActionMenu = null; };
                menu.IsOpen = true;
            }
        }
    }

    private async Task ApplyWindowAction(nint window, bool close)
    {
        if (windowActionBusy || !WindowThumbnail.IsWindow(window)) return;
        windowActionBusy = true;
        try
        {
            if (!close && !Desktop.IsVisibleAndRestored(window)) { Hide(); return; }
            bool sent = close ? WindowThumbnail.CloseWindow(window) : await Desktop.ActivateFromDock(window, true);
            if (!sent && ElevatedWindowAction.NeedsAdminFallback(window))
                sent = await ElevatedWindowAction.Request(window, close: close, minimize: !close, owner: Owner ?? this);
            if (sent) Hide();
            else MessageBox.Show(close ? "O Windows não permitiu solicitar o fechamento desta janela." : "O Windows não permitiu minimizar esta janela.", "Orby Launcher");
        }
        catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
        { MessageBox.Show(error.Message, "Orby Launcher"); }
        finally { windowActionBusy = false; }
    }
}
