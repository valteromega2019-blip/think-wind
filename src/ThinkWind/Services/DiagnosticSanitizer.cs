using System;
using System.Text.RegularExpressions;

namespace OrbyLauncher.Services;

internal static class DiagnosticSanitizer
{
    internal static string Limit(string? value, int maximum) => string.IsNullOrEmpty(value) ? ""
        : value.Length <= maximum ? value : value[..maximum] + "… [limitado]";

    internal static string Redact(string? value, int maximum = 12000)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        // Bound the input before running patterns, including text from exceptions.
        string text = Limit(value, 32000);
        try
        {
            text = Replace(text, @"\b(?:https?|file)://[^\s<>""']+", "[URL omitida]");
            text = Replace(text, @"\b[A-Z]:[\\/][^\r\n\t""<>|]*", "[caminho omitido]");
            text = Replace(text, @"\\\\[^\r\n\t""<>|]+", "[caminho de rede omitido]");
            text = Replace(text, @"%(?:USERPROFILE|LOCALAPPDATA|APPDATA)%[^\r\n\t""<>|]*", "[perfil omitido]");
            text = Replace(text, @"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[e-mail omitido]");
            text = Replace(text, @"\b(?:github_pat_[A-Z0-9_]+|gh[pousr]_[A-Z0-9_]+|eyJ[A-Z0-9_-]+\.[A-Z0-9_-]+\.[A-Z0-9_-]+)\b", "[credencial omitida]");
            text = Replace(text, @"[""']?\b(?:password|passwd|senha|(?:access[_ -]?|refresh[_ -]?|id[_ -]?|api[_ -]?|auth[_ -]?)?token|authorization|api[_ -]?key|(?:client[_ -]?)?secret|cookie)\b[""']?\s*[:=][^\r\n]*", "[credencial omitida]");
            foreach (string personal in new[] { Environment.UserName, Environment.MachineName,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
                if (!string.IsNullOrWhiteSpace(personal))
                    text = text.Replace(personal, "[identificação omitida]", StringComparison.OrdinalIgnoreCase);
            // Remove control characters that could disguise or corrupt the preview.
            text = Replace(text, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F\u202A-\u202E\u2066-\u2069]", "");
            return Limit(text, maximum);
        }
        catch (RegexMatchTimeoutException)
        {
            // Never fall back to the original, potentially sensitive input.
            return "[Conteúdo omitido: não foi possível sanitizar dentro do prazo.]";
        }
    }

    private static string Replace(string input, string pattern, string replacement) => Regex.Replace(input,
        pattern, replacement, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
