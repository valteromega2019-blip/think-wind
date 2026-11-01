using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OrbyLauncher.Services;
internal static class LocalOptimizerAi
{
    internal static string? FindRuntime()
    {
        string[] directories = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Ollama"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama") }
            .Concat((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)).ToArray();
        foreach (string directory in directories)
        {
            try { string path = Path.Combine(directory.Trim().Trim('"'), "ollama.exe"); if (Path.IsPathFullyQualified(path) && File.Exists(path)) return path; }
            catch (ArgumentException) { }
        }
        return null;
    }
    internal static async Task StartRuntime(CancellationToken token)
    {
        try { await Models(token); return; } catch (HttpRequestException) { }
        string runtime = FindRuntime() ?? throw new InvalidOperationException("Ollama não encontrado. Reabra o Orby após instalar o Ollama, ou use a instalação padrão do Windows.");
        var info = new ProcessStartInfo(runtime, "serve") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        info.Environment["OLLAMA_HOST"] = "127.0.0.1:11434";
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Não foi possível iniciar o mecanismo local.");
        for (int i = 0; i < 30; i++)
        {
            token.ThrowIfCancellationRequested();
            try { await Models(token); return; } catch (HttpRequestException) { }
            if (process.HasExited) throw new InvalidOperationException("O mecanismo encerrou antes de iniciar. Confira sua instalação do Ollama.");
            await Task.Delay(300, token);
        }
        throw new InvalidOperationException("O mecanismo ainda não respondeu. Tente novamente.");
    }
    internal static async Task DownloadModel(string model, IProgress<string> progress, CancellationToken token)
    {
        if (!Regex.IsMatch(model, @"^[a-zA-Z0-9][a-zA-Z0-9_.-]*(?::[a-zA-Z0-9_.-]+)?$") || model.Contains("cloud", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Informe o nome de um modelo local do catálogo Ollama, sem endereço de servidor.");
        using var client = Client(); client.Timeout = Timeout.InfiniteTimeSpan;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(45));
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/pull") { Content = new StringContent(JsonSerializer.Serialize(new { model, stream = true }), Encoding.UTF8, "application/json") };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token); response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var reader = new StreamReader(stream);
        bool success = false; DateTime reported = DateTime.MinValue;
        while (await reader.ReadLineAsync(timeout.Token) is string line)
        {
            if (line.Length > 65536) throw new InvalidOperationException("Resposta inesperada do serviço local.");
            using var json = JsonDocument.Parse(line); var root = json.RootElement;
            if (root.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
            string status = root.TryGetProperty("status", out var statusValue) ? statusValue.GetString() ?? "" : "";
            success |= status == "success";
            if (DateTime.UtcNow - reported < TimeSpan.FromMilliseconds(200) && !success) continue;
            reported = DateTime.UtcNow;
            string detail = root.TryGetProperty("total", out var total) && total.TryGetInt64(out long size) && size > 0 && root.TryGetProperty("completed", out var done) && done.TryGetInt64(out long bytes) ? $" · {bytes / 1048576d:0} / {size / 1048576d:0} MB" : "";
            progress.Report(status + detail);
        }
        if (!success) throw new InvalidOperationException("Download não confirmado. Você pode tentar novamente para retomar pelo Ollama.");
    }
    private static HttpClient Client() => new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
    { BaseAddress = new Uri("http://127.0.0.1:11434/"), Timeout = TimeSpan.FromSeconds(90), MaxResponseContentBufferSize = 262144 };
    private static bool Local(JsonElement model)
    {
        string name = model.GetProperty("name").GetString() ?? "";
        return name.Length > 0 && !name.Contains("cloud", StringComparison.OrdinalIgnoreCase)
            && (!model.TryGetProperty("remote_host", out var host) || string.IsNullOrEmpty(host.GetString()));
    }
    internal static async Task<string[]> Models(CancellationToken token)
    {
        using var client = Client(); using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(4));
        using var response = await client.GetAsync("api/tags", deadline.Token); response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
        return json.RootElement.GetProperty("models").EnumerateArray().Where(Local).Select(model => model.GetProperty("name").GetString()!).Take(50).ToArray();
    }
    internal static async Task<string> Suggest(string model, HardwareSample? sample, Preferences settings, CancellationToken token)
    {
        var installed = await Models(token);
        if (!installed.Contains(model, StringComparer.Ordinal)) throw new InvalidOperationException("Selecione um modelo local instalado. Modelos remotos/cloud não são aceitos.");
        var snapshot = new
        {
            collected = sample?.Time,
            cpu = new { name = sample?.Cpu.Name, temperature = sample?.Cpu.Temperature, load = sample?.Cpu.Load },
            gpu = new { name = sample?.Gpu.Name, temperature = sample?.Gpu.Temperature, load = sample?.Gpu.Load },
            ramUsedGb = sample?.RamUsed, ramTotalGb = sample?.RamTotal,
            gameModeEnabled = settings.GameModeEnabled, optimizerEnabled = settings.OptimizerEnabled, powerPreference = settings.OptimizerPowerPlan
        };
        using var client = Client();
        var body = new
        {
            model, stream = false, keep_alive = 0,
            system = "Você é um assistente local de diagnóstico para jogos. Responda em português, em até 5 recomendações curtas. Analise somente as métricas fornecidas. Distinga dados ausentes, observações e hipóteses. Uma amostra não comprova gargalo nem ganho de FPS. Não recomende limpar RAM/standby/working sets, desativar antivírus/serviços, alterar registro, prioridade realtime, encerrar processos do sistema ou comandos de shell. Sugira apenas: preservar ou ajustar plano de energia com cautela térmica, revisar manualmente aplicativos, usar Modo Jogo existente, verificar refrigeração ou coletar mais dados. Não invente aplicativos em execução. Nada será executado automaticamente.",
            prompt = "Sugira ações conservadoras para este retrato do computador: " + JsonSerializer.Serialize(snapshot),
            options = new { temperature = .2, num_predict = 450, num_ctx = 2048, num_thread = 2 }
        };
        using var response = await client.PostAsync("api/generate", new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), token);
        response.EnsureSuccessStatusCode(); using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        string text = json.RootElement.GetProperty("response").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("O modelo não retornou recomendações.");
        return text.Length > 6000 ? text[..6000] : text;
    }
}
