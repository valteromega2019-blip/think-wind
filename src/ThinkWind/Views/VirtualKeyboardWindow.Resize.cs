using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;
internal sealed partial class VirtualKeyboardWindow
{
    [StructLayout(LayoutKind.Sequential)] private struct CursorPoint { internal int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
    private bool resizing, savingResize;
    private OverlayChrome.PixelRect resizeStart;
    private CursorPoint resizePointer;
    private void AddResizeEdges()
    {
        if (Content is not Grid root) return;
        void Edge(int horizontal, int vertical)
        {
            var thumb = new Thumb
            {
                Focusable = false, IsTabStop = false,
                HorizontalAlignment = horizontal < 0 ? HorizontalAlignment.Left : horizontal > 0 ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                VerticalAlignment = vertical < 0 ? VerticalAlignment.Top : vertical > 0 ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
                Cursor = horizontal == 0 ? Cursors.SizeNS : vertical == 0 ? Cursors.SizeWE : horizontal == vertical ? Cursors.SizeNWSE : Cursors.SizeNESW,
                ToolTip = "Arraste para redimensionar o teclado"
            };
            if (horizontal != 0) thumb.Width = vertical == 0 ? 8 : 20;
            if (vertical != 0) thumb.Height = horizontal == 0 ? 8 : 20;
            var hit = new FrameworkElementFactory(typeof(Border)); hit.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = hit };
            System.Windows.Automation.AutomationProperties.SetName(thumb, "Redimensionar teclado");
            thumb.DragStarted += (_, _) =>
            {
                resizing = OverlayChrome.GetWindowRect(new WindowInteropHelper(this).Handle, out resizeStart) && GetCursorPos(out resizePointer);
            };
            thumb.DragDelta += (_, _) =>
            {
                if (!resizing || !GetCursorPos(out var pointer)) return;
                var rect = resizeStart; int dx = pointer.X - resizePointer.X, dy = pointer.Y - resizePointer.Y;
                double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? Display.Scale;
                int minWidth = (int)Math.Round(Math.Min(480, Display.Width - 24) * scale), minHeight = (int)Math.Round(Math.Min(260, Display.Height - 32) * scale);
                int maxWidth = Math.Max(minWidth, Math.Min((int)Math.Round(2400 * scale), Display.Work.Width - (int)Math.Round(24 * scale)));
                int maxHeight = Math.Max(minHeight, Math.Min((int)Math.Round(1200 * scale), Display.Work.Height - (int)Math.Round(32 * scale)));
                if (horizontal < 0) rect.Left = rect.Right - Math.Clamp(resizeStart.Right - resizeStart.Left - dx, minWidth, maxWidth);
                else if (horizontal > 0) rect.Right = rect.Left + Math.Clamp(resizeStart.Right - resizeStart.Left + dx, minWidth, maxWidth);
                if (vertical < 0) rect.Top = rect.Bottom - Math.Clamp(resizeStart.Bottom - resizeStart.Top - dy, minHeight, maxHeight);
                else if (vertical > 0) rect.Bottom = rect.Top + Math.Clamp(resizeStart.Bottom - resizeStart.Top + dy, minHeight, maxHeight);
                int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
                rect.Left = Math.Clamp(rect.Left, Display.Work.Left, Math.Max(Display.Work.Left, Display.Work.Right - width)); rect.Right = rect.Left + width;
                rect.Top = Math.Clamp(rect.Top, Display.Work.Top, Math.Max(Display.Work.Top, Display.Work.Bottom - height)); rect.Bottom = rect.Top + height;
                Width = width / scale; Height = height / scale;
                OverlayChrome.Move(new WindowInteropHelper(this).Handle, rect);
                UpdateClip();
            };
            thumb.DragCompleted += (_, e) =>
            {
                if (!resizing) return;
                resizing = false;
                if (e.Canceled) { OverlayChrome.Move(new WindowInteropHelper(this).Handle, resizeStart); UpdateClip(); return; }
                double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? Display.Scale;
                if (!OverlayChrome.GetWindowRect(new WindowInteropHelper(this).Handle, out var rectangle)) return;
                savingResize = true;
                try { Store.Update(p => { p.KeyboardWidth = (rectangle.Right - rectangle.Left) / scale; p.KeyboardHeight = (rectangle.Bottom - rectangle.Top) / scale; }); }
                finally { savingResize = false; }
            };
            Panel.SetZIndex(thumb, 10); root.Children.Add(thumb);
        }
        Edge(-1, 0); Edge(1, 0); Edge(0, -1); Edge(0, 1);
        Edge(-1, -1); Edge(1, -1); Edge(-1, 1); Edge(1, 1);
    }
}
