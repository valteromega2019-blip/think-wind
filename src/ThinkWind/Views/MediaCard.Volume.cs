using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed partial class MediaCard
{
    private string[] volumePaths = Array.Empty<string>();
    private StackPanel? volumeRow;
    private Button? volumeDown, volumeUp;
    private Slider? volumeSlider;
    private bool syncingVolume;
    private double? pendingVolume;
    private readonly TextBlock volumeLabel = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 42, TextAlignment = TextAlignment.Center };
    private readonly DispatcherTimer volumeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? volumeLifetime;
    private bool volumeBusy;
    private Action? openPlayer;

    internal void ConfigurePlayerActions(string[] paths, Action open)
    {
        openPlayer = open;
        volumePaths = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (volumeRow is not null) return;
        volumeRow = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 30, 10, 8), Width = 38 };
        volumeDown = VolumeButton("Diminuir volume do player", "\uE993", -5);
        volumeUp = VolumeButton("Aumentar volume do player", "\uE995", 5);
        volumeLabel.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text");
        volumeSlider = new Slider { Minimum = 0, Maximum = 100, Width = 96, Height = 32, SmallChange = 5, LargeChange = 10,
            IsMoveToPointEnabled = true, LayoutTransform = new RotateTransform(-90), ToolTip = "Volume do player" };
        System.Windows.Automation.AutomationProperties.SetName(volumeSlider, "Volume do player");
        volumeSlider.ValueChanged += async (_, _) =>
        {
            if (syncingVolume) return;
            pendingVolume = volumeSlider.Value;
            await ReadPlayerVolume();
        };
        volumeRow.Children.Add(volumeUp); volumeRow.Children.Add(volumeSlider); volumeRow.Children.Add(volumeDown); volumeRow.Children.Add(volumeLabel);
        body.Margin = new Thickness(16, 14, 58, 14);
        Children.Add(volumeRow);
        metadataRow.Cursor = Cursors.Hand;
        metadataRow.ToolTip = "Abrir player";
        metadataRow.MouseLeftButtonUp += (_, e) => { openPlayer?.Invoke(); e.Handled = true; };
        volumeTimer.Tick += async (_, _) => await ReadPlayerVolume();
    }
    private Button VolumeButton(string label, string glyph, int amount)
    {
        var button = new Button { Content = Icon(glyph), ToolTip = label, Width = 34, Height = 30, Padding = new Thickness(3), Margin = new Thickness(4, 0, 4, 0) };
        button.SetResourceReference(StyleProperty, "Think.IconButton");
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += async (_, _) => await ReadPlayerVolume(amount);
        return button;
    }
    private void SetVolumeActive(bool active)
    {
        volumeTimer.Stop(); volumeLifetime?.Cancel(); volumeLifetime?.Dispose(); volumeLifetime = null;
        pendingVolume = null;
        if (!active || volumeRow is null) return;
        volumeLifetime = new CancellationTokenSource(); volumeTimer.Start(); _ = ReadPlayerVolume();
    }
    private async Task ReadPlayerVolume(int change = 0)
    {
        if (volumeBusy || volumeLifetime is null) return;
        var token = volumeLifetime.Token;
        volumeBusy = true;
        try
        {
            var matches = (await AppAudioMixer.ReadAsync(token, includeInactive: true))
                .Where(a => volumePaths.Contains(a.Id, StringComparer.OrdinalIgnoreCase)).ToArray();
            token.ThrowIfCancellationRequested();
            if (matches.Length != 1)
            {
                pendingVolume = null;
                volumeSlider!.IsEnabled = false;
                volumeLabel.Text = "—";
                volumeRow!.ToolTip = "Volume indisponível: o Windows não expôs uma sessão única deste player.";
                return;
            }
            volumeSlider!.IsEnabled = true;
            var target = matches[0];
            double? requested = pendingVolume; pendingVolume = null;
            double value = Math.Clamp(requested ?? target.Volume + change, 0, 100);
            bool applied = change == 0 && requested is null || await AppAudioMixer.SetAsync(target, value, value > 0 ? false : null, token);
            token.ThrowIfCancellationRequested();
            volumeLabel.Text = applied ? $"{value:0}%" : "—";
            if (applied && pendingVolume is null && !volumeSlider!.IsMouseCaptureWithin)
            {
                syncingVolume = true;
                try { volumeSlider.Value = value; }
                finally { syncingVolume = false; }
            }
            volumeRow!.ToolTip = "Volume do aplicativo no mixer do Windows. Players baseados no navegador podem compartilhar esse volume com outras abas.";
        }
        catch (OperationCanceledException) { }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        { pendingVolume = null; if (!token.IsCancellationRequested) volumeLabel.Text = "—"; }
        finally
        {
            volumeBusy = false;
            if (!token.IsCancellationRequested && pendingVolume is not null)
                _ = Dispatcher.BeginInvoke(new Action(() => _ = ReadPlayerVolume()));
        }
    }
}
