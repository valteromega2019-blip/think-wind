using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class CallPreviewCard : StackPanel, IDisposable
{
    private readonly TextBlock heading = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock status = new() { FontSize = 11, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis, MinHeight = 18, Margin = new Thickness(0, 8, 0, 4) };
    private readonly WrapPanel controls = new();
    private readonly Button globalMute = new() { FontSize = 11, Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button outputMute = new() { FontSize = 11, Padding = new Thickness(10, 7, 10, 7), HorizontalAlignment = HorizontalAlignment.Left };
    private readonly AudioService audio = new();
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private CancellationTokenSource? lifetime;
    private CallSnapshot? snapshot;
    private List<Desktop.RunningWindow> windows = new();
    private bool busy;
    private bool callAvailable = true;
    private int missingReads;
    private string controlsSignature = "";
    private (AudioState Mic, AudioState Output)? restore;
    internal bool IsInteracting => busy || IsMouseCaptureWithin;
    internal event Action? LayoutChanged;
    internal event Action? CallEnded;
    internal CallPreviewCard()
    {
        status.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = status });
        heading.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TextBlock.Text)) { Source = heading });
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
                if (current is null) { MissingCall(); return; }
                missingReads = 0; callAvailable = true; snapshot = current; Render();
            }
            catch (OperationCanceledException) { }
            finally { busy = false; }
        };
    }
    internal void Present(CallSnapshot call, IEnumerable<Desktop.RunningWindow> source)
    {
        Stop(); callAvailable = true; missingReads = 0; snapshot = call; windows = source.Where(w => w.Handle == call.Window).ToList();
        lifetime = new CancellationTokenSource(); Render(); refresh.Start();
    }
    internal void Stop() { refresh.Stop(); lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null; }
    private void MissingCall()
    {
        missingReads++;
        // Native controls can disappear briefly while Discord applies a command.
        // Never replace this card under the user's pointer during that interval.
        if (missingReads < 2) { status.Text = "Atualizando os controles da chamada…"; return; }
        callAvailable = false;
        heading.Text = (snapshot?.App ?? "Aplicativo") + " · Verificando chamada";
        controls.IsEnabled = false;
        status.Text = "Controles temporariamente indisponíveis. A prévia permanece aberta enquanto você estiver sobre ela.";
        if (Window.GetWindow(this) is WindowPreview owner && owner.PointerInside) return;
        Stop(); CallEnded?.Invoke();
    }
    private void Render()
    {
        if (snapshot is null) return;
        heading.Text = snapshot.App + " · Em chamada";
        controls.IsEnabled = callAvailable;
        string nextSignature = snapshot.App + string.Join("|", snapshot.Commands.Select(command => $"{command.Action}:{command.AccessibleName}:{command.Muted}"));
        if (controlsSignature != nextSignature)
        {
            controlsSignature = nextSignature;
            controls.Children.Clear();
            foreach (var command in snapshot.Commands)
            {
                string glyph = command.Action switch { CallAction.Microphone => "\uE720", CallAction.Headphones => "\uE7F6", _ => "\uE717" };
                var symbol = new Grid { Width = 30, Height = 30, HorizontalAlignment = HorizontalAlignment.Center };
                symbol.Children.Add(new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                if (command.Muted == true)
                    symbol.Children.Add(new System.Windows.Shapes.Line { X1 = 4, Y1 = 26, X2 = 26, Y2 = 4, Stroke = new SolidColorBrush(Color.FromRgb(248, 92, 106)), StrokeThickness = 2.5, IsHitTestVisible = false });
                string label = command.Action switch
                {
                    CallAction.Leave => "Sair da chamada",
                    CallAction.Microphone => command.Muted == true ? "Mic mudo" : command.Muted == false ? "Mic aberto" : "Microfone",
                    _ => command.Muted == true ? "Áudio mudo" : command.Muted == false ? "Áudio ativo" : "Áudio"
                };
                var button = new Button { Content = new StackPanel { Children =
                    {
                        symbol,
                        new TextBlock { Text = label, MaxWidth = 82, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 7, 0, 0) }
                    } }, ToolTip = command.AccessibleName, MinWidth = 86, MinHeight = 84, Padding = new Thickness(10), Margin = new Thickness(3, 10, 3, 0) };
                System.Windows.Automation.AutomationProperties.SetName(button, command.AccessibleName);
                if (command.Muted == true || command.Action == CallAction.Leave) button.Foreground = new SolidColorBrush(Color.FromRgb(248, 92, 106));
                else if (command.Muted == false) button.SetResourceReference(Control.ForegroundProperty, "Think.Accent");
                button.SetResourceReference(Control.BackgroundProperty, command.Muted == false ? "Think.AccentSurface" : "Think.Control");
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
                            if (updated is null) MissingCall();
                            else { missingReads = 0; callAvailable = true; snapshot = updated; Render(); }
                        }
                    }
                    catch (OperationCanceledException) { }
                    finally { busy = false; controls.IsEnabled = callAvailable; }
                };
                controls.Children.Add(button);
            }
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
