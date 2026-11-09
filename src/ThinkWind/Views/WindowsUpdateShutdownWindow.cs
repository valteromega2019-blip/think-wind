using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

// Separate privileged role: the normal launcher remains unelevated, with drag
// and drop and existing settings untouched. No restart or forced termination.
internal sealed class WindowsUpdateShutdownWindow : Window
{
    private bool running = true;
    internal WindowsUpdateShutdownWindow()
    {
        Title = "Orby · Atualizar e desligar"; Icon = AppBrand.WindowIcon;
        Width = 500; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(23, 26, 30)); Foreground = Brushes.White;
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock { Text = "Atualizações do Windows", FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        var status = new TextBlock { Text = "Preparando…", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 16), Foreground = Brushes.White };
        body.Children.Add(status);
        var progress = new ProgressBar { IsIndeterminate = true, Height = 8, Foreground = new SolidColorBrush(Color.FromRgb(91, 204, 175)) };
        body.Children.Add(progress);
        var close = new Button { Content = "Fechar", IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0), Padding = new Thickness(16, 7, 16, 7) };
        close.Click += (_, _) => Close(); body.Children.Add(close); Content = body;
        Closing += (_, e) => { if (running) e.Cancel = true; };
        Closed += (_, _) => Application.Current.Shutdown();
        Loaded += async (_, _) =>
        {
            using var guard = new Mutex(false, "Local\\OrbyLauncher.WindowsUpdateShutdown");
            bool ownsGuard = false;
            try
            {
                try { ownsGuard = guard.WaitOne(0); }
                catch (AbandonedMutexException) { ownsGuard = true; }
                if (!ownsGuard) { status.Text = "Uma instalação já está em andamento. Acompanhe a outra janela do Orby."; return; }
                await Task.Run(() => WindowsUpdatePower.InstallDownloaded(message => Dispatcher.BeginInvoke(new Action(() => status.Text = message))));
                status.Text = "O Windows aceitou o desligamento após instalar as atualizações.";
            }
            catch (Exception error)
            {
                status.Text = error is InvalidOperationException ? error.Message
                    : "Não foi possível instalar as atualizações. O PC não será desligado. Confira o Windows Update. Código: 0x" + error.HResult.ToString("X8");
            }
            finally
            {
                if (ownsGuard) guard.ReleaseMutex();
                running = false; progress.IsIndeterminate = false; close.IsEnabled = true;
            }
        };
    }
}
