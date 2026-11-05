using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class AlarmNotice : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override bool HideOnClose => false;
    private readonly DispatcherTimer sound = new() { Interval = TimeSpan.FromSeconds(5) };
    private int repeats;
    internal AlarmNotice(SettingsStore store, ClockAlarm alarm, AlarmService alarms) : base(store)
    {
        Title = "Orby · Alarme"; ShowActivated = false;
        var content = new StackPanel { Margin = new Thickness(8) };
        content.Children.Add(new TextBlock { Text = "⏰ " + alarm.Name, FontSize = 20, TextWrapping = TextWrapping.Wrap });
        content.Children.Add(new TextBlock { Text = $"{alarm.Minute / 60:00}:{alarm.Minute % 60:00}", FontSize = 28 });
        if (alarm.IsAgenda && alarm.EventDate is DateTime date)
        {
            content.Children.Add(ClockDesign.Text(date.ToString("dd/MM/yyyy · HH:mm"), 14));
            if (!string.IsNullOrWhiteSpace(alarm.Notes))
                content.Children.Add(ClockDesign.Text(alarm.Notes.Length > 240 ? alarm.Notes[..240] + "…" : alarm.Notes, 12));
        }
        var buttons = new WrapPanel();
        buttons.Children.Add(ActionButton("Adiar 5 min", () => { alarms.Snooze(alarm); Close(); }));
        buttons.Children.Add(ActionButton("Dispensar", Close)); content.Children.Add(buttons); Surface.Child = content;
        void Play()
        {
            if (alarm.Sound == "Silent" || repeats++ >= 12) { sound.Stop(); return; }
            if (alarm.Sound == "Beep") System.Media.SystemSounds.Beep.Play();
            else if (alarm.Sound == "Exclamation") System.Media.SystemSounds.Exclamation.Play();
            else System.Media.SystemSounds.Asterisk.Play();
        }
        sound.Tick += (_, _) => Play();
        Loaded += (_, _) => { RefreshSettings(); Play(); if (alarm.Sound != "Silent") sound.Start(); };
        Closed += (_, _) => sound.Stop();
    }
    public override void Position()
    {
        Surface.Child.Measure(new Size(320, double.PositiveInfinity));
        PlaceNear(340, Surface.Child.DesiredSize.Height + 20, new OrbyLauncher.Native.OverlayChrome.PixelRect
        { Left = Display.Bounds.Right - (int)(360 * Display.Scale), Right = Display.Bounds.Right - (int)(20 * Display.Scale), Top = Display.Bounds.Top + 36, Bottom = Display.Bounds.Top + 37 }, false);
    }
}
