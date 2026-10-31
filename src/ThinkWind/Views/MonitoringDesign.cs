using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal static class MonitoringDesign
{
    internal static void Install(ResourceDictionary resources, Preferences? preferences = null)
    {
        object Read(string key) => Application.Current.TryFindResource(key) ?? Brushes.Transparent;
        resources["Bg"] = Read("Think.Background");
        resources["Card"] = Read("Think.GlassCard");
        resources["Fg"] = Read("Think.Text"); resources["Muted"] = Read("Think.Muted");
        resources["Faint"] = Read("Think.Muted"); resources["Line"] = Read("Think.Hover");
        resources["Accent"] = Read("Think.Accent");
        var card = new Style(typeof(Border));
        card.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Think.GlassCard")));
        card.Setters.Add(new Setter(Border.BorderBrushProperty, resources["Line"]));
        card.Setters.Add(new Setter(Border.BorderThicknessProperty, new Thickness(0)));
        card.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(20)));
        card.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(10)));
        resources["CardBorder"] = card;
        var button = new Style(typeof(Button), Application.Current.TryFindResource(typeof(Button)) as Style);
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
        button.Setters.Add(new Setter(Control.FontSizeProperty, 11d));
        button.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 28d));
        button.Setters.Add(new Setter(FrameworkElement.CursorProperty, Cursors.Hand));
        resources["OutlineButton"] = button;
    }
}
