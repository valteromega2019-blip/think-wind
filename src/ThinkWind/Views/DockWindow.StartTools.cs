using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace ThinkWind.Views;

internal sealed partial class DockWindow
{
    private SystemToolsWindow? startTools;
    private void InitializeStartTools()
    {
        IsVisibleChanged += (_, _) => { if (!IsVisible) startTools?.Close(); };
        Closed += (_, _) => startTools?.Close();
    }
    private void AttachStartTools(Button start)
    {
        start.ToolTip = "Iniciar Think Wind · botão direito: ferramentas do Windows";
        start.PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; OpenStartTools(start); };
        start.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Apps || (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)))
            { e.Handled = true; OpenStartTools(start); }
        };
    }
    private void OpenStartTools(Button anchor)
    {
        if (startTools is not null) { startTools.Close(); return; }
        StopPreviews(); launcher?.Dismiss(); magnifier.Reset(); lastHover = DateTime.UtcNow;
        var popup = new SystemToolsWindow(Store, anchor, above: true) { Owner = this, MonitorDeviceOverride = MonitorDeviceOverride };
        startTools = popup;
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(startTools, popup)) startTools = null;
            lastHover = DateTime.UtcNow;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!IsVisible) return;
                if (settingsAfterMenu) { settingsAfterMenu = false; RefreshSettings(); }
                else RefreshApps();
            }));
        };
        popup.Show(); popup.Position(); popup.Activate();
    }
}
