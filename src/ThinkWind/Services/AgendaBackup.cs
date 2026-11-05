using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OrbyLauncher.Services;

internal sealed class AgendaBackup
{
    private readonly object gate = new();
    private string? pending;
    private string? lastQueued;
    private Task? worker;
    internal string Folder { get; }
    internal bool InOneDrive { get; }
    internal string Status { get; private set; } = "Backup ainda não atualizado.";
    internal event Action? Changed;
    internal AgendaBackup()
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var roots = new[] { Environment.GetEnvironmentVariable("OneDrive"), Environment.GetEnvironmentVariable("OneDriveConsumer"), Environment.GetEnvironmentVariable("OneDriveCommercial") }
            .Where(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)).Select(p => Path.GetFullPath(p!)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        string? root = roots.FirstOrDefault(p => documents.StartsWith(p.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) ?? roots.FirstOrDefault();
        InOneDrive = root is not null;
        string parent = root is null ? documents : documents.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? documents : root;
        if (string.IsNullOrWhiteSpace(parent)) parent = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Folder = Path.Combine(parent, "Orby", "Backup da agenda");
    }
    internal void Queue(ClockAlarm[] entries)
    {
        // Immutable snapshot: never enumerate mutable UI-owned models on the worker.
        string json = JsonSerializer.Serialize(entries.Where(a => a.IsAgenda), new JsonSerializerOptions { WriteIndented = true });
        lock (gate)
        {
            if (lastQueued == json) return;
            lastQueued = json;
            pending = json;
            if (worker is null || worker.IsCompleted) worker = Task.Run(WritePending);
        }
    }
    private void WritePending()
    {
        while (true)
        {
            string? json;
            lock (gate)
            {
                json = pending; pending = null;
                if (json is null) { worker = null; return; }
            }
            try
            {
                var items = (JsonSerializer.Deserialize<ClockAlarm[]>(json) ?? Array.Empty<ClockAlarm>()).OrderBy(a => a.EventDate).ToArray();
                Directory.CreateDirectory(Folder);
                string data = Path.Combine(Folder, "agenda.json");
                if (File.Exists(data) && File.ReadAllText(data) != json) File.Copy(data, Path.Combine(Folder, "agenda-anterior.json"), true);
                Write("agenda.json", json); Write("agenda.html", Html(items)); Write("agenda.ics", Calendar(items));
                Status = "Backup atualizado: " + Folder + (InOneDrive ? " · pasta do OneDrive (sincronização depende do cliente)" : " · cópia local, sem OneDrive detectado");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                lock (gate) lastQueued = null;
                Status = "Não foi possível atualizar o backup. Os dados locais da agenda foram mantidos. Pasta: " + Folder;
            }
            Changed?.Invoke();
        }
    }
    private void Write(string name, string text)
    {
        string file = Path.Combine(Folder, name);
        File.WriteAllText(file + ".tmp", text, new UTF8Encoding(false));
        File.Move(file + ".tmp", file, true);
    }
    private static string Html(ClockAlarm[] items)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Agenda · Orby</title><style>body{font:16px system-ui;max-width:900px;margin:32px auto;padding:0 20px;color:#20252b;background:#f4f6f8}article{background:white;border-left:5px solid;padding:18px;margin:14px 0;border-radius:12px}h2{margin:0 0 8px}p{white-space:pre-wrap;overflow-wrap:anywhere}small{color:#4b5563}</style><h1>Agenda · Orby</h1><p>Backup de leitura. Atualizado em ");
        html.Append(WebUtility.HtmlEncode(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))).Append(". Edite os itens no Orby.</p>");
        foreach (var item in items)
        {
            string color = AlarmService.NormalizeAgendaColor(item.ColorHex);
            html.Append("<article style=\"border-color:").Append(color.Length > 0 ? color : "#438c79").Append("\"><h2>").Append(WebUtility.HtmlEncode(item.Name)).Append("</h2><small>");
            html.Append(WebUtility.HtmlEncode(item.EventDate?.ToString(item.EndDate is null ? "dd/MM/yyyy · Nota" : "dd/MM/yyyy HH:mm")));
            if (item.EndDate.HasValue) html.Append(" – ").Append(item.EndDate.Value.ToString("HH:mm"));
            html.Append("</small><p>").Append(WebUtility.HtmlEncode(item.Notes)).Append("</p>");
            if (item.Enabled) html.Append("<small>Lembrete: ").Append(item.ReminderMinutes).Append(" minutos antes</small>");
            html.Append("</article>");
        }
        if (items.Length == 0) html.Append("<p>Nenhum evento ou nota salvo.</p>");
        return html.Append("</html>").ToString();
    }
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
    private static string Calendar(ClockAlarm[] items)
    {
        var text = new StringBuilder();
        void Line(string line)
        {
            // RFC 5545: fold at <=75 UTF-8 octets without splitting a Unicode rune.
            int bytes = 0;
            foreach (var rune in line.EnumerateRunes())
            {
                int count = rune.Utf8SequenceLength;
                if (bytes + count > 74) { text.Append("\r\n "); bytes = 1; }
                text.Append(rune.ToString()); bytes += count;
            }
            text.Append("\r\n");
        }
        Line("BEGIN:VCALENDAR"); Line("VERSION:2.0"); Line("PRODID:-//Orby//Agenda//PT-BR"); Line("CALSCALE:GREGORIAN");
        foreach (var item in items.Where(a => a.EventDate.HasValue))
        {
            var start = item.EventDate!.Value;
            Line("BEGIN:VEVENT"); Line("UID:" + item.Id + "@orby.local"); Line("DTSTAMP:" + DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'"));
            if (item.EndDate is DateTime end)
            {
                Line("DTSTART:" + DateTime.SpecifyKind(start, DateTimeKind.Local).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"));
                Line("DTEND:" + DateTime.SpecifyKind(end, DateTimeKind.Local).ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'"));
            }
            else { Line("DTSTART;VALUE=DATE:" + start.ToString("yyyyMMdd")); Line("DTEND;VALUE=DATE:" + start.AddDays(1).ToString("yyyyMMdd")); }
            Line("SUMMARY:" + Escape(item.Name)); Line("DESCRIPTION:" + Escape(item.Notes));
            if (item.Enabled && item.EndDate.HasValue)
            {
                Line("BEGIN:VALARM"); Line("ACTION:DISPLAY"); Line("DESCRIPTION:" + Escape(item.Name)); Line("TRIGGER:-PT" + item.ReminderMinutes + "M"); Line("END:VALARM");
            }
            Line("END:VEVENT");
        }
        Line("END:VCALENDAR"); return text.ToString();
    }
    internal void Flush()
    {
        Task? task; lock (gate) task = worker;
        try { task?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException) { Status = "O backup não terminou. Os dados originais da agenda foram mantidos."; }
    }
}
