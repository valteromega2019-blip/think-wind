using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbyLauncher.Services;

internal static class Typography
{
    internal const string DefaultFamily = "Segoe UI Variable";
    internal const string DefaultColor = "#F4F6FA";
    private static string[]? installed;
    internal static string[] Installed => installed ??= Fonts.SystemFontFamilies.Select(f => f.Source)
        .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase).ToArray();
    internal static string NormalizeFamily(string? family) => !string.IsNullOrWhiteSpace(family)
        && Installed.Contains(family, StringComparer.CurrentCultureIgnoreCase) ? family : DefaultFamily;
    internal static double Luminance(Color c)
    {
        static double Channel(byte value) { double v = value / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
    }
    internal static double Contrast(Color a, Color b)
    { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
    internal static Color Ink(Color requested, Color background, bool automatic)
    {
        if (!automatic || Contrast(requested, background) >= 4.5) return requested;
        return Contrast(Colors.White, background) >= Contrast(Colors.Black, background) ? Colors.White : Colors.Black;
    }
    internal static Color Blend(Color foreground, Color background, double opacity) => Color.FromRgb(
        (byte)Math.Round(foreground.R * opacity + background.R * (1 - opacity)),
        (byte)Math.Round(foreground.G * opacity + background.G * (1 - opacity)),
        (byte)Math.Round(foreground.B * opacity + background.B * (1 - opacity)));

    internal static void ApplyContrast(DependencyObject root, Color background, Preferences preferences)
    {
        var visited = new HashSet<DependencyObject>();
        var requested = (Color)ColorConverter.ConvertFromString(preferences.FontColor);
        var brushes = new Dictionary<Color, SolidColorBrush>();
        Walk(root, background);
        void Walk(DependencyObject? node, Color behind)
        {
            if (node is null || !visited.Add(node)) return;
            // Resource updates below can rebuild templates. Capture children
            // first and ignore empty slots in newly detached Viewboxes.
            var children = new List<DependencyObject>();
            if (node is Visual)
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                    if (VisualTreeHelper.GetChild(node, i) is { } visual) children.Add(visual);
            foreach (object child in LogicalTreeHelper.GetChildren(node))
                if (child is DependencyObject dependency) children.Add(dependency);
            if (node is Border border && border.Background is SolidColorBrush surface && surface.Color.A > 0)
            {
                behind = Blend(surface.Color, behind, surface.Color.A / 255d);
                var ink = Ink(requested, behind, preferences.AutomaticTextContrast);
                if (!brushes.TryGetValue(ink, out var brush)) { brush = new SolidColorBrush(ink); brush.Freeze(); brushes.Add(ink, brush); }
                border.Resources["Think.Text"] = SystemParameters.HighContrast ? SystemColors.WindowTextBrush : brush;
                border.Resources["Think.Muted"] = border.Resources["Think.Text"];
                border.Resources["Fg"] = border.Resources["Think.Text"];
                border.Resources["Muted"] = border.Resources["Think.Text"];
                border.Resources["Faint"] = border.Resources["Think.Text"];
            }
            foreach (var child in children) Walk(child, behind);
        }
    }
}
