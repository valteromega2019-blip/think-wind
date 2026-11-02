using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class AlarmPanel : StackPanel
{
    private readonly AlarmService service;
    private readonly StackPanel list = new(), editor = new();
    private readonly TextBox time = new() { Text = "07:00", MaxLength = 5 }, name = new() { Text = "Alarme", MaxLength = 80 };
    private readonly ComboBox sound = new() { ItemsSource = new[] { "Padrão", "Aviso", "Bipe", "Sem som" }, SelectedIndex = 0 };
    private readonly CheckBox[] days = new CheckBox[7];
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private string? editing;
    private int page;
    internal event Action? LayoutChanged;
    internal AlarmPanel(AlarmService source)
    {
        service = source;
        Children.Add(MakeButton("+ Adicionar alarme", () => Edit(null)));
        Children.Add(list);
        editor.Visibility = Visibility.Collapsed;
        editor.Children.Add(new TextBlock { Text = "Nome" }); editor.Children.Add(name);
        editor.Children.Add(new TextBlock { Text = "Horário · HH:mm" }); editor.Children.Add(time);
        var week = new WrapPanel();
        string[] labels = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
        for (int i = 0; i < 7; i++) { days[i] = new CheckBox { Content = labels[i], Margin = new Thickness(3, 6, 3, 6) }; week.Children.Add(days[i]); }
        editor.Children.Add(week); editor.Children.Add(new TextBlock { Text = "Sem dias selecionados: tocar uma vez na próxima ocorrência.", TextWrapping = TextWrapping.Wrap });
        editor.Children.Add(sound);
        var buttons = new WrapPanel();
        buttons.Children.Add(MakeButton("Salvar", Save));
        buttons.Children.Add(MakeButton("Cancelar", () => { editor.Visibility = Visibility.Collapsed; LayoutChanged?.Invoke(); })); editor.Children.Add(buttons);
        Children.Add(editor); Children.Add(status);
        foreach (var field in new Control[] { time, name, sound }) { field.Margin = new Thickness(0, 4, 0, 8); field.Padding = new Thickness(8); }
        Children.Add(new TextBlock { Text = "O Orby precisa estar aberto. O alarme não liga nem desperta o PC; após suspensão, ocorrências das últimas 24 h são avisadas ao retornar.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 8, 0, 0) });
        Loaded += (_, _) => { service.Changed += Render; Render(); };
        Unloaded += (_, _) => service.Changed -= Render;
    }
    private static Button MakeButton(string text, Action action)
    { var b = new Button { Content = text, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(2, 4, 2, 4) }; b.Click += (_, _) => action(); return b; }
    private void Render()
    {
        list.Children.Clear(); var items = service.Items.OrderBy(a => a.Minute).ToArray();
        page = Math.Clamp(page, 0, Math.Max(0, (items.Length - 1) / 3));
        foreach (var alarm in items.Skip(page * 3).Take(3))
        {
            var card = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };
            var enabled = new CheckBox { Content = new TextBlock { Text = $"{alarm.Minute / 60:00}:{alarm.Minute % 60:00} · {alarm.Name}", TextWrapping = TextWrapping.Wrap }, IsChecked = alarm.Enabled };
            enabled.Click += (_, _) => service.Toggle(alarm, enabled.IsChecked == true); card.Children.Add(enabled);
            string[] labels = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
            card.Children.Add(new TextBlock { Text = alarm.Days.Count == 0 ? "Uma vez" : string.Join(" · ", alarm.Days.OrderBy(d => d).Select(d => labels[(int)d])), FontSize = 11 });
            var actions = new WrapPanel(); actions.Children.Add(MakeButton("Editar", () => Edit(alarm))); actions.Children.Add(MakeButton("Excluir", () => service.Remove(alarm.Id))); card.Children.Add(actions);
            var border = new Border { Child = card, CornerRadius = new CornerRadius(14), Padding = new Thickness(10) };
            border.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard"); list.Children.Add(border);
        }
        if (items.Length == 0) list.Children.Add(new TextBlock { Text = "Nenhum alarme. Adicione o primeiro horário." });
        if (items.Length > 3)
        {
            var pages = new WrapPanel();
            var back = MakeButton("‹", () => { page--; Render(); }); back.IsEnabled = page > 0; pages.Children.Add(back);
            pages.Children.Add(new TextBlock { Text = $"{page + 1} / {(items.Length + 2) / 3}", VerticalAlignment = VerticalAlignment.Center });
            var next = MakeButton("›", () => { page++; Render(); }); next.IsEnabled = (page + 1) * 3 < items.Length; pages.Children.Add(next); list.Children.Add(pages);
        }
        status.Text = service.Error ?? ""; LayoutChanged?.Invoke();
    }
    private void Edit(ClockAlarm? alarm)
    {
        editing = alarm?.Id; name.Text = alarm?.Name ?? "Alarme";
        time.Text = alarm is null ? "07:00" : $"{alarm.Minute / 60:00}:{alarm.Minute % 60:00}";
        for (int i = 0; i < 7; i++) days[i].IsChecked = alarm?.Days.Contains((DayOfWeek)i) == true;
        sound.SelectedIndex = alarm?.Sound switch { "Exclamation" => 1, "Beep" => 2, "Silent" => 3, _ => 0 };
        editor.Visibility = Visibility.Visible; LayoutChanged?.Invoke();
    }
    private void Save()
    {
        if (!TimeSpan.TryParseExact(time.Text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var value))
        { status.Text = "Informe um horário de 00:00 a 23:59."; return; }
        var original = service.Items.FirstOrDefault(a => a.Id == editing);
        service.Put(new ClockAlarm { Id = editing ?? Guid.NewGuid().ToString("N"), Name = string.IsNullOrWhiteSpace(name.Text) ? "Alarme" : name.Text.Trim(),
            Enabled = original?.Enabled ?? true, LastDate = original?.Minute == (int)value.TotalMinutes ? original.LastDate : null,
            Minute = (int)value.TotalMinutes, Days = Enumerable.Range(0, 7).Where(i => days[i].IsChecked == true).Select(i => (DayOfWeek)i).ToList(),
            Sound = sound.SelectedIndex switch { 1 => "Exclamation", 2 => "Beep", 3 => "Silent", _ => "Asterisk" } });
        editor.Visibility = Visibility.Collapsed; Render();
    }
}
