using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;
internal static class LocalOptimizerAi
{
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
