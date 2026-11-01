using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;
internal static class OptimizerAnalysis
{
    internal sealed record Report(string Text, OptimizerCloseCandidate[] Candidates);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    private sealed record Reading(int Id, string Name, long Started, double Cpu, long Tick, long Memory, bool Review);
    internal static async Task<Report> Analyze(HardwareSample? sample, bool gameMode, bool enabled, string mode, bool overlay, CancellationToken token)
    {
        return await Task.Run(async () =>
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
            string windows = Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) + Path.DirectorySeparatorChar;
            var first = new Dictionary<int, Reading>(); int unavailable = 0;
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        string name = process.ProcessName;
                        string? path = process.MainModule?.FileName;
                        bool review = process.Id != Environment.ProcessId && process.Id != foreground && process.SessionId != 0
                            && path is not null && !path.StartsWith(windows, StringComparison.OrdinalIgnoreCase)
                            && !name.StartsWith("Orby", StringComparison.OrdinalIgnoreCase)
                            && !new[] { "steam", "steamwebhelper", "EpicGamesLauncher", "Discord", "Tretalk", "MsMpEng" }.Contains(name, StringComparer.OrdinalIgnoreCase);
                        first[process.Id] = new(process.Id, name, process.StartTime.ToUniversalTime().Ticks, process.TotalProcessorTime.TotalMilliseconds, Stopwatch.GetTimestamp(), process.WorkingSet64, review);
                    }
                    catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException) { unavailable++; }
                }
            }
            await Task.Delay(1200, token);
            var rows = new List<(int Id, string Name, double Cpu, long Memory, bool Review)>();
            foreach (var item in first.Values)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var process = Process.GetProcessById(item.Id);
                    if (process.StartTime.ToUniversalTime().Ticks != item.Started) continue;
                    double elapsed = Stopwatch.GetElapsedTime(item.Tick).TotalMilliseconds;
                    double cpu = Math.Clamp((process.TotalProcessorTime.TotalMilliseconds - item.Cpu) / elapsed / Environment.ProcessorCount * 100, 0, 100);
                    rows.Add((item.Id, item.Name, cpu, process.WorkingSet64, item.Review));
                }
                catch (Exception e) when (e is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException) { unavailable++; }
            }
            var result = new StringBuilder("ANÁLISE LOCAL · " + DateTime.Now.ToString("HH:mm") + "\n\n");
            result.AppendLine("CONFIGURAÇÕES");
            try
            {
                string plan = GameOptimizer.ReadPlan().ToString();
                result.AppendLine("• Plano de energia atual do Windows: " + (plan switch
                {
                    "381b4222-f694-41f0-9685-ff5bb260df2e" => "Equilibrado",
                    "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" => "Alto desempenho",
                    "a1841308-3541-4fab-bc81-f71556f20b4a" => "Economia de energia",
                    _ => "Personalizado (" + plan + ")"
                }) + ".");
            }
            catch (InvalidOperationException) { result.AppendLine("• Plano atual do Windows indisponível nesta leitura."); }
            result.AppendLine(gameMode ? "• Modo Jogo do launcher: ativado." : "• Ative o Modo Jogo do launcher para reduzir trabalho da interface durante jogos.");
            result.AppendLine(enabled ? "• Perfil automático habilitado: " + mode + "." : "• Perfil automático desativado. Escolha um modo e clique em Otimizar para prepará-lo.");
            if (mode == "Economia") result.AppendLine("• Economia reduz consumo, mas pode limitar FPS. Para priorizar FPS, avalie Desempenho e acompanhe temperaturas.");
            if (mode == "Desempenho") result.AppendLine("• Desempenho pode aumentar calor e consumo; compare com Equilibrado no seu jogo.");
            if (overlay) result.AppendLine("• Overlay habilitado. Compare com ele desativado se estiver investigando quedas de desempenho.");
            if (sample is not null && DateTime.Now - sample.Time < TimeSpan.FromSeconds(30))
            {
                if (sample.RamTotal is > 0 && sample.RamUsed is { } used && used / sample.RamTotal.Value >= .85)
                    result.AppendLine("• RAM acima de 85%: revise aplicativos pesados e abas que não estiver usando; não force limpeza de memória.");
                if (sample.DiskTotal is > 0 && sample.DiskFree is { } free && free / sample.DiskTotal.Value < .1)
                    result.AppendLine("• Unidade monitorada com menos de 10% livre: revise armazenamento pelas Configurações do Windows.");
                if (sample.Cpu.Load is >= 90) result.AppendLine("• CPU acima de 90% nesta amostra: compare tarefas concorrentes e opções do jogo que dependem da CPU.");
                if (sample.Gpu.Load is >= 95) result.AppendLine("• GPU acima de 95% nesta amostra: avalie resolução, efeitos ou limite de FPS para reduzir carga.");
                result.AppendLine("• Temperaturas observadas: CPU " + Format(sample.Cpu.Temperature) + " · GPU " + Format(sample.Gpu.Temperature) + ". Limites térmicos dependem do componente.");
            }
            else result.AppendLine("• Sensores recentes indisponíveis; nenhuma conclusão térmica ou de pressão de RAM foi feita.");
            result.AppendLine("\nMAIOR CONSUMO OBSERVADO");
            foreach (var row in rows.OrderByDescending(r => r.Cpu).ThenByDescending(r => r.Memory).Take(6))
                result.AppendLine($"• {row.Name}: CPU {row.Cpu:0.0}% · RAM {row.Memory / 1048576d:0} MB");
            result.AppendLine("\nAPLICATIVOS PARA REVISAR");
            var closable = OptimizerProcessCloser.Candidates(rows.Where(r => r.Review && (r.Cpu >= 2 || r.Memory >= 512L * 1048576))
                .OrderByDescending(r => r.Cpu).ThenByDescending(r => r.Memory).Take(20).Select(r => r.Id), token);
            if (closable.Length == 0) result.AppendLine("• Nenhum aplicativo elegível para fechamento nesta amostra. Processos protegidos ou sem verificação disponível ficam de fora.");
            foreach (string name in closable.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase))
                result.AppendLine("• " + name + ": se não estiver usando, selecione abaixo e aprove o fechamento. Pode ser necessário ao jogo; confirme antes.");
            result.AppendLine("\nAmostra curta, não diagnóstico de necessidade. CPU normalizada pelo total de processadores lógicos; RAM inclui páginas compartilhadas e não representa memória que será liberada. " + unavailable + " leituras indisponíveis/encerradas. Nada foi fechado ou alterado por esta análise.");
            return new Report(result.ToString(), closable);
        }, token);
    }
    private static string Format(double? temperature) => temperature is { } value ? value.ToString("0") + " °C" : "indisponível";
}

