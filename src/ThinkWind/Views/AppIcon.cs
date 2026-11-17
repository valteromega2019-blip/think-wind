using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Globalization;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
// Keeps Image.Source for native async loading and immediately returning to originals.
internal sealed class AppIcon : Image
{
    internal string AppName { get; init; } = "";
    internal string Target { get; init; } = "";
    internal string? AppId { get; init; }
    internal bool HighlightRunning { get; init; }
    internal bool PreserveOriginal { get; init; }
    internal AppIcon()
    {
        Loaded += (_, _) => { AppIconTheme.Changed -= ThemeChanged; AppIconTheme.Changed += ThemeChanged; InvalidateVisual(); };
        Unloaded += (_, _) => AppIconTheme.Changed -= ThemeChanged;
    }
    private void ThemeChanged() => InvalidateVisual();
    protected override Size ArrangeOverride(Size finalSize)
    {
        var nativeSize = base.ArrangeOverride(finalSize);
        // Image returns zero when Source is absent. Our vector/fallback still
        // has content and must retain the slot reserved by the Dock or drawer.
        return Source is null || AppIconTheme.Enabled || AppIconTheme.HasCustom(Target, AppId)
            ? finalSize : nativeSize;
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (!PreserveOriginal && !GameLibrary.IsGame(Target, AppId) && (AppIconTheme.Enabled || AppIconTheme.HasCustom(Target, AppId))) AppIconTheme.Draw(dc, RenderSize, AppName, Target, AppId, HighlightRunning);
        else if (Source is not null) base.OnRender(dc);
        else
        {
            double side = System.Math.Min(RenderSize.Width, RenderSize.Height);
            if (side <= 0) return;
            string glyph = Target.StartsWith("ms-settings:", System.StringComparison.OrdinalIgnoreCase) ? "\uE713" : "\uE8A5";
            var ink = TryFindResource("Think.Text") as Brush ?? Brushes.White;
            var text = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe Fluent Icons, Segoe MDL2 Assets"), side * .8, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(text, new Point((RenderSize.Width - text.Width) / 2, (RenderSize.Height - text.Height) / 2));
        }
    }
}
