using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Threading;

namespace OrbyLauncher.Services;

internal sealed class ClockAlarm
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Alarme";
    public int Minute { get; set; } = 420;
    public bool Enabled { get; set; } = true;
    public List<DayOfWeek> Days { get; set; } = new();
    public string Sound { get; set; } = "Asterisk";
    public DateTime? LastDate { get; set; }
    public bool IsAgenda { get; set; }
    public DateTime? EventDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Notes { get; set; } = "";
    public int ReminderMinutes { get; set; }
    public string ColorHex { get; set; } = "";
}

// One scheduler per app, independent of the visibility/lifetime of clock panels.
// Wall-clock scheduling intentionally does not wake a suspended computer.
internal sealed class AlarmService : IDisposable
{
    private readonly string path;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<ClockAlarm> alarms = new();
    private readonly AgendaBackup backup = new();
    private bool disposed;
    private readonly bool backupEnabled;
    internal string BackupFolder => backup.Folder;
    internal string BackupStatus => backup.Status;
    private readonly Dictionary<string, DateTime> snoozes = new();
    private DateTime previous = DateTime.Now.AddSeconds(-1);
    internal IReadOnlyList<ClockAlarm> Items => alarms;
    internal event Action? Changed;
    internal event Action<ClockAlarm>? Ring;
    internal string? Error { get; private set; }

