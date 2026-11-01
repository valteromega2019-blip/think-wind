using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace OrbyLauncher.Services;
internal sealed class GameOptimizer
{
    [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(nint root, out nint scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(nint root, ref Guid scheme);
    [DllImport("kernel32.dll")] private static extern nint LocalFree(nint pointer);
    private sealed record Recovery(Guid Original, Guid Applied);
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "optimizer-recovery.json");
    private Recovery? recovery;
    internal bool Active { get; private set; }
    internal bool GameForeground { get; private set; }
    internal string Status { get; private set; } = "Pronto · perfil automático desativado.";
    internal event Action? Changed;
    private static Guid ReadPlan()
    {
        uint result = PowerGetActiveScheme(0, out nint pointer);
        if (result != 0) throw new InvalidOperationException("O Windows não informou o plano de energia: " + result);
        try { return Marshal.PtrToStructure<Guid>(pointer); } finally { LocalFree(pointer); }
    }
    internal void Recover()
    {
        try { if (File.Exists(path)) { recovery = JsonSerializer.Deserialize<Recovery>(File.ReadAllText(path)); Restore(); } }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) { Status = "Recuperação pendente: " + e.Message; }
    }
    internal void Apply(bool game, Preferences preferences)
    {
        if (GameForeground != game) { GameForeground = game; Changed?.Invoke(); }
        if (!game || !preferences.OptimizerEnabled) { if (Active || recovery is not null) Restore(); return; }
        if (Active) return;
        try
        {
            if (preferences.OptimizerPowerPlan != "Keep")
            {
                if (recovery is not null) throw new InvalidOperationException("Restaure a sessão anterior primeiro.");
                Guid original = ReadPlan();
                Guid selected = preferences.OptimizerPowerPlan == "Balanced" ? new("381b4222-f694-41f0-9685-ff5bb260df2e") : new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                if (original != selected)
                {
                    var saved = new Recovery(original, selected); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(saved)); File.Move(path + ".tmp", path, true); recovery = saved;
                    uint result = PowerSetActiveScheme(0, ref selected);
                    if (result != 0) { Restore(); throw new InvalidOperationException("Plano indisponível ou recusado pelo Windows: " + result); }
                }
            }
            Active = true; Status = "Jogo reconhecido · perfil ativo. As funções configuradas do launcher foram preservadas.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { Status = e.Message; }
        Changed?.Invoke();
    }
    internal void Restore()
    {
        try
        {
            if (recovery is { } saved)
            {
                if (ReadPlan() == saved.Applied)
                { Guid original = saved.Original; uint result = PowerSetActiveScheme(0, ref original); if (result != 0) throw new InvalidOperationException("Restauração recusada pelo Windows: " + result); }
                File.Delete(path); recovery = null;
            }
            Active = false; Status = "Perfil encerrado · plano restaurado ou escolha externa preservada.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { Status = "Restauração pendente: " + e.Message; }
        Changed?.Invoke();
    }
}
