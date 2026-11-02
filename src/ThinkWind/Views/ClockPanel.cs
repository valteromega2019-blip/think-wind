using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class ClockPanel : StackPanel, IDisposable
{
    private readonly TextBlock clock = Text(32), date = Text(13), elapsed = Text(24), remaining = Text(24), notice = Text(12);
    private readonly Stopwatch stopwatch = new(), countdown = new();
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private TimeSpan duration;
    private bool visible;
    internal ClockPanel()
    {
        Margin = new Thickness(6);
        clock.HorizontalAlignment = date.HorizontalAlignment = HorizontalAlignment.Center;
        Children.Add(clock); Children.Add(date);
        var tabs = new WrapPanel { Margin = new Thickness(0, 12, 0, 8) };
        var watchArea = new StackPanel(); var timerArea = new StackPanel { Visibility = Visibility.Collapsed };
        var start = Button("Iniciar", () => { });
        start.Click += (_, _) => { if (stopwatch.IsRunning) stopwatch.Stop(); else stopwatch.Start(); start.Content = stopwatch.IsRunning ? "Pausar" : "Continuar"; Refresh(); };
        watchArea.Children.Add(elapsed);
        var watchButtons = new WrapPanel(); watchButtons.Children.Add(start);
        watchButtons.Children.Add(Button("Zerar", () => { stopwatch.Reset(); start.Content = "Iniciar"; Refresh(); })); watchArea.Children.Add(watchButtons);
        var minutes = new TextBox { Text = "5", MinWidth = 70, MaxLength = 4, Margin = new Thickness(0, 4, 8, 4), Padding = new Thickness(8, 5, 8, 5) };
        System.Windows.Automation.AutomationProperties.SetName(minutes, "Duração do temporizador em minutos, de 1 a 1440");
        var input = new WrapPanel(); input.Children.Add(minutes); input.Children.Add(new TextBlock { Text = "minutos", VerticalAlignment = VerticalAlignment.Center });
        timerArea.Children.Add(input); timerArea.Children.Add(remaining);
        var timerButtons = new WrapPanel();
        var run = Button("Iniciar", () => { });
        run.Click += (_, _) =>
        {
            if (countdown.IsRunning) { countdown.Stop(); run.Content = "Continuar"; }
            else
            {
                if (duration == TimeSpan.Zero)
                {
                    if (!int.TryParse(minutes.Text, out int value) || value is < 1 or > 1440) { notice.Text = "Informe de 1 a 1440 minutos."; return; }
                    duration = TimeSpan.FromMinutes(value); countdown.Reset();
                }
                countdown.Start(); run.Content = "Pausar"; notice.Text = "O temporizador continua se você fechar este painel.";
            }
            Refresh(); UpdateTimer();
        };
        timerButtons.Children.Add(run);
        timerButtons.Children.Add(Button("Cancelar", () => { countdown.Reset(); duration = TimeSpan.Zero; run.Content = "Iniciar"; notice.Text = ""; Refresh(); UpdateTimer(); }));
        timerArea.Children.Add(timerButtons); timerArea.Children.Add(notice);
        var watchTab = Button("Cronômetro", () => { watchArea.Visibility = Visibility.Visible; timerArea.Visibility = Visibility.Collapsed; });
        var timerTab = Button("Temporizador", () => { watchArea.Visibility = Visibility.Collapsed; timerArea.Visibility = Visibility.Visible; });
        tabs.Children.Add(watchTab); tabs.Children.Add(timerTab); Children.Add(tabs); Children.Add(watchArea); Children.Add(timerArea);
        Children.Add(Button("Abrir alarmes do Windows ↗", () => SystemActions.Run("ms-clock:")));
        tick.Tick += (_, _) =>
        {
            if (countdown.IsRunning && countdown.Elapsed >= duration)
            {
                countdown.Reset(); duration = TimeSpan.Zero; run.Content = "Iniciar"; notice.Text = "Tempo concluído.";
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
        var button = new Button { Content = title, Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 4, 6, 4) };
        button.Click += (_, _) => action(); return button;
    }
    private void Refresh()
    {
        clock.Text = DateTime.Now.ToString("HH:mm:ss");
        date.Text = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        elapsed.Text = Format(stopwatch.Elapsed);
        remaining.Text = Format(duration > countdown.Elapsed ? duration - countdown.Elapsed : TimeSpan.Zero);
    }
    private static string Format(TimeSpan value) => ((int)value.TotalHours).ToString("00") + value.ToString(@"\:mm\:ss");
    private void UpdateTimer() { if (visible || countdown.IsRunning) tick.Start(); else tick.Stop(); }
    internal void SetActive(bool active) { visible = active; if (active) Refresh(); UpdateTimer(); }
    public void Dispose() { tick.Stop(); stopwatch.Stop(); countdown.Stop(); }
}
