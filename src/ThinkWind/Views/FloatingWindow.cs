using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal abstract class FloatingWindow : Window
{
    protected readonly SettingsStore Store;
    protected readonly Border Surface;
    protected abstract bool AttachedTop { get; }
    protected OverlayChrome.Display Display => OverlayChrome.GetDisplay(Store.Value.MonitorIndex, Store.Value.MonitorDeviceName);
    private readonly DispatcherTimer animation = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch elapsed = new();
    private OverlayChrome.PixelRect start, target;
    private bool placed, positioning;
    private double radius;
    private readonly Border centerGuide = new() { Width = 1, Opacity = .35, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
    private (int Width, int Height, int Radius, bool Top) lastClip;
    protected FloatingWindow(SettingsStore store)
    {
        Store = store;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000; Top = -10000; Width = 320; Height = 56;
        Background = Brushes.Transparent;
        SetResourceReference(ForegroundProperty, "Think.Text");
        FontFamily = new FontFamily("Segoe UI Variable Text");
        Surface = new Border { BorderThickness = new Thickness(0), Padding = new Thickness(10), SnapsToDevicePixels = true };
        var frame = new Grid(); frame.Children.Add(Surface);
        centerGuide.SetResourceReference(Border.BackgroundProperty, "Think.Accent");
        frame.Children.Add(centerGuide); Content = frame;
        SourceInitialized += (_, _) =>
        {
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WindowMessage);
            ApplyAppearance();
        };
        Loaded += (_, _) => Position();
        Closing += (_, e) => { if (Application.Current is App app && !app.IsExiting) { e.Cancel = true; Hide(); } };
        Closed += (_, _) => animation.Stop();
        animation.Tick += (_, _) =>
        {
            var progress = Math.Clamp(elapsed.Elapsed.TotalMilliseconds / Math.Max(1, Store.Value.AnimationMilliseconds), 0, 1);
            var eased = 1 - Math.Pow(1 - progress, 3);
            Move(new OverlayChrome.PixelRect
            {
                Left = Blend(start.Left, target.Left, eased), Top = Blend(start.Top, target.Top, eased),
                Right = Blend(start.Right, target.Right, eased), Bottom = Blend(start.Bottom, target.Bottom, eased)
            });
            if (progress >= 1) animation.Stop();
        };
    }
    private static int Blend(int a, int b, double amount) => (int)Math.Round(a + (b - a) * amount);
    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0005) UpdateClip();
        if (message is 0x007E or 0x02E0 or 0x001A)
        {
            if (!positioning) Dispatcher.BeginInvoke(new Action(() => { ApplyAppearance(); Position(); }));
        }
        return 0;
    }
    protected void ApplyAppearance()
    {
        var p = Store.Value;
        radius = AttachedTop ? p.IslandRadius : p.DockRadius;
        var material = AttachedTop ? p.IslandMaterial : p.DockMaterial;
        var alpha = (AttachedTop ? p.IslandOpacity : p.DockOpacity) ?? p.TintOpacity;
        bool backdrop = OverlayChrome.Material(this, SystemParameters.HighContrast ? "Solid" : material, ThemeService.Light);
        var color = ThemeService.BaseColor;
        color.A = (byte)Math.Round(255 * (backdrop ? alpha : 1));
        Surface.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(color);
        Surface.CornerRadius = AttachedTop ? new CornerRadius(0, 0, radius, radius) : new CornerRadius(radius);
        centerGuide.Visibility = Store.IsEditing ? Visibility.Visible : Visibility.Collapsed;
        lastClip = default; UpdateClip();
    }
    private void UpdateClip()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || !OverlayChrome.GetWindowRect(hwnd, out var rectangle)) return;
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        var next = (rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, (int)Math.Round(radius * scale), AttachedTop);
        if (next == lastClip || next.Item1 < 1 || next.Item2 < 1) return;
        lastClip = next;
        OverlayChrome.Clip(hwnd, next.Item1, next.Item2, next.Item3, AttachedTop);
    }
    protected void Place(double width, double height, bool animate = false)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0) return;
        var display = Display;
        int margin = AttachedTop ? 0 : (int)Math.Round(Store.Value.DockBottomOffset * display.Scale);
        int w = (int)Math.Round(Math.Clamp(width, 48, Math.Max(48, display.Width - 16)) * display.Scale);
        int h = (int)Math.Round(Math.Clamp(height, 6, Math.Max(56, display.Height - 8)) * display.Scale);
        double offset = AttachedTop ? Store.Value.IslandHorizontalOffset : Store.Value.DockHorizontalOffset;
        int x = (int)Math.Round(display.Work.Left + (display.Work.Width - w) / 2d + offset * display.Scale);
        x = Math.Clamp(x, display.Work.Left + (AttachedTop ? 0 : 8), Math.Max(display.Work.Left + 8, display.Work.Right - w - (AttachedTop ? 0 : 8)));
        int y = AttachedTop ? display.Bounds.Top : display.Work.Bottom - margin - h;
        target = new OverlayChrome.PixelRect { Left = x, Top = y, Right = x + w, Bottom = y + h };
        animation.Stop();
        if (animate && placed && SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0 && OverlayChrome.GetWindowRect(hwnd, out start)
            && Math.Abs(start.Left - target.Left) < display.Work.Width / 2 && Math.Abs(start.Top - target.Top) < display.Work.Height / 2)
        { elapsed.Restart(); animation.Start(); }
        else Move(target);
        placed = true;
    }
    private void Move(OverlayChrome.PixelRect rectangle)
    {
        positioning = true;
        try { OverlayChrome.Move(new WindowInteropHelper(this).Handle, rectangle); UpdateClip(); }
        finally { positioning = false; }
    }
    protected void AttachPositionGrip(FrameworkElement grip)
    {
        System.Drawing.Point? down = null;
        double original = 0;
        grip.Cursor = Cursors.SizeWE;
        grip.ToolTip = "Arraste para ajustar a posição horizontal";
        grip.MouseLeftButtonDown += (_, e) =>
        {
            if (!Store.IsEditing) return;
            down = System.Windows.Forms.Cursor.Position;
            original = AttachedTop ? Store.Value.IslandHorizontalOffset : Store.Value.DockHorizontalOffset;
            grip.CaptureMouse(); e.Handled = true;
        };
        grip.MouseMove += (_, e) =>
        {
            if (down is null || !grip.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
            var delta = (System.Windows.Forms.Cursor.Position.X - down.Value.X) / Display.Scale;
            if (AttachedTop) Store.Value.IslandHorizontalOffset = original + delta;
            else Store.Value.DockHorizontalOffset = original + delta;
            Position();
        };
        grip.MouseLeftButtonUp += (_, e) =>
        {
            if (down is null) return;
            down = null; grip.ReleaseMouseCapture(); Store.Update(_ => { }); e.Handled = true;
        };
        grip.LostMouseCapture += (_, _) => { if (down is not null) { down = null; Store.Update(_ => { }); } };
    }
    public virtual void RefreshSettings() { ApplyAppearance(); Position(); }
    protected static Button ActionButton(string label, Action action, string? tooltip = null)
    {
        var button = new Button { Content = label, ToolTip = tooltip ?? label };
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip ?? label);
        button.Click += (_, _) =>
        {
            if (LayoutDrag.Active) return;
            try { action(); }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            { MessageBox.Show(e.Message, "Think Wind"); }
        };
        return button;
    }
    protected static TextBlock Glyph(string glyph, double size = 19) => new() { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    public abstract void Position();
}
