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
        Left = area.Left; Top = area.Top; Width = area.Width; Height = Math.Max(area.Height, dockIcon.Bottom - area.Top);
        var view = new Viewport3D { IsHitTestVisible = false, ClipToBounds = true };
        view.Camera = new OrthographicCamera(new Point3D(Width / 2, Height / 2, 10), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), Width);
        for (int row = 0; row <= 32; row++)
        {
            double y = row / 32d;
            mesh.TextureCoordinates.Add(new Point(0, y)); mesh.TextureCoordinates.Add(new Point(1, y));
            mesh.Positions.Add(new Point3D()); mesh.Positions.Add(new Point3D());
            if (row == 32) continue;
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
        // Top unfolds first; the base follows with a delay, creating curved sides.
        double rise = 1 - Math.Pow(1 - p, 3);
        double top = icon.Top + (target.Top - icon.Top) * rise;
        double bottom = icon.Bottom + (target.Bottom - icon.Bottom) * rise;
        for (int row = 0; row <= 32; row++)
        {
            double y = row / 32d;
            double phase = Math.Clamp((p - .3 * y) / (1 - .3 * y), 0, 1);
            double unfold = phase * phase * (3 - 2 * phase);
            double left = icon.Left + (target.Left - icon.Left) * unfold;
            double right = icon.Right + (target.Right - icon.Right) * unfold;
            double screenY = top + (bottom - top) * y;
            mesh.Positions[row * 2] = new Point3D(left - target.Left, Height - (screenY - target.Top), 0);
            mesh.Positions[row * 2 + 1] = new Point3D(right - target.Left, Height - (screenY - target.Top), 0);
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
            using var capture = new Drawing.Bitmap(width, height, Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Drawing.Graphics.FromImage(capture)) graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, new Drawing.Size(width, height));
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
