using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class AgendaPanel : Grid, IDisposable
{
    private readonly AlarmService service;
    private readonly Calendar calendar = new();
    private readonly StackPanel body = new();
    private readonly TextBlock status = ClockDesign.Text("", 11, true);
    private DateTime selected = DateTime.Today;
    private int page;
    private bool editing;
    internal event Action? LayoutChanged;
    internal AgendaPanel(AlarmService alarms)
    {
        service = alarms;
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        ColumnDefinitions.Add(new ColumnDefinition());
        calendar.SetResourceReference(StyleProperty, "Think.Calendar");
        calendar.SelectedDate = selected; calendar.VerticalAlignment = VerticalAlignment.Top;
        var month = new StackPanel { Margin = new Thickness(0, 0, 12, 0) };
        month.Children.Add(calendar);
        month.Children.Add(ClockDesign.Button("\uE787", "Hoje", () => { calendar.DisplayDate = DateTime.Today; calendar.SelectedDate = DateTime.Today; }));
        month.Children.Add(ClockDesign.Text("Escolha um dia para organizar eventos e notas. Os lembretes usam os alarmes do Orby.", 12, true));
        Children.Add(month);
        var right = ClockDesign.Card(body); SetColumn(right, 1); Children.Add(right);
        calendar.SelectedDatesChanged += (_, _) => { selected = calendar.SelectedDate ?? DateTime.Today; page = 0; Render(); };
        service.Changed += Changed;
        Render();
    }
    private void Changed() { if (IsVisible && !editing) Render(); }
    private void Render()
    {
        editing = false;
        body.Children.Clear();
        var title = ClockDesign.Text(selected.ToString("dddd, dd MMMM"), 16); title.FontWeight = FontWeights.SemiBold;
        body.Children.Add(title);
        var actions = new WrapPanel();
        actions.Children.Add(ClockDesign.Button("\uE710", "Evento", () => Edit(null, false)));
        actions.Children.Add(ClockDesign.Button("\uE70F", "Nota", () => Edit(null, true))); body.Children.Add(actions);
        var entries = service.Items.Where(a => a.IsAgenda && a.EventDate?.Date == selected.Date).OrderBy(a => a.EventDate).ToArray();
        page = Math.Clamp(page, 0, Math.Max(0, (entries.Length - 1) / 3));
        foreach (var entry in entries.Skip(page * 3).Take(3))
        {
            var card = new StackPanel();
            var label = ClockDesign.Text(entry.Name, 14); label.FontWeight = FontWeights.SemiBold; card.Children.Add(label);
            card.Children.Add(ClockDesign.Text(entry.EndDate is null ? "Nota do dia" : $"{entry.EventDate:HH:mm} � {entry.EndDate:HH:mm}", 12, true));
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
            card.Children.Add(buttons); var surface = ClockDesign.Card(card, 8); surface.Margin = new Thickness(0, 6, 0, 0); body.Children.Add(surface);
        }
        if (entries.Length == 0) body.Children.Add(ClockDesign.Text("Dia livre. Adicione um evento ou escreva uma nota.", 13, true));
        if (entries.Length > 3)
        {
            var navigation = new WrapPanel();
            var back = ClockDesign.Button("\uE76B", "Anterior", () => { page--; Render(); }, true); back.IsEnabled = page > 0; navigation.Children.Add(back);
            navigation.Children.Add(ClockDesign.Text($"{page + 1} / {(entries.Length + 2) / 3}", 12));
            var next = ClockDesign.Button("\uE76C", "Pr�xima", () => { page++; Render(); }, true); next.IsEnabled = (page + 1) * 3 < entries.Length; navigation.Children.Add(next); body.Children.Add(navigation);
        }
        status.Text = service.Error ?? "Salvo neste PC � lembretes enquanto o Orby estiver aberto";
        body.Children.Add(status); LayoutChanged?.Invoke();
    }
    private void Edit(ClockAlarm? original, bool note)
    {
        editing = true;
        body.Children.Clear();
        body.Children.Add(ClockDesign.Text(note ? "Nota do dia" : "Evento da agenda", 16));
        var title = new TextBox { Text = original?.Name ?? "", MaxLength = 80 };
        body.Children.Add(ClockDesign.Text("T�tulo", 12)); body.Children.Add(title);
        var date = new DatePicker { SelectedDate = original?.EventDate?.Date ?? selected };
        body.Children.Add(ClockDesign.Text("Data", 12)); body.Children.Add(date);
        var start = new AlarmTimePicker { MinuteOfDay = original is null ? 9 * 60 : original.Minute };
        var end = new AlarmTimePicker { MinuteOfDay = original?.EndDate is DateTime endDate ? endDate.Hour * 60 + endDate.Minute : 10 * 60 };
        if (!note)
        {
            body.Children.Add(ClockDesign.Text("In�cio", 12)); body.Children.Add(start);
            body.Children.Add(ClockDesign.Text("Fim (mesmo dia)", 12)); body.Children.Add(end);
        }
        var notes = new TextBox { Text = original?.Notes ?? "", MaxLength = 4000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 80, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(ClockDesign.Text("Notas", 12)); body.Children.Add(notes);
        int[] offsets = { -1, 0, 5, 10, 15, 30, 60, 1440 };
        var reminder = new ComboBox { ItemsSource = new[] { "Sem lembrete", "Na hora", "5 min antes", "10 min antes", "15 min antes", "30 min antes", "1 hora antes", "1 dia antes" }, SelectedIndex = original?.Enabled == true ? Math.Max(1, Array.IndexOf(offsets, original.ReminderMinutes)) : 0 };
        var sound = new ComboBox { ItemsSource = new[] { "Padr�o", "Aviso", "Bipe", "Sem som" }, SelectedIndex = original?.Sound switch { "Exclamation" => 1, "Beep" => 2, "Silent" => 3, _ => 0 } };
        if (!note) { body.Children.Add(reminder); body.Children.Add(sound); }
        var message = ClockDesign.Text("", 12); body.Children.Add(message);
        var actions = new WrapPanel();
        actions.Children.Add(ClockDesign.Button("\uE711", "Cancelar", Render));
        actions.Children.Add(ClockDesign.Button("\uE74E", "Salvar", () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || date.SelectedDate is not DateTime day) { message.Text = "Informe um t�tulo e uma data."; return; }
            int begin = 0, finish = 0;
            if (!note && (!start.TryGetMinute(out begin) || !end.TryGetMinute(out finish) || finish <= begin)) { message.Text = "O hor�rio final deve ser depois do in�cio."; return; }
            var when = day.Date.AddMinutes(begin);
            int offset = note ? -1 : offsets[Math.Max(0, reminder.SelectedIndex)];
            if (offset >= 0 && when.AddMinutes(-offset) <= DateTime.Now) { message.Text = "Escolha um lembrete futuro ou Sem lembrete."; return; }
            if (original is null && service.Items.Count >= 100) { message.Text = "Limite de 100 alarmes e itens da agenda atingido."; return; }
            service.Put(new ClockAlarm { Id = original?.Id ?? Guid.NewGuid().ToString("N"), IsAgenda = true, Name = title.Text.Trim(), Notes = notes.Text, Minute = begin, EventDate = when, EndDate = note ? null : day.Date.AddMinutes(finish), Enabled = offset >= 0, ReminderMinutes = Math.Max(0, offset), Sound = sound.SelectedIndex switch { 1 => "Exclamation", 2 => "Beep", 3 => "Silent", _ => "Asterisk" } });
            if (service.Error is not null) { message.Text = service.Error; return; }
            selected = day.Date; calendar.SelectedDate = selected; calendar.DisplayDate = selected; Render();
        }));
        body.Children.Add(actions); LayoutChanged?.Invoke();
    }
    public void Dispose() => service.Changed -= Changed;
    internal void Refresh() { if (!editing) Render(); }
}
