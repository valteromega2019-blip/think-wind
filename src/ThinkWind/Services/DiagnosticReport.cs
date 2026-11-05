using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace OrbyLauncher.Services;

internal sealed record DiagnosticIssue(Uri Url, bool Summarized);

internal static class DiagnosticReport
{
    private const string IssueBase = "https://github.com/valteromega2019-blip/think-wind/issues/new";
    private const int UrlLimit = 1800;

    internal static string Build(string description, DiagnosticSnapshot snapshot)
    {
        var report = new StringBuilder();
        report.AppendLine("# Relato do Orby Launcher");
        report.AppendLine("Versão: " + AppBrand.Version);
        report.AppendLine("Windows: " + RuntimeInformation.OSDescription);
        report.AppendLine("Arquitetura do aplicativo: " + RuntimeInformation.ProcessArchitecture);
        report.AppendLine("Runtime: " + RuntimeInformation.FrameworkDescription);
        report.AppendLine("Preparado em UTC: " + DateTimeOffset.UtcNow.ToString("O"));
        report.AppendLine("\n## O que aconteceu\n" + DiagnosticSanitizer.Redact(description, 4000));
        report.AppendLine("\n## Registros recentes (até 8 eventos)");
        if (snapshot.Entries.Length == 0) report.AppendLine("Nenhum erro registrado localmente. A descrição acima é o relato do usuário.");
        foreach (var entry in snapshot.Entries.Take(8))
        {
            report.AppendLine($"\n{entry.Utc:O} · {entry.Version} · {entry.Area} · {(entry.Fatal ? "não tratado" : "recuperável")}");
            report.AppendLine(entry.Type + "\n" + entry.Message);
            report.AppendLine(DiagnosticSanitizer.Limit(entry.Stack, 1800));
        }
        if (snapshot.Notice.Length > 0) report.AppendLine("\nLeitura local: " + snapshot.Notice);
        report.AppendLine("\nRelatório revisado pelo usuário. Não inclui arquivos pessoais, inventário de processos ou configurações completas.");
        return DiagnosticSanitizer.Redact(report.ToString(), 24000);
    }

    internal static DiagnosticIssue CreateIssue(string title, string report)
    {
        string safeTitle = DiagnosticSanitizer.Redact(title, 100).Replace('\r', ' ').Replace('\n', ' ');
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Relato do Orby Launcher";
        string safeReport = DiagnosticSanitizer.Redact(report, 24000);
        string prefix = IssueBase + "?title=" + Uri.EscapeDataString(safeTitle) + "&body=";
        string complete = prefix + Uri.EscapeDataString(safeReport);
        if (complete.Length <= UrlLimit) return new(new Uri(complete), false);
        const string suffix = "\n\n[Resumo limitado pelo tamanho do link. Se necessário, cole o relatório completo ou anexe o TXT usando o próprio GitHub.]";
        int length = Math.Min(safeReport.Length, 1200);
        while (length > 0)
        {
            // Avoid splitting a UTF-16 surrogate pair when shortening the summary.
            if (char.IsHighSurrogate(safeReport[length - 1])) length--;
            string candidate = prefix + Uri.EscapeDataString(safeReport[..length] + suffix);
            if (candidate.Length <= UrlLimit) return new(new Uri(candidate), true);
            length--;
        }
        return new(new Uri(prefix + Uri.EscapeDataString(suffix)), true);
    }
}
