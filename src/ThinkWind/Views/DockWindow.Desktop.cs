using System;
using System.Windows;
using System.Windows.Controls;

namespace ThinkWind.Views;

internal sealed partial class DockWindow
{
    private const double ShowDesktopSlotWidth = 24;
    private Button ShowDesktopButton()
    {
        var button = ActionButton("Mostrar área de trabalho", () =>
        {
            StopPreviews(); launcher?.Dismiss(); startTools?.Close();
            SystemToolsMenu.ShowDesktop();
            lastHover = DateTime.UtcNow;
        });
        button.Style = (Style)FindResource("Think.IconButton");
        button.Width = ShowDesktopSlotWidth - 6;
        button.Height = Math.Clamp(fit.Icon + 8, 24, 36);
        button.Padding = new Thickness(0); button.Margin = new Thickness(6, 0, 0, 0);
        button.VerticalAlignment = VerticalAlignment.Center;
        var line = new Border { Width = 2, Height = 22, CornerRadius = new CornerRadius(1), Opacity = .65 };
        line.SetResourceReference(Border.BackgroundProperty, "Think.Text");
        button.Content = line;
        void Highlight(bool active)
        {
            line.Opacity = active ? 1 : .65;
            line.SetResourceReference(Border.BackgroundProperty, active ? "Think.Accent" : "Think.Text");
        }
        button.MouseEnter += (_, _) => Highlight(true);
        button.MouseLeave += (_, _) => Highlight(button.IsKeyboardFocusWithin);
        button.GotKeyboardFocus += (_, _) => Highlight(true);
        button.LostKeyboardFocus += (_, _) => Highlight(button.IsMouseOver);
        return button;
    }
}
