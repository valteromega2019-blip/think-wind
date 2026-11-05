using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using OrbyLauncher.Native;
using Drawing = System.Drawing;

namespace OrbyLauncher.Views;

// Experimental visual only. Never hides, reshapes or blocks the target HWND.
// Its snapshot lives only for this transition and is never persisted.
internal sealed class WindowEnvelopeAnimation : Window
{
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint handle);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SendMessageTimeoutW(nint window,
        uint message, nint wParam, nint lParam, uint flags, uint timeout, out nuint result);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint window, int index, int value);
    private readonly MeshGeometry3D mesh = new();
    private readonly FrameClock clock = new();
    private readonly Stopwatch elapsed = new();
    private readonly Rect target, icon;
    private readonly double duration;
    private readonly bool closing;
    private static WindowEnvelopeAnimation? current;
    internal static void Cancel() { current?.Close(); current = null; }
    private WindowEnvelopeAnimation(BitmapSource image, Rect area, Rect dockIcon, bool minimize, double milliseconds)
    {
        target = area; icon = dockIcon; closing = minimize; duration = milliseconds;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        IsHitTestVisible = false;
        Rect canvas = Rect.Union(area, dockIcon);
        Left = canvas.Left; Top = canvas.Top; Width = canvas.Width; Height = canvas.Height;
        var view = new Viewport3D { IsHitTestVisible = false, ClipToBounds = true };
        view.Camera = new OrthographicCamera(new Point3D(Width / 2, Height / 2, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), Width);
        for (int row = 0; row <= 64; row++)
        {
            double y = row / 64d;
            mesh.TextureCoordinates.Add(new Point(0, y)); mesh.TextureCoordinates.Add(new Point(1, y));
            mesh.Positions.Add(new Point3D()); mesh.Positions.Add(new Point3D());
            if (row == 64) continue;
            int i = row * 2;
            foreach (int index in new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 }) mesh.TriangleIndices.Add(index);
        }
        var material = new DiffuseMaterial(new ImageBrush(image) { Stretch = Stretch.Fill });
        var model = new GeometryModel3D(mesh, material) { BackMaterial = material };
        var scene = new Model3DGroup(); scene.Children.Add(new AmbientLight(Colors.White)); scene.Children.Add(model);
        view.Children.Add(new ModelVisual3D { Content = scene }); Content = view;
        SourceInitialized += (_, _) =>
        {
            nint hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, -20, GetWindowLong(hwnd, -20) | 0x20 | 0x08000000 | 0x80);
        };
        clock.Tick += (_, _) =>
        {
            double p = Math.Clamp(elapsed.Elapsed.TotalMilliseconds / duration, 0, 1);
            Update(closing ? 1 - p : p);
            if (p >= 1) Close();
        };
        Closed += (_, _) => { clock.Stop(); Content = null; if (ReferenceEquals(current, this)) current = null; };
        Update(closing ? 1 : 0);
        Loaded += (_, _) => { elapsed.Restart(); clock.Start(); };
    }
    private void Update(double p)
    {
        // The same normalized geometry is traversed backwards on minimize.
        // Start at the icon's center, rather than its top-left or window center.
        double ease = p * p * p * (p * (p * 6 - 15) + 10);
        double centerX = icon.Left + icon.Width / 2;
        double centerY = icon.Top + icon.Height / 2;
        double top = centerY + (target.Top - centerY) * ease;
        double bottom = centerY + (target.Bottom - centerY) * ease;
        for (int row = 0; row <= 64; row++)
        {
            double y = row / 64d;
            double phase = Math.Clamp((p - .24 * y) / (1 - .24 * y), 0, 1);
            double unfold = phase * phase * (3 - 2 * phase);
            double seedWidth = icon.Width * Math.Clamp(p * 10, 0, 1);
            double left = centerX - seedWidth / 2 + (target.Left - centerX + seedWidth / 2) * unfold;
            double right = centerX + seedWidth / 2 + (target.Right - centerX - seedWidth / 2) * unfold;
            double screenY = top + (bottom - top) * y;
            mesh.Positions[row * 2] = new Point3D(left - Left, Height - (screenY - Top), 0);
            mesh.Positions[row * 2 + 1] = new Point3D(right - Left, Height - (screenY - Top), 0);
        }
        Opacity = Math.Clamp(p * 8, 0, 1);
    }
    internal static void Play(nint window, FrameworkElement source, bool minimize, double milliseconds)
    {
        if (!SystemParameters.ClientAreaAnimation || milliseconds <= 0 || !source.IsVisible
            || Desktop.GetForegroundWindow() != window || !OverlayChrome.GetWindowRect(window, out var bounds)) return;
        int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
        if (width < 60 || height < 60 || width > 8000 || height > 5000 || (long)width * height > 9000000) return;
        // Fullscreen belongs to games/video: no experimental overlay there.
        var screen = System.Windows.Forms.Screen.FromHandle(window).Bounds;
        if (bounds.Left <= screen.Left && bounds.Top <= screen.Top && bounds.Right >= screen.Right && bounds.Bottom >= screen.Bottom) return;
        try
        {
            current?.Close();
            double scale = VisualTreeHelper.GetDpi(source).DpiScaleX;
            var point = source.PointToScreen(new Point());
            // Ask only this HWND to render itself. Never copy desktop pixels or
            // substitute another foreground window when the app cannot render.
            using var capture = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format32bppRgb);
            using (var graphics = Drawing.Graphics.FromImage(capture))
            {
                graphics.Clear(Drawing.Color.Magenta);
                nint dc = graphics.GetHdc();
                try
                {
                    // WM_PRINT: nonclient, client, background and children.
                    // Bounded wait; protected/GPU-only apps may not implement it.
                    if (SendMessageTimeoutW(window, 0x0317, dc, (nint)0x1E, 0x22, 80, out _) == 0) return;
                }
                finally { graphics.ReleaseHdc(dc); }
            }
            int usable = 0;
            for (int y = 1; y < 8; y++)
                for (int x = 1; x < 8; x++)
                {
                    var pixel = capture.GetPixel(width * x / 8, height * y / 8);
                    if (pixel.ToArgb() == Drawing.Color.Magenta.ToArgb()) return;
                    if (pixel.R > 3 || pixel.G > 3 || pixel.B > 3) usable++;
                }
            if (usable < 8) return; // No desktop screenshot fallback.
            nint bitmap = capture.GetHbitmap(); BitmapSource image;
            try { image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(Math.Min(1280, width), Math.Min(720, height))); image.Freeze(); }
            finally { DeleteObject(bitmap); }
            var area = new Rect(bounds.Left / scale, bounds.Top / scale, width / scale, height / scale);
            var origin = new Rect(point.X / scale, point.Y / scale, source.ActualWidth, source.ActualHeight);
            current = new WindowEnvelopeAnimation(image, area, origin, minimize, Math.Clamp(milliseconds, 240, 500)); current.Show();
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception or ExternalException)
        { current?.Close(); current = null; }
    }
}
