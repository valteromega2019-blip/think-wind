using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
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
    protected virtual bool UsesIslandAppearance => AttachedTop;
    protected virtual bool CollapsedDock => false;
    protected virtual bool FullWidthTop => false;
    protected virtual string? TopPanelSide => null;
    protected virtual double TopEdgeOffset => 0;
    protected virtual bool SuppressMaterial => false;
    protected virtual Color MaterialTint => ThemeService.BaseColor;
    protected virtual bool MaterialLight => ThemeService.Light;
    protected virtual Thickness TransparentInsets => new();
    protected virtual bool HideOnClose => true;
    protected virtual string? MaterialOverride => null;
    protected OverlayChrome.Display Display => OverlayChrome.GetDisplay(Store.Value.MonitorIndex, Store.Value.MonitorDeviceName);
    private readonly FrameClock animation = new();
    private readonly Stopwatch elapsed = new();
    private OverlayChrome.PixelRect start, target;
    private bool placed, positioning;
    private BackdropHost? backdrop;
    internal nint BackdropHandle => backdrop?.Handle ?? 0;
    internal bool HasNativeMaterial => backdrop?.MaterialAvailable == true;
    protected bool IsPositionAnimating => animation.IsRunning;
    private double radius;
    private double animationDuration;
    private readonly Border centerGuide = new() { Width = 1, Opacity = .35, IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center, Visibility = Visibility.Collapsed };
    private (int Width, int Height, int Radius, bool Top, double Scale) lastClip;
    protected FloatingWindow(SettingsStore store)
    {
        Store = store;
        Icon = AppBrand.WindowIcon;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = -10000; Top = -10000; Width = 320; Height = 56;
        Background = Brushes.Transparent;
        // Remove the themed Window border/background container as well as the
        // non-client frame. Only our alpha-clipped content is painted.
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = RelativeSource.TemplatedParent });
        Template = new ControlTemplate(typeof(Window)) { VisualTree = presenter };
        SetResourceReference(ForegroundProperty, "Think.Text");
        FontFamily = new FontFamily("Segoe UI Variable Text");
        Surface = new Border { BorderThickness = new Thickness(0), Padding = new Thickness(10), SnapsToDevicePixels = true };
        var frame = new Grid(); frame.Children.Add(Surface);
        centerGuide.SetResourceReference(Border.BackgroundProperty, "Think.Accent");
        frame.Children.Add(centerGuide); Content = frame;
        SourceInitialized += (_, _) =>
        {
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            source.AddHook(WindowMessage);
            backdrop = new BackdropHost(this);
            ApplyAppearance();
        };
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) { if (animation.IsRunning) { animation.Stop(); if (placed) Move(target); } backdrop?.Hide(); }
            else UpdateClip();
        };
        Loaded += (_, _) => Position();
        Closing += (_, e) => { if (HideOnClose && Application.Current is App app && !app.IsExiting) { e.Cancel = true; Hide(); } };
        Closed += (_, _) => { animation.Stop(); backdrop?.Dispose(); };
        animation.Tick += (_, _) =>
        {
            var progress = Math.Clamp(elapsed.Elapsed.TotalMilliseconds / Math.Max(1, animationDuration), 0, 1);
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
        if (message == 0x0005 && !positioning) UpdateClip();
        if (message is 0x007E or 0x02E0 or 0x001A)
        {
            if (!positioning) Dispatcher.BeginInvoke(new Action(() => { ApplyAppearance(); Position(); }));
        }
        return 0;
    }
    protected void ApplyAppearance()
    {
        FontSizing.Apply(this, Store.Value.FontScale);
        var p = Store.Value;
        radius = CollapsedDock ? 3 : FullWidthTop ? 0 : UsesIslandAppearance ? p.IslandRadius : p.DockRadius;
        var material = MaterialOverride ?? (UsesIslandAppearance ? p.IslandMaterial : p.DockMaterial);
        var alpha = (UsesIslandAppearance ? p.IslandOpacity : p.DockOpacity) ?? p.TintOpacity;
        backdrop?.SetMaterial(SystemParameters.HighContrast ? "Solid" : material, MaterialLight);
        var color = MaterialTint;
        color.A = (byte)Math.Round(255 * (backdrop?.MaterialAvailable == true ? alpha : 1));
        Surface.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(color);
        Surface.CornerRadius = AttachedTop ? new CornerRadius(0, 0, radius, radius) : new CornerRadius(radius);
        Surface.Margin = TransparentInsets;
        centerGuide.Visibility = Store.IsEditing ? Visibility.Visible : Visibility.Collapsed;
        UpdateClip();
    }
    private void UpdateClip()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0 || !OverlayChrome.GetWindowRect(hwnd, out var rectangle)) return;
        if (SuppressMaterial) { backdrop?.Hide(); return; }
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        var next = (rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, (int)Math.Round(radius * scale), AttachedTop, scale);
        if (next.Item1 < 1 || next.Item2 < 1) return;
        var inset = TransparentInsets;
        if (Surface.Margin != inset) Surface.Margin = inset;
        if (inset != new Thickness())
        {
            // The foreground HWND is a transparent canvas for protruding icons.
            // Only the compact material HWND is rounded/clipped and painted.
            ((Grid)Content).Clip = null; lastClip = default;
            rectangle.Left += (int)Math.Round(inset.Left * scale); rectangle.Right -= (int)Math.Round(inset.Right * scale);
            rectangle.Top += (int)Math.Round(inset.Top * scale); rectangle.Bottom -= (int)Math.Round(inset.Bottom * scale);
            if (rectangle.Right > rectangle.Left && rectangle.Bottom > rectangle.Top)
                backdrop?.Synchronize(rectangle, next.Item3, AttachedTop, IsVisible);
            else backdrop?.Hide();
            return;
        }
        // The foreground is per-pixel alpha; only the material HWND uses an HRGN.
        // Transparent pixels outside this WPF geometry do not receive mouse input.
        if (next != lastClip)
        {
            lastClip = next;
            var geometry = new StreamGeometry();
            double width = next.Item1 / scale, height = next.Item2 / scale;
            double r = Math.Min(radius, Math.Min(width, height) / 2);
            using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(AttachedTop ? 0 : r, 0), true, true);
                context.LineTo(new Point(AttachedTop ? width : width - r, 0), true, false);
                if (!AttachedTop) context.ArcTo(new Point(width, r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(width, height - r), true, false);
                context.ArcTo(new Point(width - r, height), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(r, height), true, false);
                context.ArcTo(new Point(0, height - r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                context.LineTo(new Point(0, AttachedTop ? 0 : r), true, false);
                if (!AttachedTop) context.ArcTo(new Point(r, 0), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze(); ((Grid)Content).Clip = geometry;
        }
        backdrop?.Synchronize(rectangle, next.Item3, AttachedTop, IsVisible);
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
        int y = AttachedTop ? display.Bounds.Top + (int)Math.Round(TopEdgeOffset * display.Scale) : display.Work.Bottom - margin - h;
        if (AttachedTop && (TopPanelSide is "Left" or "Right"))
            x = TopPanelSide == "Left" ? display.Work.Left + 8 : display.Work.Right - w - 8;
        else if (AttachedTop && TopPanelSide == "Center")
            x = display.Work.Left + (display.Work.Width - w) / 2;
        if (AttachedTop && FullWidthTop) { x = display.Bounds.Left; w = display.Bounds.Width; }
        if (CollapsedDock)
        {
            w = Math.Min(display.Bounds.Width, (int)Math.Round(96 * display.Scale));
            h = Math.Max(1, (int)Math.Round(6 * display.Scale));
            x = display.Bounds.Left + (display.Bounds.Width - w) / 2;
            y = display.Bounds.Bottom - h - 4;
        }
        var nextTarget = new OverlayChrome.PixelRect { Left = x, Top = y, Right = x + w, Bottom = y + h };
        if (placed && target.Left == nextTarget.Left && target.Top == nextTarget.Top && target.Right == nextTarget.Right && target.Bottom == nextTarget.Bottom)
        {
            // The shell/WPF may move a registered appbar after Show. A cached
            // target is not proof that the actual native rectangle is still there.
            if (!animation.IsRunning && OverlayChrome.GetWindowRect(hwnd, out var actual)
                && (actual.Left != target.Left || actual.Top != target.Top || actual.Right != target.Right || actual.Bottom != target.Bottom)) Move(target);
            return;
        }
        target = nextTarget;
        animation.Stop();
        if (animate && placed && SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0 && OverlayChrome.GetWindowRect(hwnd, out start)
            && ((AttachedTop && TopPanelSide is not null) || Math.Abs(start.Left - target.Left) < display.Work.Width / 2) && Math.Abs(start.Top - target.Top) < display.Work.Height / 2)
        { animationDuration = Store.Value.AnimationMilliseconds; elapsed.Restart(); animation.Start(); }
        else Move(target);
        placed = true;
    }
    private void Move(OverlayChrome.PixelRect rectangle)
    {
        positioning = true;
        try { OverlayChrome.Move(new WindowInteropHelper(this).Handle, rectangle); UpdateClip(); }
        finally { positioning = false; }
    }
    internal OverlayChrome.PixelRect Bounds
    {
        get { OverlayChrome.GetWindowRect(new WindowInteropHelper(this).Handle, out var rectangle); return rectangle; }
    }
    internal OverlayChrome.PixelRect TargetBounds => placed ? target : Bounds;
    protected void PlaceNear(double width, double height, OverlayChrome.PixelRect anchor, bool animate)
    {
        if (new WindowInteropHelper(this).Handle == 0) return;
        var display = Display;
        int w = (int)Math.Round(Math.Min(width, display.Width - 16) * display.Scale);
        int h = (int)Math.Round(Math.Min(height, display.Height - 16) * display.Scale);
        int x = Math.Clamp((anchor.Left + anchor.Right - w) / 2, display.Work.Left + 8, Math.Max(display.Work.Left + 8, display.Work.Right - w - 8));
        int y = Math.Clamp(anchor.Bottom + (int)(8 * display.Scale), display.Bounds.Top + 8, Math.Max(display.Bounds.Top + 8, display.Work.Bottom - h - 8));
        var next = new OverlayChrome.PixelRect { Left = x, Top = y, Right = x + w, Bottom = y + h };
        if (placed && target.Left == next.Left && target.Top == next.Top && target.Right == next.Right && target.Bottom == next.Bottom) return;
        animation.Stop(); target = next;
        if (animate && placed && Store.Value.AnimationMilliseconds > 0 && SystemParameters.ClientAreaAnimation && OverlayChrome.GetWindowRect(new WindowInteropHelper(this).Handle, out start))
        { animationDuration = Store.Value.AnimationMilliseconds; elapsed.Restart(); animation.Start(); }
        else Move(target);
        placed = true;
    }
    protected void PlaceAbove(double width, double height, OverlayChrome.PixelRect anchor, bool animate)
    {
        if (new WindowInteropHelper(this).Handle == 0) return;
        var display = Display;
        int gap = Math.Max(1, (int)Math.Round(8 * display.Scale));
        int w = Math.Max(48, (int)Math.Round(Math.Min(width, display.Width - 16) * display.Scale));
        int available = Math.Max(48, anchor.Top - display.Work.Top - gap * 2);
        int h = Math.Min(available, Math.Max(48, (int)Math.Round(height * display.Scale)));
        int x = Math.Clamp((anchor.Left + anchor.Right - w) / 2, display.Work.Left + gap, Math.Max(display.Work.Left + gap, display.Work.Right - w - gap));
        int y = Math.Max(display.Work.Top + gap, anchor.Top - gap - h);
        var next = new OverlayChrome.PixelRect { Left = x, Top = y, Right = x + w, Bottom = y + h };
        if (placed && next.Left == target.Left && next.Top == target.Top && next.Right == target.Right && next.Bottom == target.Bottom) return;
        animation.Stop(); target = next;
        if (animate && placed && SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0 && OverlayChrome.GetWindowRect(new WindowInteropHelper(this).Handle, out start)
            && Math.Abs(start.Left - target.Left) < display.Work.Width / 2 && Math.Abs(start.Top - target.Top) < display.Work.Height / 2)
        { animationDuration = Store.Value.AnimationMilliseconds; elapsed.Restart(); animation.Start(); }
        else Move(target);
        placed = true;
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
    public virtual void RefreshAppearance() => ApplyAppearance();
    public virtual void RefreshSettings() { ApplyAppearance(); Position(); }
    protected static Button ActionButton(string label, Action action, string? tooltip = null)
    {
        var button = new Button { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }, ToolTip = tooltip ?? label };
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
