using System;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace OrbyLauncher.Services;

internal static class ThemeService
{
    internal static event Action? Changed;
    static ThemeService() { OrbyLauncher.Views.PopupBackdrop.Initialize(); }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SendMessageTimeout(nint window, uint message, nuint parameter, string setting, uint flags, uint timeout, out nuint result);
    private static readonly System.Threading.SemaphoreSlim themeChanges = new(1, 1);
    internal static async Task SelectTheme(SettingsStore store, string theme)
    {
        if (theme is not ("Auto" or "Light" or "Dark")) return;
        await themeChanges.WaitAsync();
        try
        {
            if (theme == "Auto") { store.Update(p => p.Theme = theme); return; }
            // Only explicit user theme changes write Windows preferences.
            // Startup and ordinary appearance refreshes never change the OS.
            await Task.Run(() => SetWindowsTheme(theme == "Light"));
            store.Update(p => p.Theme = theme);
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            MessageBox.Show("Não foi possível alterar o tema do Windows. " + error.Message, "Orby Launcher");
        }
        finally { themeChanges.Release(); }
    }
    private static void SetWindowsTheme(bool light)
    {
        // Windows personalization values: AppsUseLightTheme + SystemUsesLightTheme.
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", true);
        string[] names = { "AppsUseLightTheme", "SystemUsesLightTheme" };
        var old = new object?[2]; var kinds = new RegistryValueKind[2];
        for (int i = 0; i < names.Length; i++)
        { old[i] = key.GetValue(names[i]); if (old[i] is not null) kinds[i] = key.GetValueKind(names[i]); }
        try
        {
            foreach (string name in names) key.SetValue(name, light ? 1 : 0, RegistryValueKind.DWord);
        }
        catch
        {
            for (int i = 0; i < names.Length; i++)
            {
                try { if (old[i] is null) key.DeleteValue(names[i], false); else key.SetValue(names[i], old[i]!, kinds[i]); }
                catch (Exception rollback) when (rollback is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { }
            }
            throw;
        }
        // Inform Explorer and applications. A hung recipient cannot block the UI;
        // a timeout doesn't undo preferences successfully saved by Windows.
        SendMessageTimeout((nint)0xFFFF, 0x001A, 0, "ImmersiveColorSet", 2, 100, out _);
    }
    private static string? current;
    internal static bool Light { get; private set; }
    internal static bool IsLight(Preferences p)
    {
        if (p.Theme != "Auto") return p.Theme == "Light";
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException) { return false; }
    }
    internal static Color BaseColor => Light ? Color.FromRgb(242, 245, 249) : Color.FromRgb(17, 19, 24);
    internal static void Apply(Preferences p, bool refreshSystem = false)
    {
        var light = IsLight(p);
        var next = string.Join('|', p.Theme, p.AccentColor, light, SystemParameters.HighContrast, p.InnerSurfaceOpacity,
            p.FontFamily, p.FontColor, p.AutomaticTextContrast, p.UseCustomBarColor, p.BarBackgroundColor, p.DockOpacity, p.IslandOpacity);
        if (current == next && !refreshSystem) return;
        current = next; Light = light;
        var resources = Application.Current.Resources;
        Set("Think.Background", light ? "#F2F5F9" : "#111318");
        // Neutral shading reduces colour casts over saturated wallpaper in both themes.
        Layer("Think.Surface", "#111318", p.InnerSurfaceOpacity * .7);
        Layer("Think.GlassCard", "#111318", p.InnerSurfaceOpacity * .7);
        Layer("Think.Control", "#111318", p.InnerSurfaceOpacity);
        var background = p.UseCustomBarColor ? (Color)ColorConverter.ConvertFromString(p.BarBackgroundColor) : BaseColor;
        Layer("Think.Popup", background.ToString(), p.IslandOpacity ?? p.TintOpacity);
        var requested = (Color)ColorConverter.ConvertFromString(p.FontColor);
        var inkText = Typography.Ink(requested, background, p.AutomaticTextContrast);
        Set("Think.Text", inkText.ToString());
        Set("Think.Muted", inkText.ToString());
        Set("Think.ArtworkText", Typography.Ink(requested, Color.FromRgb(17, 19, 24), p.AutomaticTextContrast).ToString());
        resources["Think.FontFamily"] = new FontFamily(p.FontFamily + ", Segoe UI Variable, Segoe UI");
        Layer("Think.Hover", light ? "#CCD8E4" : "#303B48", Math.Min(.95, p.InnerSurfaceOpacity + .12));
        var accent = (Color)ColorConverter.ConvertFromString(p.AccentColor);
        // Preserve the chosen hue, but lift/darken the control ink for readability.
        double luminance = (.2126 * accent.R + .7152 * accent.G + .0722 * accent.B) / 255;
        double mix = light && luminance > .5 ? .45 : !light && luminance < .55 ? .45 : 0;
        byte target = light ? (byte)0 : (byte)255;
        var ink = Color.FromRgb((byte)(accent.R + (target - accent.R) * mix), (byte)(accent.G + (target - accent.G) * mix), (byte)(accent.B + (target - accent.B) * mix));
        Set("Think.Accent", ink.ToString());
        Set("Think.AccentInk", ink.ToString());
        Set("Think.AccentSurface", Color.FromArgb(light ? (byte)28 : (byte)38, accent.R, accent.G, accent.B).ToString());
        if (SystemParameters.HighContrast)
        {
            resources["Think.Background"] = SystemColors.WindowBrush;
            resources["Think.Text"] = SystemColors.WindowTextBrush;
            resources["Think.Muted"] = SystemColors.WindowTextBrush;
            resources["Think.ArtworkText"] = SystemColors.WindowTextBrush;
            resources["Think.Surface"] = SystemColors.ControlBrush;
            resources["Think.GlassCard"] = SystemColors.ControlBrush;
            resources["Think.Control"] = SystemColors.ControlBrush;
            resources["Think.Popup"] = SystemColors.ControlBrush;
            resources["Think.Accent"] = SystemColors.HighlightBrush;
            resources["Think.AccentInk"] = SystemColors.HighlightBrush;
            resources["Think.AccentSurface"] = SystemColors.ControlBrush;
            resources["Think.Hover"] = SystemColors.HighlightBrush;
        }
        Changed?.Invoke();
        void Set(string key, string color)
        { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); resources[key] = brush; }
        void Layer(string key, string value, double opacity)
        { var color = (Color)ColorConverter.ConvertFromString(value); color.A = (byte)Math.Round(255 * opacity); Set(key, color.ToString()); }
    }
}
