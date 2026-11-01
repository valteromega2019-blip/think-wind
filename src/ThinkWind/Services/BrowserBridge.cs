using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OrbyLauncher.Services;

internal static class BrowserBridge
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "BrowserBridge");
    private const string HostName = "com.think.orby_tabs";
    private static readonly string[] RegistryPaths = { @"Software\Google\Chrome", @"Software\Microsoft\Edge", @"Software\BraveSoftware\Brave-Browser", @"Software\Chromium", @"Software\Vivaldi", @"Software\Opera Software\Opera Stable" };
    internal static string ExtensionFolder => Path.Combine(AppContext.BaseDirectory, "BrowserExtension");
    internal static void Register(string extensionId)
    {
        if (!Regex.IsMatch(extensionId, "^[a-p]{32}$")) throw new InvalidOperationException("Cole o ID de 32 letras mostrado na extensão.");
        string executable = Path.Combine(AppContext.BaseDirectory, "OrbyBrowserHost.exe");
        if (!File.Exists(executable)) throw new IOException("O conector do navegador não está nesta compilação.");
        Directory.CreateDirectory(Folder);
        var origins = new List<string>();
        string manifest = Path.Combine(Folder, "host.json");
        if (File.Exists(manifest))
        {
            using var previous = JsonDocument.Parse(File.ReadAllText(manifest));
            origins.AddRange(previous.RootElement.GetProperty("allowed_origins").EnumerateArray().Select(origin => origin.GetString()!).Where(origin => Regex.IsMatch(origin, "^chrome-extension://[a-p]{32}/$")));
        }
        origins.Add("chrome-extension://" + extensionId + "/");
        File.WriteAllText(manifest, JsonSerializer.Serialize(new { name = HostName, description = "Orby · Prévias locais de abas", path = executable, type = "stdio", allowed_origins = origins.Distinct().TakeLast(8).ToArray() }));
        foreach (string browser in RegistryPaths)
        {
            using var key = Registry.CurrentUser.CreateSubKey(browser + @"\NativeMessagingHosts\" + HostName);
            key.SetValue("", manifest);
        }
    }
    internal static void Disconnect()
    {
        foreach (string browser in RegistryPaths) Registry.CurrentUser.DeleteSubKeyTree(browser + @"\NativeMessagingHosts\" + HostName, false);
        ClearSnapshots();
        string manifest = Path.Combine(Folder, "host.json"); if (File.Exists(manifest)) File.Delete(manifest);
    }
    internal static void ClearSnapshots()
    {
        string cache = Path.Combine(Folder, "Snapshots");
        if (!Directory.Exists(cache) || (File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0) return;
        // Delete only our bounded snapshot files; never recurse through user paths.
        foreach (string file in Directory.EnumerateFiles(cache, "*.json")) File.Delete(file);
    }
    internal static IReadOnlyList<BrowserTab> ApplyImages(IReadOnlyList<BrowserTab> tabs, IReadOnlyDictionary<nint, string> windowTitles)
    {
        string cache = Path.Combine(Folder, "Snapshots");
        if (!Directory.Exists(cache) || (File.GetAttributes(cache) & FileAttributes.ReparsePoint) != 0) return tabs;
        var snapshots = new List<JsonDocument>();
        try
        {
            foreach (var file in new DirectoryInfo(cache).GetFiles("*.json").OrderByDescending(file => file.LastWriteTimeUtc).Take(32))
            {
                if (DateTime.UtcNow - file.LastWriteTimeUtc > TimeSpan.FromMinutes(15)) { try { file.Delete(); } catch (IOException) { } continue; }
                if (file.Length > 800000 || (file.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                try { snapshots.Add(JsonDocument.Parse(File.ReadAllBytes(file.FullName), new JsonDocumentOptions { MaxDepth = 12 })); }
                catch (Exception e) when (e is IOException or JsonException) { }
            }
            return tabs.Select(tab =>
            {
                if (!windowTitles.TryGetValue(tab.Window, out var windowTitle)) return tab;
                var matching = snapshots.Where(document =>
                {
                    string active = document.RootElement.GetProperty("activeTitle").GetString() ?? "";
                    bool Matches(string title) => title == active || title.StartsWith(active + " - ", StringComparison.Ordinal);
                    return active.Length > 0 && Matches(windowTitle) && windowTitles.Values.Count(Matches) == 1;
                }).ToArray();
                if (matching.Length != 1) return tab;
                var images = matching[0].RootElement.GetProperty("tabs").EnumerateArray()
                    .Where(item => item.GetProperty("title").GetString() == tab.Title && item.GetProperty("image").ValueKind == JsonValueKind.String).ToArray();
                if (images.Length != 1) return tab;
                long capturedAt = images[0].GetProperty("capturedAt").GetInt64();
                long age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - capturedAt;
                if (capturedAt <= 0 || age > 900000 || age < -5000) return tab;
                string? image = images[0].GetProperty("image").GetString();
                return image is not null && image.Length <= 140000 && image.StartsWith("data:image/jpeg;base64,", StringComparison.Ordinal)
                    ? tab with { PreviewImage = image, CapturedAt = capturedAt } : tab;
            }).ToArray();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return tabs; }
        finally { foreach (var snapshot in snapshots) snapshot.Dispose(); }
    }
}
