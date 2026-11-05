using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OrbyLauncher.Services;

internal sealed class SettingsBackupDocument
{
    public string Format { get; set; } = "";
    public int Version { get; set; }
    public Preferences? Preferences { get; set; }
}
internal static class BackupReader
{
    internal static string Read(string file)
    {
        var info = new FileInfo(file);
        if (!info.Exists || info.Length > 10 * 1024 * 1024) throw new InvalidDataException("Arquivo ausente ou maior que 10 MB.");
        return File.ReadAllText(file);
    }
    internal static Preferences Settings(string file)
    {
        var backup = JsonSerializer.Deserialize<SettingsBackupDocument>(Read(file));
        if (backup is null || backup.Format != "Orby.Settings" || backup.Version != 1 || backup.Preferences is null)
            throw new InvalidDataException("Este arquivo não é um backup de configurações compatível do Orby.");
        return backup.Preferences;
    }
    internal static ClockAlarm[] Agenda(string file)
    {
        var items = JsonSerializer.Deserialize<ClockAlarm[]>(Read(file)) ?? throw new InvalidDataException("Agenda inválida.");
        if (items.Length > 100 || items.Any(a => a is null || !a.IsAgenda || !Guid.TryParseExact(a.Id, "N", out _) || a.EventDate is null
            || a.EventDate.Value.Year is < 1900 or > 9998
            || a.Minute is < 0 or >= 1440 || a.Name is null || a.Name.Length > 80 || a.Notes is null || a.Notes.Length > 4000
            || a.ReminderMinutes is < 0 or > 10080 || (a.EndDate.HasValue && a.EndDate <= a.EventDate)
            || (!string.IsNullOrEmpty(a.ColorHex) && AlarmService.NormalizeAgendaColor(a.ColorHex).Length == 0))
            || items.Select(a => a.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != items.Length)
            throw new InvalidDataException("O arquivo contém itens de agenda inválidos ou incompatíveis.");
        foreach (var item in items) item.Sound = AlarmService.NormalizeSound(item.Sound);
        return items;
    }
}
