using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

internal static class AppearanceChecks
{
    internal static void Contrast()
    {
        foreach (byte r in new byte[] { 0, 64, 128, 192, 255 })
        foreach (byte g in new byte[] { 0, 64, 128, 192, 255 })
        foreach (byte b in new byte[] { 0, 64, 128, 192, 255 })
        {
            var background = Color.FromRgb(r, g, b);
            var ink = Typography.Ink(Colors.White, background, true);
            Require(Typography.Contrast(ink, background) >= 4.5, "Contraste automático abaixo de 4,5:1.");
            Require(Typography.Ink(Colors.Red, background, false) == Colors.Red, "A opção manual deve preservar a cor.");
        }
    }
    internal static void Persistence(string path)
    {
        var store = new SettingsStore(path);
        Require(store.Value.FontFamily == Typography.DefaultFamily, "Fonte padrão incorreta.");
        store.Update(p => { p.FontFamily = "Arial"; p.FontColor = "#12A0EF"; p.AutomaticTextContrast = false; }); store.Flush();
        var loaded = new SettingsStore(path);
        Require(loaded.Value.FontFamily == "Arial" && loaded.Value.FontColor == "#12A0EF" && !loaded.Value.AutomaticTextContrast, "Preferências não persistiram.");
        File.WriteAllText(path, "{\"FontFamily\":null,\"FontColor\":\"inválida\"}");
        loaded = new SettingsStore(path);
        Require(loaded.Value.FontFamily == Typography.DefaultFamily && loaded.Value.FontColor == Typography.DefaultColor, "Perfil inválido não foi normalizado.");
    }
    internal static void ResourcesAndLayout()
    {
        foreach (string theme in new[] { "Dark", "Light" })
        foreach (string background in new[] { "#111318", "#F2F5F9", "#7440D0" })
        {
            var p = new Preferences { Theme = theme, UseCustomBarColor = true, BarBackgroundColor = background, IslandOpacity = .3 };
            ThemeService.Apply(p);
            Require(SystemParameters.HighContrast || ((SolidColorBrush)Application.Current.Resources["Think.Popup"]).Color.A == 76, "Popup ignora opacidade em um dos temas.");
            foreach (string family in Typography.Installed.Where(f => f is "Arial" or "Consolas" or "Segoe UI Variable"))
            foreach (double scale in new[] { 1d, 1.25, 1.5, 2, 2.5 })
            {
                p.FontFamily = family; ThemeService.Apply(p);
                var text = new TextBlock { Text = "Áudio · Configurações · 0123456789", FontSize = 14, TextWrapping = TextWrapping.Wrap };
                var border = new Border { Width = 280, Child = text, Background = new SolidColorBrush(Color.FromArgb(200, 17, 19, 24)) };
                var window = new Window { Content = border };
                FontSizing.Apply(window, scale); Typography.ApplyContrast(border, Colors.White, p);
                border.Measure(new Size(280, double.PositiveInfinity)); border.Arrange(new Rect(new Point(), border.DesiredSize));
                Require(text.FontFamily.Source.StartsWith(family) && text.FontSize == 14 * scale, "Fonte ou escala não aplicada.");
                Require(text.DesiredSize.Height <= border.ActualHeight && text.DesiredSize.Width <= border.ActualWidth, "Texto ultrapassa o bloco.");
                var ink = ((SolidColorBrush)border.Resources["Think.Text"]).Color;
                Require(Typography.Contrast(ink, Typography.Blend(Color.FromRgb(17, 19, 24), Colors.White, 200 / 255d)) >= 4.5, "Bloco não recebeu contraste local.");
            }
        }
    }
    internal static void NativeMenus()
    {
        foreach (string theme in new[] { "Dark", "Light" })
        {
            ThemeService.Apply(new Preferences { Theme = theme, IslandOpacity = .3 });
            var owner = new Window { Width = 240, Height = 100, ShowActivated = false, ShowInTaskbar = false };
            var menu = new ContextMenu(); menu.Items.Add(new MenuItem { Header = "Menu Acrylic" });
            menu.PlacementTarget = owner; menu.SetResourceReference(Control.BackgroundProperty, "Think.Popup");
            try
            {
                owner.Show(); menu.IsOpen = true; menu.UpdateLayout();
                Require(SystemParameters.HighContrast || PopupBackdrop.HasMaterial(menu), "Menu não recebeu composição nativa em " + theme);
                var frame = new System.Windows.Threading.DispatcherFrame();
                bool closed = false;
                menu.Closed += (_, _) => { closed = true; frame.Continue = false; };
                var timeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timeout.Tick += (_, _) => frame.Continue = false;
                menu.IsOpen = false;
                if (!closed) { timeout.Start(); try { System.Windows.Threading.Dispatcher.PushFrame(frame); } finally { timeout.Stop(); } }
                Require(closed, "Menu não concluiu o fechamento.");
                Require(!PopupBackdrop.HasMaterial(menu), "Material do menu não foi liberado ao fechar.");
            }
            finally { menu.IsOpen = false; owner.Close(); }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
