using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

// Popup HWNDs cannot inherit a Window backdrop. Give them the same independent
// native composition host, with no activation and no opaque WPF base layer.
internal static class PopupBackdrop
{
    private sealed class Lease : IDisposable
    {
        private readonly FrameworkElement content;
        private readonly HwndSource source;
        private readonly BackdropHost host;
        private bool syncing;
        internal bool Available => host.MaterialAvailable;
        internal bool UsesSource(HwndSource candidate) => ReferenceEquals(source, candidate);
        internal void Refresh() => Sync();
        internal Lease(FrameworkElement element)
        {
            content = element;
            source = (HwndSource)PresentationSource.FromVisual(element);
            host = new BackdropHost(source.Handle, false, 0, "Menu");
            try { source.AddHook(Message); content.SizeChanged += SizeChanged; content.IsVisibleChanged += VisibilityChanged; Sync(); }
            catch { source.RemoveHook(Message); content.SizeChanged -= SizeChanged; content.IsVisibleChanged -= VisibilityChanged; host.Dispose(); throw; }
        }
        private nint Message(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        { if (message == 0x0047 || message == 0x02E0) Sync(); return 0; }
        private void SizeChanged(object sender, SizeChangedEventArgs e) => Sync();
        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => Sync();
        private void Sync()
        {
            if (syncing || source.IsDisposed) return;
            syncing = true;
            try
            {
            host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
            if (OverlayChrome.GetWindowRect(source.Handle, out var rect))
                host.Synchronize(rect, Corner(content) * (source.CompositionTarget?.TransformToDevice.M11 ?? 1), false, content.IsVisible);
            if (!host.MaterialAvailable) Fallback(content);
            else RestoreBackground(content);
            }
            finally { syncing = false; }
        }
        public void Dispose() { source.RemoveHook(Message); content.SizeChanged -= SizeChanged; content.IsVisibleChanged -= VisibilityChanged; host.Dispose(); }
    }
    private static readonly ConditionalWeakTable<FrameworkElement, Lease> leases = new();
    private static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(PopupBackdrop), new PropertyMetadata(false));
    private static readonly DependencyProperty FallbackAppliedProperty = DependencyProperty.RegisterAttached("FallbackApplied", typeof(bool), typeof(PopupBackdrop), new PropertyMetadata(false));
    internal static bool HasMaterial(FrameworkElement content) => leases.TryGetValue(content, out var lease) && lease.Available;
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((s, _) => OpenWhenReady((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent, new RoutedEventHandler((s, _) => Close((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((s, _) => OpenWhenReady((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.ClosedEvent, new RoutedEventHandler((s, _) => Close((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(Popup), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        { if (s is Popup popup && popup.Name == "PART_Popup") Attach(popup); }));
        // ComboBox popup roots are separate HWND trees and may never raise a
        // Loaded event through the Popup itself. Attach to the actual template.
        EventManager.RegisterClassHandler(typeof(ComboBox), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is ComboBox combo) { combo.DropDownOpened -= ComboOpened; combo.DropDownOpened += ComboOpened; }
        }));
    }
    internal static void ComboOpened(object? sender, EventArgs args)
    {
        if (sender is not ComboBox combo) return;
        combo.ApplyTemplate();
        if (combo.Template.FindName("PART_Popup", combo) is not Popup popup) return;
        popup.PlacementTarget = combo;
        Attach(popup);
        if (popup.Child is FrameworkElement child) OpenWhenReady(child);
        combo.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            // Opened can run before the separate popup HWND becomes visible.
            // Synchronize once after its first layout, not with a polling timer.
            if (popup.IsOpen && popup.Child is FrameworkElement visible && visible.IsVisible) OpenWhenReady(visible);
        }));
    }
    internal static void Attach(Popup popup)
    {
        if ((bool)popup.GetValue(AttachedProperty)) return;
        popup.SetValue(AttachedProperty, true);
        popup.Opened += (_, _) => { if (popup.Child is FrameworkElement content) OpenWhenReady(content); };
        popup.Closed += (_, _) => { if (popup.Child is FrameworkElement content) Close(content); };
    }
    internal static double Corner(DependencyObject node)
    {
        if (node is Border border) return Math.Max(border.CornerRadius.TopLeft, border.CornerRadius.BottomLeft);
        return node is Visual && VisualTreeHelper.GetChildrenCount(node) > 0 ? Corner(VisualTreeHelper.GetChild(node, 0)) : 12;
    }
    private static void Open(FrameworkElement content)
    {
        if (PresentationSource.FromVisual(content) is not HwndSource source) return;
        if (leases.TryGetValue(content, out var existing) && existing.UsesSource(source))
        {
            existing.Refresh();
            return;
        }
        Close(content);
        try { leases.Add(content, new Lease(content)); }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            Fallback(content);
        }
    }
    private static void OpenWhenReady(FrameworkElement content)
    {
        if (PresentationSource.FromVisual(content) is HwndSource) { Open(content); return; }
        content.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (content.IsVisible && PresentationSource.FromVisual(content) is HwndSource) Open(content);
        }));
    }
    private static void Close(FrameworkElement content)
    {
        try { if (leases.TryGetValue(content, out var lease)) { leases.Remove(content); lease.Dispose(); } }
        finally { RestoreBackground(content); }
    }
    private static DependencyProperty? BackgroundProperty(FrameworkElement content) => content switch
    {
        Control => Control.BackgroundProperty,
        Border => Border.BackgroundProperty,
        _ => null
    };
    private static void Fallback(FrameworkElement content)
    {
        if (BackgroundProperty(content) is not { } property) return;
        // Keep the original value source (including bindings and dynamic resources).
        content.SetCurrentValue(property, content.FindResource("Think.Background"));
        content.SetValue(FallbackAppliedProperty, true);
    }
    private static void RestoreBackground(FrameworkElement content)
    {
        if (!(bool)content.GetValue(FallbackAppliedProperty)) return;
        content.ClearValue(FallbackAppliedProperty);
        if (BackgroundProperty(content) is { } property) content.InvalidateProperty(property);
    }
}
