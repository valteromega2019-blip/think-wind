using System;
using System.IO;
using System.Linq;
using ThinkWind.Native;
using ThinkWind.Services;

internal static class Program
{
    private static int passed, failed, skipped;
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ThinkWindChecks", Guid.NewGuid().ToString("N"));
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--visual-background")) return VisualBackground.Run();
        Directory.CreateDirectory(Root);
        Run("Configuração ausente usa valores padrão", () =>
        {
            var store = Store("missing");
            Require(store.Value.ShowAudio && store.Value.Shortcuts.Count == 2 && store.LoadWarning is null);
        });
        Run("Preferências e atalhos persistem após reabrir", () =>
        {
            var store = Store("roundtrip");
            store.Value.DockScale = 1.25; store.Value.TintOpacity = .35;
            store.Value.AutoHide = true; store.Value.ShowAudio = false;
            store.Value.ShowMemory = false; store.Value.ShowBattery = false;
            store.Value.MonitorIndex = 2;
            store.Value.Shortcuts.Clear(); store.Value.Shortcuts.Add(new("Aplicativo de exemplo", @"C:\Example\app.exe"));
            store.Save(); var loaded = Store("roundtrip");
            Require(loaded.Value.DockScale == 1.25 && loaded.Value.TintOpacity == .35 && loaded.Value.AutoHide);
            Require(!loaded.Value.ShowAudio && !loaded.Value.ShowMemory && !loaded.Value.ShowBattery && loaded.Value.MonitorIndex == 2);
            Require(loaded.Value.Shortcuts.SequenceEqual(store.Value.Shortcuts) && loaded.LoadWarning is null);
        });
        Run("Limites de tamanho e opacidade são normalizados", () =>
        {
            File.WriteAllText(PathFor("limits"), "{\"DockScale\":99,\"TintOpacity\":-4}");
            var store = Store("limits"); Require(store.Value.DockScale == 1.5 && store.Value.TintOpacity == .2);
        });
        Run("JSON inválido é preservado e gera aviso", () =>
        {
            File.WriteAllText(PathFor("corrupt"), "{broken");
            var store = Store("corrupt");
            Require(store.LoadWarning is not null && store.Value.Shortcuts.Count == 2);
            Require(File.ReadAllText(PathFor("corrupt")) == "{broken");
        });
        Run("Atalhos nulos ou vazios são descartados", () =>
        {
            File.WriteAllText(PathFor("shortcuts"), "{\"Shortcuts\":[null,{\"Name\":\"Vazio\",\"Target\":\" \"},{\"Name\":\"OK\",\"Target\":\"explorer.exe\"}]}");
            Require(Store("shortcuts").Value.Shortcuts.Count == 1);
        });
        Run("Lista de atalhos nula não impede inicialização", () =>
        {
            File.WriteAllText(PathFor("null-list"), "{\"Shortcuts\":null}");
            Require(Store("null-list").Value.Shortcuts.Count == 0);
        });
        Run("Salvar substitui o arquivo sem deixar temporário", () =>
        {
            var store = Store("atomic"); store.Save(); store.Value.DockScale = .9; store.Save();
            Require(Store("atomic").Value.DockScale == .9 && !File.Exists(PathFor("atomic") + ".tmp"));
        });
        Run("Falha de escrita é reportada", () =>
        {
            var file = PathFor("not-directory"); File.WriteAllText(file, "file");
            try { new SettingsStore(Path.Combine(file, "settings.json")).Save(); }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }
            throw new InvalidOperationException("Escrita impossível não gerou erro.");
        });
        Run("Enumeração de janelas nativas retorna títulos", () =>
        {
            var windows = Desktop.Windows();
            Require(windows.Count > 0 && windows.All(w => w.Handle != 0 && !string.IsNullOrWhiteSpace(w.Title)));
        });
        Run("Uso de RAM está entre 1 e 100", () => Require(Desktop.MemoryPercent() is > 0 and <= 100));
        Run("Ilha começa compacta e hover não abre painel completo", () =>
        {
            var state = new IslandInteraction(); Require(state.Stage == IslandStage.Compact);
            state.Enter(); Require(state.Stage == IslandStage.Hover); state.Leave(); Require(state.Stage == IslandStage.Compact);
        });
        Run("Painel completo retorna ao estágio anterior", () =>
        {
            var state = new IslandInteraction(); state.Enter(); state.OpenFull(); state.Leave();
            Require(state.Stage == IslandStage.Full); state.CloseFull(); Require(state.Stage == IslandStage.Hover);
        });
        Run("Mini-ilha compacta bloqueia expansão por hover", () =>
        {
            var state = new IslandInteraction(); state.OpenMini(); state.Enter(); state.Leave();
            Require(state.Stage == IslandStage.Compact && state.MiniOpen); state.CloseMini(); state.Enter(); Require(state.Stage == IslandStage.Hover);
        });
        Run("Mini-ilha mantém estágio hover sem promover painel", () =>
        {
            var state = new IslandInteraction(); state.Enter(); state.OpenMini(); state.Leave();
            Require(state.Stage == IslandStage.Hover); state.CloseMini(); state.Leave(); Require(state.Stage == IslandStage.Compact);
        });
        Run("Rede retorna snapshot real sem afirmar acesso à internet", () =>
        {
            var state = NetworkService.Read(); Require(!string.IsNullOrWhiteSpace(state.Name) && !string.IsNullOrWhiteSpace(state.Kind));
        });
        foreach (bool top in new[] { false, true }) foreach (bool light in new[] { false, true })
            Run($"Composição {(top ? "ilha" : "dock")} / {(light ? "claro" : "escuro")}: alfa, HWNDs e recorte", () => OverlayChecks.Check(top, light, PathFor("composition-" + top + light)));
        Run("Mini de volume fecha por Esc sem fechar a ilha", () => OverlayChecks.MiniEscape(PathFor("mini-escape")));
        Run("Mini de rede fecha ao ativar outra janela", () => OverlayChecks.MiniOutsideFocus(PathFor("mini-outside")));
        CheckAudio(CoreAudio.Flow.Render, "Saída");
        CheckAudio(CoreAudio.Flow.Capture, "Microfone");
        Console.WriteLine($"RESULTADO: {passed} aprovados; {failed} falhos; {skipped} ignorados.");
        Console.WriteLine("Arquivos temporários isolados: " + Root);
        BackdropComposition.Shutdown();
        return failed == 0 ? 0 : 1;
    }
    private static void CheckAudio(CoreAudio.Flow flow, string name)
    {
        var audio = new AudioService();
        Run(name + ": enumerar dispositivos ativos", () =>
        {
            var devices = audio.Devices(flow);
            Require(devices.All(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Name)));
            Console.WriteLine($"  {devices.Count} dispositivo(s) ativo(s)");
        });
        var available = audio.Devices(flow);
        if (available.Count == 0) { skipped++; Console.WriteLine("SKIP " + name + ": sem hardware ativo para volume e mudo."); return; }
        Run(name + ": ler volume e mudo", () =>
        {
            foreach (var device in available)
            { var state = audio.Read(flow, device.Id); Require(state.Id == device.Id && double.IsFinite(state.Volume) && state.Volume is >= 0 and <= 100); }
        });
        Run(name + ": escrita de volume e mudo preservando os valores atuais", () =>
        {
            var original = audio.Read(flow, available[0].Id);
            // Exercise the real setter without changing the user's audio level or mute.
            audio.SetVolume(flow, original.Id, original.Volume); audio.SetMuted(flow, original.Id, original.Muted);
            var readback = audio.Read(flow, original.Id);
            Require(Math.Abs(readback.Volume - original.Volume) < 2 && readback.Muted == original.Muted);
        });
        Run(name + ": endpoint inválido gera erro recuperável", () =>
        {
            try { audio.Read(flow, "ThinkWind-invalid-endpoint"); }
            catch (AudioUnavailableException) { return; }
            throw new InvalidOperationException("Endpoint inválido não foi rejeitado.");
        });
    }
    private static void Run(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
    }
    private static string PathFor(string name) => Path.Combine(Root, name + ".json");
    private static SettingsStore Store(string name) => new(PathFor(name));
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Condição não atendida."); }
}
