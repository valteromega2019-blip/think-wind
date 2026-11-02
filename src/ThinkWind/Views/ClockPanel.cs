using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class ClockPanel : StackPanel, IDisposable
{
    private readonly TextBlock clock = Text(32), date = Text(13), elapsed = Text(24), remaining = Text(24), notice = Text(12);
    private readonly Stopwatch stopwatch = new(), countdown = new();
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ProgressBar timeline = ClockDesign.Meter();
    private TimeSpan duration;
    private bool visible;
    private TextBox? minutesInput;
    private bool completed;
    private bool watchSelected = true;
    internal event Action? LayoutChanged;
    internal ClockPanel()
    {
        Margin = new Thickness(6);
        clock.FontWeight = FontWeights.SemiBold;
        date.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted");
        elapsed.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        remaining.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent");
        elapsed.HorizontalAlignment = remaining.HorizontalAlignment = HorizontalAlignment.Center;
        clock.HorizontalAlignment = date.HorizontalAlignment = HorizontalAlignment.Center;
        var clockContent = new StackPanel(); clockContent.Children.Add(clock); clockContent.Children.Add(date);
        Children.Add(Card(clockContent));
        var tabs = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 12, 0, 8) };
        var watchArea = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var timerArea = new StackPanel { Visibility = Visibility.Hidden, VerticalAlignment = VerticalAlignment.Center };
        var alarms = new AlarmPanel(((App)Application.Current).Alarms) { Visibility = Visibility.Hidden };
        alarms.LayoutChanged += () => LayoutChanged?.Invoke();
        var start = Button("Iniciar", () => { });
        start.Click += (_, _) => { if (stopwatch.IsRunning) stopwatch.Stop(); else stopwatch.Start(); start.Content = Label(stopwatch.IsRunning ? "\uE769" : "\uE768", stopwatch.IsRunning ? "Pausar" : "Continuar"); Refresh(); UpdateTimer(); };
        watchArea.Children.Add(elapsed);
        var watchHint = ClockDesign.Text("Cada instante conta.", 12, true); watchHint.TextAlignment = TextAlignment.Center; watchHint.Margin = new Thickness(0, 4, 0, 16); watchArea.Children.Add(watchHint);
        var watchButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center }; watchButtons.Children.Add(start);
        watchButtons.Children.Add(Button("Zerar", () => { stopwatch.Reset(); start.Content = Label("\uE768", "Iniciar"); Refresh(); UpdateTimer(); })); watchArea.Children.Add(watchButtons);
        var minutes = new TextBox { Text = "5", MinWidth = 70, MaxLength = 4, Margin = new Thickness(0, 4, 8, 4), Padding = new Thickness(8, 5, 8, 5) };
        minutesInput = minutes;
        System.Windows.Automation.AutomationProperties.SetName(minutes, "Duração do temporizador em minutos, de 1 a 1440");
        var input = new WrapPanel(); input.Children.Add(minutes); input.Children.Add(new TextBlock { Text = "minutos", VerticalAlignment = VerticalAlignment.Center });
        timerArea.Children.Add(input); timerArea.Children.Add(remaining); timerArea.Children.Add(timeline);
        var timerButtons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
        var run = Button("Iniciar", () => { });
        var presets = new WrapPanel { Margin = new Thickness(0, 2, 0, 6) };
        foreach (int value in new[] { 5, 10, 15, 30 })
        {
            var preset = Button(value + " min", () =>
            {
                if (countdown.IsRunning || duration != TimeSpan.Zero)
                { notice.Text = "Cancele o temporizador atual para escolher outra duração."; return; }
                minutes.Text = value.ToString(CultureInfo.InvariantCulture);
                completed = false;
                notice.Text = value + " minutos selecionados. Clique em Iniciar.";
                remaining.Text = Format(TimeSpan.FromMinutes(value));
            });
            preset.Tag = value;
            preset.Content = value + " min";
            presets.Children.Add(preset);
        }
        void HighlightPresets()
        {
            foreach (Button preset in presets.Children)
            {
                bool selected = int.TryParse(minutes.Text, out int value) && value == (int)preset.Tag;
                preset.SetResourceReference(Control.BackgroundProperty, selected ? "Think.AccentSurface" : "Think.Control");
                preset.SetResourceReference(Control.ForegroundProperty, selected ? "Think.Accent" : "Think.Text");
            }
        }
        minutes.TextChanged += (_, _) =>
        {
            completed = false;
            HighlightPresets();
            if (duration == TimeSpan.Zero && int.TryParse(minutes.Text, out int value) && value is >= 1 and <= 1440)
                remaining.Text = Format(TimeSpan.FromMinutes(value));
        };
        HighlightPresets(); timerArea.Children.Insert(0, presets);
        run.Click += (_, _) =>
        {
            if (countdown.IsRunning) { countdown.Stop(); run.Content = Label("\uE768", "Continuar"); }
            else
            {
                if (duration == TimeSpan.Zero)
                {
                    if (!int.TryParse(minutes.Text, out int value) || value is < 1 or > 1440) { notice.Text = "Informe de 1 a 1440 minutos."; return; }
                    duration = TimeSpan.FromMinutes(value); countdown.Reset();
                }
                completed = false; countdown.Start(); run.Content = Label("\uE769", "Pausar"); notice.Text = "O temporizador continua se você fechar este painel.";
            }
            Refresh(); UpdateTimer();
        };
        timerButtons.Children.Add(run);
        timerButtons.Children.Add(Button("Cancelar", () => { countdown.Reset(); duration = TimeSpan.Zero; run.Content = Label("\uE768", "Iniciar"); notice.Text = ""; Refresh(); UpdateTimer(); }));
        notice.MinHeight = 36; notice.MaxHeight = 48; notice.TextAlignment = TextAlignment.Center;
        timerArea.Children.Add(timerButtons); timerArea.Children.Add(notice);
        var watchTab = Button("Cronômetro", () => { if (watchArea.Visibility == Visibility.Visible) return; watchSelected = true; alarms.Visibility = Visibility.Hidden; watchArea.Visibility = Visibility.Visible; timerArea.Visibility = Visibility.Hidden; ClockDesign.Reveal(watchArea); Refresh(); UpdateTimer(); LayoutChanged?.Invoke(); });
        var timerTab = Button("Temporizador", () => { if (timerArea.Visibility == Visibility.Visible) return; watchSelected = false; alarms.Visibility = Visibility.Hidden; watchArea.Visibility = Visibility.Hidden; timerArea.Visibility = Visibility.Visible; ClockDesign.Reveal(timerArea); Refresh(); UpdateTimer(); LayoutChanged?.Invoke(); });
        var alarmTab = Button("Alarmes", () => { if (alarms.Visibility == Visibility.Visible) return; watchSelected = false; watchArea.Visibility = timerArea.Visibility = Visibility.Hidden; alarms.Visibility = Visibility.Visible; ClockDesign.Reveal(alarms); UpdateTimer(); LayoutChanged?.Invoke(); });
        alarmTab.Content = ClockDesign.Label("\uE823", "Alarmes", true);
        watchTab.Content = ClockDesign.Label("\uE916", "Cronômetro", true); timerTab.Content = ClockDesign.Label("\uE91E", "Temporizador", true);
        void Highlight(bool timer)
        {
            alarmTab.SetResourceReference(Control.BackgroundProperty, "Think.Control");
            alarmTab.SetResourceReference(Control.ForegroundProperty, "Think.Text");
            watchTab.SetResourceReference(Control.BackgroundProperty, timer ? "Think.Control" : "Think.AccentSurface");
            timerTab.SetResourceReference(Control.BackgroundProperty, timer ? "Think.AccentSurface" : "Think.Control");
            watchTab.SetResourceReference(Control.ForegroundProperty, timer ? "Think.Text" : "Think.Accent");
            timerTab.SetResourceReference(Control.ForegroundProperty, timer ? "Think.Accent" : "Think.Text");
        }
        watchTab.Click += (_, _) => Highlight(false); timerTab.Click += (_, _) => Highlight(true); Highlight(false);
        alarmTab.Click += (_, _) =>
        {
            foreach (var tab in new[] { watchTab, timerTab, alarmTab })
            {
                tab.SetResourceReference(Control.BackgroundProperty, tab == alarmTab ? "Think.AccentSurface" : "Think.Control");
                tab.SetResourceReference(Control.ForegroundProperty, tab == alarmTab ? "Think.Accent" : "Think.Text");
            }
        };
        tabs.Children.Add(watchTab); tabs.Children.Add(timerTab); tabs.Children.Add(alarmTab); Children.Add(tabs);
        var tools = new Grid { MinHeight = 320 }; tools.Children.Add(watchArea); tools.Children.Add(timerArea); tools.Children.Add(alarms); Children.Add(Card(tools));
        Children.Add(Button("Abrir alarmes do Windows ↗", () => SystemActions.Run("ms-clock:")));
        tick.Tick += (_, _) =>
        {
            if (countdown.IsRunning && countdown.Elapsed >= duration)
            {
                countdown.Reset(); duration = TimeSpan.Zero; completed = true; run.Content = Label("\uE768", "Iniciar"); notice.Text = "Tempo concluído.";
                System.Media.SystemSounds.Asterisk.Play(); UpdateTimer();
            }
            if (visible) Refresh();
        };
        Refresh();
    }
    private static TextBlock Text(double size)
    {
        var text = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
        text.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); return text;
    }
    private static Button Button(string title, Action action)
    {
        return ClockDesign.Button(title == "Iniciar" ? "\uE768" : title == "Zerar" ? "\uE777" : title == "Cancelar" ? "\uE711" : "\uE823", title, action);
    }
    private static Border Card(UIElement content)
    {
        var card = new Border { Child = content, CornerRadius = new CornerRadius(20), Padding = new Thickness(12), Margin = new Thickness(0, 3, 0, 3) };
        card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard"); return card;
    }
    private static UIElement Label(string glyph, string title)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 16, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center }); return row;
    }
    private void Refresh()
    {
        clock.Text = DateTime.Now.ToString("HH:mm:ss");
        date.Text = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        var watchTime = stopwatch.Elapsed;
        elapsed.Text = Format(watchTime) + watchTime.ToString(@"\.ff");
        var pending = !completed && duration == TimeSpan.Zero && int.TryParse(minutesInput?.Text, out int value) && value is >= 1 and <= 1440 ? TimeSpan.FromMinutes(value) : TimeSpan.Zero;
        remaining.Text = Format(duration > countdown.Elapsed ? duration - countdown.Elapsed : pending);
        double progress = duration.TotalSeconds > 0 ? Math.Clamp(1 - countdown.Elapsed.TotalSeconds / duration.TotalSeconds, 0, 1) : completed ? 0 : 1;
        if (Math.Abs(timeline.Value - progress) > .0001)
        {
            double previous = timeline.Value; timeline.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, null); timeline.Value = progress;
            double milliseconds = ((App)Application.Current).AnimationDuration;
            if (timeline.IsVisible && visible && milliseconds > 0 && SystemParameters.ClientAreaAnimation)
                timeline.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, new DoubleAnimation(previous, progress, TimeSpan.FromMilliseconds(Math.Min(220, milliseconds))) { FillBehavior = FillBehavior.Stop });
        }
        notice.Visibility = string.IsNullOrWhiteSpace(notice.Text) ? Visibility.Hidden : Visibility.Visible;
        notice.ToolTip = notice.Text;
    }
    private static string Format(TimeSpan value) => ((int)value.TotalHours).ToString("00") + value.ToString(@"\:mm\:ss");
    private void UpdateTimer()
    {
        var interval = TimeSpan.FromMilliseconds(visible && watchSelected && stopwatch.IsRunning ? 100 : 1000);
        if (tick.Interval != interval) tick.Interval = interval;
        if (visible || countdown.IsRunning) tick.Start(); else tick.Stop();
    }
    internal void SetActive(bool active) { visible = active; if (active) Refresh(); UpdateTimer(); }
    public void Dispose() { tick.Stop(); stopwatch.Stop(); countdown.Stop(); }
}
