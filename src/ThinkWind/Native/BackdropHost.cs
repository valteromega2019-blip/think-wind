using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ThinkWind.Native;

// WPF owns a per-pixel-alpha HWND for controls. DWM owns this separate, unpainted
// native HWND immediately behind it. A layered WPF window is not a DWM backdrop target.
internal sealed class BackdropHost : IDisposable
{
    private delegate nint WindowProc(nint window, uint message, nint wParam, nint lParam);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct WindowClass
    {
        internal uint Size, Style;
        internal WindowProc Procedure;
        internal int ClassExtra, WindowExtra;
        internal nint Instance, Icon, Cursor, Background;
        internal string? MenuName;
        internal string ClassName;
        internal nint SmallIcon;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { internal int Left, Right, Top, Bottom; }
    private static readonly WindowProc Procedure = ProcessMessage;
    private static readonly string ClassName = "ThinkWind.NativeBackdrop";
    private static ushort atom;
    private readonly nint foreground;
    internal nint Handle { get; private set; }
    internal bool MaterialAvailable { get; private set; }
    private BackdropComposition? composition;
    private bool compositionFailed;
    private (int Width, int Height, int Radius, bool Top) lastGeometry;
    private OverlayChrome.PixelRect lastRectangle;
    private bool synchronized, lastVisible;
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowEx(uint extended, string type, string name, uint style, int x, int y, int width, int height, nint owner, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint DefWindowProc(nint window, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref Margins margins);
    internal BackdropHost(Window window)
    {
        var helper = new WindowInteropHelper(window); foreground = helper.Handle;
        if (atom == 0)
        {
            var type = new WindowClass { Size = (uint)Marshal.SizeOf<WindowClass>(), Procedure = Procedure, Instance = GetModuleHandle(null), ClassName = ClassName };
            atom = RegisterClassEx(ref type);
            if (atom == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        // No GDI redirection bitmap: Composition is the only painted surface.
        uint extended = window.ShowInTaskbar ? 0x08240008u : 0x08200088u;
        Handle = CreateWindowEx(extended, ClassName, "Think Wind — Material · " + window.Title, 0x80000000, -10000, -10000, 1, 1, helper.Owner, 0, GetModuleHandle(null), 0);
        if (Handle == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        // Ownership guarantees that the alpha content stays above its own material.
        helper.Owner = Handle;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        DwmExtendFrameIntoClientArea(Handle, ref margins);
        int none = unchecked((int)0xFFFFFFFE), square = 1;
        DwmSetWindowAttribute(Handle, 34, ref none, 4);
        DwmSetWindowAttribute(Handle, 33, ref square, 4);
    }
    internal void SetMaterial(string material, bool light)
    {
        int dark = light ? 0 : 1, type = material == "Mica" ? 2 : material == "Acrylic" ? 3 : 1;
        DwmSetWindowAttribute(Handle, 20, ref dark, 4);
        if (material == "Acrylic")
        {
            // System backdrop Acrylic falls back to an opaque fill when inactive.
            // A host backdrop brush samples behind this HWND without stealing focus.
            int host = 1, none = 1;
            DwmSetWindowAttribute(Handle, 38, ref none, 4);
            int result = DwmSetWindowAttribute(Handle, 17, ref host, 4);
            if (result >= 0 && composition is null && !compositionFailed)
            {
                try { composition = new BackdropComposition(Handle); synchronized = false; }
                catch (Exception e) when (e is COMException or InvalidCastException or InvalidOperationException or NotSupportedException)
                { compositionFailed = true; }
            }
            composition?.Enable(true); MaterialAvailable = composition is not null;
        }
        else
        {
            composition?.Enable(false);
            MaterialAvailable = type == 2 && DwmSetWindowAttribute(Handle, 38, ref type, 4) >= 0;
            if (type == 1) DwmSetWindowAttribute(Handle, 38, ref type, 4);
        }
    }
    internal void Synchronize(OverlayChrome.PixelRect rectangle, double radius, bool top, bool visible)
    {
        if (Handle == 0) return;
        int width = Math.Max(1, rectangle.Right - rectangle.Left), height = Math.Max(1, rectangle.Bottom - rectangle.Top);
        var geometry = (width, height, (int)Math.Round(radius), top);
        if (!synchronized || lastGeometry != geometry)
        { composition?.Resize(width, height); OverlayChrome.Clip(Handle, width, height, radius, top); lastGeometry = geometry; }
        bool show = visible && MaterialAvailable;
        if (!synchronized || lastVisible != show || rectangle.Left != lastRectangle.Left || rectangle.Top != lastRectangle.Top || rectangle.Right != lastRectangle.Right || rectangle.Bottom != lastRectangle.Bottom)
            SetWindowPos(Handle, foreground, rectangle.Left, rectangle.Top, width, height, 0x0010 | (show ? 0x0040u : 0x0080u));
        lastRectangle = rectangle; lastVisible = show; synchronized = true;
    }
    internal void Hide() { if (Handle != 0) ShowWindow(Handle, 0); lastVisible = false; }
    private static nint ProcessMessage(nint window, uint message, nint wParam, nint lParam) => message switch
    {
        0x0084 => -1, // HTTRANSPARENT: never consume clicks intended for content/desktop.
        0x0021 => 3,  // MA_NOACTIVATE.
        0x0014 => 1,  // No GDI background brush, fill or opaque erase rectangle.
        0x0083 => 0,  // Entire native client area, without non-client frame.
        _ => DefWindowProc(window, message, wParam, lParam)
    };
    public void Dispose() { composition?.Dispose(); composition = null; var window = Handle; Handle = 0; if (window != 0) DestroyWindow(window); }
}
