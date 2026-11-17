using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private void BuildIconStyleChoices(Panel parent)
    {
        Note(parent, "Clique no exemplo para aplicar. A prévia sem fundo mostra um aplicativo ativo.");
        var choices = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
        parent.Children.Add(choices);
        var cards = new List<(string Id, Border Card, Button Button, TextBlock Check)>();
        foreach (var option in new[] { ("Original", "Original"), ("Tile", "Com fundo"), ("Floating", "Sem fundo") })
        {
            string id = option.Item1;
            var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            body.Children.Add(new IconStylePreview(id) { Width = 64, Height = 64, Margin = new Thickness(0, 4, 0, 10) });
            var label = new TextBlock { Text = option.Item2, FontSize = 12, FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); body.Children.Add(label);
            var check = Glyph("\uE73E", 13); check.Margin = new Thickness(0, 7, 0, 0);
            check.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); body.Children.Add(check);
            var card = new Border { Child = body, Padding = new Thickness(7), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1.5) };
            var button = new Button { Content = card, Width = 104, MinHeight = 134, Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 6, 6), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Style = (Style)FindResource("Think.IconButton"), ToolTip = "Aplicar estilo: " + option.Item2 };
            AutomationProperties.SetName(button, "Estilo dos ícones: " + option.Item2);
            button.Click += (_, _) => Change(p => { p.UnifiedAppIcons = id != "Original"; p.AppIconsWithoutBackground = id == "Floating"; });
            choices.Children.Add(button); cards.Add((id, card, button, check));
        }
        void RefreshChoices()
        {
            string selected = !store.Value.UnifiedAppIcons ? "Original" : store.Value.AppIconsWithoutBackground ? "Floating" : "Tile";
            foreach (var item in cards)
            {
                bool active = selected == item.Id;
                item.Card.SetResourceReference(Border.BackgroundProperty, active ? "Think.AccentSurface" : "Think.Control");
                item.Card.SetResourceReference(Border.BorderBrushProperty, active ? "Think.Accent" : "Think.Hover");
                item.Check.Visibility = active ? Visibility.Visible : Visibility.Hidden;
                AutomationProperties.SetItemStatus(item.Button, active ? "Selecionado" : "Não selecionado");
            }
        }
        EventHandler changed = (_, _) => RefreshChoices();
        choices.Loaded += (_, _) => { store.Changed -= changed; store.Changed += changed; RefreshChoices(); };
        choices.Unloaded += (_, _) => store.Changed -= changed;
        RefreshChoices();
    }

    private sealed class IconStylePreview : FrameworkElement
    {
        private readonly string style;
        private static readonly Lazy<BitmapImage> original = new(() =>
        {
            var image = new BitmapImage(); image.BeginInit();
            image.UriSource = new Uri("pack://application:,,,/OrbyLauncher;component/Assets/OrbyLauncher.png");
            image.DecodePixelWidth = 128; image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit(); image.Freeze(); return image;
        });
        internal IconStylePreview(string value)
        {
            style = value; IsHitTestVisible = false;
            Loaded += (_, _) => { AppIconTheme.Changed -= Refresh; AppIconTheme.Changed += Refresh; InvalidateVisual(); };
            Unloaded += (_, _) => AppIconTheme.Changed -= Refresh;
        }
        private void Refresh() => InvalidateVisual();
        protected override void OnRender(DrawingContext dc)
        {
            if (style == "Original") dc.DrawImage(original.Value, new Rect(RenderSize));
            else AppIconTheme.DrawPreview(dc, RenderSize, style == "Floating");
        }
    }
}
