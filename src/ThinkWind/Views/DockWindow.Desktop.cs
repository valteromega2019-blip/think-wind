using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;

internal sealed partial class DockWindow
{
    private const double ShowDesktopSlotWidth = 40;
    private Button? desktopDragButton;
    private bool desktopShownForDrag;
    private readonly DispatcherTimer desktopDragDelay = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private bool OverDesktopDragTarget(DragEventArgs e)
    {
        if (desktopDragButton is not { IsVisible: true } button) return false;
        var point = e.GetPosition(button);
        return point.X >= 0 && point.Y >= 0 && point.X < button.ActualWidth && point.Y < button.ActualHeight;
    }
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
        desktopDragButton = button;
        button.AllowDrop = true;
        void DragOver(object sender, DragEventArgs e)
        {
            if (!HasDragContent(e)) return;
            BeginFileDrag(); ClearFileTarget(); pageDrop.Stop();
            e.Effects = DragDropEffects.None; e.Handled = true;
            Highlight(true);
            if (!desktopShownForDrag && !desktopDragDelay.IsEnabled) desktopDragDelay.Start();
        }
        button.AddHandler(DragDrop.DragEnterEvent, new DragEventHandler(DragOver), true);
        button.AddHandler(DragDrop.DragOverEvent, new DragEventHandler(DragOver), true);
        button.DragLeave += (_, e) =>
        {
            if (OverDesktopDragTarget(e)) return;
            desktopDragDelay.Stop(); Highlight(false);
        };
        button.Unloaded += (_, _) => desktopDragDelay.Stop();
        return button;
    }
}
