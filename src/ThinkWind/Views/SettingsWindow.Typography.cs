using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private Action? refreshTypography;
    private void AddTypography(Panel parent)
    {
        AppearanceLabel(parent, "Fonte e tamanho");
        parent.Children.Add(new TextBlock { Text = "Pesquisar fonte instalada", Margin = new Thickness(0, 6, 0, 6), TextWrapping = TextWrapping.Wrap });
        var search = new TextBox { MinHeight = 36, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 6) };
        System.Windows.Automation.AutomationProperties.SetName(search, "Pesquisar fontes instaladas");
        var fonts = new ComboBox { ItemsSource = Typography.Installed, SelectedItem = store.Value.FontFamily, MinHeight = 36, MaxDropDownHeight = 240 };
        System.Windows.Automation.AutomationProperties.SetName(fonts, "Fonte da interface");
        search.TextChanged += (_, _) =>
        {
            bool previous = building; building = true;
            try { fonts.ItemsSource = Typography.Installed.Where(f => f.Contains(search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray(); fonts.SelectedItem = store.Value.FontFamily; }
            finally { building = previous; }
        };
        fonts.SelectionChanged += (_, _) => { if (fonts.SelectedItem is string family) Change(p => p.FontFamily = family); };
        parent.Children.Add(search); parent.Children.Add(fonts);
        Slider(parent, "Tamanho dos textos", .8, 1.4, store.Value.FontScale, v => Change(p => p.FontScale = v), "P0", .05);
        AppearanceLabel(parent, "Cor e legibilidade");
        var hex = AddColorSetting(parent, "Cor da fonte · HEX", store.Value.FontColor, value => Change(p => p.FontColor = value));
        var rgb = new TextBox { Padding = new Thickness(8), MinHeight = 36, Margin = new Thickness(0, 0, 0, 8), Text = Rgb(store.Value.FontColor) };
        System.Windows.Automation.AutomationProperties.SetName(rgb, "Cor da fonte RGB: vermelho, verde, azul");
        rgb.TextChanged += (_, _) =>
        {
            var values = rgb.Text.Split(',');
            if (values.Length == 3 && byte.TryParse(values[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                && byte.TryParse(values[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                && byte.TryParse(values[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                Change(p => p.FontColor = $"#{r:X2}{g:X2}{b:X2}");
        };
        parent.Children.Add(new TextBlock { Text = "Ou use RGB · vermelho, verde, azul (0–255)", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) }); parent.Children.Add(rgb);
        refreshTypography = () =>
        {
            bool previous = building; building = true;
            try
            {
                if (hex.Text != store.Value.FontColor) hex.Text = store.Value.FontColor;
                if (Rgb(store.Value.FontColor) != rgb.Text && !rgb.IsKeyboardFocusWithin) rgb.Text = Rgb(store.Value.FontColor);
            }
            finally { building = previous; }
        };
        rgb.LostKeyboardFocus += (_, _) => refreshTypography?.Invoke();
        AddContrastControl(parent);
        Note(parent, "Com o contraste automático, a cor pode ser ajustada para manter a leitura sobre o fundo escolhido.");
        AppearanceLabel(parent, "Prévia em tempo real");
        var preview = new TextBlock { Text = "Prévia · Aa Bb 0123456789\nSua interface, do seu jeito.", FontSize = 18, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
        var sample = new Border { CornerRadius = new CornerRadius(14), Child = preview, Margin = new Thickness(0, 8, 0, 8) };
        sample.SetResourceReference(Border.BackgroundProperty, "Think.Control"); parent.Children.Add(sample);
        parent.Children.Add(Button("Restaurar tipografia padrão", () =>
        {
            store.Update(p => { p.FontFamily = Typography.DefaultFamily; p.FontColor = Typography.DefaultColor; p.AutomaticTextContrast = true; p.FontScale = 1; });
            Build();
        }));
        static string Rgb(string value) { var c = (Color)ColorConverter.ConvertFromString(value); return $"{c.R}, {c.G}, {c.B}"; }
    }
}