    internal AlarmService(string file)
    {
        path = file;
        backupEnabled = string.Equals(Path.GetFullPath(file), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OrbyLauncher", "alarms.json"), StringComparison.OrdinalIgnoreCase);
        try
        {
            if (File.Exists(path)) alarms.AddRange((JsonSerializer.Deserialize<List<ClockAlarm>>(File.ReadAllText(path)) ?? new())
                .Where(a => a is not null && a.Minute is >= 0 and < 1440 && Guid.TryParseExact(a.Id, "N", out _))
                .DistinctBy(a => a.Id).Take(100));
            foreach (var alarm in alarms)
            {
                alarm.Days = (alarm.Days ?? new()).Where(d => (int)d is >= 0 and <= 6).Distinct().ToList();
                alarm.Name = string.IsNullOrWhiteSpace(alarm.Name) ? "Alarme" : alarm.Name[..Math.Min(80, alarm.Name.Length)];
                alarm.Sound = NormalizeSound(alarm.Sound);
                alarm.Notes = (alarm.Notes ?? "")[..Math.Min(4000, (alarm.Notes ?? "").Length)];
                alarm.ReminderMinutes = Math.Clamp(alarm.ReminderMinutes, 0, 10080);
                alarm.ColorHex = NormalizeAgendaColor(alarm.ColorHex);
                if (alarm.IsAgenda && alarm.EventDate is null) alarm.Enabled = false;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { Error = "Não foi possível ler os alarmes. O arquivo original foi preservado."; }
        backup.Changed += BackupChanged;
        if (backupEnabled && Error is null && File.Exists(path)) backup.Queue(alarms.ToArray());
        timer.Tick += (_, _) => Check(); UpdateTimer();
    }
    private void BackupChanged()
    {
        if (disposed || timer.Dispatcher.HasShutdownStarted) return;
        timer.Dispatcher.BeginInvoke(new Action(() => { if (!disposed) Changed?.Invoke(); }));
    }

    internal static string NormalizeSound(string? sound) => sound is "Beep" or "Exclamation" or "Silent" ? sound : "Asterisk";
    internal static string NormalizeAgendaColor(string? color) => color is { Length: 7 } && color[0] == '#'
        && color.Skip(1).All(Uri.IsHexDigit) ? color.ToUpperInvariant() : "";
    internal void Put(ClockAlarm value)
    {
        value.ColorHex = NormalizeAgendaColor(value.ColorHex);
        if (value.Minute is < 0 or >= 1440) return;
        var index = alarms.FindIndex(a => a.Id == value.Id);
        if (index < 0) { if (alarms.Count >= 100) return; alarms.Add(value); } else alarms[index] = value;
        snoozes.Remove(value.Id); Save(); UpdateTimer(); Changed?.Invoke();
    }
    internal void Remove(string id) { alarms.RemoveAll(a => a.Id == id); snoozes.Remove(id); Save(); UpdateTimer(); Changed?.Invoke(); }
    internal void Toggle(ClockAlarm alarm, bool enabled) { alarm.Enabled = enabled; snoozes.Remove(alarm.Id); Save(); UpdateTimer(); Changed?.Invoke(); }
    internal void Snooze(ClockAlarm alarm)
    { if (alarms.Any(a => a.Id == alarm.Id)) { snoozes[alarm.Id] = DateTime.Now.AddMinutes(5); UpdateTimer(); } }
    private void UpdateTimer()
    { if (alarms.Any(a => a.Enabled) || snoozes.Count > 0) { if (!timer.IsEnabled) previous = DateTime.Now.AddSeconds(-1); timer.Start(); } else timer.Stop(); }
    private void Check()
    {
        var now = DateTime.Now;
        // At most one day of missed occurrences after resume. Never fire twice
        // for the same local date, including a repeated DST hour.
        var from = previous < now.AddDays(-1) ? now.AddDays(-1) : previous;
        previous = now;
        foreach (var alarm in alarms.ToArray())
        {
            if (snoozes.TryGetValue(alarm.Id, out var delayed) && now >= delayed)
            { snoozes.Remove(alarm.Id); Ring?.Invoke(alarm); }
            if (!alarm.Enabled) continue;
            if (alarm.IsAgenda)
            {
                if (alarm.EventDate is not DateTime start) continue;
                var due = start.AddMinutes(-alarm.ReminderMinutes);
                if (due > from && due <= now && alarm.LastDate is null)
                {
                    alarm.LastDate = now; alarm.Enabled = false;
                    Save(); Changed?.Invoke(); Ring?.Invoke(alarm);
                }
                continue;
            }
            for (var day = from.Date; day <= now.Date; day = day.AddDays(1))
            {
                var due = day.AddMinutes(alarm.Minute);
                if (due <= from || due > now || alarm.LastDate?.Date >= day
                    || (alarm.Days.Count > 0 && !alarm.Days.Contains(day.DayOfWeek))) continue;
                alarm.LastDate = day;
                if (alarm.Days.Count == 0) alarm.Enabled = false;
                Save(); Changed?.Invoke(); Ring?.Invoke(alarm);
                break;
            }
        }
        UpdateTimer();
    }
    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(alarms, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(path + ".tmp", path, true); Error = null;
            if (backupEnabled) backup.Queue(alarms.ToArray());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Error = "Não foi possível salvar os alarmes: " + e.Message; }
    }
    public void Dispose() { disposed = true; backup.Changed -= BackupChanged; timer.Stop(); snoozes.Clear(); backup.Flush(); }
    internal void ExportBackup() { backup.Queue(alarms.ToArray()); if (!backup.Flush()) throw new IOException(backup.Status); }
    internal void RestoreAgenda(ClockAlarm[] imported)
    {
        var before = alarms.ToArray();
        var retained = before.Where(a => !a.IsAgenda).ToArray();
        if (retained.Length + imported.Length > 100 || retained.Any(a => imported.Any(b => b.Id == a.Id)))
            throw new InvalidDataException("A agenda não cabe no limite de 100 itens com os alarmes atuais, ou contém identificadores em conflito.");
        if (File.Exists(path)) File.Copy(path, path + ".before-restore", true);
        alarms.Clear(); alarms.AddRange(retained); alarms.AddRange(imported);
        Save();
        if (Error is not null) { alarms.Clear(); alarms.AddRange(before); throw new IOException(Error); }
        foreach (var item in before.Where(a => a.IsAgenda)) snoozes.Remove(item.Id);
        previous = DateTime.Now; UpdateTimer(); Changed?.Invoke();
    }
}
