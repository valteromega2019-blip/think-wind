using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class SettingsWindow : Window
{
    public SettingsWindow(SettingsStore store, Action apply)
    {
        Title = "Think Wind · Personalização";
        Width = 510; Height = 610; MinWidth = 420; MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(19, 22, 35));
        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        panel.Children.Add(new TextBlock { Text = "Faça do seu jeito.", FontSize = 28, Margin = new Thickness(0, 0, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "Barra flutuante e Dynamic Island", Margin = new Thickness(0, 0, 0, 20) });
        panel.Children.Add(new TextBlock { Text = "Tamanho da barra" });
        var scale = new Slider { Minimum = .75, Maximum = 1.5, Value = store.Value.DockScale, TickFrequency = .05, IsSnapToTickEnabled = true, Margin = new Thickness(0, 12, 0, 16) };
        panel.Children.Add(scale);
        panel.Children.Add(new TextBlock { Text = "Opacidade da tonalidade sobre o Acrylic" });
        var opacity = new Slider { Minimum = .2, Maximum = 1, Value = store.Value.TintOpacity, Margin = new Thickness(0, 12, 0, 16) };
        panel.Children.Add(opacity);
        var hide = new CheckBox { Content = "Recolher a barra quando estiver ociosa", IsChecked = store.Value.AutoHide }; panel.Children.Add(hide);
        var ram = new CheckBox { Content = "Mostrar uso real de memória RAM", IsChecked = store.Value.ShowMemory }; panel.Children.Add(ram);
        var battery = new CheckBox { Content = "Mostrar bateria quando disponível", IsChecked = store.Value.ShowBattery }; panel.Children.Add(battery);
        var startup = new CheckBox { Content = "Iniciar junto com o Windows" };
        try { startup.IsChecked = SystemActions.StartupEnabled(); }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException) { startup.IsEnabled = false; }
        panel.Children.Add(startup);
        panel.Children.Add(new TextBlock { Text = "Monitor da barra e da ilha", Margin = new Thickness(0, 12, 0, 8) });
        var screens = System.Windows.Forms.Screen.AllScreens;
        var monitor = new ComboBox { Margin = new Thickness(0, 0, 0, 12) };
        foreach (var screen in screens) monitor.Items.Add($"{screen.DeviceName} · {screen.Bounds.Width} × {screen.Bounds.Height}");
        monitor.SelectedIndex = Math.Clamp(store.Value.MonitorIndex, 0, screens.Length - 1);
        panel.Children.Add(monitor);
        panel.Children.Add(new TextBlock { Text = "Clique com o botão direito em um atalho da barra para mover ou remover. A barra original permanece disponível nesta primeira versão.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        var save = new Button { Content = "Salvar preferências" };
        save.Click += (_, _) =>
        {
            try
            {
                // Startup is changed only when explicitly saving this setting.
                if (startup.IsEnabled && (startup.IsChecked == true) != SystemActions.StartupEnabled()) SystemActions.SetStartup(startup.IsChecked == true);
                store.Value.DockScale = scale.Value; store.Value.TintOpacity = opacity.Value;
                store.Value.AutoHide = hide.IsChecked == true; store.Value.ShowMemory = ram.IsChecked == true;
                store.Value.ShowBattery = battery.IsChecked == true; store.Value.MonitorIndex = monitor.SelectedIndex;
                store.Save(); apply(); Close();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { MessageBox.Show("Não foi possível salvar todas as preferências.\n" + e.Message, "Think Wind"); }
        };
        panel.Children.Add(save);
    }
}
