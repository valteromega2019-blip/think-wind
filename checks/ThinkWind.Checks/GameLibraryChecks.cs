using System;
using System.Linq;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

internal static class GameLibraryChecks
{
    internal static int Run()
    {
        int passed = 0;
        void Check(bool condition, string name)
        { if (!condition) throw new InvalidOperationException(name); Console.WriteLine("PASS " + name); passed++; }
        try
        {
            var steamShortcut = new AppEntry("Jogo", "game.lnk", "Jogos", null, ResolvedTarget: @"C:\Steam\steam.exe");
            Check(GameLibrary.Detect(steamShortcut), "Classificacao de jogo prevalece sobre executavel Steam do atalho");
            Check(!GameLibrary.Detect(steamShortcut with { Category = "Aplicativos" }), "Steam sem classificacao continua aplicativo");
            Check(GameLibrary.Detect(new("Jogo", "steam://rungameid/123", "Aplicativos", null)), "URI de jogo Steam reconhecida");
            var games = SteamLibraryEntries.Read(new ShellIcons()).ToArray();
            Console.WriteLine("Jogos nos registros locais: " + string.Join(", ", games.Select(game => game.Name)));
            Check(games.All(game => game.Category == "Jogos" && GameLibrary.IsGameUri(game.Target)), "Registros instalados usam categoria e URI corretas");
            GameLibrary.Update(games);
            Check(games.All(GameLibrary.IsGame), "Jogos registrados reconhecidos pelo filtro do Iniciar");
            Console.WriteLine($"Jogos: {passed} aprovados; 0 falhas"); return 0;
        }
        catch (Exception error) { Console.WriteLine("FAIL " + error); return 1; }
    }
}
