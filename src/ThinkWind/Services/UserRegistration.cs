using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;

// Optional identification, not authentication. No Windows/hardware identifier.
// Kept outside preferences and their automatic/cloud backup.
internal sealed class UserRegistration : IDisposable
{
    // Set only after the independent service has been provisioned and reviewed.
    internal const string ServiceUrl = "";
    private sealed record Profile(bool Offered = false, string Name = "", string Email = "",
        string Id = "", string Token = "", DateTime LastSeenUtc = default);
    private sealed record Receipt(string Id, string Token);
    private readonly string path;
    private Profile profile = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim network = new(1, 1);
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12) };
    internal bool Configured => Uri.TryCreate(ServiceUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.UserInfo.Length == 0;
    internal bool Registered => profile.Id.Length > 0 && profile.Token.Length > 0;
    internal bool ShouldOffer => Configured && !profile.Offered && !Registered;
    internal string Name => profile.Name;
    internal string Email => profile.Email;
    internal UserRegistration(string statePath)
    {
        path = statePath;
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 16384)
            {
                var saved = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path));
                if (saved is not null && saved.Name is { Length: <= 80 } && saved.Email is { Length: <= 254 }
                    && saved.Id is { Length: <= 64 } && saved.Token is { Length: <= 128 }
                    && ((saved.Id.Length == 0 && saved.Token.Length == 0) || (Guid.TryParse(saved.Id, out _) && ValidToken(saved.Token)))) profile = saved;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }
    }
    internal static bool ValidEmail(string email) => email.Length <= 254 && !email.Contains('\r') && !email.Contains('\n')
        && MailAddress.TryCreate(email, out var address) && address.Address.Equals(email, StringComparison.OrdinalIgnoreCase)
        && address.Host.Contains('.');
    private static bool ValidToken(string? token)
    {
        if (token is not { Length: >= 32 and <= 128 }) return false;
        foreach (char value in token) if (!char.IsAsciiLetterOrDigit(value) && value != '-') return false;
        return true;
    }
    internal void Skip()
    {
        var next = profile with { Offered = true };
        try { Save(next); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { profile = next; }
    }
    internal async Task RegisterAsync(string name, string email)
    {
        name = name.Trim(); email = email.Trim().ToLowerInvariant();
        if (name.Length is < 1 or > 80 || !ValidEmail(email)) throw new InvalidOperationException("Preencha seu nome e um e-mail válido.");
        if (!Configured) throw new InvalidOperationException("O cadastro online ainda não está disponível. Você pode continuar usando o Orby normalmente.");
        await network.WaitAsync(lifetime.Token);
        try
        {
            // Name stays on this PC. Server derives a keyed identifier from email;
            // it does not retain the email address or receive the user's name.
            using var response = await http.PostAsJsonAsync(ServiceUrl + "/v1/register", new { email, version = AppBrand.Version }, lifetime.Token);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 4096) throw new InvalidDataException("Resposta de cadastro inválida.");
            var receipt = await response.Content.ReadFromJsonAsync<Receipt>(cancellationToken: lifetime.Token);
            if (receipt is null || !Guid.TryParse(receipt.Id, out _) || !ValidToken(receipt.Token))
                throw new InvalidDataException("Resposta de cadastro inválida.");
            Save(new Profile(true, name, email, receipt.Id, receipt.Token, DateTime.UtcNow));
        }
        finally { network.Release(); }
    }
    internal async Task ReportActivityAsync()
    {
        bool entered = false;
        try
        {
            if (!Configured || !Registered || DateTime.UtcNow - profile.LastSeenUtc < TimeSpan.FromDays(1)) return;
            entered = await network.WaitAsync(0, lifetime.Token);
            if (!entered) return;
            using var request = new HttpRequestMessage(HttpMethod.Post, ServiceUrl + "/v1/activity");
            request.Headers.Authorization = new("Bearer", profile.Token);
            request.Content = JsonContent.Create(new { id = profile.Id, version = AppBrand.Version });
            using var response = await http.SendAsync(request, lifetime.Token);
            response.EnsureSuccessStatusCode();
            Save(profile with { LastSeenUtc = DateTime.UtcNow });
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException) { }
        finally { if (entered) network.Release(); }
    }
    internal async Task RemoveAsync()
    {
        if (!Registered) return;
        if (!Configured) throw new InvalidOperationException("O serviço de cadastro está indisponível.");
        await network.WaitAsync(lifetime.Token);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, ServiceUrl + "/v1/registration/" + Uri.EscapeDataString(profile.Id));
            request.Headers.Authorization = new("Bearer", profile.Token);
            using var response = await http.SendAsync(request, lifetime.Token);
            response.EnsureSuccessStatusCode();
            Save(new Profile(Offered: true));
        }
        finally { network.Release(); }
    }
    private void Save(Profile next)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(next)); File.Move(temporary, path, true);
        profile = next;
    }
    public void Dispose() { lifetime.Cancel(); http.Dispose(); }
}
