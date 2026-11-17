using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class WindowPreview
{
    private FrameworkElement? CreateGroupActions()
    {
        if (windows.Where(window => WindowThumbnail.IsWindow(window.Handle)).DistinctBy(window => window.Handle).Take(2).Count() < 2) return null;
        var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 8, 4, 0) };
        foreach (var action in new[] { ("Minimizar todas", "\uE921", false), ("Fechar todas", "\uE711", true) })
        {
            bool close = action.Item3;
            var button = ActionButton(action.Item1, () => _ = ApplyGroupAction(close));
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(Glyph(action.Item2, 13));
            label.Children.Add(new TextBlock { Text = action.Item1, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            button.Content = label; button.Padding = new Thickness(12, 8, 12, 8); button.Margin = new Thickness(3);
            row.Children.Add(button);
        }
        return PreviewSurface(row, false);
    }

    private async Task ApplyGroupAction(bool close)
    {
        if (windowActionBusy) return;
        windowActionBusy = true;
        EndWindowPeek();
        var targets = windows.Where(window => WindowThumbnail.IsWindow(window.Handle)).DistinctBy(window => window.Handle).ToArray();
        int failures = 0;
        try
        {
            foreach (var target in targets)
            {
                if (!WindowThumbnail.IsWindow(target.Handle)) continue;
                if (!close && !Desktop.IsVisibleAndRestored(target.Handle)) continue;
                try
                {
                    bool sent = close ? WindowThumbnail.CloseWindow(target.Handle) : Desktop.MinimizeWindow(target.Handle);
                    if (!sent && ElevatedWindowAction.NeedsAdminFallback(target.Handle))
                        sent = await ElevatedWindowAction.Request(target.Handle, close: close, minimize: !close, owner: Owner ?? this);
                    if (!sent && WindowThumbnail.IsWindow(target.Handle)) failures++;
                }
                catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception)
                { DiagnosticLog.Record("Prévias: ação em todas as janelas", error); failures++; }
            }
            // WM_CLOSE preserves unsaved-document prompts; no process is killed.
            Hide();
            if (failures > 0) MessageBox.Show($"O Windows não permitiu a ação em {failures} janela(s). As demais solicitações foram enviadas.", "Orby Launcher");
        }
        finally { windowActionBusy = false; }
    }
}
