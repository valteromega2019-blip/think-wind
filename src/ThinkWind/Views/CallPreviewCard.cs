using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class CallPreviewCard : StackPanel, IDisposable
{
    private readonly TextBlock heading = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) };
    private readonly WrapPanel controls = new();
    private readonly Button globalMute = new() { FontSize = 11, Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button outputMute = new() { FontSize = 11, Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly AudioService audio = new();
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? lifetime;
    private CallSnapshot? snapshot;
    private List<Desktop.RunningWindow> windows = new();
    private bool busy;
    private (AudioState Mic, AudioState Output)? restore;
    internal bool IsInteracting => busy || IsMouseCaptureWithin;
    internal event Action? LayoutChanged;
    internal event Action? CallEnded;
    internal CallPreviewCard()
    {
        Children.Add(heading); Children.Add(controls);
        var open = new Button { Content = "Abrir chamada", FontSize = 11, Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left };
        open.Click += async (_, _) =>
        {
            if (snapshot is not { } current) return;
            if (!await Desktop.ActivateFromDock(current.Window, false)) status.Text = "Abra a chamada pelo aplicativo. O Windows não permitiu ativar a janela.";
        };
        Children.Add(open);
        var note = new TextBlock { Text = "Áudio do Windows · dispositivos padrão", FontSize = 11, Margin = new Thickness(4, 12, 0, 4), TextWrapping = TextWrapping.Wrap };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); Children.Add(note);
        Children.Add(outputMute); Children.Add(globalMute); Children.Add(status);
        outputMute.Click += (_, _) =>
        {
            try
            {
                var current = audio.Read(CoreAudio.Flow.Render, null);
                audio.SetMuted(CoreAudio.Flow.Render, current.Id, !current.Muted);
                status.Text = "Saída padrão do Windows alterada: " + current.Name + ".";
            }
            catch (AudioUnavailableException) { status.Text = "Saída de áudio indisponível."; }
            Render();
        };
        globalMute.Click += (_, _) => ToggleGlobalMute();
        refresh.Tick += async (_, _) =>
        {
            if (busy || lifetime is null) return;
            busy = true; var token = lifetime.Token;
            try
            {
                var current = await CallIntegration.FindAsync(windows, token);
                if (token.IsCancellationRequested) return;
                if (current is null) { Stop(); CallEnded?.Invoke(); return; }
                snapshot = current; Render();
            }
            catch (OperationCanceledException) { }
            finally { busy = false; }
        };
    }
    internal void Present(CallSnapshot call, IEnumerable<Desktop.RunningWindow> source)
    {
        Stop(); snapshot = call; windows = source.Where(w => w.Handle == call.Window).ToList();
        lifetime = new CancellationTokenSource(); Render(); refresh.Start();
    }
    internal void Stop() { refresh.Stop(); lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; }
    private void Render()
    {
        if (snapshot is null) return;
        heading.Text = snapshot.App + " · Em chamada";
        controls.Children.Clear();
        foreach (var command in snapshot.Commands)
        {
            string glyph = command.Action switch { CallAction.Microphone => "\uE720", CallAction.Headphones => "\uE7F6", _ => "\uE711" };
            var button = new Button { Content = new StackPanel { Children =
                {
                    new TextBlock { Text = glyph, FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons"), FontSize = 19, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock { Text = command.AccessibleName.Split('(')[0].Trim(), MaxWidth = 82, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 11, Margin = new Thickness(0, 5, 0, 0) }
                } }, ToolTip = command.AccessibleName, MinWidth = 76, Padding = new Thickness(10), Margin = new Thickness(3, 10, 3, 0) };
            System.Windows.Automation.AutomationProperties.SetName(button, command.AccessibleName);
            if (command.Action == CallAction.Leave) button.SetResourceReference(Control.ForegroundProperty, "Think.Accent");
            button.Click += async (_, _) =>
            {
                if (busy || lifetime is null || snapshot is null) return;
                busy = true; controls.IsEnabled = false; var token = lifetime.Token;
                try
                {
                    bool sent = await CallIntegration.ExecuteAsync(snapshot, command, token);
                    if (token.IsCancellationRequested) return;
                    status.Text = sent ? "Comando enviado ao aplicativo." : "Controle indisponível. Abra a chamada para continuar.";
                    if (sent)
                    {
                        var updated = await CallIntegration.FindAsync(windows, token);
                        if (token.IsCancellationRequested) return;
                        if (updated is null) { Stop(); CallEnded?.Invoke(); }
                        else { snapshot = updated; Render(); }
                    }
                }
                catch (OperationCanceledException) { }
                finally { busy = false; controls.IsEnabled = true; }
            };
            controls.Children.Add(button);
        }
        globalMute.Content = restore.HasValue ? "Restaurar mic + áudio padrão" : "Silenciar mic + áudio padrão";
        try
        {
            var output = audio.Read(CoreAudio.Flow.Render, null);
            outputMute.Content = output.Muted ? "Ativar saída de áudio padrão" : "Silenciar saída de áudio padrão";
            outputMute.ToolTip = output.Name; outputMute.IsEnabled = true;
        }
        catch (AudioUnavailableException) { outputMute.Content = "Saída de áudio indisponível"; outputMute.IsEnabled = false; }
        LayoutChanged?.Invoke();
    }
    private void ToggleGlobalMute()
    {
        try
        {
            if (restore is { } previous)
            {
                audio.SetMuted(CoreAudio.Flow.Capture, previous.Mic.Id, previous.Mic.Muted);
                audio.SetMuted(CoreAudio.Flow.Render, previous.Output.Id, previous.Output.Muted);
                restore = null; status.Text = "Estados anteriores restaurados nos dispositivos controlados.";
            }
            else
            {
                var mic = audio.Read(CoreAudio.Flow.Capture, null); var output = audio.Read(CoreAudio.Flow.Render, null);
                audio.SetMuted(CoreAudio.Flow.Capture, mic.Id, true);
                try { audio.SetMuted(CoreAudio.Flow.Render, output.Id, true); }
                catch (AudioUnavailableException) { audio.SetMuted(CoreAudio.Flow.Capture, mic.Id, mic.Muted); throw; }
                restore = (mic, output); status.Text = "Microfone e saída padrão silenciados no Windows. Isso afeta outros aplicativos que usam esses dispositivos.";
            }
        }
        catch (AudioUnavailableException) { status.Text = "Dispositivo indisponível. Confira o áudio nas configurações do Windows."; }
        Render();
    }
    public void Dispose() => Stop();
}
