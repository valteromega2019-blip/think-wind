using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

internal static class IconographyChecks
{
    internal static int Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        int passed = 0, failed = 0;
        void Check(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("PASS " + name); }
            catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error); }
        }
        Check("Modo experimental desligado por padrão e em perfis anteriores", () =>
        {
            Require(!new Preferences().UnifiedAppIcons, "Novo perfil ativou o modo.");
            Require(JsonSerializer.Deserialize<Preferences>("{}")?.UnifiedAppIcons == false, "Perfil anterior ativou o modo.");
            Require(!new Preferences().AppIconsWithoutBackground, "Novo perfil removeu o fundo.");
        });
        Check("Preferência de ativação persiste em arquivo temporário", () =>
        {
            string path = Path.Combine(Path.GetTempPath(), "OrbyIconography", Guid.NewGuid().ToString("N"), "settings.json");
            var store = new SettingsStore(path); Require(!store.Value.UnifiedAppIcons, "Padrão inesperado.");
            store.Value.UnifiedAppIcons = true; store.Save();
            Require(new SettingsStore(path).Value.UnifiedAppIcons, "A ativação não persistiu.");
            store.Value.AppIconsWithoutBackground = true; store.Save();
            Require(new SettingsStore(path).Value.AppIconsWithoutBackground, "Estilo sem fundo não persistiu.");
            store.Value.UnifiedAppIcons = false; store.Save();
            Require(!new SettingsStore(path).Value.UnifiedAppIcons, "A desativação não persistiu.");
        });
        Check("Complementos são uma lista plana de símbolos válidos, sem IDs duplicados", () =>
        {
            using var stream = typeof(Preferences).Assembly.GetManifestResourceStream("OrbyLauncher.Iconography.Supplemental");
            Require(stream is not null, "Complemento ausente.");
            using var data = JsonDocument.Parse(stream!);
            var entries = data.RootElement.EnumerateArray().ToArray();
            Require(entries.Length >= 37, "Complemento incompleto.");
            Require(entries.Select(entry => entry.GetProperty("id").GetString()).Distinct().Count() == entries.Length, "IDs repetidos.");
            foreach (var entry in entries)
            {
                string id = entry.GetProperty("id").GetString()!;
                string path = entry.GetProperty("path").GetString()!;
                bool evenOdd = entry.TryGetProperty("fillRule", out var rule) && rule.GetString() == "evenodd";
                var shape = Geometry.Parse((evenOdd ? "F0 " : "F1 ") + path);
                if (evenOdd) shape = shape.GetOutlinedPathGeometry();
                Require(!shape.Bounds.IsEmpty && shape.Bounds.Width > 0 && shape.Bounds.Height > 0, id + " vazio.");
            }
            Console.WriteLine("  " + entries.Length + " complementos processados.");
        });
        Check("Microsoft, Adobe e ArtCraft resolvem símbolos próprios em vez de iniciais", () =>
        {
            var resolve = typeof(AppIconTheme).GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (var sample in new[] {
                ("Microsoft Store", "Microsoft.WindowsStore_8wekyb3d8bbwe!App"),
                ("Word", "WINWORD.EXE"), ("Excel", "EXCEL.EXE"), ("Microsoft Teams", "MSTeams!App"),
                ("Adobe Photoshop", "Photoshop.exe"), ("Adobe Illustrator", "Illustrator.exe"),
                ("After Effects", "AfterFX.exe"), ("Adobe Premiere Pro", "Adobe Premiere Pro.exe"),
                ("ArtCraft Launcher", "artcraft-launcher.exe"), ("WordCraft", "wordcraft.exe"),
                ("GridCraft", "gridcraft.exe"), ("DeckCraft", "deckcraft.exe"),
                ("VectorCraft", "vectorcraft.exe"), ("SolidCraft", "solidcraft.exe"),
                ("ChatGPT", "ChatGPT.exe"), ("Visual Studio Code", "Code.exe"),
                ("Hex2Think", "hex-to-think.exe"), ("Tretalk", "tretalk.exe") })
            {
                var symbol = resolve.Invoke(null, new object?[] { sample.Item1, sample.Item2, null })!;
                Require(symbol.GetType().GetProperty("Path")!.GetValue(symbol) is string, sample.Item1 + " sem vetor.");
            }
        });
        Check("Ícones sem fundo atualizam imediatamente e só destacam o primeiro plano", () =>
        {
            AppIconTheme.Apply(new Preferences { UnifiedAppIcons = true, AppIconsWithoutBackground = true, AccentColor = "#3478D4" });
            var spotify = new AppIcon { AppName = "Spotify", Target = "spotify.exe", Width = 64, Height = 64 };
            spotify.Measure(new Size(64, 64)); spotify.Arrange(new Rect(0, 0, 64, 64));
            spotify.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            int changes = 0; void Changed() => changes++;
            AppIconTheme.Changed += Changed;
            try
            {
                AppIconTheme.UpdateForegroundApp("spotify.exe", null);
                Require(spotify.RenderSize.Width == 64 && spotify.RenderSize.Height == 64, "Controle sem imagem nativa perdeu o tamanho.");
                Require(CountColor(Render(spotify), Color.FromRgb(52, 120, 212)) > 50, "Destaque não mudou no próximo desenho.");
                Require(Pixel(Render(spotify), 0, 0, 1).A == 0, "Fundo não foi removido.");
                int before = changes;
                AppIconTheme.UpdateForegroundApp("spotify.exe", null);
                Require(changes == before, "Mesmo primeiro plano gerou atualização redundante.");
                AppIconTheme.UpdateForegroundApp("Code.exe", null);
                Require(CountColor(Render(spotify), Colors.White) > 50, "App em segundo plano continuou aceso.");
                Require(CountColor(Render(spotify), Color.FromRgb(52, 120, 212)) == 0, "Destaque antigo ficou retido.");
                AppIconTheme.UpdateForegroundApp("", null);
                Require(CountColor(Render(spotify), Colors.White) > 50, "Sem app ativo não ficou branco.");
            }
            finally { AppIconTheme.Changed -= Changed; spotify.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        });
        Check("Catálogo completo é compatível com a geometria WPF", () =>
        {
            using var stream = typeof(Preferences).Assembly.GetManifestResourceStream("OrbyLauncher.Iconography");
            Require(stream is not null, "Recurso ausente.");
            using var data = JsonDocument.Parse(stream!); int count = 0;
            foreach (var entry in data.RootElement.EnumerateArray())
            {
                try { var shape = Geometry.Parse("F1 " + entry.GetProperty("path").GetString()); shape.Freeze(); count++; }
                catch (Exception error) { throw new Exception("Vetor incompatível: " + entry.GetProperty("id").GetString(), error); }
            }
            Require(count == 3472, "Quantidade diferente da base fixada.");
            Console.WriteLine("  " + count + " vetores processados.");
        });
        Check("Símbolos e monogramas renderizam em quatro escalas DPI", () =>
        {
            AppIconTheme.Apply(new Preferences { UnifiedAppIcons = true, AccentColor = "#3478D4" });
            foreach (double scale in new[] { 1d, 1.25, 1.5, 2d })
            foreach (var sample in new[] { ("Spotify", "spotify.exe"), ("WhatsApp", "whatsapp.exe"), ("Configurações", "ms-settings:"), ("Documentos", @"C:\Users\Example\Documents"), ("Adobe Photoshop", "Photoshop.exe"), ("Aplicativo desconhecido", "unknown.exe") })
            {
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen()) AppIconTheme.Draw(dc, new Size(64, 64), sample.Item1, sample.Item2, null);
                var bitmap = new RenderTargetBitmap((int)(64 * scale), (int)(64 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(visual); byte[] pixels = Pixels(bitmap); int whites = 0;
                for (int index = 0; index < pixels.Length; index += 4)
                    if (pixels[index] > 245 && pixels[index + 1] > 245 && pixels[index + 2] > 245 && pixels[index + 3] > 245) whites++;
                Require(whites > 10, sample.Item1 + " não exibiu símbolo branco em " + scale);
                Require(Pixel(bitmap, 4, 32, scale) == Color.FromRgb(52, 120, 212), "Fundo não acompanha a cor escolhida.");
            }
        });
        Check("Alternar e recolorir preserva a imagem original", () =>
        {
            var source = new DrawingImage(new GeometryDrawing(Brushes.Red, null, new RectangleGeometry(new Rect(0, 0, 24, 24)))); source.Freeze();
            var icon = new AppIcon { AppName = "Spotify", Target = "spotify.exe", Source = source, Width = 64, Height = 64 };
            icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            try
            {
                icon.Measure(new Size(64, 64)); icon.Arrange(new Rect(0, 0, 64, 64));
                AppIconTheme.Apply(new Preferences { UnifiedAppIcons = true, AccentColor = "#3478D4" });
                Require(Pixel(Render(icon), 4, 32, 1) == Color.FromRgb(52, 120, 212), "Modo não aplicado ao controle.");
                AppIconTheme.Apply(new Preferences { UnifiedAppIcons = true, AccentColor = "#6C3BCD" });
                Require(Pixel(Render(icon), 4, 32, 1) == Color.FromRgb(108, 59, 205), "Cor não mudou em tempo real.");
                AppIconTheme.Apply(new Preferences { UnifiedAppIcons = false });
                Require(ReferenceEquals(icon.Source, source), "Imagem original foi substituída.");
                Require(Pixel(Render(icon), 32, 32, 1) == Colors.Red, "Ícone original não voltou.");
            }
            finally { icon.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
        });
        AppIconTheme.Apply(new Preferences()); app.Shutdown();
        Console.WriteLine($"Iconografia: {passed} aprovados; {failed} falhas. Nenhuma preferência real alterada.");
        return failed == 0 ? 0 : 1;
    }
    private static RenderTargetBitmap Render(FrameworkElement icon)
    {
        // Off-screen fixtures have no normal presentation loop. Process the
        // arrange/render invalidation queued by InvalidateVisual before capture;
        // otherwise RenderTargetBitmap replays the preceding frame's drawing.
        icon.UpdateLayout();
        var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(icon); return bitmap;
    }
    private static byte[] Pixels(BitmapSource image)
    { byte[] pixels = new byte[image.PixelWidth * image.PixelHeight * 4]; image.CopyPixels(pixels, image.PixelWidth * 4, 0); return pixels; }
    private static Color Pixel(BitmapSource image, int x, int y, double scale)
    { var pixels = Pixels(image); int at = ((int)(y * scale) * image.PixelWidth + (int)(x * scale)) * 4; return Color.FromArgb(pixels[at + 3], pixels[at + 2], pixels[at + 1], pixels[at]); }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static int CountColor(BitmapSource image, Color color)
    {
        var pixels = Pixels(image); int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
            if (pixels[i] == color.B && pixels[i + 1] == color.G && pixels[i + 2] == color.R && pixels[i + 3] == color.A) count++;
        return count;
    }
}
