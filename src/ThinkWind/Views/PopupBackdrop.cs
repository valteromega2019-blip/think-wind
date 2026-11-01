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
        internal bool Available => host.MaterialAvailable;
        internal Lease(FrameworkElement element)
        {
            content = element;
            source = (HwndSource)PresentationSource.FromVisual(element);
            host = new BackdropHost(source.Handle, false, 0, "Menu");
            try { source.AddHook(Message); content.SizeChanged += SizeChanged; Sync(); }
            catch { source.RemoveHook(Message); content.SizeChanged -= SizeChanged; host.Dispose(); throw; }
        }
        private nint Message(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
        { if (message == 0x0047 || message == 0x02E0) Sync(); return 0; }
        private void SizeChanged(object sender, SizeChangedEventArgs e) => Sync();
        private void Sync()
        {
            host.SetMaterial(SystemParameters.HighContrast ? "Solid" : "Acrylic", ThemeService.Light);
            if (OverlayChrome.GetWindowRect(source.Handle, out var rect))
                host.Synchronize(rect, Corner(content) * (source.CompositionTarget?.TransformToDevice.M11 ?? 1), false, content.IsVisible);
            if (!host.MaterialAvailable) Fallback(content);
        }
        public void Dispose() { source.RemoveHook(Message); content.SizeChanged -= SizeChanged; host.Dispose(); }
    }
    private static readonly ConditionalWeakTable<FrameworkElement, Lease> leases = new();
    private static readonly DependencyProperty AttachedProperty = DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(PopupBackdrop), new PropertyMetadata(false));
    internal static bool HasMaterial(FrameworkElement content) => leases.TryGetValue(content, out var lease) && lease.Available;
    internal static void Initialize()
    {
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.OpenedEvent, new RoutedEventHandler((s, _) => Open((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(ContextMenu), ContextMenu.ClosedEvent, new RoutedEventHandler((s, _) => Close((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((s, _) => Open((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.ClosedEvent, new RoutedEventHandler((s, _) => Close((FrameworkElement)s)));
        EventManager.RegisterClassHandler(typeof(Popup), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        { if (s is Popup popup && popup.Name == "PART_Popup") Attach(popup); }));
    }
    internal static void Attach(Popup popup)
    {
        if ((bool)popup.GetValue(AttachedProperty)) return;
        popup.SetValue(AttachedProperty, true);
        popup.Opened += (_, _) => { if (popup.Child is FrameworkElement content) Open(content); };
        popup.Closed += (_, _) => { if (popup.Child is FrameworkElement content) Close(content); };
    }
    internal static double Corner(DependencyObject node)
    {
        if (node is Border border) return Math.Max(border.CornerRadius.TopLeft, border.CornerRadius.BottomLeft);
        return node is Visual && VisualTreeHelper.GetChildrenCount(node) > 0 ? Corner(VisualTreeHelper.GetChild(node, 0)) : 12;
    }
    private static void Open(FrameworkElement content)
    {
        Close(content);
        if (PresentationSource.FromVisual(content) is not HwndSource) return;
        try { leases.Add(content, new Lease(content)); }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            Fallback(content);
        }
    }
    private static void Close(FrameworkElement content)
    { if (leases.TryGetValue(content, out var lease)) { leases.Remove(content); lease.Dispose(); } }
    private static void Fallback(FrameworkElement content)
    {
        if (content is Control control) control.SetResourceReference(Control.BackgroundProperty, "Think.Background");
        else if (content is Border border) border.SetResourceReference(Border.BackgroundProperty, "Think.Background");
    }
}
