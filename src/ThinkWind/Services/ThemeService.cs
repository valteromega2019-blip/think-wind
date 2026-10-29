using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ThinkWind.Services;

internal static class ThemeService
{
    private static (string Theme, string Accent, bool Light, bool Contrast)? current;
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
        var light = p.Theme == "Auto" && current?.Theme == "Auto" && !refreshSystem ? Light : IsLight(p);
        var next = (p.Theme, p.AccentColor, light, SystemParameters.HighContrast);
        if (current.HasValue && current.Value.Equals(next)) return;
        current = next; Light = light;
        var resources = Application.Current.Resources;
        Set("Think.Background", light ? "#F2F5F9" : "#111318");
        Set("Think.Surface", light ? "#E5EBF2" : "#1A1E25");
        Set("Think.GlassCard", light ? "#10000000" : "#0DFFFFFF");
        Set("Think.Control", light ? "#E0E6ED" : "#222832");
        Set("Think.Text", light ? "#18212D" : "#F4F6FA");
        Set("Think.Muted", light ? "#506071" : "#AAB3C1");
        Set("Think.Hover", light ? "#CCD8E4" : "#303B48");
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
            resources["Think.Surface"] = SystemColors.ControlBrush;
            resources["Think.GlassCard"] = SystemColors.ControlBrush;
            resources["Think.Control"] = SystemColors.ControlBrush;
            resources["Think.Accent"] = SystemColors.HighlightBrush;
            resources["Think.AccentInk"] = SystemColors.HighlightBrush;
            resources["Think.AccentSurface"] = SystemColors.ControlBrush;
            resources["Think.Hover"] = SystemColors.HighlightBrush;
        }
        void Set(string key, string color)
        { var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); brush.Freeze(); resources[key] = brush; }
    }
}
