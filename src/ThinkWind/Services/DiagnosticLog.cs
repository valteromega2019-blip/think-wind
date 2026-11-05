using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OrbyLauncher.Services;

internal sealed record DiagnosticEntry(DateTimeOffset Utc, string Version, string Area,
    string Type, string Message, string Stack, bool Fatal);
internal sealed record DiagnosticSnapshot(DiagnosticEntry[] Entries, string Notice);

internal static class DiagnosticLog
{
    private const int FileLimit = 512 * 1024;
    private static readonly object gate = new();
    private static readonly Dictionary<string, DateTime> repeated = new();
    private static readonly Lazy<string> directory = new(() => Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "Diagnostics"));
    private static string lastFailure = "";
    private static readonly Lazy<bool> mayPersist = new(() =>
    {
        // Elevated helpers must not append to files controlled by a standard
        // user (hard links/reparse points could redirect a privileged write).
        using var identity = WindowsIdentity.GetCurrent();
        return !new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    });

    internal static void Record(string area, Exception error, bool fatal = false)
    {
        if (!Monitor.TryEnter(gate, 20)) return;
        try
        {
            if (!mayPersist.Value) return;
            var messages = new StringBuilder(); var stacks = new StringBuilder();
            Exception? current = error;
            for (int depth = 0; current is not null && depth < 3; depth++, current = current.InnerException)
            {
                messages.AppendLine(current.GetType().FullName + ": " + DiagnosticSanitizer.Limit(current.Message, 1500));
                stacks.AppendLine(DiagnosticSanitizer.Limit(current.StackTrace, 3500));
            }
            var entry = new DiagnosticEntry(DateTimeOffset.UtcNow, DiagnosticSanitizer.Redact(AppBrand.Version, 80),
                DiagnosticSanitizer.Redact(area, 80), DiagnosticSanitizer.Redact(error.GetType().FullName, 160),
                DiagnosticSanitizer.Redact(messages.ToString(), 4000), DiagnosticSanitizer.Redact(stacks.ToString(), 8000), fatal);
            string fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                entry.Area + entry.Type + entry.Message + entry.Stack)));
            if (repeated.TryGetValue(fingerprint, out var previous) && DateTime.UtcNow - previous < TimeSpan.FromMinutes(1)) return;
            EnsureSafeDirectory();
            byte[] line = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
            if (line.Length > FileLimit) { lastFailure = "Um evento excedeu o limite e foi omitido."; return; }
            // Keep the budget even if an old/corrupted file predates this code.
            for (int index = 0; index < 3; index++)
                if (File.Exists(LogPath(index)) && new FileInfo(LogPath(index)).Length > FileLimit)
                    File.Delete(LogPath(index));
            string path = LogPath(0);
            if (File.Exists(path) && new FileInfo(path).Length + line.Length > FileLimit)
            {
                if (File.Exists(LogPath(2))) File.Delete(LogPath(2));
                if (File.Exists(LogPath(1))) File.Move(LogPath(1), LogPath(2));
                File.Move(path, LogPath(1));
            }
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
                stream.Write(line);
            if (repeated.Count >= 64) repeated.Remove(repeated.Keys.First());
            repeated[fingerprint] = DateTime.UtcNow;
            lastFailure = "";
        }
        catch (Exception) { lastFailure = "Não foi possível gravar parte dos registros locais."; }
        finally { Monitor.Exit(gate); }
    }

    internal static DiagnosticSnapshot Snapshot()
    {
        lock (gate)
        {
            var entries = new List<DiagnosticEntry>(); string notice = lastFailure;
            try
            {
                if (!mayPersist.Value) return new(Array.Empty<DiagnosticEntry>(), "O histórico é mantido pela interface sem elevação.");
                EnsureSafeDirectory();
                for (int index = 0; index < 3; index++)
                {
                    string path = LogPath(index); if (!File.Exists(path)) continue;
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    if (stream.Length > FileLimit) { notice = "Um registro excedeu o limite e foi omitido."; continue; }
                    byte[] bytes = new byte[(int)stream.Length];
                    int read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
                    foreach (string line in Encoding.UTF8.GetString(bytes, 0, read).Split('\n'))
                    {
                        if (line.Length == 0) continue;
                        if (line.Length > 128000) { notice = "Um evento inválido foi omitido."; continue; }
                        try
                        {
                            var entry = JsonSerializer.Deserialize<DiagnosticEntry>(line);
                            if (entry is not null) entries.Add(entry with
                            {
                                Version = DiagnosticSanitizer.Redact(entry.Version, 80), Area = DiagnosticSanitizer.Redact(entry.Area, 80),
                                Type = DiagnosticSanitizer.Redact(entry.Type, 160), Message = DiagnosticSanitizer.Redact(entry.Message, 4000),
                                Stack = DiagnosticSanitizer.Redact(entry.Stack, 8000)
                            });
                        }
                        catch (JsonException) { notice = "Um evento incompleto ou inválido foi omitido."; }
                    }
                }
            }
            catch (Exception) { notice = "Não foi possível ler todos os registros locais."; }
            return new(entries.OrderByDescending(entry => entry.Utc).Take(40).ToArray(), notice);
        }
    }

    private static string LogPath(int index) => Path.Combine(directory.Value, index == 0 ? "diagnostics.jsonl" : $"diagnostics.{index}.jsonl");
    private static void EnsureSafeDirectory()
    {
        Directory.CreateDirectory(directory.Value);
        if ((File.GetAttributes(directory.Value) & FileAttributes.ReparsePoint) != 0) throw new IOException("Diretório não permitido.");
        for (int index = 0; index < 3; index++)
            if (File.Exists(LogPath(index)) && (File.GetAttributes(LogPath(index)) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Registro não permitido.");
    }
}
