using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ThinkWind.Native;
using ThinkWind.Services;
using ThinkWind.Views;

internal static class OverlayChecks
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint window, int index);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(nint window, nint region);
    [DllImport("gdi32.dll")] private static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(nint region, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint region);
    internal static void Check(bool top, bool light, string path)
    {
        _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var store = new SettingsStore(path);
        store.Value.Theme = light ? "Light" : "Dark";
        ThemeService.Apply(store.Value);
        var window = new Surface(store, top);
        try
        {
            window.Show(); window.UpdateLayout();
            var hwnd = new WindowInteropHelper(window).Handle;
            Require((GetWindowLong(hwnd, -20) & 0x80000) != 0, "Conteúdo deve ser WS_EX_LAYERED.");
            Require(window.BackdropHandle != 0 && (GetWindowLong(window.BackdropHandle, -20) & 0x80000) == 0, "Material deve ter HWND não-layered separado.");
            Require((GetWindowLong(window.BackdropHandle, -20) & 0x200000) != 0, "Material não pode possuir bitmap GDI de redirecionamento.");
            Require(window.HasNativeMaterial, "Windows Composition não aceitou o material neste ambiente.");
            OverlayChrome.GetWindowRect(hwnd, out var front); OverlayChrome.GetWindowRect(window.BackdropHandle, out var back);
            Require(front.Left == back.Left && front.Top == back.Top && front.Right == back.Right && front.Bottom == back.Bottom, "Superfícies devem ter os mesmos limites.");
            var region = CreateRectRgn(0, 0, 0, 0);
            try
            {
                Require(GetWindowRgn(window.BackdropHandle, region) > 0, "Material deve possuir recorte.");
                Require(!PtInRegion(region, 0, front.Bottom - front.Top - 1), "Canto externo inferior não pode bloquear cliques.");
                Require(PtInRegion(region, (front.Right - front.Left) / 2, (front.Bottom - front.Top) / 2), "Centro deve pertencer ao material.");
            }
            finally { DeleteObject(region); }
            var root = (FrameworkElement)window.Content;
            int width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth)), height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(0, height - 1, 1, 1), pixel, 4, 0);
            Require(pixel[3] == 0, "Pixels externos devem possuir alfa zero, não cinza opaco.");
            bitmap.CopyPixels(new Int32Rect(width / 2, height / 2, 1, 1), pixel, 4, 0);
            Require(pixel[3] is > 0 and < 255, "Tonalidade central deve preservar alfa parcial.");
            Require(Math.Abs(root.ActualWidth - window.ActualWidth) < 2, "Conteúdo deve acompanhar o HWND durante redimensionamento.");
            window.Expand();
            for (int i = 0; i < 8; i++)
            {
                var frame = new System.Windows.Threading.DispatcherFrame();
                var tick = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(35) };
                tick.Tick += (_, _) => { tick.Stop(); frame.Continue = false; };
                tick.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame); window.UpdateLayout();
                OverlayChrome.GetWindowRect(hwnd, out front); OverlayChrome.GetWindowRect(window.BackdropHandle, out back);
                Require(front.Left == back.Left && front.Top == back.Top && front.Right == back.Right && front.Bottom == back.Bottom, "Material e conteúdo não podem se separar na animação.");
            }
            Require(window.ActualHeight > 150 && window.ActualWidth > 340, "Animação deve concluir com as dimensões solicitadas.");
        }
        finally { window.Close(); }
    }
    internal static void MiniEscape(string path)
    {
        var store = new SettingsStore(path); ThemeService.Apply(store.Value);
        var mini = new MiniIslandWindow(store, MiniIslandKind.Volume, () => new OverlayChrome.PixelRect { Left = 100, Right = 320, Top = 0, Bottom = 50 });
        int dismissed = 0; mini.Dismissed += () => dismissed++;
        try
        {
            mini.Present(); Require(mini.IsVisible, "Mini deve abrir independentemente.");
            mini.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(mini), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
            Require(!mini.IsVisible && dismissed == 1, "Esc deve fechar a mini e avisar a ilha uma única vez.");
        }
        finally { mini.Close(); }
    }
    internal static void MiniOutsideFocus(string path)
    {
        var store = new SettingsStore(path); ThemeService.Apply(store.Value);
        var mini = new MiniIslandWindow(store, MiniIslandKind.Network, () => new OverlayChrome.PixelRect { Left = 100, Right = 320, Top = 0, Bottom = 50 });
        var other = new Window { Title = "Think Wind · Check de foco", Width = 100, Height = 60, ShowInTaskbar = false };
        try
        {
            mini.Present(); Require(mini.IsVisible, "Mini de rede deve abrir.");
            other.Show(); other.Activate();
            Require(!mini.IsVisible, "Ativar outra janela deve fechar a mini de rede.");
        }
        finally { other.Close(); mini.Close(); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    internal static void ThumbnailsAndClose(string path)
    {
        Application.Current.Resources["Think.IconButton"] = new Style(typeof(System.Windows.Controls.Button));
        var first = new Window { Title = "Think Wind Check — janela 1", Width = 220, Height = 140, ShowInTaskbar = false, Background = Brushes.Red };
        var second = new Window { Title = "Think Wind Check — janela 2", Width = 220, Height = 140, ShowInTaskbar = false, Background = Brushes.Blue };
        var store = new SettingsStore(path); var preview = new WindowPreview(store, () => new OverlayChrome.PixelRect { Left = 100, Right = 600, Top = 800, Bottom = 850 });
        bool firstClosed = false, secondClosed = false; first.Closed += (_, _) => firstClosed = true; second.Closed += (_, _) => secondClosed = true;
        try
        {
            first.Show(); second.Show(); first.WindowState = WindowState.Minimized;
            nint one = new WindowInteropHelper(first).Handle, two = new WindowInteropHelper(second).Handle;
            preview.Present(new[] { new Desktop.RunningWindow(one, first.Title, null, true), new Desktop.RunningWindow(two, second.Title, null, false) }); preview.UpdateLayout();
            Require(WindowThumbnail.ActiveCount == 2, "DWM deve registrar as duas janelas, inclusive minimizada.");
            Require(WindowThumbnail.CloseWindow(one), "WM_CLOSE deve ser enfileirado.");
            var frame = new System.Windows.Threading.DispatcherFrame(); var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
            Require(firstClosed && !secondClosed && WindowThumbnail.IsWindow(two), "Fechamento individual não pode afetar a segunda janela.");
            preview.Hide(); Require(WindowThumbnail.ActiveCount == 0, "Ocultar preview deve liberar todas as miniaturas nativas.");
        }
        finally { preview.Close(); if (!firstClosed) first.Close(); if (!secondClosed) second.Close(); }
    }
    internal static void DockOverflow(string path)
    {
        var store = new SettingsStore(path); ThemeService.Apply(store.Value);
        var window = new OverflowSurface(store);
        try
        {
            window.Show(); window.UpdateLayout();
            var root = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth), (int)Math.Ceiling(root.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root); var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(120, 55, 1, 1), pixel, 4, 0);
            Require(pixel[2] > 200 && pixel[3] == 255, "Ícone ampliado deve aparecer acima do material sem recorte.");
            bitmap.CopyPixels(new Int32Rect(5, 20, 1, 1), pixel, 4, 0);
            Require(pixel[3] == 0, "Reserva fora do ícone deve ser transparente.");
            OverlayChrome.GetWindowRect(new WindowInteropHelper(window).Handle, out var front); OverlayChrome.GetWindowRect(window.BackdropHandle, out var back);
            Require(back.Top > front.Top && back.Left > front.Left && back.Bottom == front.Bottom, "Material deve continuar compacto dentro da janela transparente.");
        }
        finally { window.Close(); }
    }
    private sealed class OverflowSurface : FloatingWindow
    {
        protected override bool AttachedTop => false;
        protected override Thickness TransparentInsets => new(16, 72, 16, 0);
        internal OverflowSurface(SettingsStore store) : base(store)
        {
            var icon = new System.Windows.Controls.Border { Width = 64, Height = 64, Background = Brushes.Red, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, RenderTransformOrigin = new Point(.5, 1), RenderTransform = new ScaleTransform(1.648, 1.648) };
            ((System.Windows.Controls.Grid)Content).Children.Add(new AdornerDecoratorHost(icon, true) { Margin = new Thickness(26, 82, 26, 10) });
        }
        public override void Position() => Place(240, 160);
    }
    private sealed class Surface : FloatingWindow
    {
        private readonly bool top;
        protected override bool AttachedTop => top;
        internal Surface(SettingsStore store, bool attached) : base(store) { top = attached; Title = "Think Wind · Check de composição"; }
        public override void Position() => Place(260, 72);
        internal void Expand() => Place(360, 180, true);
    }
}
