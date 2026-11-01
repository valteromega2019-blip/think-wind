using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;
internal sealed record ReleaseUpdate(string Tag, ReleaseVersion Version, string Notes, Uri Download, long Size, string Sha256);
internal sealed record UpdateProgress(long Bytes, long Total, double BytesPerSecond);
internal sealed class GithubUpdates : IDisposable
{
    internal const string Repository = "valteromega2019-blip/think-wind";
    private readonly HttpClient http;
    internal GithubUpdates(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromMinutes(15);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OrbyLauncher/" + AppBrand.Version);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }
    internal async Task<ReleaseUpdate?> CheckAsync(CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(30));
        ReleaseVersion installed = ReleaseVersion.Parse(AppBrand.Version) ?? throw new InvalidDataException("Versão instalada inválida.");
        ReleaseUpdate? best = null;
        // The latest endpoint excludes beta releases. Enumerate published releases
        // and compare SemVer; stable installs never opt into pre-releases.
        for (int page = 1; page <= 10; page++)
        {
            using var response = await http.GetAsync($"https://api.github.com/repos/{Repository}/releases?per_page=100&page={page}", deadline.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token).ConfigureAwait(false));
            foreach (var release in json.RootElement.EnumerateArray())
            {
                if (release.GetProperty("draft").GetBoolean()) continue;
                string tag = release.GetProperty("tag_name").GetString() ?? "";
                var version = ReleaseVersion.Parse(tag);
                if (version is null || version.CompareTo(installed) <= 0 || (installed.Pre.Length == 0 && (version.Pre.Length > 0 || release.GetProperty("prerelease").GetBoolean()))) continue;
                if (best is not null && version.CompareTo(best.Version) <= 0) continue;
                var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == $"OrbyLauncher-Setup-{tag.TrimStart('v')}.exe").ToArray();
                if (assets.Length != 1) continue;
                var asset = assets[0];
                string digest = asset.TryGetProperty("digest", out var d) ? d.GetString() ?? "" : "";
                if (!Regex.IsMatch(digest, "^sha256:[a-fA-F0-9]{64}$")) continue;
                var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
                if (!TrustedDownload(url)) continue;
                long size = asset.GetProperty("size").GetInt64(); if (size <= 0 || size > 1024L * 1024 * 1024) continue;
                best = new(tag, version, release.GetProperty("body").GetString() ?? "Sem notas publicadas.", url, size, digest[7..]);
            }
            if (json.RootElement.GetArrayLength() < 100) return best;
        }
        throw new InvalidDataException("Limite de consulta atingido. Não foi possível determinar a versão mais recente com segurança.");
    }
    internal static bool TrustedDownload(Uri uri) => uri.Scheme == "https" && uri.Host == "github.com" && uri.AbsolutePath.StartsWith("/" + Repository + "/releases/download/", StringComparison.Ordinal) && uri.UserInfo.Length == 0;
    internal async Task<string> DownloadAsync(ReleaseUpdate update, bool authorized, IProgress<UpdateProgress>? progress, CancellationToken token)
    {
        if (!authorized) throw new InvalidOperationException("Download não autorizado.");
        if (!TrustedDownload(update.Download) || !Regex.IsMatch(update.Sha256, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("Origem inválida.");
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "setup.exe"), partial = path + ".partial";
        try
        {
            using var response = await http.GetAsync(update.Download, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri is { } final && (final.Scheme != "https" || (final.Host != "github.com" && final.Host != "release-assets.githubusercontent.com" && final.Host != "objects.githubusercontent.com")))
                throw new InvalidDataException("Redirecionamento do download não autorizado.");
            if (response.Content.Headers.ContentLength is long length && length != update.Size) throw new InvalidDataException("Tamanho divergente.");
            await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920]; long total = 0; var watch = Stopwatch.StartNew(); double reported = -1;
                while (true)
                {
                    using var idle = CancellationTokenSource.CreateLinkedTokenSource(token); idle.CancelAfter(TimeSpan.FromSeconds(90));
                    int read = await input.ReadAsync(buffer, idle.Token).ConfigureAwait(false); if (read == 0) break;
                    total += read; if (total > update.Size) throw new InvalidDataException("Download excedeu o tamanho esperado.");
                    await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    if (watch.Elapsed.TotalSeconds - reported >= .15) { progress?.Report(new(total, update.Size, total / Math.Max(.001, watch.Elapsed.TotalSeconds))); reported = watch.Elapsed.TotalSeconds; }
                }
                if (total != update.Size) throw new InvalidDataException("Download interrompido. Tente novamente.");
                progress?.Report(new(total, update.Size, total / Math.Max(.001, watch.Elapsed.TotalSeconds)));
            }
            await VerifyHashAsync(partial, update.Sha256, token).ConfigureAwait(false);
            File.Move(partial, path); return path;
        }
        catch { if (File.Exists(partial)) File.Delete(partial); throw; }
    }
    internal static async Task VerifyHashAsync(string path, string expected, CancellationToken token)
    {
        await using var file = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(file, token).ConfigureAwait(false);
        if (!CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(expected))) throw new InvalidDataException("SHA-256 divergente. Instalação bloqueada.");
    }
    internal static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    internal static ProcessStartInfo Helper(string path, string hash, bool install, string? readyFile = null)
    {
        string script = Path.Combine(AppContext.BaseDirectory, "UpdateInstaller.ps1");
        if (!File.Exists(script)) throw new IOException("O auxiliar de atualização não foi encontrado. Reinstale a versão oficial manualmente.");
        string command = $"& {Quote(script)} -Installer {Quote(path)} -ExpectedHash {Quote(hash)} -Launcher {Quote(Environment.ProcessPath!)} -WaitPid {(install ? Environment.ProcessId : 0)}";
        if (readyFile is not null) command += " -ReadyFile " + Quote(readyFile);
        return new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(command)))
        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = !install, RedirectStandardError = !install };
    }
    internal static async Task<Process> PrepareInstallAsync(string path, string hash, CancellationToken token)
    {
        string ready = Path.Combine(Path.GetDirectoryName(path)!, "ready-" + Guid.NewGuid().ToString("N") + ".txt");
        Process? process = null;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(90));
        try
        {
            process = Process.Start(Helper(path, hash, true, ready)) ?? throw new IOException("Não foi possível iniciar o auxiliar de instalação.");
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                if (process.HasExited) throw new InvalidDataException("O auxiliar de atualização encerrou antes de confirmar a instalação. O launcher continuará aberto. Consulte o registro em OrbyLauncher\\Updates.");
                if (File.Exists(ready) && File.ReadAllText(ready).Trim() == "ready") return process;
                await Task.Delay(100, deadline.Token).ConfigureAwait(false);
            }
        }
        catch
        {
            if (process is not null)
            { try { if (!process.HasExited) process.Kill(); } catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { } process.Dispose(); }
            throw;
        }
        finally { try { File.Delete(ready); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } }
    }
    internal static async Task AuthenticateAsync(string path, string hash, CancellationToken token)
    {
        using var process = Process.Start(Helper(path, hash, false)) ?? throw new IOException("Não foi possível validar a assinatura.");
        var output = process.StandardOutput.ReadToEndAsync(token); var error = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token).ConfigureAwait(false); }
        catch { try { process.Kill(); } catch (InvalidOperationException) { } throw; }
        string message = await output.ConfigureAwait(false); await error.ConfigureAwait(false);
        if (process.ExitCode != 0) throw new InvalidDataException(message.Length > 0 ? message.Trim() : "Publicador não autenticado. Instalação bloqueada.");
    }
    public void Dispose() => http.Dispose();
}
