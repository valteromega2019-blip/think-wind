using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private void BuildAppearance()
    {
        var colors = Section("Aparência", "Tema e cores. O destaque colore botões e indicadores; o fundo define a tonalidade das duas barras.");
        Select(colors, "Tema do launcher e Windows", store.Value.Theme, new[] { new Choice("Auto", "Automático · acompanhar Windows"), new Choice("Dark", "Escuro"), new Choice("Light", "Claro") }, async value => { if (!building) await ThemeService.SelectTheme(store, value); });
        AddColorSetting(colors, "Cor de destaque", store.Value.AccentColor, value => Change(p => p.AccentColor = value));
        AddContrastControl(colors);
        Check(colors, "Personalizar a cor de fundo das duas barras", store.Value.UseCustomBarColor, value => Change(p => p.UseCustomBarColor = value));
        AddColorSetting(colors, "Cor de fundo", store.Value.BarBackgroundColor, value => Change(p => p.BarBackgroundColor = value));
        Note(colors, "Para usar esta cor, ative a opção acima. Desativada, o fundo acompanha o tema ou o aplicativo, conforme suas configurações.");

        var surfaces = Section("Transparência", "Materiais e opacidade. Ajuste cada barra separadamente; o teclado acompanha a barra superior.");
        AddContrastControl(surfaces);
        AppearanceLabel(surfaces, "Barra de aplicativos · inferior");
        Select(surfaces, "Efeito do fundo", store.Value.DockMaterial, Materials(), value => Change(p => p.DockMaterial = value));
        Slider(surfaces, "Opacidade da barra inferior", .05, 1, store.Value.DockOpacity ?? store.Value.TintOpacity, value => Change(p => p.DockOpacity = value), "P0");
        AppearanceLabel(surfaces, "Central de controle · superior");
        Select(surfaces, "Efeito do fundo", store.Value.IslandMaterial, Materials(), value => Change(p => p.IslandMaterial = value));
        Slider(surfaces, "Opacidade da barra superior", .05, 1, store.Value.IslandOpacity ?? store.Value.TintOpacity, value => Change(p => p.IslandOpacity = value), "P0");
        AppearanceLabel(surfaces, "Blocos internos");
        Slider(surfaces, "Opacidade dos blocos e controles", .1, .9, store.Value.InnerSurfaceOpacity, value => Change(p => p.InnerSurfaceOpacity = value), "P0", .05);
        Note(surfaces, "Este ajuste altera os blocos dentro dos painéis, sem apagar seus textos e ícones.");

        AddTypography(Section("Tipografia", "Escolha a fonte, ajuste o tamanho e confira a prévia. As opções afetam somente o Orby."));
        var motion = Section("Animações", "Valores menores deixam as transições mais rápidas. Zero desativa as animações configuráveis.");
        Slider(motion, "Duração das transições", 0, 600, store.Value.AnimationMilliseconds, value => Change(p => p.AnimationMilliseconds = value), "0 'ms'");
    }
    private static void AppearanceLabel(Panel parent, string label) => parent.Children.Add(new TextBlock
    { Text = label, FontSize = 13, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 6) });
}
