using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class IslandWindow : FloatingWindow
{
    protected override bool AttachedTop => true;
    private readonly IslandInteraction interaction = new();
    internal IslandStage Stage => interaction.Stage;
    private bool calendarOpen;
    private readonly DockPanel root = new();
    private readonly StackPanel header = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel modules = new();
    private readonly StackPanel hoverPanel = new();
    private readonly WrapPanel hoverQuick = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock hoverStatus = new() { FontSize = 11, Margin = new Thickness(8), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer hoverEnter = new() { Interval = TimeSpan.FromMilliseconds(180) };
    private readonly DispatcherTimer hoverLeave = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly Dictionary<MiniIslandKind, MiniIslandWindow> minis = new();
    private readonly ScrollViewer scroll;
    private readonly ScrollViewer headerScroll;
    private readonly TextBlock clock = new() { FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
    private readonly TextBlock metrics = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4), FontSize = 13 };
    private readonly System.Windows.Controls.Calendar calendar = new() { Margin = new Thickness(4) };
    private readonly AudioEndpointControl output, microphone;
    private readonly Dictionary<string, Border> cards = new();
    private readonly Dictionary<string, UIElement> bodies = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Action openSettings;
    private DateTime nextDeviceRefresh;
    private DateTime nextNetworkRefresh;
    private DateTime nextProcessRefresh;
    private DateTime lastMiniDismissed;
    private MiniIslandKind? lastMiniKind;
    private bool previewWasOpen;
    public IslandWindow(SettingsStore store, Action settings) : base(store)
    {
        Title = "Think Wind — Dynamic Island"; openSettings = settings;
        var audio = new AudioService();
        output = new AudioEndpointControl(audio, CoreAudio.Flow.Render);
        microphone = new AudioEndpointControl(audio, CoreAudio.Flow.Capture);
        calendar.SetResourceReference(StyleProperty, "Think.Calendar");
        headerScroll = new ScrollViewer { Content = header, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        DockPanel.SetDock(headerScroll, Dock.Top); root.Children.Add(headerScroll);
        hoverPanel.Children.Add(hoverQuick); hoverPanel.Children.Add(hoverStatus);
        hoverPanel.Children.Add(ActionButton("Painel completo", () => OpenFull(false)));
        DockPanel.SetDock(hoverPanel, Dock.Top); root.Children.Add(hoverPanel);
        scroll = new ScrollViewer { Content = modules, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed };
        root.Children.Add(new AdornerDecoratorHost(scroll)); Surface.Child = root;
        BuildBodies();
        Loaded += (_, _) => { RefreshSettings(); UpdateClock(); timer.Start(); };
        timer.Tick += (_, _) => { UpdateClock(); RefreshAudio(); UpdateHoverStatus(); };
        hoverEnter.Tick += (_, _) => { hoverEnter.Stop(); if (IsMouseOver && !interaction.MiniOpen) Transition(() => interaction.Enter()); };
        hoverLeave.Tick += (_, _) =>
        {
            hoverLeave.Stop();
            if (!IsMouseOver && !Store.IsEditing && !Store.IsCustomizing && !LayoutDrag.Active) Transition(() => interaction.Leave());
        };
        MouseEnter += (_, _) =>
        {
            hoverLeave.Stop();
            if (Stage == IslandStage.Compact && !interaction.MiniOpen) { hoverEnter.Stop(); hoverEnter.Start(); }
        };
        MouseLeave += (_, _) => { hoverEnter.Stop(); if (!interaction.MiniOpen) { hoverLeave.Stop(); hoverLeave.Start(); } };
        Deactivated += (_, _) =>
        {
            if (Store.IsEditing || Store.IsCustomizing || LayoutDrag.Active || interaction.MiniOpen) return;
            Transition(() => { interaction.CloseFull(); if (!IsMouseOver) interaction.Compact(); });
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (Store.IsEditing) Store.SetEditing(false);
            DismissMinis();
            Transition(() => { if (Stage == IslandStage.Full) interaction.CloseFull(); else interaction.Compact(); }); e.Handled = true;
        };
        LayoutDrag.Completed += DragCompleted;
        Closed += (_, _) => { timer.Stop(); hoverEnter.Stop(); hoverLeave.Stop(); LayoutDrag.Completed -= DragCompleted; };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { hoverEnter.Stop(); hoverLeave.Stop(); DismissMinis(); } };
    }
    private void DragCompleted() { RefreshSettings(); }
    private void BuildBodies()
    {
        var connections = new WrapPanel();
        foreach (var control in LayoutCatalog.QuickControls.Where(q => q.Target is not null))
            connections.Children.Add(ActionButton(control.Name, () => SystemActions.Run(control.Target!)));
        connections.Children.Add(ActionButton("Saída padrão do Windows", () => SystemActions.Run("ms-settings:sound")));
        connections.Children.Add(ActionButton("Microfone e permissões", () => SystemActions.Run("ms-settings:privacy-microphone")));
        connections.Children.Add(ActionButton("Bandeja do Windows", () => SystemActions.Run("ms-settings:taskbar")));
        bodies["connections"] = connections;
        var media = new WrapPanel();
        media.Children.Add(ActionButton("Anterior", () => Desktop.Key(0xB1)));
        media.Children.Add(ActionButton("Reproduzir / pausar", () => Desktop.Key(0xB3)));
        media.Children.Add(ActionButton("Próxima", () => Desktop.Key(0xB0)));
        bodies["media"] = media;
        var audio = new StackPanel(); audio.Children.Add(output); audio.Children.Add(microphone);
        audio.Children.Add(new TextBlock { Text = "Controle o volume de cada dispositivo. A troca do padrão fica nas configurações de áudio do Windows.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(8), Opacity = .75 });
        bodies["audio"] = audio;
        bodies["metrics"] = metrics;
        var calendarPanel = new StackPanel();
        calendarPanel.Children.Add(ActionButton("Abrir / recolher calendário", () => { calendarOpen = !calendarOpen; calendar.Visibility = calendarOpen ? Visibility.Visible : Visibility.Collapsed; Resize(true); }));
        calendarPanel.Children.Add(calendar); bodies["calendar"] = calendarPanel;
        var background = new StackPanel();
        var running = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(6) };
        background.Children.Add(new ScrollViewer { Content = running, MaxHeight = 140, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        background.Children.Add(ActionButton("Atualizar aplicativos", () => running.Text = RunningProcesses()));
        background.Children.Add(ActionButton("Gerenciador de Tarefas", () => SystemActions.Run("taskmgr.exe")));
        bodies["background"] = background;
        var notifications = new WrapPanel();
        notifications.Children.Add(ActionButton("Central de notificações do Windows", () => Desktop.Chord(0x5B, 0x4E)));
        notifications.Children.Add(ActionButton("Configurar notificações", () => SystemActions.Run("ms-settings:notifications")));
        bodies["notifications"] = notifications;
        background.Tag = running;
    }
    public override void RefreshSettings()
    {
        if (LayoutDrag.Active) return;
        ApplyAppearance();
        // Reuse audio controls so live styling never resets the selected device.
        modules.Children.Clear();
        foreach (var card in cards.Values) { if (card.Child is Panel panel) panel.Children.Clear(); }
        cards.Clear();
        foreach (var id in Store.Value.ModuleOrder)
        {
            if (!Store.ModuleVisible(id)) continue;
            var body = bodies[id];
            if (body is FrameworkElement element && element.Parent is Panel parent) parent.Children.Remove(element);
            var content = new StackPanel();
            var title = new DockPanel { Margin = new Thickness(4, 0, 4, 8) };
            var grip = new TextBlock { Text = Store.IsEditing ? "⋮⋮  " + LayoutCatalog.ModuleName(id) : LayoutCatalog.ModuleName(id), FontSize = 11, FontWeight = FontWeights.SemiBold, Padding = new Thickness(4), Cursor = Store.IsEditing ? Cursors.SizeAll : Cursors.Arrow };
            grip.SetResourceReference(TextBlock.ForegroundProperty, Store.IsEditing ? "Think.Accent" : "Think.Muted");
            if (Store.IsEditing)
            {
                var hide = ActionButton("Ocultar", () => Store.Update(p => p.HiddenModules.Add(id)));
                hide.Padding = new Thickness(8, 3, 8, 3); DockPanel.SetDock(hide, Dock.Right); title.Children.Add(hide);
            }
            title.Children.Add(grip); content.Children.Add(title); content.Children.Add(body);
            var card = new Border { Child = content, CornerRadius = new CornerRadius(14), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, Store.Value.IslandSpacing + 4) };
            card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
            LayoutDrag.Attach(card, "modules", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.ModuleOrder, from, to, after)), true, grip);
            cards[id] = card; modules.Children.Add(card);
        }
        if (previewWasOpen && !Store.IsCustomizing && !Store.IsEditing) interaction.Compact();
        previewWasOpen = Store.IsCustomizing;
        if (Store.IsEditing || Store.IsCustomizing) interaction.OpenFull();
        BuildHeader(); UpdateStageVisibility();
        UpdateClock(); Position(); RefreshAudio();
        foreach (var mini in minis.Values) if (mini.IsVisible) mini.RefreshSettings();
    }
    private void BuildHeader()
    {
        foreach (var old in header.Children.OfType<Button>()) if (ReferenceEquals(old.Content, clock)) old.Content = null;
        header.Children.Clear();
        headerScroll.HorizontalScrollBarVisibility = Store.IsEditing ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        hoverQuick.Children.Clear();
        var scale = Store.Value.IslandScale;
        if (Store.IsEditing)
        {
            var grip = Glyph("\uE784", 15); grip.Width = 22; AttachPositionGrip(grip); header.Children.Add(grip);
        }
        var headerIds = Store.IsEditing ? Store.Value.QuickOrder.Where(id => !Store.Value.HiddenQuickControls.Contains(id)) : new[] { "clock", "wifi", "audio" }.AsEnumerable();
        foreach (var id in headerIds)
        {
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            if (id == "clock")
            {
                var clockButton = ActionButton("Relógio e calendário", () =>
                {
                    if (Store.IsEditing) return;
                    if (!Store.ModuleVisible("calendar")) Desktop.Chord(0x5B, 0x4E);
                    else OpenFull(true);
                });
                clockButton.Style = (Style)FindResource("Think.IconButton"); clockButton.Content = clock;
                clockButton.Padding = new Thickness(0); clockButton.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
                LayoutDrag.Attach(clockButton, "quick", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, after)));
                header.Children.Add(clockButton); continue;
            }
            var button = ActionButton(control.Name, () =>
            {
                if (Store.IsEditing) return;
                if (id == "audio") ShowMini(MiniIslandKind.Volume);
                else if (id == "wifi") ShowMini(MiniIslandKind.Network);
                else if (id == "calendar") OpenFull(true);
                else if (id == "settings") openSettings();
                else if (id == "notifications") Desktop.Chord(0x5B, 0x4E);
                else if (control.Target is not null) SystemActions.Run(control.Target);
            });
            button.Content = Glyph(control.Glyph, 17 * scale);
            button.Style = (Style)FindResource("Think.IconButton");
            button.Width = 32 * scale; button.Height = 32 * scale;
            button.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
            LayoutDrag.Attach(button, "quick", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, after)));
            header.Children.Add(button);
        }
        if (Stage == IslandStage.Full || Store.IsEditing)
        {
            var close = ActionButton(Store.IsEditing ? "Concluir edição" : "Fechar painel completo", () =>
            { if (Store.IsEditing) Store.SetEditing(false); Transition(() => interaction.CloseFull()); });
            close.Style = (Style)FindResource("Think.IconButton"); close.Content = Glyph("\uE70E", 13);
            close.Width = 32; close.Height = 32; close.Margin = new Thickness(4, 0, 0, 0); header.Children.Add(close);
        }
        foreach (var id in Store.Value.QuickOrder.Where(id => id is not ("clock" or "wifi" or "audio") && !Store.Value.HiddenQuickControls.Contains(id)))
        {
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            var quick = ActionButton(control.Name, () =>
            {
                if (id == "calendar") OpenFull(true);
                else if (id == "settings") openSettings();
                else if (id == "notifications") Desktop.Chord(0x5B, 0x4E);
                else if (control.Target is not null) SystemActions.Run(control.Target);
            });
            quick.Style = (Style)FindResource("Think.IconButton"); quick.Content = Glyph(control.Glyph, 18);
            quick.Width = 36; quick.Height = 34; hoverQuick.Children.Add(quick);
        }
    }
    private void UpdateClock()
    {
        clock.Text = DateTime.Now.ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        clock.ToolTip = DateTime.Now.ToString("dddd, dd 'de' MMMM", CultureInfo.GetCultureInfo("pt-BR"));
        if (!IsVisible || Stage != IslandStage.Full || !Store.ModuleVisible("metrics")) return;
        string text = Store.Value.ShowMemory ? $"RAM  {Desktop.MemoryPercent()}%" : "";
        if (Store.Value.ShowBattery)
        {
            var power = System.Windows.Forms.SystemInformation.PowerStatus;
            if (power.BatteryLifePercent is >= 0 and <= 1 && !power.BatteryChargeStatus.HasFlag(System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)) text += $"     Bateria  {power.BatteryLifePercent:P0}";
        }
        metrics.Text = string.IsNullOrWhiteSpace(text) ? "Ative RAM ou bateria em Personalizar." : text;
    }
    private void RefreshAudio()
    {
        if (!IsVisible || Stage != IslandStage.Full || !Store.ModuleVisible("audio")) return;
        bool refresh = DateTime.UtcNow >= nextDeviceRefresh;
        if (refresh) nextDeviceRefresh = DateTime.UtcNow.AddSeconds(5);
        output.Poll(refresh); microphone.Poll(refresh);
    }
    private void Transition(Action change)
    {
        var previous = Stage; change();
        if (previous == Stage) return;
        BuildHeader(); UpdateStageVisibility(); Resize(true); UpdateHoverStatus(); UpdateClock(); RefreshAudio();
    }
    private void OpenFull(bool showCalendar)
    {
        DismissMinis(); hoverEnter.Stop(); hoverLeave.Stop(); calendarOpen = showCalendar;
        Transition(() => interaction.OpenFull());
        UpdateStageVisibility(); Resize(true);
        if (showCalendar) Dispatcher.BeginInvoke(new Action(() => cards.GetValueOrDefault("calendar")?.BringIntoView()));
    }
    private void UpdateStageVisibility()
    {
        hoverPanel.Visibility = Stage == IslandStage.Hover ? Visibility.Visible : Visibility.Collapsed;
        scroll.Visibility = Stage == IslandStage.Full ? Visibility.Visible : Visibility.Collapsed;
        calendar.Visibility = calendarOpen ? Visibility.Visible : Visibility.Collapsed;
        if (Stage == IslandStage.Full && Store.ModuleVisible("background") && DateTime.UtcNow >= nextProcessRefresh && bodies["background"] is StackPanel background && background.Tag is TextBlock text)
        { text.Text = RunningProcesses(); nextProcessRefresh = DateTime.UtcNow.AddSeconds(5); }
    }
    private void ShowMini(MiniIslandKind kind)
    {
        hoverEnter.Stop(); hoverLeave.Stop();
        if (lastMiniKind == kind && DateTime.UtcNow - lastMiniDismissed < TimeSpan.FromMilliseconds(200)) return;
        if (minis.TryGetValue(kind, out var existing) && existing.IsVisible) { existing.Dismiss(); return; }
        DismissMinis(); interaction.OpenMini();
        if (!minis.TryGetValue(kind, out var mini))
        {
            mini = new MiniIslandWindow(Store, kind, MiniAnchor) { Owner = this, ShowInTaskbar = ShowInTaskbar };
            mini.Dismissed += () =>
            {
                lastMiniKind = kind; lastMiniDismissed = DateTime.UtcNow;
                if (minis.Values.Any(m => m.IsVisible)) return;
                interaction.CloseMini(); hoverLeave.Stop(); hoverLeave.Start();
            };
            minis[kind] = mini;
        }
        mini.Present();
    }
    private OverlayChrome.PixelRect MiniAnchor()
    {
        var anchor = TargetBounds;
        if (Stage == IslandStage.Full) anchor.Bottom = anchor.Top + (int)((Math.Max(40, 36 * Store.Value.IslandScale) + 10) * Display.Scale);
        return anchor;
    }
    private void DismissMinis() { foreach (var mini in minis.Values) mini.Dismiss(); interaction.CloseMini(); }
    private void UpdateHoverStatus()
    {
        if (!IsVisible || Stage != IslandStage.Hover || DateTime.UtcNow < nextNetworkRefresh) return;
        nextNetworkRefresh = DateTime.UtcNow.AddSeconds(5);
        try
        {
            var network = NetworkService.Read();
            hoverStatus.Text = (network.Connected ? network.Kind + " · " + network.Name : "Sem rede ativa") + (Store.Value.ShowMemory ? $"    RAM {Desktop.MemoryPercent()}%" : "");
        }
        catch (System.Net.NetworkInformation.NetworkInformationException) { hoverStatus.Text = "Rede · abra o acesso rápido para conferir a conexão"; }
    }
    private static string RunningProcesses()
    {
        var names = new List<string>();
        using var current = System.Diagnostics.Process.GetCurrentProcess();
        foreach (var process in System.Diagnostics.Process.GetProcesses())
        {
            using (process)
            {
                try { if (process.SessionId == current.SessionId && process.Id != current.Id) names.Add(process.ProcessName); }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }
        return string.Join("  ·  ", names.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n));
    }
    public override void Position() => Resize(false);
    private void Resize(bool animate)
    {
        var p = Store.Value;
        Surface.Padding = new Thickness(12, 5, 12, Stage == IslandStage.Compact ? 5 : 12);
        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double compact = Math.Min(Display.Width - 16, Math.Max(220 * p.IslandScale, header.DesiredSize.Width + 32));
        double width = Stage == IslandStage.Compact ? compact : Stage == IslandStage.Hover ? Math.Min(Display.Width - 16, Math.Max(compact, 340)) : Math.Min(Display.Width - 16, Math.Max(compact, p.IslandExpandedWidth));
        double headerHeight = Math.Max(40, 36 * p.IslandScale) + 10;
        if (header.DesiredSize.Width + 24 > width) headerHeight += SystemParameters.HorizontalScrollBarHeight;
        modules.Measure(new Size(Math.Max(1, width - 40), double.PositiveInfinity));
        hoverPanel.Measure(new Size(Math.Max(1, width - 24), double.PositiveInfinity));
        double height = Stage == IslandStage.Compact ? headerHeight : Stage == IslandStage.Hover ? headerHeight + hoverPanel.DesiredSize.Height + 12 : Math.Min(Display.Height - 8, headerHeight + modules.DesiredSize.Height + 16);
        scroll.MaxHeight = Math.Max(0, Display.Height - headerHeight - 24);
        Place(width, height, animate);
    }
}
