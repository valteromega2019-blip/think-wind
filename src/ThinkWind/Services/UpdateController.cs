using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OrbyLauncher.Services;
internal sealed class UpdateController : IDisposable
{
    private readonly GithubUpdates api;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Func<bool> blocked;
    private DateTime nextCheck = DateTime.MinValue;
    private readonly string statePath;
    private string notified = "";
    internal ReleaseUpdate? Available { get; private set; }
    internal string Status { get; private set; } = "Nenhuma verificação realizada.";
    internal UpdateProgress? Progress { get; private set; }
    internal bool Busy { get; private set; }
    internal event Action? Changed;
    internal event Action? Notify;
    internal event Action? InstallStarting;
    internal UpdateController(Func<bool> blocked, string? notificationPath = null, GithubUpdates? source = null)
    {
        api = source ?? new GithubUpdates();
        this.blocked = blocked;
        statePath = notificationPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "Updates", "notification.json");
        try { if (File.Exists(statePath)) notified = JsonSerializer.Deserialize<string>(File.ReadAllText(statePath)) ?? ""; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }
        string result = Path.Combine(Path.GetDirectoryName(statePath)!, "installation-result.json");
        try { if (File.Exists(result)) Status = "Última instalação: " + File.ReadAllText(result); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        timer.Tick += Tick;
    }
    internal void Start() => timer.Start();
    internal static bool CanNotify(long uptime, bool blocked, string tag, string notified) => uptime >= 600000 && !blocked && tag.TrimStart('v') != notified;
    private async void Tick(object? sender, EventArgs args)
    {
        if (blocked()) return;
        if (!Busy && DateTime.UtcNow >= nextCheck) await CheckAsync();
        if (Available is not null && ShouldNotify(Environment.TickCount64, blocked(), Available.Tag)) Notify?.Invoke();
    }
    internal bool ShouldNotify(long uptime, bool isBlocked, string tag) => CanNotify(uptime, isBlocked, tag, notified);
    internal bool MarkNotified()
    {
        if (Available is null) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            string value = Available.Tag.TrimStart('v');
            File.WriteAllText(statePath + ".tmp", JsonSerializer.Serialize(value));
            File.Move(statePath + ".tmp", statePath, true); notified = value; return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status = "Não foi possível salvar o aviso: " + e.Message; Changed?.Invoke(); return false; }
    }
    internal async Task CheckAsync()
    {
        if (Busy) return;
        Busy = true; Status = "Consultando GitHub…"; Changed?.Invoke();
        try
        {
            Available = await api.CheckAsync(lifetime.Token);
            Status = Available is null ? "Você está na versão mais recente compatível com seu canal." : "Atualização disponível: " + Available.Tag;
            nextCheck = DateTime.UtcNow.AddHours(6);
        }
        catch (Exception e) when (e is System.Net.Http.HttpRequestException or IOException or JsonException or OperationCanceledException or InvalidOperationException or System.Collections.Generic.KeyNotFoundException or FormatException or ArgumentException)
        { Status = "Não foi possível verificar: " + e.Message; nextCheck = DateTime.UtcNow.AddMinutes(15); }
        finally { Busy = false; Changed?.Invoke(); }
    }
    internal void Cancel() => operation?.Cancel();
    internal async Task DownloadAndInstallAsync(bool authorized, Action saveSettings)
    {
        if (!authorized || Busy || Available is null) return;
        Busy = true; Progress = null; var update = Available;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); operation = cancellation;
        string? path = null; bool installing = false;
        try
        {
            Status = "Baixando atualização autorizada…"; Changed?.Invoke();
            path = await api.DownloadAsync(update, authorized, new Progress<UpdateProgress>(p => { Progress = p; Changed?.Invoke(); }), cancellation.Token);
            Status = "Validando assinatura do publicador…"; Changed?.Invoke();
            using var validation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); validation.CancelAfter(TimeSpan.FromSeconds(90));
            await GithubUpdates.AuthenticateAsync(path, update.Sha256, validation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            saveSettings();
            if (blocked()) throw new InvalidOperationException("Instalação adiada: saia do jogo ou tela cheia e tente novamente.");
            using var helper = Process.Start(GithubUpdates.Helper(path, update.Sha256, true)) ?? throw new IOException("Não foi possível iniciar o auxiliar de instalação.");
            installing = true;
            Status = "Encerrando para instalar…"; InstallStarting?.Invoke();
        }
        catch (OperationCanceledException) { Status = "Operação cancelada ou tempo de espera excedido. Nenhuma instalação iniciada."; }
        catch (Exception e) when (e is IOException or System.Net.Http.HttpRequestException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { Status = e.Message; }
        finally
        {
            if (!installing && path is not null)
                try { File.Delete(path); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            operation = null; Busy = false; Changed?.Invoke();
        }
    }
    public void Dispose() { timer.Stop(); lifetime.Cancel(); operation?.Cancel(); api.Dispose(); lifetime.Dispose(); }
}
