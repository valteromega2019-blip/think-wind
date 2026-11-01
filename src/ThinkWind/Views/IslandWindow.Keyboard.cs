using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;
internal sealed partial class IslandWindow
{
    // Handle only this window's mouse activation for the keyboard button.
    // This is an HWND message handler, not a global input hook.
    private void PreserveKeyboardTarget()
    {
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(
            (nint hwnd, int message, nint wp, nint lp, ref bool handled) =>
            {
                if (message != 0x21 || Store.IsEditing) return 0;
                DependencyObject? node = Mouse.DirectlyOver as DependencyObject;
                while (node is Visual)
                {
                    if (node is Button button && AutomationProperties.GetName(button) == "Teclado virtual")
                    { VirtualKeyboardInput.RememberTarget(); handled = true; return 3; }
                    node = VisualTreeHelper.GetParent(node);
                }
                return 0;
            });
    }
}
