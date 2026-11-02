using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class AlarmPanel : StackPanel
{
    private const int PageSize = 2;
    private readonly AlarmService service;
    private readonly StackPanel list = new(), editor = new();
    private readonly TextBox time = new() { Text = "07:00", MaxLength = 5 }, name = new() { Text = "Alarme", MaxLength = 80 };
    private readonly ComboBox sound = new() { ItemsSource = new[] { "Padrão", "Aviso", "Bipe", "Sem som" }, SelectedIndex = 0 };
    private readonly ToggleButton[] days = new ToggleButton[7];
    private readonly Dictionary<string, LiquidToggle> switches = new();
    private readonly TextBlock status = ClockDesign.Text("", 11, true);
    private readonly Button add;
    private string? editing, signature;
    private int page;
    internal event Action? LayoutChanged;
    internal AlarmPanel(AlarmService source)
    {
        service = source;
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        add = ClockDesign.Button("\uE710", "Novo alarme", () => Edit(null)); DockPanel.SetDock(add, Dock.Right); heading.Children.Add(add);
        var title = ClockDesign.Text("Seus alarmes", 14); title.FontWeight = FontWeights.SemiBold; title.VerticalAlignment = VerticalAlignment.Center; heading.Children.Add(title); Children.Add(heading);
        // Hidden pages still measure, so list/editor keep one common size.
        editor.Visibility = Visibility.Hidden;
        var body = new Grid { MinHeight = 260 }; body.Children.Add(list); body.Children.Add(editor); Children.Add(body);
        var fields = new Grid(); fields.ColumnDefinitions.Add(new ColumnDefinition()); fields.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
        var timeField = new StackPanel(); timeField.Children.Add(ClockDesign.Text("Horário", 11, true)); time.FontSize = 20; timeField.Children.Add(time); fields.Children.Add(timeField);
        var nameField = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; nameField.Children.Add(ClockDesign.Text("Nome", 11, true)); nameField.Children.Add(name); Grid.SetColumn(nameField, 1); fields.Children.Add(nameField); editor.Children.Add(fields);
        editor.Children.Add(ClockDesign.Text("Repetir nos dias", 11, true));
        var week = new UniformGrid { Columns = 7, Margin = new Thickness(0, 4, 0, 6) };
        string[] labels = { "D", "S", "T", "Q", "Q", "S", "S" };
        string[] titles = { "Domingo", "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado" };
        for (int i = 0; i < 7; i++)
        {
            var day = new ToggleButton { Content = labels[i], ToolTip = titles[i], Padding = new Thickness(0), MinHeight = 30, Margin = new Thickness(2), Template = DayTemplate() };
            day.SetResourceReference(Control.ForegroundProperty, "Think.Text"); day.SetResourceReference(Control.BackgroundProperty, "Think.Control");
            System.Windows.Automation.AutomationProperties.SetName(day, titles[i]); days[i] = day; week.Children.Add(day);
        }
        editor.Children.Add(week); editor.Children.Add(ClockDesign.Text("Nenhum dia marcado: tocar uma vez.", 10, true));
        var soundRow = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var soundIcon = ClockDesign.Text("\uE767", 16); soundIcon.FontFamily = new FontFamily("Segoe Fluent Icons"); soundIcon.Margin = new Thickness(0, 0, 8, 0); soundIcon.VerticalAlignment = VerticalAlignment.Center; soundRow.Children.Add(soundIcon); soundRow.Children.Add(sound); editor.Children.Add(soundRow);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 6, 0, 0) };
        actions.Children.Add(ClockDesign.Button("\uE711", "Cancelar", CloseEditor));
        var save = ClockDesign.Button("\uE74E", "Salvar", Save); save.SetResourceReference(Control.BackgroundProperty, "Think.AccentSurface"); save.SetResourceReference(Control.ForegroundProperty, "Think.Accent"); actions.Children.Add(save); editor.Children.Add(actions);
        foreach (var field in new Control[] { time, name, sound }) { field.Margin = new Thickness(0, 4, 0, 8); field.Padding = new Thickness(8); field.MinHeight = 34; }
        System.Windows.Automation.AutomationProperties.SetName(time, "Horário do alarme em HH:mm"); System.Windows.Automation.AutomationProperties.SetName(name, "Nome do alarme"); System.Windows.Automation.AutomationProperties.SetName(sound, "Som do alarme");
        status.MinHeight = 18; status.Margin = new Thickness(0, 4, 0, 0); Children.Add(status);
        var help = ClockDesign.Text("Funciona enquanto o Orby estiver aberto.", 10, true); help.ToolTip = "Não liga nem desperta o PC. Após suspensão, avisa as ocorrências das últimas 24 h durante as quais o Orby permaneceu aberto."; Children.Add(help);
        Loaded += (_, _) => { service.Changed += Render; Render(); }; Unloaded += (_, _) => service.Changed -= Render;
    }
    private static ControlTemplate DayTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border), "Day"); border.SetValue(Border.CornerRadiusProperty, new CornerRadius(15)); border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.BorderThicknessProperty, new Thickness(1)); border.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ContentPresenter)); content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); border.AppendChild(content);
        var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = border };
        var active = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true }; active.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Think.AccentSurface"), "Day")); active.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Think.Accent"))); template.Triggers.Add(active);
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Think.Hover"), "Day")); template.Triggers.Add(hover);
        var focus = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true }; focus.Setters.Add(new Setter(Border.BorderBrushProperty, new DynamicResourceExtension("Think.Accent"), "Day")); template.Triggers.Add(focus); return template;
    }
    private void Render()
    {
        var items = service.Items.OrderBy(a => a.Minute).ToArray(); page = Math.Clamp(page, 0, Math.Max(0, (items.Length - 1) / PageSize));
        string next = page + "|" + System.Text.Json.JsonSerializer.Serialize(items.Select(a => new { a.Id, a.Name, a.Minute, a.Days, a.Sound }));
        status.Text = service.Error ?? "";
        if (next == signature)
        {
            foreach (var alarm in items) if (switches.TryGetValue(alarm.Id, out var toggle)) toggle.IsChecked = alarm.Enabled;
            return; // Preserve the live spring instead of recreating the toggle.
        }
        signature = next; list.Children.Clear(); switches.Clear();
        foreach (var alarm in items.Skip(page * PageSize).Take(PageSize))
        {
            string id = alarm.Id;
            var card = new DockPanel();
            var side = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; DockPanel.SetDock(side, Dock.Right); card.Children.Add(side);
            var enabled = new LiquidToggle { IsChecked = alarm.Enabled, ToolTip = "Ativar ou desativar " + alarm.Name };
            System.Windows.Automation.AutomationProperties.SetName(enabled, "Ativar ou desativar " + alarm.Name);
            enabled.Click += (_, _) => { var current = service.Items.FirstOrDefault(a => a.Id == id); if (current is not null) service.Toggle(current, enabled.IsChecked == true); };
            switches[id] = enabled; side.Children.Add(enabled);
            var actions = new WrapPanel(); actions.Children.Add(ClockDesign.Button("\uE70F", "Editar alarme", () => Edit(service.Items.FirstOrDefault(a => a.Id == id)), true)); actions.Children.Add(ClockDesign.Button("\uE74D", "Excluir alarme", () => service.Remove(id), true)); side.Children.Add(actions);
            var info = new StackPanel(); var hour = ClockDesign.Text($"{alarm.Minute / 60:00}:{alarm.Minute % 60:00}", 26); hour.FontWeight = FontWeights.SemiBold; hour.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); info.Children.Add(hour);
            var label = ClockDesign.Text(alarm.Name, 12); label.TextWrapping = TextWrapping.NoWrap; label.TextTrimming = TextTrimming.CharacterEllipsis; label.ToolTip = alarm.Name; info.Children.Add(label);
            string[] labels = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" }; info.Children.Add(ClockDesign.Text(alarm.Days.Count == 0 ? "Uma vez" : string.Join(" · ", alarm.Days.OrderBy(d => d).Select(d => labels[(int)d])), 10, true)); card.Children.Add(info);
            var surface = ClockDesign.Card(card); surface.Margin = new Thickness(0, 0, 0, 6); list.Children.Add(surface);
        }
        if (items.Length == 0)
        {
            var empty = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 28, 8, 20) };
            var icon = ClockDesign.Text("\uE823", 34); icon.FontFamily = new FontFamily("Segoe Fluent Icons"); icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); icon.HorizontalAlignment = HorizontalAlignment.Center; empty.Children.Add(icon);
            var label = ClockDesign.Text("Seu próximo lembrete começa aqui.", 14); label.TextAlignment = TextAlignment.Center; label.Margin = new Thickness(0, 12, 0, 6); empty.Children.Add(label);
            var hint = ClockDesign.Text("Toque em Novo alarme para escolher um horário.", 12, true); hint.TextAlignment = TextAlignment.Center; empty.Children.Add(hint); list.Children.Add(empty);
        }
        if (items.Length > PageSize)
        {
            var pages = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var back = ClockDesign.Button("\uE76B", "Anterior", () => { page--; Render(); }, true); back.IsEnabled = page > 0; pages.Children.Add(back);
            var count = ClockDesign.Text($"{page + 1} / {(items.Length + PageSize - 1) / PageSize}", 11, true); count.VerticalAlignment = VerticalAlignment.Center; count.Margin = new Thickness(8, 0, 8, 0); pages.Children.Add(count);
            var forward = ClockDesign.Button("\uE76C", "Próxima", () => { page++; Render(); }, true); forward.IsEnabled = (page + 1) * PageSize < items.Length; pages.Children.Add(forward); list.Children.Add(pages);
        }
        LayoutChanged?.Invoke();
    }
    private void CloseEditor() { editor.Visibility = Visibility.Hidden; list.Visibility = Visibility.Visible; add.IsEnabled = true; status.Text = service.Error ?? ""; ClockDesign.Reveal(list); LayoutChanged?.Invoke(); }
    private void Edit(ClockAlarm? alarm)
    {
        editing = alarm?.Id; name.Text = alarm?.Name ?? "Alarme"; time.Text = alarm is null ? "07:00" : $"{alarm.Minute / 60:00}:{alarm.Minute % 60:00}";
        for (int i = 0; i < 7; i++) days[i].IsChecked = alarm?.Days.Contains((DayOfWeek)i) == true;
        sound.SelectedIndex = alarm?.Sound switch { "Exclamation" => 1, "Beep" => 2, "Silent" => 3, _ => 0 };
        list.Visibility = Visibility.Hidden; editor.Visibility = Visibility.Visible; add.IsEnabled = false; status.Text = ""; ClockDesign.Reveal(editor); LayoutChanged?.Invoke();
    }
    private void Save()
    {
        if (!TimeSpan.TryParseExact(time.Text, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture, out var value)) { status.Text = "Informe um horário de 00:00 a 23:59."; return; }
        var original = service.Items.FirstOrDefault(a => a.Id == editing);
        service.Put(new ClockAlarm { Id = editing ?? Guid.NewGuid().ToString("N"), Name = string.IsNullOrWhiteSpace(name.Text) ? "Alarme" : name.Text.Trim(), Enabled = original?.Enabled ?? true,
            LastDate = original?.Minute == (int)value.TotalMinutes ? original.LastDate : null, Minute = (int)value.TotalMinutes,
            Days = Enumerable.Range(0, 7).Where(i => days[i].IsChecked == true).Select(i => (DayOfWeek)i).ToList(), Sound = sound.SelectedIndex switch { 1 => "Exclamation", 2 => "Beep", 3 => "Silent", _ => "Asterisk" } });
        CloseEditor(); Render();
    }
}
