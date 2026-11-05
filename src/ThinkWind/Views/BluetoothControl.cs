using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class BluetoothControl : StackPanel, IDisposable
{
    internal static readonly string[] DeviceSelectors =
    {
        "System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"",
        "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\""
    };
    private readonly Dictionary<string, DeviceInformation> devices = new();
    private readonly HashSet<string> seenDuringScan = new();
    private readonly List<DeviceWatcher> watchers = new();
    private readonly List<Radio> radios = new();
    private readonly Dictionary<string, System.Windows.Media.ImageSource> glyphs = new();
    private readonly HashSet<string> loadingGlyphs = new();
    private readonly HashSet<string> unavailableGlyphs = new();
    private readonly StackPanel list = new() { MinHeight = 232 }, navigation = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
    private readonly Button toggle = new() { Padding = new Thickness(10, 6, 10, 6), Content = "Consultar Bluetooth" };
    private bool active, busy, queued, disposed;
    private readonly SettingsStore? preferences;
    private BluetoothPairingWindow? pairingPrompt;
    private Windows.Foundation.IAsyncInfo? operation;
    private int page, generation;
    internal bool IsInteracting => busy || IsMouseCaptureWithin;
    internal int ActiveWatchers => watchers.Count(watcher => watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted);
    internal event Action? LayoutChanged;
    internal BluetoothControl(SettingsStore? settings = null)
    {
        preferences = settings;
        Children.Add(toggle); Children.Add(list); Children.Add(navigation); Children.Add(status);
        toggle.Click += async (_, _) => await Toggle();
        var native = Button("Conectar/desconectar nas configurações do Windows", () => SystemActions.Run("ms-settings:bluetooth")); Children.Add(native);
    }
    private static Button Button(string text, Action action)
    { var button = new Button { Content = text, FontSize = 11, Padding = new Thickness(8, 6, 8, 6) }; button.Click += (_, _) => action(); return button; }
    internal async void SetActive(bool value)
    {
        active = value; int request = ++generation; StopWatching();
        if (!value || disposed) return;
        seenDuringScan.Clear(); Render();
        status.Text = "Atualizando adaptador, pareados e dispositivos disponíveis…";
        try
        {
            var available = await Radio.GetRadiosAsync().AsTask().ConfigureAwait(false);
            await OnUI(() =>
            {
            if (!active || disposed || request != generation) return;
            foreach (var radio in available.Where(radio => radio.Kind == RadioKind.Bluetooth)) { radios.Add(radio); radio.StateChanged += RadioChanged; }
            foreach (string selector in DeviceSelectors)
            {
                var watcher = DeviceInformation.CreateWatcher(selector, new[] { "System.Devices.Aep.IsConnected", "System.Devices.BatteryLife", "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.ProtocolId" }, DeviceInformationKind.AssociationEndpoint);
                watcher.Added += Added; watcher.Updated += Updated; watcher.Removed += Removed; watcher.EnumerationCompleted += Completed; watcher.Stopped += Stopped;
                watchers.Add(watcher); watcher.Start();
            }
            Render(); status.Text = "Procurando dispositivos pareados e próximos…";
            });
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or ArgumentException or InvalidOperationException)
        {
            DiagnosticLog.Record("Descoberta Bluetooth", error);
            await OnUI(() => { if (active) { status.Text = "Não foi possível consultar o Bluetooth. Reabra o painel para tentar novamente."; LayoutChanged?.Invoke(); } });
        }
    }
    private Task OnUI(Action action)
    { if (Dispatcher.HasShutdownStarted) return Task.CompletedTask; if (Dispatcher.CheckAccess()) { action(); return Task.CompletedTask; } return Dispatcher.InvokeAsync(action).Task; }
    private void Dispatch(Action action)
    { if (!disposed && !Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(() => { if (active && !disposed) action(); })); }
    private void Added(DeviceWatcher sender, DeviceInformation info) => Dispatch(() => { if (!watchers.Contains(sender)) return; seenDuringScan.Add(info.Id); devices[info.Id] = info; QueueRender(); });
    private void Updated(DeviceWatcher sender, DeviceInformationUpdate info) => Dispatch(() => { if (!watchers.Contains(sender)) return; if (devices.TryGetValue(info.Id, out var device)) device.Update(info); QueueRender(); });
    private void Removed(DeviceWatcher sender, DeviceInformationUpdate info) => Dispatch(() => { if (watchers.Contains(sender)) { devices.Remove(info.Id); QueueRender(); } });
    private void Completed(DeviceWatcher sender, object args) => Dispatch(() =>
    {
        if (!watchers.Contains(sender)) return;
        if (watchers.Count == DeviceSelectors.Length && watchers.All(w => w.Status == DeviceWatcherStatus.EnumerationCompleted))
            foreach (string id in devices.Keys.Where(id => !seenDuringScan.Contains(id)).ToArray()) devices.Remove(id);
        status.Text = devices.Count == 0 ? "Nenhum dispositivo encontrado. Ative o modo de pareamento do dispositivo."
            : "Pareados e disponíveis · selecione Parear para adicionar um dispositivo.";
        QueueRender();
    });
    private void Stopped(DeviceWatcher sender, object args) => Dispatch(() => { if (watchers.Contains(sender) && sender.Status == DeviceWatcherStatus.Aborted) { status.Text = "Descoberta interrompida pelo Windows. Confira permissões e estado do adaptador."; QueueRender(); } });
    private void RadioChanged(Radio sender, object args) => Dispatch(QueueRender);
    private void QueueRender()
    {
        if (queued) return; queued = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { queued = false; if (active && !disposed && !busy) Render(); }));
    }
    private void Render()
    {
        toggle.IsEnabled = !busy && radios.Count > 0;
        toggle.Content = radios.Count == 0 ? "Adaptador Bluetooth indisponível" : radios.Any(radio => radio.State == RadioState.On) ? "Bluetooth ligado — desativar" : "Bluetooth desligado — ativar";
        toggle.SetResourceReference(Control.BackgroundProperty, radios.Any(radio => radio.State == RadioState.On) ? "Think.AccentSurface" : "Think.Control");
        list.Children.Clear(); navigation.Children.Clear();
        var ordered = devices.Values.Where(device => !string.IsNullOrWhiteSpace(device.Name)).OrderByDescending(device => device.Pairing.IsPaired).ThenBy(device => device.Name).ToArray();
        int pages = Math.Max(1, (ordered.Length + 3) / 4); page = Math.Clamp(page, 0, pages - 1);
        bool? previousGroup = null;
        foreach (var device in ordered.Skip(page * 4).Take(4))
        {
            if (previousGroup != device.Pairing.IsPaired)
            {
                var group = new TextBlock { Text = device.Pairing.IsPaired ? "Pareados" : "Disponíveis para parear",
                    FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(6, 8, 6, 4) };
                group.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); list.Children.Add(group);
                previousGroup = device.Pairing.IsPaired;
            }
            bool connected = device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var state) && state is true;
            string battery = "";
            if (device.Properties.TryGetValue("System.Devices.BatteryLife", out var charge) && (charge is byte or ushort or int or uint))
            { long amount = Convert.ToInt64(charge); if (amount is >= 0 and <= 100) battery = $" · bateria {amount}%"; }
            var row = new DockPanel { Margin = new Thickness(4) };
            var actions = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (device.Pairing.IsPaired)
            {
                var connection = Button(connected && TryClassicAddress(device, out _) ? "Desconectar" : "Gerenciar conexão", async () => await Disconnect(device));
                connection.IsEnabled = !busy; actions.Children.Add(connection);
            }
            var action = Button(device.Pairing.IsPaired ? "Esquecer dispositivo" : "Parear", async () => await Pair(device));
            action.IsEnabled = !busy && (device.Pairing.IsPaired || device.Pairing.CanPair); actions.Children.Add(action);
            DockPanel.SetDock(actions, Dock.Right); row.Children.Add(actions);
            var iconSlot = new Grid { Width = 26, Height = 24, VerticalAlignment = VerticalAlignment.Center };
            var fallback = new TextBlock { Text = "\uE702", FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 18 };
            var image = new Image { Width = 22, Height = 22, Stretch = System.Windows.Media.Stretch.Uniform };
            iconSlot.Children.Add(fallback); iconSlot.Children.Add(image); DockPanel.SetDock(iconSlot, Dock.Left); row.Children.Add(iconSlot);
            LoadGlyph(device, image, fallback);
            row.Children.Add(new TextBlock { Text = device.Name + "\n" + (connected ? "Conectado" : device.Pairing.IsPaired ? "Pareado" : "Disponível") + battery, FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center }); list.Children.Add(row);
        }
        if (pages > 1)
        {
            var back = Button("‹", () => { page--; Render(); }); back.IsEnabled = page > 0;
            var next = Button("›", () => { page++; Render(); }); next.IsEnabled = page + 1 < pages;
            navigation.Children.Add(back); navigation.Children.Add(new TextBlock { Text = $"{page + 1}/{pages}", Margin = new Thickness(8) }); navigation.Children.Add(next);
        }
        LayoutChanged?.Invoke();
    }
    private async void LoadGlyph(DeviceInformation device, Image image, TextBlock fallback)
    {
        if (glyphs.TryGetValue(device.Id, out var cached)) { image.Source = cached; fallback.Visibility = Visibility.Collapsed; return; }
        if (unavailableGlyphs.Contains(device.Id) || !loadingGlyphs.Add(device.Id)) return;
        int request = generation;
        try
        {
            using var stream = await device.GetGlyphThumbnailAsync().AsTask().ConfigureAwait(false);
            if (stream is null) { await OnUI(() => unavailableGlyphs.Add(device.Id)); return; }
            await OnUI(() =>
            {
                if (!active || disposed || request != generation) return;
                using var input = stream.AsStreamForRead();
                var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze();
                if (glyphs.Count > 128) glyphs.Clear(); glyphs[device.Id] = bitmap; image.Source = bitmap; fallback.Visibility = Visibility.Collapsed;
            });
        }
        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or FormatException)
        { await OnUI(() => unavailableGlyphs.Add(device.Id)); }
        finally { await OnUI(() => loadingGlyphs.Remove(device.Id)); }
    }
    private async Task Toggle()
    {
        if (busy) return; busy = true; Render();
        try
        {
            var access = await Radio.RequestAccessAsync();
            if (access != RadioAccessStatus.Allowed) { status.Text = "O Windows não autorizou controlar o rádio Bluetooth."; return; }
            var target = radios.Any(radio => radio.State == RadioState.On) ? RadioState.Off : RadioState.On;
            foreach (var radio in radios.ToArray()) if (await radio.SetStateAsync(target) != RadioAccessStatus.Allowed) status.Text = "O Windows não confirmou a alteração do rádio.";
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException) { status.Text = error.Message; }
        finally { busy = false; if (active && !disposed) Render(); }
    }
    private async Task Pair(DeviceInformation device)
    {
        if (busy) return; busy = true; Render();
        try
        {
            if (device.Pairing.IsPaired)
            {
                if (!(await Prompt(device, "Esquecer este dispositivo? Será necessário parear novamente para usá-lo.")).Accepted || !active || disposed) return;
                var unpair = device.Pairing.UnpairAsync(); operation = unpair;
                var result = await unpair; status.Text = "Windows: " + result.Status;
            }
            else
            {
                var custom = device.Pairing.Custom;
                int requestGeneration = generation;
                async void Requested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs request)
                {
                    Windows.Foundation.Deferral? deferral = null;
                    try
                    {
                        deferral = request.GetDeferral();
                        string instruction = request.PairingKind switch
                        {
                            DevicePairingKinds.DisplayPin => "Digite ou confirme este código no dispositivo e, depois, confirme aqui.",
                            DevicePairingKinds.ProvidePin => "Digite o PIN mostrado ou informado pelo dispositivo.",
                            DevicePairingKinds.ConfirmPinMatch => "Confira se este código é o mesmo mostrado no dispositivo.",
                            DevicePairingKinds.ConfirmOnly => "Confirma o pareamento com este dispositivo? O Windows pode solicitar uma confirmação adicional.",
                            _ => ""
                        };
                        if (instruction.Length == 0) return;
                        if ((request.PairingKind is DevicePairingKinds.DisplayPin or DevicePairingKinds.ConfirmPinMatch) && string.IsNullOrWhiteSpace(request.Pin))
                            return; // Never ask the user to confirm a missing code.
                        var reply = await Dispatcher.InvokeAsync(() => requestGeneration == generation && active && !disposed
                            ? Prompt(device, instruction, request.PairingKind,
                                request.PairingKind is DevicePairingKinds.DisplayPin or DevicePairingKinds.ConfirmPinMatch ? request.Pin : "")
                            : Task.FromResult(new BluetoothPairingReply(false))).Task.Unwrap().ConfigureAwait(false);
                        if (!reply.Accepted || requestGeneration != generation || !active || disposed) return;
                        if (request.PairingKind == DevicePairingKinds.ProvidePin) request.Accept(reply.Pin);
                        else request.Accept();
                    }
                    catch (Exception error) when (error is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException or ArgumentException)
                    { await OnUI(() => { if (active && !disposed) status.Text = "O Windows não concluiu a confirmação de pareamento."; }); }
                    finally
                    {
                        try { deferral?.Complete(); }
                        catch (Exception error) when (error is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
                    }
                }
                custom.PairingRequested += Requested;
                try
                {
                    var kinds = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.DisplayPin | DevicePairingKinds.ProvidePin | DevicePairingKinds.ConfirmPinMatch;
                    var pair = custom.PairAsync(kinds, DevicePairingProtectionLevel.Default); operation = pair;
                    var result = await pair;
                    if (active && !disposed) status.Text = result.Status switch
                    {
                        DevicePairingResultStatus.Paired => "Dispositivo pareado. O estado da conexão é atualizado pelo Windows.",
                        DevicePairingResultStatus.AlreadyPaired => "Este dispositivo já está pareado.",
                        _ => "Pareamento: " + result.Status
                    };
                }
                finally { custom.PairingRequested -= Requested; pairingPrompt?.Close(); pairingPrompt = null; }
            }
        }
        catch (Exception error) when (error is UnauthorizedAccessException or System.Runtime.InteropServices.COMException or InvalidOperationException or OperationCanceledException or ArgumentException) { if (active) status.Text = error.Message; }
        finally { operation = null; busy = false; if (active && !disposed) Render(); }
    }
    private async Task<BluetoothPairingReply> Prompt(DeviceInformation device, string instruction,
        DevicePairingKinds kind = DevicePairingKinds.ConfirmOnly, string pin = "")
    {
        if (disposed || !active || preferences is null || pairingPrompt is not null) return new(false);
        var owner = Window.GetWindow(this);
        var prompt = new BluetoothPairingWindow(preferences, device.Name, instruction, kind, pin)
        { Owner = owner, MonitorDeviceOverride = (owner as FloatingWindow)?.MonitorDeviceOverride };
        pairingPrompt = prompt;
        try { prompt.Show(); prompt.Position(); prompt.Activate(); return await prompt.Completion; }
        finally { if (prompt.IsVisible) prompt.Close(); if (ReferenceEquals(pairingPrompt, prompt)) pairingPrompt = null; }
    }
    private static bool TryClassicAddress(DeviceInformation device, out ulong address)
    {
        address = 0;
        return device.Properties.TryGetValue("System.Devices.Aep.ProtocolId", out var protocol)
            && Guid.TryParse(Convert.ToString(protocol), out var id) && id == new Guid("e0cbf06c-cd8b-4647-bb8a-263b43f0f974")
            && device.Properties.TryGetValue("System.Devices.Aep.DeviceAddress", out var value)
            && BluetoothConnection.TryAddress(Convert.ToString(value), out address);
    }
    private async Task Disconnect(DeviceInformation device)
    {
        if (busy || disposed || !active) return;
        if (!device.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var state) || state is not true
            || !TryClassicAddress(device, out ulong address))
        {
            status.Text = "Este dispositivo deve ser conectado ou desconectado pelo gerenciamento do Windows, sem remover o pareamento.";
            SystemActions.Run("ms-settings:bluetooth"); return;
        }
        busy = true; Render();
        try
        {
            if (!(await Prompt(device, "Desconectar agora? O dispositivo continuará pareado e poderá reconectar automaticamente conforme seu funcionamento.")).Accepted || !active || disposed) return;
            status.Text = "Solicitando desconexão ao Windows…";
            var result = await Task.Run(() => BluetoothConnection.Disconnect(address));
            if (active && !disposed) status.Text = result.Message;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException
            or DllNotFoundException or EntryPointNotFoundException or NotSupportedException)
        {
            DiagnosticLog.Record("Desconexão Bluetooth", error);
            if (active && !disposed) status.Text = "Desconexão não confirmada pelo Windows/driver. O pareamento foi mantido. Use as configurações de Bluetooth abaixo.";
        }
        finally { busy = false; if (active && !disposed) Render(); }
    }
    private void StopWatching()
    {
        pairingPrompt?.Close(); pairingPrompt = null;
        try { operation?.Cancel(); } catch (System.Runtime.InteropServices.COMException) { }
        foreach (var radio in radios) radio.StateChanged -= RadioChanged; radios.Clear();
        foreach (var watcher in watchers)
        {
            watcher.Added -= Added; watcher.Updated -= Updated; watcher.Removed -= Removed; watcher.EnumerationCompleted -= Completed; watcher.Stopped -= Stopped;
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted) watcher.Stop();
        }
        watchers.Clear(); unavailableGlyphs.Clear();
    }
    public void Dispose() { disposed = true; active = false; generation++; StopWatching(); devices.Clear(); seenDuringScan.Clear(); glyphs.Clear(); }
}
