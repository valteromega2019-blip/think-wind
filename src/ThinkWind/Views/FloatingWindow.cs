using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal abstract class FloatingWindow : Window
{
    protected readonly SettingsStore Store;
    protected readonly Border Surface;
    protected FloatingWindow(SettingsStore store)
    {
        Store = store;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        Foreground = Brushes.White;
        FontFamily = new FontFamily("Segoe UI Variable Display");
        Surface = new Border { CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), Padding = new Thickness(8) };
        Content = Surface;
        Closing += (_, e) =>
        {
            if (Application.Current is App app && !app.IsExiting) { e.Cancel = true; Hide(); }
        };
        SourceInitialized += (_, _) =>
        {
            ApplyTint(Desktop.Acrylic(this));
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle).AddHook(WindowMessage);
        };
    }
    // Recompute after DPI or display topology changes. All positioning is in WPF DIPs.
    private nint WindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message is 0x007E or 0x02E0 or 0x001A) Dispatcher.BeginInvoke(new Action(Position));
        return 0;
    }
    private bool acrylic;
    private void ApplyTint(bool supported)
    {
        acrylic = supported;
        Surface.Background = new SolidColorBrush(Color.FromArgb((byte)(supported ? Store.Value.TintOpacity * 255 : 255), 19, 22, 35));
    }
    public virtual void RefreshSettings() { ApplyTint(acrylic); Position(); }
    protected Rect WorkArea()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var screen = screens[Math.Clamp(Store.Value.MonitorIndex, 0, screens.Length - 1)];
        var area = screen.WorkingArea;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var origin = transform.Transform(new Point(area.Left, area.Top));
        var size = transform.Transform(new Vector(area.Width, area.Height));
        return new Rect(origin, new Size(size.X, size.Y));
    }
    protected void AnimateHeight(double value)
    {
        var from = ActualHeight > 0 ? ActualHeight : Height;
        if (!double.IsFinite(from)) from = value;
        Height = value;
        BeginAnimation(HeightProperty, new DoubleAnimation(from, value, TimeSpan.FromMilliseconds(240)) { FillBehavior = FillBehavior.Stop, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
    }
    protected static Button ActionButton(string label, Action action, string? tooltip = null)
    {
        var button = new Button { Content = label, ToolTip = tooltip ?? label };
        System.Windows.Automation.AutomationProperties.SetName(button, tooltip ?? label);
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
            { MessageBox.Show(e.Message, "Think Wind"); }
        };
        return button;
    }
    public abstract void Position();
}
