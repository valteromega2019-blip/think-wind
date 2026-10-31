// Adapted from the user-owned Think Monitor source; runs independently in Think Wind.
using System;
using System.Windows;
using System.Windows.Media;

namespace ThinkWind.Services;

public sealed class OverlayAppearanceOptions
{
    public static bool IsValidColor(string? value) { try { return !string.IsNullOrWhiteSpace(value) && value.Length == 7 && value[0] == '#' && ColorConverter.ConvertFromString(value) is Color; } catch (Exception error) when (error is FormatException or NotSupportedException) { return false; } }
    public bool SyncWithAppTheme { get; set; } = true;
    public string AccentColor { get; set; } = "#00BFFF";
    public string BackgroundColor { get; set; } = "#202020";
    public string TextColor { get; set; } = "#F5F5F5";
    public double BackgroundTransparency { get; set; } = 18;
    public double BlockTransparency { get; set; } = 10;
    public double GraphTransparency { get; set; } = 15;
    public int CornerRadius { get; set; } = 8;

    public OverlayAppearanceOptions Normalize()
    {
        AccentColor = ColorValue(AccentColor, "#00BFFF");
        BackgroundColor = ColorValue(BackgroundColor, "#202020");
        TextColor = ColorValue(TextColor, "#F5F5F5");
        BackgroundTransparency = Percentage(BackgroundTransparency, 18);
        BlockTransparency = Percentage(BlockTransparency, 10);
        GraphTransparency = Percentage(GraphTransparency, 15);
        CornerRadius = Math.Clamp(CornerRadius, 0, 24);
        return this;
    }

    public OverlayAppearanceOptions Clone() => new OverlayAppearanceOptions {
        SyncWithAppTheme = SyncWithAppTheme, AccentColor = AccentColor,
        BackgroundColor = BackgroundColor, TextColor = TextColor,
        BackgroundTransparency = BackgroundTransparency, BlockTransparency = BlockTransparency,
        GraphTransparency = GraphTransparency, CornerRadius = CornerRadius
    }.Normalize();

    private static double Percentage(double value, double fallback) => double.IsFinite(value) ? Math.Clamp(value, 0, 100) : fallback;
    private static string ColorValue(string? value, string fallback) => OverlayAppearanceOptions.IsValidColor(value) ? value!.Trim().ToUpperInvariant() : fallback;
}

/// <summary>Resolved overlay colors. Alpha applies only to surfaces; labels stay opaque.</summary>
public sealed record OverlayPalette(Color Background, Color Block, Color Graph, Color Text, Color Muted,
    Color Accent, Color Border, bool TextAdjustedForContrast)
{
    public static OverlayPalette Resolve(OverlayAppearanceOptions? options, ResourceDictionary? resources = null, Preferences? island = null)
    {
        var settings = (options ?? new OverlayAppearanceOptions()).Clone();
        Color Read(string key, Color fallback) => resources?[key] is SolidColorBrush brush ? Opaque(brush.Color) : fallback;
        Color background = Parse(settings.BackgroundColor), text = Parse(settings.TextColor), accent = Parse(settings.AccentColor);
        Color block, border, muted;
        if (settings.SyncWithAppTheme) {
            background = ThemeService.BaseColor; text = Read("Think.Text", text); accent = Read("Think.Accent", accent);
            block = Mix(background, text, .06); border = Read("Think.Hover", Mix(background, text, .25));
            muted = Read("Think.Muted", Mix(background, text, .75));
            if (island is not null)
            {
                double opacity = Math.Clamp(island.IslandOpacity ?? island.TintOpacity, 0, 1);
                settings.BackgroundTransparency = (1 - opacity) * 100;
                settings.BlockTransparency = (1 - Math.Clamp(island.InnerSurfaceOpacity * .12 * opacity, 0, 1)) * 100;
                settings.GraphTransparency = (1 - Math.Clamp(island.InnerSurfaceOpacity * .16 * opacity, 0, 1)) * 100;
                settings.CornerRadius = (int)Math.Clamp(island.IslandRadius, 0, 24);
            }
        }
        else {
            block = Mix(background, text, .06); border = Mix(background, text, .25); muted = Mix(background, text, .75);
        }
        bool adjusted = Contrast(text, background) < 3;
        if (adjusted) {
            text = Contrast(Colors.White, background) >= Contrast(Colors.Black, background) ? Colors.White : Colors.Black;
            muted = Mix(background, text, .8);
        }
        return new OverlayPalette(Alpha(background, settings.BackgroundTransparency), Alpha(block, settings.BlockTransparency),
            Alpha(Mix(background, accent, .07), settings.GraphTransparency), Opaque(text), Opaque(muted),
            Opaque(accent), Opaque(border), adjusted);
    }

    public static SolidColorBrush Brush(Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); return brush; }
    private static Color Parse(string value) => (Color)ColorConverter.ConvertFromString(value);
    private static Color Opaque(Color value) => Color.FromRgb(value.R, value.G, value.B);
    private static Color Alpha(Color value, double transparency) => Color.FromArgb((byte)Math.Round((100 - transparency) * 2.55), value.R, value.G, value.B);
    private static Color Mix(Color from, Color to, double weight) => Color.FromRgb((byte)Math.Round(from.R + (to.R-from.R)*weight),
        (byte)Math.Round(from.G+(to.G-from.G)*weight),(byte)Math.Round(from.B+(to.B-from.B)*weight));
    private static double Luminance(Color color) {
        static double Channel(byte value) { double v = value / 255.0; return v <= .04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); }
        return .2126*Channel(color.R)+.7152*Channel(color.G)+.0722*Channel(color.B);
    }
    private static double Contrast(Color a, Color b) { double x=Luminance(a), y=Luminance(b); return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
}
