using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal abstract class FloatingWindow : Window
{
    protected readonly SettingsStore Store;
    protected readonly Border Surface;
    protected abstract bool AttachedTop { get; }
    protected virtual bool SquareTopCorners => AttachedTop;
    protected virtual bool UsesIslandAppearance => AttachedTop;
    protected virtual bool CollapsedDock => false;
    protected virtual bool FullWidthTop => false;
    protected virtual bool FullWidthBottom => false;
    protected virtual int? ReservedBottomTop => null;
    protected virtual string? TopPanelSide => null;
    protected virtual double TopEdgeOffset => 0;
    protected virtual bool SuppressMaterial => false;
    protected virtual bool TransparentSurface => false;
    protected virtual Color MaterialTint => Store.Value.UseCustomBarColor
        ? (Color)ColorConverter.ConvertFromString(Store.Value.BarBackgroundColor) : ThemeService.BaseColor;
    protected virtual bool MaterialLight => ThemeService.Light;
    protected virtual Thickness TransparentInsets => new();
    protected virtual bool HideOnClose => true;
    protected virtual string? MaterialOverride => null;
    protected virtual double TextScale => Store.Value.FontScale;
    internal string? MonitorDeviceOverride { get; set; }
    private bool retiring;
    internal void Retire() { retiring = true; Close(); }
    protected OverlayChrome.Display Display => OverlayChrome.GetDisplay(Store.Value.MonitorIndex, MonitorDeviceOverride ?? Store.Value.MonitorDeviceName);
    private readonly FrameClock animation = new();
    private readonly Stopwatch elapsed = new();
    private OverlayChrome.PixelRect start, target;
    private bool placed, positioning;
    private bool contrastPending;
    private BackdropHost? backdrop;
    internal nint BackdropHandle => backdrop?.Handle ?? 0;
    internal bool HasNativeMaterial => backdrop?.MaterialAvailable == true;
    internal void RefreshTextContrastSoon()
    {
        if (contrastPending) return;
        contrastPending = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            contrastPending = false;
            if (IsVisible) Typography.ApplyContrast(Surface, MaterialTint, Store.Value);
        }));
    }
    internal double AnimationMilliseconds => Store.Value.AnimationMilliseconds;
    protected bool IsPositionAnimating => animation.IsRunning;
    private double radius;
    private double animationDuration;
    private UIElement? openingContent;
    private (int Width, int Height, int Radius, bool Top, double Scale) lastClip;
    private readonly RectangleGeometry roundedClip = new();
    private readonly RectangleGeometry topClip = new();
    private GeometryGroup? roundedTopClip;
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
        var adorners = new FrameworkElementFactory(typeof(System.Windows.Documents.AdornerDecorator));
        adorners.AppendChild(presenter);
        Template = new ControlTemplate(typeof(Window)) { VisualTree = adorners };
        SetResourceReference(ForegroundProperty, "Think.Text");
        SetResourceReference(FontFamilyProperty, "Think.FontFamily");
        Surface = new Border { BorderThickness = new Thickness(0), Padding = new Thickness(10), SnapsToDevicePixels = true };
        var frame = new Grid(); frame.Children.Add(Surface);
        Content = frame;
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
            if (!IsVisible)
            {
                if (animation.IsRunning) { animation.Stop(); if (placed) Move(target); } backdrop?.Hide();
                openingContent?.BeginAnimation(UIElement.OpacityProperty, null); openingContent = null;
            }
            else
            {
                UpdateClip();
                if (SystemParameters.ClientAreaAnimation && Store.Value.AnimationMilliseconds > 0 && Surface.Child is UIElement content)
                {
                    openingContent = content;
                    content.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(.9, 1,
                        TimeSpan.FromMilliseconds(Math.Min(180, Store.Value.AnimationMilliseconds)))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop }, HandoffBehavior.SnapshotAndReplace);
                }
            }
        };
        Loaded += (_, _) => Position();
        Closing += (_, e) => { if (!retiring && HideOnClose && Application.Current is App app && !app.IsExiting) { e.Cancel = true; Hide(); } };
        Closed += (_, _) => { animation.Stop(); backdrop?.Dispose(); };
        animation.Tick += (_, _) =>
        {
            var progress = Math.Clamp(elapsed.Elapsed.TotalMilliseconds / Math.Max(1, animationDuration), 0, 1);
            // Smoother endpoint acceleration, without changing the duration.
            var eased = progress * progress * progress * (progress * (progress * 6 - 15) + 10);
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
        // DragMove and native positioning can move the content without a size
        // change. Keep the independent material HWND aligned on those moves.
        // Our animated Move already synchronizes once after SetWindowPos.
        if ((message == 0x0005 || message == 0x0047) && !positioning) UpdateClip();
        if (message is 0x007E or 0x02E0 or 0x001A)
        {
            if (!positioning) Dispatcher.BeginInvoke(new Action(() => { ApplyAppearance(); Position(); }));
        }
        return 0;
    }
    protected void ApplyAppearance()
    {
        FontSizing.Apply(this, TextScale);
        var p = Store.Value;
        radius = CollapsedDock ? 3 : FullWidthTop || FullWidthBottom ? 0 : UsesIslandAppearance ? p.IslandRadius : p.DockRadius;
        var material = MaterialOverride ?? (UsesIslandAppearance ? p.IslandMaterial : p.DockMaterial);
        var alpha = (UsesIslandAppearance ? p.IslandOpacity : p.DockOpacity) ?? p.TintOpacity;
        // Both themes use the same backdrop and user opacity, without a light-only opaque veil.
        backdrop?.SetMaterial(SystemParameters.HighContrast ? "Solid" : material, MaterialLight);
        var color = MaterialTint;
        var foreground = Typography.Ink((Color)ColorConverter.ConvertFromString(p.FontColor), color, p.AutomaticTextContrast);
        var textBrush = new SolidColorBrush(foreground); textBrush.Freeze();
        Resources["Think.Text"] = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : textBrush;
        Resources["Think.Muted"] = Resources["Think.Text"];
        color.A = (byte)Math.Round(255 * (backdrop?.MaterialAvailable == true ? alpha : 1));
        collapsedHandleHighlighted = false;
        Surface.Background = SystemParameters.HighContrast ? SystemColors.WindowBrush : new SolidColorBrush(color);
        Typography.ApplyContrast(Surface, MaterialTint, p);
        Surface.BorderThickness = new Thickness(CollapsedDock && SystemParameters.HighContrast ? .75 : 0);
        Surface.BorderBrush = SystemParameters.HighContrast ? SystemColors.WindowTextBrush
            : new SolidColorBrush(MaterialLight ? Color.FromArgb(110, 28, 40, 54) : Color.FromArgb(185, 215, 228, 242));
        // Keep the same theme tint while collapsing. Switching immediately to
        // a pale handle gradient painted the entire shrinking bar light gray.
        if (TransparentSurface) Surface.Background = Brushes.Transparent;
        var corners = SquareTopCorners ? new CornerRadius(0, 0, radius, radius) : new CornerRadius(radius);
        if (Surface.CornerRadius != corners) Surface.CornerRadius = corners;
        Surface.Margin = TransparentInsets;
        UpdateClip();
    }
    private bool collapsedHandleHighlighted;
    protected void UpdateClip() => UpdateClip(null);
    private void UpdateClip(OverlayChrome.PixelRect? destination)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == 0) return;
        OverlayChrome.PixelRect rectangle;
        if (destination is { } requested) rectangle = requested;
        else if (!OverlayChrome.GetWindowRect(hwnd, out rectangle)) return;
        if (SuppressMaterial)
        {
            // Reused preview windows can still carry the rounded clip of a
            // previous, smaller native-window/player panel. Individual-card
            // mode owns its outlines; retaining that clip slices the new fan.
            ((Grid)Content).Clip = null;
            lastClip = default;
            if (destination.HasValue) OverlayChrome.Move(hwnd, rectangle);
            backdrop?.Hide();
            return;
        }
        if (backdrop is null && destination.HasValue) OverlayChrome.Move(hwnd, rectangle);
        var scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        var next = (rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top, (int)Math.Round(radius * scale), SquareTopCorners, scale);
        if (next.Item1 < 1 || next.Item2 < 1) return;
        // Reveal the light handle only at its final compact dimensions, so the
        // expanded Acrylic does not flash pale during the shrinking animation.
        bool highlightHandle = CollapsedDock && next.Item1 / scale <= 100 && next.Item2 / scale <= 8;
        if (highlightHandle != collapsedHandleHighlighted)
        {
            collapsedHandleHighlighted = highlightHandle;
            if (highlightHandle && !SystemParameters.HighContrast)
            {
                Surface.Background = new SolidColorBrush(Color.FromArgb(205, 210, 224, 237));
                Surface.BorderThickness = new Thickness(.5);
            }
        }
        var inset = TransparentInsets;
        if (Surface.Margin != inset) Surface.Margin = inset;
        if (inset != new Thickness())
        {
            // The foreground HWND is a transparent canvas for protruding icons.
            // Only the compact material HWND is rounded/clipped and painted.
            ((Grid)Content).Clip = null; lastClip = default;
            rectangle.Left += (int)Math.Round(inset.Left * scale); rectangle.Right -= (int)Math.Round(inset.Right * scale);
            rectangle.Top += (int)Math.Round(inset.Top * scale); rectangle.Bottom -= (int)Math.Round(inset.Bottom * scale);
            AlignSurfaceCorners(rectangle, scale);
            if (rectangle.Right > rectangle.Left && rectangle.Bottom > rectangle.Top)
                backdrop?.Synchronize(rectangle, next.Item3, SquareTopCorners, IsVisible, destination);
            else { if (destination is { } content) OverlayChrome.Move(hwnd, content); backdrop?.Hide(); }
            return;
        }
        // The foreground is per-pixel alpha; only the material HWND uses an HRGN.
        // Transparent pixels outside this WPF geometry do not receive mouse input.
        AlignSurfaceCorners(rectangle, scale);
        if (next != lastClip)
        {
            lastClip = next;
            double width = next.Item1 / scale, height = next.Item2 / scale;
            double r = Math.Min(radius, Math.Min(width, height) / 2);
            roundedClip.Rect = new Rect(0, 0, width, height);
            roundedClip.RadiusX = roundedClip.RadiusY = r;
            Geometry geometry = roundedClip;
            if (SquareTopCorners)
            {
                if (roundedTopClip is null)
                {
                    roundedTopClip = new GeometryGroup { FillRule = FillRule.Nonzero };
                    roundedTopClip.Children.Add(roundedClip); roundedTopClip.Children.Add(topClip);
                }
                topClip.Rect = new Rect(0, 0, width, r);
                geometry = roundedTopClip;
            }
            if (!ReferenceEquals(((Grid)Content).Clip, geometry)) ((Grid)Content).Clip = geometry;
        }
        backdrop?.Synchronize(rectangle, next.Item3, SquareTopCorners, IsVisible, destination);
    }
    private void AlignSurfaceCorners(OverlayChrome.PixelRect rectangle, double scale)
    {
        double effective = Math.Clamp(radius, 0, Math.Max(0, Math.Min(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top)) / (2 * scale));
        var corners = SquareTopCorners ? new CornerRadius(0, 0, effective, effective) : new CornerRadius(effective);
        if (Surface.CornerRadius != corners) Surface.CornerRadius = corners;
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
        {
            int panelMargin = (int)Math.Round(12 * display.Scale);
            x = TopPanelSide == "Left" ? display.Work.Left + panelMargin : display.Work.Right - w - panelMargin;
            x = Math.Clamp(x, display.Work.Left, Math.Max(display.Work.Left, display.Work.Right - w));
        }
        else if (AttachedTop && TopPanelSide == "Center")
            x = display.Work.Left + (display.Work.Width - w) / 2;
        if (AttachedTop && FullWidthTop) { x = display.Bounds.Left; w = display.Bounds.Width; }
        if (FullWidthBottom)
        {
            x = display.Bounds.Left; w = display.Bounds.Width; y = display.Bounds.Bottom - h;
            if (ReservedBottomTop is int surfaceTop)
            {
                // Match the actual shell reservation, including auto-hide edge
                // strips and per-monitor pixel rounding. Headroom stays clear.
                y = surfaceTop - (int)Math.Round(TransparentInsets.Top * display.Scale);
                h = Math.Max(6, display.Bounds.Bottom - y);
            }
        }
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
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (!OverlayChrome.GetWindowRect(handle, out var current) || current.Left != rectangle.Left || current.Top != rectangle.Top || current.Right != rectangle.Right || current.Bottom != rectangle.Bottom)
                UpdateClip(rectangle);
            else UpdateClip();
        }
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
            { MessageBox.Show(e.Message, "Orby Launcher"); }
        };
        return button;
    }
    protected static TextBlock Glyph(string glyph, double size = 19) => new() { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    public abstract void Position();
}
