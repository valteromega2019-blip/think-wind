using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class WifiControl : StackPanel, IDisposable
{
    private readonly WifiService service = new();
    private readonly ComboBox adapters = new() { DisplayMemberPath = nameof(WifiAdapterInfo.Name), Margin = new Thickness(4), MinHeight = 30 };
    private readonly StackPanel networks = new();
    private readonly TextBlock status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
    private readonly Button radio = new() { Padding = new Thickness(10, 5, 10, 5) };
    private readonly StackPanel credentials = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(4) };
    private readonly PasswordBox password = new() { MaxLength = 64, Padding = new Thickness(10), MinHeight = 34 };
    private readonly TextBlock selectedName = new() { FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 8) };
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private WifiSnapshot snapshot = new(Array.Empty<WifiAdapterInfo>(), Array.Empty<WifiNetwork>());
    private WifiNetwork? selected;
    private bool active, disposed, reading, operating, updating;
    private DateTime nextScan;
    private int page, menus;
    internal bool IsInteracting => operating || menus > 0 || credentials.Visibility == Visibility.Visible || adapters.IsDropDownOpen || IsMouseCaptureWithin;
    internal bool IsRefreshing => reading;
    internal event Action? LayoutChanged;
    internal WifiControl()
    {
        var commands = new WrapPanel(); commands.Children.Add(radio);
        commands.Children.Add(Button("Atualizar redes", () => Refresh(true)));
        Children.Add(adapters); Children.Add(commands); Children.Add(networks); Children.Add(navigation);
        System.Windows.Automation.AutomationProperties.SetName(adapters, "Adaptador Wi-Fi");
        System.Windows.Automation.AutomationProperties.SetName(password, "Senha da rede Wi-Fi");
        password.SetResourceReference(PasswordBox.BackgroundProperty, "Think.Control"); password.SetResourceReference(PasswordBox.ForegroundProperty, "Think.Text");
        credentials.Children.Add(selectedName); credentials.Children.Add(password);
        var actions = new WrapPanel(); actions.Children.Add(Button("Conectar", async () => await ConnectSelected())); actions.Children.Add(Button("Cancelar", ClearCredentials)); credentials.Children.Add(actions);
        Children.Add(credentials); Children.Add(status);
        adapters.SelectionChanged += (_, _) => { if (!updating) { ClearCredentials(); page = 0; Render(); } };
        radio.Click += async (_, _) =>
        {
            if (operating || adapters.SelectedItem is not WifiAdapterInfo adapter || !adapter.CanToggle) return;
            await Operate(() => service.SetRadioAsync(adapter.Id, adapter.RadioOn != true), "Alterando o rádio Wi-Fi…");
        };
        timer.Tick += (_, _) => Refresh(false);
    }
    private static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Padding = new Thickness(10, 5, 10, 5), FontSize = 11 };
        button.Click += (_, _) => action(); return button;
    }
    internal void SetActive(bool value)
    {
        active = value;
        if (value) { timer.Start(); Refresh(false); }
        else { timer.Stop(); ClearCredentials(); }
    }
    internal async void Refresh(bool scan)
    {
        if (!active || disposed || reading || operating || IsInteracting) return;
        reading = true;
        try
        {
            bool requestScan = scan || DateTime.UtcNow >= nextScan;
            if (requestScan) nextScan = DateTime.UtcNow.AddSeconds(30);
            var result = await service.ReadAsync(requestScan).ConfigureAwait(false);
            await OnUI(() => { if (!disposed && active && !operating) Apply(result); });
        }
        catch (Exception error) when (error is WifiUnavailableException or System.Runtime.InteropServices.COMException or TaskCanceledException)
        { await OnUI(() => { if (active && !disposed) { status.Text = error.Message; LayoutChanged?.Invoke(); } }); }
        finally { await OnUI(() => reading = false); }
    }
    private Task OnUI(Action action)
    {
        if (Dispatcher.HasShutdownStarted) return Task.CompletedTask;
        if (Dispatcher.CheckAccess()) { action(); return Task.CompletedTask; }
        return Dispatcher.InvokeAsync(action).Task;
    }
    private void Apply(WifiSnapshot result)
    {
        Guid id = (adapters.SelectedItem as WifiAdapterInfo)?.Id ?? Guid.Empty; snapshot = result; updating = true;
        adapters.ItemsSource = result.Adapters; adapters.SelectedItem = result.Adapters.FirstOrDefault(adapter => adapter.Id == id) ?? result.Adapters.FirstOrDefault(); updating = false;
        if (credentials.Visibility == Visibility.Visible && selected is not null && !result.Networks.Any(network => network.Key == selected.Key)) ClearCredentials();
        Render();
    }
    private void Render()
    {
        var adapter = adapters.SelectedItem as WifiAdapterInfo;
        radio.IsEnabled = !operating && adapter?.CanToggle == true;
        radio.Content = adapter?.RadioOn == true ? "Wi-Fi ligado" : adapter?.RadioOn == false ? "Wi-Fi desligado" : "Rádio indisponível";
        radio.SetResourceReference(Control.BackgroundProperty, adapter?.RadioOn == true ? "Think.AccentSurface" : "Think.Control");
        adapters.IsEnabled = !operating;
        var items = snapshot.Networks.Where(network => network.Adapter == adapter?.Id).ToArray();
        int pages = Math.Max(1, (items.Length + 3) / 4); page = Math.Clamp(page, 0, pages - 1);
        networks.Children.Clear(); navigation.Children.Clear();
        foreach (var network in items.Skip(page * 4).Take(4))
        {
            var button = new Button { HorizontalContentAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(4, 2, 4, 2), Padding = new Thickness(10, 6, 10, 6), IsEnabled = !operating && (network.Connectable || network.Connected) };
            var row = new DockPanel(); var signal = new TextBlock { Text = $"{network.Signal}%" + (network.Connected ? "  ✓" : network.Secure ? "  •" : ""), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(signal, Dock.Right); row.Children.Add(signal);
            row.Children.Add(new TextBlock { Text = network.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            button.Content = row; button.ToolTip = network.Connected ? "Desconectar desta rede" : network.Profile.Length > 0 ? "Conectar usando o perfil do Windows" : "Conectar à rede";
            button.SetResourceReference(Control.BackgroundProperty, network.Connected ? "Think.AccentSurface" : "Think.Control");
            button.Click += async (_, _) =>
            {
                if (network.Connected) await Operate(() => service.DisconnectAsync(network.Adapter), "Desconectando…");
                else if (network.Profile.Length > 0 || !network.Secure) { selected = network; await ConnectSelected(); }
                else if (!network.CanCreateProfile) { status.Text = "Rede corporativa ou segurança não suportada: configure um perfil no Windows antes de conectar."; LayoutChanged?.Invoke(); }
                else { selected = network; selectedName.Text = network.Name; credentials.Visibility = Visibility.Visible; password.Focus(); LayoutChanged?.Invoke(); }
            };
            if (network.Profile.Length > 0 && network.CanCreateProfile)
            {
                var menu = new ContextMenu(); var change = new MenuItem { Header = "Usar outra senha nesta conexão" };
                change.Click += (_, _) => { selected = network with { Profile = "" }; selectedName.Text = network.Name; credentials.Visibility = Visibility.Visible; password.Focus(); LayoutChanged?.Invoke(); };
                menu.Opened += (_, _) => menus++; menu.Closed += (_, _) => menus = Math.Max(0, menus - 1);
                menu.Items.Add(change); button.ContextMenu = menu;
            }
            networks.Children.Add(button);
        }
        if (pages > 1)
        {
            var back = Button("‹", () => { page--; Render(); }); back.IsEnabled = page > 0 && !operating;
            var next = Button("›", () => { page++; Render(); }); next.IsEnabled = page + 1 < pages && !operating;
            navigation.Children.Add(back); navigation.Children.Add(new TextBlock { Text = $"{page + 1}/{pages}", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }); navigation.Children.Add(next);
        }
        if (!operating)
        {
            var connected = items.FirstOrDefault(network => network.Connected);
            status.Text = adapter is null ? "Nenhum adaptador Wi-Fi disponível." : adapter.Error ?? (connected is not null ? "Conectado a " + connected.Name : adapter.RadioOn == false ? "Wi-Fi desligado." : "Nenhuma conexão Wi-Fi ativa.");
        }
        LayoutChanged?.Invoke();
    }
    private async Task ConnectSelected()
    {
        var network = selected; if (network is null || operating) return;
        using var secret = password.SecurePassword.Copy(); password.Clear();
        await Operate(() => service.ConnectAsync(network, secret), "Conectando a " + network.Name + "…");
    }
    private async Task Operate(Func<Task<WifiSnapshot>> action, string message)
    {
        if (operating || disposed) return;
        operating = true; status.Text = message; Render(); credentials.IsEnabled = false;
        try { var result = await action().ConfigureAwait(false); await OnUI(() => { if (!disposed && active) { ClearCredentials(); operating = false; Apply(result); } }); }
        catch (Exception error) when (error is WifiUnavailableException or TaskCanceledException or System.Runtime.InteropServices.COMException)
        { await OnUI(() => { if (!disposed && active) { operating = false; Render(); status.Text = error.Message; LayoutChanged?.Invoke(); } }); }
        finally { await OnUI(() => { operating = false; if (!disposed) credentials.IsEnabled = true; }); }
    }
    internal void ClearCredentials() { password.Clear(); selected = null; credentials.Visibility = Visibility.Collapsed; LayoutChanged?.Invoke(); }
    public void Dispose() { disposed = true; timer.Stop(); password.Clear(); service.Dispose(); }
}
