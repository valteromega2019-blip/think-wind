using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ThinkWind.Views;

internal static class LayoutDrag
{
    private sealed record Payload(string Scope, string Key);
    internal static bool Active { get; private set; }
    internal static event Action? Completed;
    internal static void Attach(FrameworkElement element, string scope, string key, Func<bool> enabled, Action<string, string, bool> move, bool vertical = false, FrameworkElement? handle = null)
    {
        var grip = handle ?? element;
        Point? origin = null;
        bool started = false;
        Marker? marker = null;
        AdornerLayer? layer = null;
        void Clear() { if (marker is not null) layer?.Remove(marker); marker = null; layer = null; }
        grip.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (!enabled()) return;
            origin = e.GetPosition(grip); started = false;
            if (handle is not null) grip.CaptureMouse();
        };
        grip.PreviewMouseMove += (_, e) =>
        {
            if (!enabled() || origin is null || e.LeftButton != MouseButtonState.Pressed) return;
            var distance = e.GetPosition(grip) - origin.Value;
            if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null; started = true; Active = true;
            if (handle is not null) grip.ReleaseMouseCapture();
            try { DragDrop.DoDragDrop(grip, new DataObject(typeof(Payload), new Payload(scope, key)), DragDropEffects.Move); }
            catch (Exception error) when (error is COMException or InvalidOperationException)
            { MessageBox.Show("Não foi possível concluir o arraste. Use os botões de reordenação em Personalizar.\n" + error.Message, "Think Wind"); }
            finally { Active = false; Clear(); Completed?.Invoke(); }
            e.Handled = true;
        };
        grip.PreviewMouseLeftButtonUp += (_, e) =>
        {
            origin = null; if (handle is not null) grip.ReleaseMouseCapture();
            if (started) { e.Handled = true; started = false; }
        };
        element.AllowDrop = true;
        element.DragOver += (_, e) =>
        {
            var payload = e.Data.GetData(typeof(Payload)) as Payload;
            if (!enabled() || payload?.Scope != scope) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
            e.Effects = DragDropEffects.Move; e.Handled = true;
            var point = e.GetPosition(element);
            bool after = vertical ? point.Y > element.ActualHeight / 2 : point.X > element.ActualWidth / 2;
            if (marker is null)
            {
                layer = AdornerLayer.GetAdornerLayer(element);
                if (layer is not null) { marker = new Marker(element, vertical); layer.Add(marker); }
            }
            if (marker is not null) { marker.After = after; marker.InvalidateVisual(); }
        };
        element.DragLeave += (_, _) => Clear();
        element.Drop += (_, e) =>
        {
            var payload = e.Data.GetData(typeof(Payload)) as Payload;
            Clear();
            if (!enabled() || payload?.Scope != scope) return;
            var point = e.GetPosition(element);
            move(payload.Key, key, vertical ? point.Y > element.ActualHeight / 2 : point.X > element.ActualWidth / 2);
            e.Handled = true;
        };
        element.Unloaded += (_, _) => Clear();
    }
    private sealed class Marker : Adorner
    {
        internal bool After;
        private readonly bool vertical;
        internal Marker(UIElement element, bool isVertical) : base(element) { vertical = isVertical; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext dc)
        {
            var size = AdornedElement.RenderSize;
            var pen = new Pen((Brush)Application.Current.FindResource("Think.Accent"), 3);
            if (vertical) { double y = After ? size.Height : 0; dc.DrawLine(pen, new Point(0, y), new Point(size.Width, y)); }
            else { double x = After ? size.Width : 0; dc.DrawLine(pen, new Point(x, 0), new Point(x, size.Height)); }
        }
    }
}
