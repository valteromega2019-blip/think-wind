using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

internal static class Release028Checks
{
    internal static void SelectionTemplate()
    {
        foreach (string theme in new[] { "Dark", "Light" })
        {
            ThemeService.Apply(new Preferences { Theme = theme });
            var check = new CheckBox { Content = "Wi-Fi", Style = SelectionCircleStyle.Create("\uE701"), IsChecked = true };
            check.Measure(new Size(400, 80)); check.Arrange(new Rect(0, 0, 400, 80)); check.UpdateLayout();
            var indicator = Descendants(check).OfType<LiquidToggle>().Single();
            Require(indicator.IsChecked == true && indicator.Glyph == "\uE701", "Estado/ícone não chegou ao template.");
            check.IsChecked = false;
            Require(indicator.IsChecked == false, "Template perdeu a ligação com o CheckBox.");
            var bitmap = new RenderTargetBitmap(400, 80, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(check); // Exercises public construction and actual vector rendering.
        }
    }
    internal static void MonitoringPreferences(string path)
    {
        var store = new SettingsStore(path);
        store.Update(p => { p.CompactMonitoringMetrics = new() { "disk", "ram", "disk", "invalid", "gpu", "cpu" }; p.MonitoringDiskId = @"d:\"; }); store.Flush();
        var loaded = new SettingsStore(path);
        Require(loaded.Value.CompactMonitoringMetrics.SequenceEqual(new[] { "disk", "ram", "gpu" }), "Blocos duplicados/inválidos ou limite de três incorreto.");
        Require(loaded.Value.MonitoringDiskId == "D:", "Unidade selecionada não persistiu/normalizou.");
        loaded.Update(p => p.MonitoringDiskId = "invalid"); loaded.Flush();
        Require(loaded.Value.MonitoringDiskId == "", "Unidade inválida aceita.");
    }
    internal static void AlarmsPersistence(string path)
    {
        using (var alarms = new AlarmService(path))
        {
            var alarm = new ClockAlarm { Name = "Lembrete", Minute = 720, Days = new() { DayOfWeek.Monday, DayOfWeek.Friday }, Sound = "Beep" };
            alarms.Put(alarm); alarms.Toggle(alarm, false);
        }
        using var loaded = new AlarmService(path);
        var saved = loaded.Items.Single();
        Require(saved.Name == "Lembrete" && saved.Minute == 720 && saved.Sound == "Beep" && !saved.Enabled && saved.Days.SequenceEqual(new[] { DayOfWeek.Monday, DayOfWeek.Friday }), "Alarme não restaurado integralmente.");
        loaded.Remove(saved.Id);
        using var empty = new AlarmService(path);
        Require(empty.Items.Count == 0 && empty.Error is null, "Exclusão não persistiu.");
    }
    internal static void DiskPicker(string path)
    {
        var store = new SettingsStore(path);
        var sample = new HardwareSample(DateTime.Now, new DeviceReading("CPU"), new DeviceReading("GPU"), 8, 16, 100, 500, "C:", "",
            Disks: new[] { new DiskReading("C:", 100, 500), new DiskReading("D:", 200, 1000) });
        var card = new HardwareSummaryCard("disk", store: store); card.Present(sample);
        card.Measure(new Size(320, 400)); card.Arrange(new Rect(0, 0, 320, card.DesiredSize.Height)); card.UpdateLayout();
        var selector = Descendants(card).OfType<UniformGrid>().Single();
        Require(selector.Visibility == Visibility.Visible && selector.Children.Count == 2, "Seletor não exibiu as duas unidades.");
        ((Button)selector.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); store.Flush();
        Require(new SettingsStore(path).Value.MonitoringDiskId == "D:", "Seleção de D: não foi salva.");
        card.Present(sample with { Disk = "D:", DiskFree = 200, DiskTotal = 1000 });
        Require(Descendants(card).OfType<TextBlock>().Any(t => t.Text == "livres em D:"), "Unidade apresentada não foi atualizada.");
        card.Present(sample with { Disks = new[] { new DiskReading("C:", 100, 500) } });
        Require(selector.Visibility == Visibility.Collapsed, "Seletor deve ficar oculto com apenas uma unidade.");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var node in Descendants(child)) yield return node; }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
