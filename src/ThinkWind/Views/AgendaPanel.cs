using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class AgendaPanel : Grid, IDisposable
{
    private readonly AlarmService service;
    private readonly Calendar calendar = new();
    private readonly StackPanel body = new();
    private readonly TextBlock status = ClockDesign.Text("", 11, true);
    private DateTime selected = DateTime.Today;
    private int page;
    private bool editing;
    private readonly bool compact;
    internal DateTime SelectedDay => selected;
    internal event Action<DateTime>? AddRequested;
    internal event Action<DateTime>? DayRequested;
    internal event Action? LayoutChanged;
    internal AgendaPanel(AlarmService alarms, bool calendarOnly = false)
    {
        service = alarms; compact = calendarOnly;
        ColumnDefinitions.Add(new ColumnDefinition { Width = compact ? new GridLength(300) : new GridLength(1, GridUnitType.Star) });
        VerticalAlignment = VerticalAlignment.Top;
        calendar.SetResourceReference(StyleProperty, "Think.Calendar");
        ConfigureCalendarDays();
        calendar.SelectedDate = selected; calendar.VerticalAlignment = VerticalAlignment.Top;
        var month = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        month.Children.Add(calendar);
        if (!compact) month.Children.Add(ClockDesign.Text("Dois cliques no dia para adicionar. Um clique para consultar a agenda.", 12, true));
        if (compact) Children.Add(month);
        else Children.Add(ClockDesign.Card(body, 12));
        calendar.SelectedDatesChanged += (_, _) => { selected = calendar.SelectedDate ?? DateTime.Today; page = 0; Render(); };
        service.Changed += Changed;
        Render();
    }
    private void Changed() { if (IsVisible && !editing) Render(); }
    private void Render()
    {
        if (compact) { QueueDayMarkers(); return; }
        editing = false;
        body.Children.Clear();
        var title = ClockDesign.Text(selected.ToString("dddd, dd MMMM"), 16); title.FontWeight = FontWeights.SemiBold;
        body.Children.Add(title);
        var entries = service.Items.Where(a => a.IsAgenda && a.EventDate?.Date == selected.Date).OrderBy(a => a.EventDate).ToArray();
        page = Math.Clamp(page, 0, Math.Max(0, (entries.Length - 1) / 3));
        foreach (var entry in entries.Skip(page * 3).Take(3))
        {
            var card = new StackPanel();
            var label = ClockDesign.Text(entry.Name, 14); label.FontWeight = FontWeights.SemiBold; card.Children.Add(label);
            card.Children.Add(ClockDesign.Text(entry.EndDate is null ? "Nota do dia" : $"{entry.EventDate:HH:mm} · {entry.EndDate:HH:mm}", 12, true));
            if (entry.Notes.Length > 0)
            {
                var excerpt = ClockDesign.Text(entry.Notes, 12);
                excerpt.MaxHeight = 48; excerpt.TextTrimming = TextTrimming.CharacterEllipsis;
                card.Children.Add(excerpt);
            }
            if (entry.Enabled) card.Children.Add(ClockDesign.Text($"Lembrete: {entry.ReminderMinutes} min antes", 11, true));
            var buttons = new WrapPanel();
            buttons.Children.Add(ClockDesign.Button("\uE70F", "Editar", () => Edit(entry, entry.EndDate is null), true));
            buttons.Children.Add(ClockDesign.Button("\uE74D", "Excluir", () => { service.Remove(entry.Id); Render(); }, true));
            card.Children.Add(buttons); var surface = ClockDesign.Card(card, 10);
            surface.BorderThickness = new Thickness(3, 0, 0, 0);
            surface.BorderBrush = AgendaBrush(entry.ColorHex);
            surface.Margin = new Thickness(0, 8, 0, 0); body.Children.Add(surface);
        }
        if (entries.Length == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(4, 24, 4, 24) };
            var icon = ClockDesign.Text("\uE787", 30); icon.FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons");
            icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); empty.Children.Add(icon);
            empty.Children.Add(ClockDesign.Text("Seu dia, do seu jeito.", 16));
            empty.Children.Add(ClockDesign.Text("Dê dois cliques em um dia para criar um evento ou uma nota.", 12, true));
            body.Children.Add(empty);
        }
        if (entries.Length > 3)
        {
            var navigation = new WrapPanel();
            var back = ClockDesign.Button("\uE76B", "Anterior", () => { page--; Render(); }, true); back.IsEnabled = page > 0; navigation.Children.Add(back);
            navigation.Children.Add(ClockDesign.Text($"{page + 1} / {(entries.Length + 2) / 3}", 12));
            var next = ClockDesign.Button("\uE76C", "Próxima", () => { page++; Render(); }, true); next.IsEnabled = (page + 1) * 3 < entries.Length; navigation.Children.Add(next); body.Children.Add(navigation);
        }
        status.Text = service.Error ?? "Salvo neste PC · lembretes enquanto o Orby estiver aberto";
        status.Margin = new Thickness(0, 12, 0, 0);
        body.Children.Add(status);
        var backupInfo = ClockDesign.Text(service.BackupStatus, 11, true);
        backupInfo.Margin = new Thickness(0, 8, 0, 0); body.Children.Add(backupInfo);
        body.Children.Add(ClockDesign.Button("\uE8B7", "Abrir pasta do backup", () =>
        {
            try { System.IO.Directory.CreateDirectory(service.BackupFolder); SystemActions.Run(service.BackupFolder); }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
            { status.Text = "Não foi possível abrir a pasta do backup."; }
        }));
        QueueDayMarkers(); LayoutChanged?.Invoke();
    }
    private void Edit(ClockAlarm? original, bool note)
    {
        editing = true;
        body.Children.Clear();
        var type = TypeSwitch(note);
        body.Children.Add(FormLabel("\uE8A5", "Tipo de registro")); body.Children.Add(type);
        var title = new TextBox { Text = original?.Name ?? "", MaxLength = 80 };
        StyleAgendaInput(title);
        body.Children.Add(FormLabel("\uE8D2", "Título do evento ou da nota")); body.Children.Add(title);
        var date = original?.EventDate?.Date ?? selected;
        body.Children.Add(ClockDesign.Text(date.ToString("dddd, dd/MM/yyyy"), 12, true));
        var now = DateTime.Now;
        int minimum = date.Date == now.Date ? now.Hour * 60 + now.Minute : 0;
        int initial = original is null ? (date.Date == now.Date ? minimum : 9 * 60) : Math.Max(minimum, original.Minute);
        var start = new AlarmTimePicker { ClampSteps = true, MinimumMinute = minimum, MinuteOfDay = initial };
        var end = new AlarmTimePicker { ClampSteps = true };
        void UpdateEnd()
        {
            if (!start.TryGetMinute(out int begin)) return;
            int nextMinimum = begin == 1439 ? 0 : begin + 1;
            bool valid = end.TryGetMinute(out int previousEnd);
            end.MinimumMinute = nextMinimum;
            if (!valid || previousEnd <= begin) end.MinuteOfDay = begin == 1439 ? 0 : Math.Min(1439, begin + 60);
        }
        end.MinuteOfDay = original?.EndDate is DateTime endDate ? endDate.Hour * 60 + endDate.Minute : initial == 1439 ? 0 : Math.Min(1439, initial + 60);
        start.Changed += UpdateEnd; UpdateEnd();
        var times = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        times.Children.Add(FormLabel("\uE823", "Início")); times.Children.Add(start);
        times.Children.Add(FormLabel("\uE823", "Fim")); times.Children.Add(end);
        body.Children.Add(times);
        var notes = new TextBox { Text = original?.Notes ?? "", MaxLength = 4000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        StyleAgendaInput(notes);
        body.Children.Add(FormLabel("\uE70F", "Descrição / anotações")); body.Children.Add(notes);
        var colorField = AddColorPicker(original?.ColorHex ?? "");
        int[] offsets = { -1, 0, 5, 10, 15, 30, 60, 1440 };
        var reminder = new ComboBox { ItemsSource = new[] { "Sem lembrete", "Na hora", "5 min antes", "10 min antes", "15 min antes", "30 min antes", "1 hora antes", "1 dia antes" }, SelectedIndex = original?.Enabled == true ? Math.Max(1, Array.IndexOf(offsets, original.ReminderMinutes)) : 0 };
        var sound = new ComboBox { ItemsSource = new[] { "Padrão", "Aviso", "Bipe", "Sem som" }, SelectedIndex = original?.Sound switch { "Exclamation" => 1, "Beep" => 2, "Silent" => 3, _ => 0 } };
        var alerts = new StackPanel();
        alerts.Children.Add(FormLabel("\uE823", "Lembrete")); alerts.Children.Add(reminder);
        alerts.Children.Add(FormLabel("\uE767", "Som do lembrete")); alerts.Children.Add(sound);
        body.Children.Add(alerts);
        void UpdateType(bool notify = true)
        {
            note = type.IsChecked == true;
            times.Visibility = alerts.Visibility = note ? Visibility.Collapsed : Visibility.Visible;
            if (notify) LayoutChanged?.Invoke();
        }
        type.Checked += (_, _) => UpdateType(); type.Unchecked += (_, _) => UpdateType(); UpdateType(false);
        var message = ClockDesign.Text("", 12); body.Children.Add(message);
        var actions = new WrapPanel();
        actions.Children.Add(ClockDesign.Button("\uE711", "Cancelar", Render));
        actions.Children.Add(ClockDesign.Button("\uE74E", "Salvar", () =>
        {
            var day = date;
            if (string.IsNullOrWhiteSpace(title.Text)) { message.Text = "Informe um título."; return; }
            string color = colorField.Text.Trim();
            if (color.Length > 0 && AlarmService.NormalizeAgendaColor(color).Length == 0)
            { message.Text = "Use uma cor HEX no formato #RRGGBB ou deixe vazio para usar o tema."; return; }
            int begin = 0, finish = 0;
            if (!note && (!start.TryGetMinute(out begin) || !end.TryGetMinute(out finish) || (finish <= begin && begin != 1439))) { message.Text = "O horário final deve ser depois do início."; return; }
            var when = day.Date.AddMinutes(begin);
            int offset = note ? -1 : offsets[Math.Max(0, reminder.SelectedIndex)];
            if (offset >= 0 && when.AddMinutes(-offset) <= DateTime.Now) { message.Text = "Escolha um lembrete futuro ou Sem lembrete."; return; }
            if (!service.Items.Any(a => a.Id == original?.Id) && service.Items.Count >= 100) { message.Text = "Limite de 100 alarmes e itens da agenda atingido."; return; }
            service.Put(new ClockAlarm { Id = original?.Id ?? Guid.NewGuid().ToString("N"), IsAgenda = true, ColorHex = color, Name = title.Text.Trim(), Notes = notes.Text, Minute = begin, EventDate = when, EndDate = note ? null : day.Date.AddMinutes(finish + (begin == 1439 && finish <= begin ? 1440 : 0)), Enabled = offset >= 0, ReminderMinutes = Math.Max(0, offset), Sound = sound.SelectedIndex switch { 1 => "Exclamation", 2 => "Beep", 3 => "Silent", _ => "Asterisk" } });
            if (service.Error is not null) { message.Text = service.Error; return; }
            selected = day.Date; calendar.SelectedDate = selected; calendar.DisplayDate = selected; Render();
        }));
        body.Children.Add(actions); LayoutChanged?.Invoke();
    }
    public void Dispose() => service.Changed -= Changed;
    internal void Refresh() { if (!editing) Render(); }
    internal void GoToToday() => SelectDay(DateTime.Today, false);
    internal void SelectDay(DateTime day, bool add)
    {
        selected = day.Date; calendar.SelectedDate = selected; calendar.DisplayDate = selected;
        if (add) Edit(null, false); else Render();
    }
}
