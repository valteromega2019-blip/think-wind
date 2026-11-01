using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrbyLauncher.Services;

internal static class UpdateChecks
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(respond(request)); }
    }
    internal static int Run()
    {
        int failed = 0, passed = 0;
        void Check(string name, Action run) { try { run(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); } }
        void Require(bool ok) { if (!ok) throw new Exception("Condição não atendida."); }
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("example")));
        string Release(string tag, bool draft = false, string? digest = null) => JsonSerializer.Serialize(new[] { new { tag_name = tag, draft, prerelease = true, body = "Novidades", assets = new[] { new { name = "OrbyLauncher-Setup-" + tag.TrimStart('v') + ".exe", browser_download_url = "https://github.com/" + GithubUpdates.Repository + "/releases/download/" + tag + "/setup.exe", size = 7, digest = digest ?? "sha256:" + hash } } } });
        GithubUpdates Api(string json) => new(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(json) }));
        Check("SemVer numérico, beta e metadados", () =>
        {
            Require(ReleaseVersion.Parse("v0.100.0")!.CompareTo(ReleaseVersion.Parse("0.99.9")) > 0);
            Require(ReleaseVersion.Parse("1.0.0-beta.10")!.CompareTo(ReleaseVersion.Parse("1.0.0-beta.2")) > 0);
            Require(ReleaseVersion.Parse("1.0.0")!.CompareTo(ReleaseVersion.Parse("1.0.0-beta")) > 0);
            Require(ReleaseVersion.Parse("1.0.0+build")!.CompareTo(ReleaseVersion.Parse("1.0.0")) == 0);
            Require(ReleaseVersion.Parse("01.0.0") is null && ReleaseVersion.Parse("1.0.0-beta.01") is null);
            Require(ReleaseVersion.Parse("1.0.0+..") is null);
            Require(ReleaseVersion.Parse("1.0.0--1")!.CompareTo(ReleaseVersion.Parse("1.0.0-1")) > 0);
        });
        Check("Consulta versões e ausência de atualização", () =>
        {
            using var latest = Api(Release("v99.0.0-beta.1")); Require(latest.CheckAsync(default).GetAwaiter().GetResult()?.Tag == "v99.0.0-beta.1");
            using var same = Api(Release("v" + AppBrand.Version)); Require(same.CheckAsync(default).GetAwaiter().GetResult() is null);
            using var draft = Api(Release("v99.0.0-beta.1", true)); Require(draft.CheckAsync(default).GetAwaiter().GetResult() is null);
            using var missing = Api(Release("v99.0.0-beta.1", digest: "")); Require(missing.CheckAsync(default).GetAwaiter().GetResult() is null);
        });
        Check("Sem autorização não existe requisição de download", () =>
        {
            var handler = new Handler(_ => throw new Exception("Requisição inesperada")); using var api = new GithubUpdates(handler);
            var update = new ReleaseUpdate("v99.0.0", ReleaseVersion.Parse("99.0.0")!, "", new Uri("https://github.com/" + GithubUpdates.Repository + "/releases/download/v99/setup.exe"), 7, hash);
            try { api.DownloadAsync(update, false, null, default).GetAwaiter().GetResult(); throw new Exception("Não bloqueou"); } catch (InvalidOperationException) { }
            Require(handler.Calls == 0);
        });
        Check("Download autorizado, integridade e adulteração", () =>
        {
            using var api = new GithubUpdates(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent("example") }));
            var update = new ReleaseUpdate("v99.0.0", ReleaseVersion.Parse("99.0.0")!, "", new Uri("https://github.com/" + GithubUpdates.Repository + "/releases/download/v99/setup.exe"), 7, hash);
            string file = api.DownloadAsync(update, true, null, default).GetAwaiter().GetResult();
            try
            {
                Require(File.ReadAllText(file) == "example");
                File.WriteAllText(file, "altered");
                try { GithubUpdates.VerifyHashAsync(file, hash, default).GetAwaiter().GetResult(); throw new Exception("Aceitou alteração"); } catch (InvalidDataException) { }
                try { api.DownloadAsync(update with { Size = 50 }, true, null, default).GetAwaiter().GetResult(); throw new Exception("Aceitou interrupção"); } catch (InvalidDataException) { }
                using var cancel = new CancellationTokenSource(); cancel.Cancel();
                try { api.DownloadAsync(update, true, null, cancel.Token).GetAwaiter().GetResult(); throw new Exception("Ignorou cancelamento"); } catch (OperationCanceledException) { }
            }
            finally { File.Delete(file); }
        });
        Check("Origem de instalação restrita", () =>
        {
            Require(!GithubUpdates.TrustedDownload(new Uri("http://github.com/" + GithubUpdates.Repository + "/releases/download/x/a.exe")));
            Require(!GithubUpdates.TrustedDownload(new Uri("https://evil.example/setup.exe")));
            Require(!GithubUpdates.TrustedDownload(new Uri("https://github.com/other/project/releases/download/x/a.exe")));
        });
        Check("Falhas de rede e resposta inválida são recuperáveis", () =>
        {
            string state = Path.Combine(Path.GetTempPath(), "OrbyUpdateChecks", Guid.NewGuid().ToString("N"), "state.json");
            using var unauthorized = new UpdateController(() => false, state, new GithubUpdates(new Handler(_ => new(HttpStatusCode.Forbidden))));
            unauthorized.CheckAsync().GetAwaiter().GetResult(); Require(!unauthorized.Busy && unauthorized.Available is null && unauthorized.Status.StartsWith("Não foi possível"));
            using var malformed = new UpdateController(() => false, state, Api("{broken"));
            malformed.CheckAsync().GetAwaiter().GetResult(); Require(!malformed.Busy && malformed.Status.StartsWith("Não foi possível"));
        });
        Check("Aviso após 10 minutos e adiado em jogos/fullscreen", () =>
        {
            Require(!UpdateController.CanNotify(599999, false, "v2.0.0", ""));
            Require(!UpdateController.CanNotify(600000, true, "v2.0.0", ""));
            Require(UpdateController.CanNotify(600000, false, "v2.0.0", ""));
            Require(!UpdateController.CanNotify(999999, false, "v2.0.0", "2.0.0"));
            Require(UpdateController.CanNotify(999999, false, "v2.0.1", "2.0.0"));
        });
        Check("Aviso persiste entre inicializações sem alterar preferências", () =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "OrbyUpdateChecks", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string preferences = Path.Combine(directory, "settings.json"), state = Path.Combine(directory, "notification.json");
            var settings = new SettingsStore(preferences); settings.Value.TintOpacity = .47; settings.Value.Shortcuts.Add(new("Meu app", "app.exe")); settings.Save();
            byte[] before = File.ReadAllBytes(preferences);
            using (var first = new UpdateController(() => false, state, Api(Release("v99.0.0-beta.1"))))
            {
                first.CheckAsync().GetAwaiter().GetResult();
                Require(first.ShouldNotify(600000, false, "v99.0.0-beta.1"));
                Require(first.MarkNotified());
            }
            using var controller = new UpdateController(() => false, state);
            Require(!controller.ShouldNotify(600000, false, "v99.0.0-beta.1"));
            Require(Convert.ToHexString(before) == Convert.ToHexString(File.ReadAllBytes(preferences)));
            Require(new SettingsStore(preferences).Value.TintOpacity == .47);
        });
        Check("Instalador não assinado é recusado antes da execução", () =>
        {
            string file = Path.GetTempFileName(); File.WriteAllText(file, "not an installer");
            try
            {
                string digest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
                try { GithubUpdates.AuthenticateAsync(file, digest, default).GetAwaiter().GetResult(); throw new Exception("Aceitou arquivo não assinado"); } catch (InvalidDataException) { }
            }
            finally { File.Delete(file); }
        });
        Check("Consulta real à API oficial (sem download)", () =>
        {
            using var api = new GithubUpdates();
            var available = api.CheckAsync(default).GetAwaiter().GetResult();
            Console.WriteLine("GitHub: " + (available?.Tag ?? "sem versão elegível mais nova"));
        });
        Console.WriteLine($"Atualizador: {passed} aprovados, {failed} falhas. Nenhum instalador executado."); return failed == 0 ? 0 : 1;
    }
}
