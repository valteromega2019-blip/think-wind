using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

internal static class PerformanceComparison
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window, int command);
    private sealed record Sample(string Scenario, bool GameForeground, double GameCpuOneCore, double LauncherCpuOneCore, double MemoryMB, int Threads);
    internal static int Run()
    {
        var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "performance-v0.10"); Directory.CreateDirectory(output);
        using var game = Process.GetProcessesByName("WardogsClient-Win64-Shipping").FirstOrDefault() ?? throw new InvalidOperationException("Wardogs não está aberto.");
        if (Process.GetProcessesByName("ThinkWind").Length != 0) throw new InvalidOperationException("Encerre a instância do Think Wind antes da comparação.");
        var samples = new List<Sample>();
        void Measure(string scenario, Process? launcher, int seconds)
        {
            game.Refresh(); double lastGame = game.TotalProcessorTime.TotalMilliseconds, lastLauncher = launcher?.TotalProcessorTime.TotalMilliseconds ?? 0;
            var watch = Stopwatch.StartNew(); double previous = 0;
            for (int i = 0; i < seconds; i++)
            {
                Thread.Sleep(1000); double now = watch.Elapsed.TotalMilliseconds, delta = now - previous; previous = now;
                game.Refresh(); double gameCpu = game.TotalProcessorTime.TotalMilliseconds, cpu = 0, memory = 0; int threads = 0;
                if (launcher is not null && !launcher.HasExited)
                { launcher.Refresh(); double total = launcher.TotalProcessorTime.TotalMilliseconds; cpu = (total - lastLauncher) / delta * 100; lastLauncher = total; memory = launcher.WorkingSet64 / 1048576d; threads = launcher.Threads.Count; }
                GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
                samples.Add(new(scenario, foreground == game.Id, (gameCpu - lastGame) / delta * 100, cpu, memory, threads)); lastGame = gameCpu;
            }
        }
        ShowWindow(game.MainWindowHandle, 9); bool focused = SetForegroundWindow(game.MainWindowHandle); Console.WriteLine("Ativação do jogo: " + focused);
        Measure("encerrado", null, 8);
        foreach (string scenario in new[] { "normal", "modo-jogo" })
        {
            System.Windows.Window? desktop = null;
            if (scenario == "modo-jogo")
            {
                _ = System.Windows.Application.Current ?? new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
                desktop = new System.Windows.Window { Title = "Think Wind — controle de retorno ao desktop", Width = 220, Height = 120, ShowInTaskbar = false };
                desktop.Show(); SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(desktop).Handle);
            }
            var start = new ProcessStartInfo(Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "diagnostic-v0.10", "ThinkWind.exe")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--diagnostic"); start.ArgumentList.Add("--diagnostic-report"); start.ArgumentList.Add(Path.Combine(output, scenario + ".json"));
            if (scenario == "normal") start.ArgumentList.Add("--diagnostic-normal");
            using var launcher = Process.Start(start) ?? throw new InvalidOperationException("Falha ao iniciar diagnóstico.");
            Thread.Sleep(3000); SetForegroundWindow(game.MainWindowHandle); Measure(scenario, launcher, 12);
            if (desktop is not null)
            {
                SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(desktop).Handle); Thread.Sleep(1800);
                SetForegroundWindow(game.MainWindowHandle); Thread.Sleep(1800);
            }
            if (!launcher.WaitForExit(12000)) throw new InvalidOperationException("Diagnóstico não encerrou dentro do prazo.");
            desktop?.Close();
            Console.WriteLine(scenario + ": " + File.ReadAllText(Path.Combine(output, scenario + ".json")));
        }
        File.WriteAllText(Path.Combine(output, "samples.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        var summary = samples.GroupBy(sample => sample.Scenario).Select(group => new { Scenario = group.Key, Samples = group.Count(), ForegroundSamples = group.Count(sample => sample.GameForeground), LauncherCpuOneCoreAverage = group.Average(sample => sample.LauncherCpuOneCore), LauncherCpuOneCorePeak = group.Max(sample => sample.LauncherCpuOneCore), LauncherMemoryMB = group.Average(sample => sample.MemoryMB), GameCpuOneCoreAverage = group.Average(sample => sample.GameCpuOneCore) });
        string json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }); File.WriteAllText(Path.Combine(output, "summary.json"), json); Console.WriteLine(json);
        return 0;
    }
}
