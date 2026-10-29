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
    private readonly StackPanel fullPanel = new();
    private readonly Viewbox moduleViewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Viewbox headerViewport;
    private readonly Viewbox hoverViewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly Viewbox navigationViewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly BrightnessControl brightness;
    private readonly MediaCard media = new();
    private readonly TrayCard trayCard = new();
    private string? selectedModule;
    private string moduleStructure = "";
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
    private DateTime lastMiniDismissed;
    private MiniIslandKind? lastMiniKind;
    public IslandWindow(SettingsStore store, Action settings) : base(store)
    {
        Title = "Think Wind — Dynamic Island"; openSettings = settings;
        brightness = new BrightnessControl(() => Display.Bounds);
        var audio = new AudioService();
        output = new AudioEndpointControl(audio, CoreAudio.Flow.Render);
        microphone = new AudioEndpointControl(audio, CoreAudio.Flow.Capture);
        calendar.SetResourceReference(StyleProperty, "Think.Calendar");
        headerViewport = new Viewbox { Child = header, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        DockPanel.SetDock(headerViewport, Dock.Top); root.Children.Add(headerViewport);
        hoverPanel.Children.Add(hoverQuick); hoverPanel.Children.Add(hoverStatus);
        hoverPanel.Children.Add(ActionButton("Painel completo", () => OpenFull(false)));
        hoverViewport.Child = hoverPanel;
        DockPanel.SetDock(hoverViewport, Dock.Top); root.Children.Add(hoverViewport);
        moduleViewport.Child = modules;
        navigationViewport.Child = navigation;
        fullPanel.Children.Add(navigationViewport); fullPanel.Children.Add(new AdornerDecoratorHost(moduleViewport));
        root.Children.Add(fullPanel); Surface.Child = root;
        BuildBodies();
        Loaded += (_, _) => { RefreshSettings(); UpdateClock(); timer.Start(); };
        timer.Tick += (_, _) => { UpdateClock(); RefreshAudio(); UpdateHoverStatus(); if (IsVisible && Stage == IslandStage.Full) { if (selectedModule == "background") trayCard.Refresh(); if (selectedModule == "connections") brightness.Refresh(); } };
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
        media.AvailabilityChanged += MediaAvailabilityChanged;
        media.LayoutChanged += ModuleSizeChanged;
        trayCard.LayoutChanged += ModuleSizeChanged;
        brightness.LayoutChanged += ModuleSizeChanged;
        LayoutDrag.Completed += DragCompleted;
        Closed += (_, _) => { timer.Stop(); hoverEnter.Stop(); hoverLeave.Stop(); LayoutDrag.Completed -= DragCompleted; media.Dispose(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { hoverEnter.Stop(); hoverLeave.Stop(); DismissMinis(); media.SetActive(false); } else UpdateStageVisibility(); };
    }
    private void DragCompleted() { RefreshSettings(); }
    private void BuildBodies()
    {
        var connections = new WrapPanel();
        foreach (var control in LayoutCatalog.QuickControls.Where(q => q.Id is "wifi" or "bluetooth" or "airplane"))
        {
            var tile = ActionButton(control.Name, () => SystemActions.Run(control.Target!), "Abrir configurações de " + control.Name);
            var content = new StackPanel(); content.Children.Add(Glyph(control.Glyph, 22));
            content.Children.Add(new TextBlock { Text = control.Name, FontSize = 11, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            tile.Content = content; tile.Width = 112; tile.Height = 78; tile.Padding = new Thickness(8); tile.Margin = new Thickness(4);
            connections.Children.Add(tile);
        }
        var connectionPanel = new StackPanel(); connectionPanel.Children.Add(connections); connectionPanel.Children.Add(brightness);
        connectionPanel.Children.Add(ActionButton("Configurações de Tela", () => SystemActions.Run("ms-settings:display")));
        bodies["connections"] = connectionPanel;
        bodies["media"] = media;
        var audio = new StackPanel(); audio.Children.Add(output); audio.Children.Add(microphone);
        audio.Children.Add(new TextBlock { Text = "Controle o volume de cada dispositivo. A troca do padrão fica nas configurações de áudio do Windows.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(8), Opacity = .75 });
        var audioSettings = new WrapPanel();
        audioSettings.Children.Add(ActionButton("Saída padrão", () => SystemActions.Run("ms-settings:sound")));
        audioSettings.Children.Add(ActionButton("Permissões do microfone", () => SystemActions.Run("ms-settings:privacy-microphone")));
        audio.Children.Add(audioSettings);
        bodies["audio"] = audio;
        bodies["metrics"] = metrics;
        var calendarPanel = new StackPanel();
        calendarPanel.Children.Add(ActionButton("Abrir / recolher calendário", () => { calendarOpen = !calendarOpen; calendar.Visibility = calendarOpen ? Visibility.Visible : Visibility.Collapsed; Resize(true); }));
        calendarPanel.Children.Add(calendar); bodies["calendar"] = calendarPanel;
        bodies["background"] = trayCard;
        var notifications = new WrapPanel();
        notifications.Children.Add(ActionButton("Central de notificações do Windows", () => Desktop.Chord(0x5B, 0x4E)));
        notifications.Children.Add(ActionButton("Configurar notificações", () => SystemActions.Run("ms-settings:notifications")));
        bodies["notifications"] = notifications;
    }
    public override void RefreshSettings()
    {
        if (LayoutDrag.Active) return;
        ApplyAppearance();
        // Reuse audio controls so live styling never resets the selected device.
        string structure = Store.IsEditing + ":" + Store.Value.ShowAudio + ":" + string.Join("|", Store.Value.ModuleOrder) + ":" + string.Join("|", Store.Value.HiddenModules);
        if (moduleStructure != structure)
        {
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
                var card = new Border { Child = content, CornerRadius = new CornerRadius(20), Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, Store.Value.IslandSpacing + 4) };
                card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
                LayoutDrag.Attach(card, "modules", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.ModuleOrder, from, to, after)), true, grip);
                cards[id] = card;
            }
            moduleStructure = structure;
        }
        foreach (var card in cards.Values) card.Margin = new Thickness(0, 0, 0, Store.Value.IslandSpacing + 4);
        if (Store.IsEditing) interaction.OpenFull();
        BuildHeader(); UpdateStageVisibility();
        UpdateClock(); Position(); RefreshAudio();
        foreach (var mini in minis.Values) if (mini.IsVisible) mini.RefreshSettings();
    }
    private void BuildHeader()
    {
        foreach (var old in header.Children.OfType<Button>()) if (ReferenceEquals(old.Content, clock)) old.Content = null;
        header.Children.Clear();
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
        if (showCalendar) { ShowModule("calendar"); Resize(true); }
    }
    private void UpdateStageVisibility()
    {
        hoverViewport.Visibility = Stage == IslandStage.Hover ? Visibility.Visible : Visibility.Collapsed;
        fullPanel.Visibility = Stage == IslandStage.Full ? Visibility.Visible : Visibility.Collapsed;
        calendar.Visibility = calendarOpen ? Visibility.Visible : Visibility.Collapsed;
        media.SetActive(IsVisible && Stage == IslandStage.Full && Store.ModuleVisible("media"));
        ShowModule(selectedModule);
    }
    private void MediaAvailabilityChanged()
    {
        if (Stage != IslandStage.Full || LayoutDrag.Active) return;
        ShowModule(selectedModule); Resize(true);
    }
    private void ModuleSizeChanged() { if (IsVisible && Stage == IslandStage.Full && !LayoutDrag.Active) Resize(true); }
    private void ShowModule(string? requested)
    {
        var visible = Store.Value.ModuleOrder.Where(id => cards.ContainsKey(id) && (id != "media" || media.Available || Store.IsEditing)).ToList();
        selectedModule = requested is not null && visible.Contains(requested) ? requested : visible.FirstOrDefault();
        modules.Children.Clear(); navigation.Children.Clear();
        if (selectedModule is null) return;
        modules.Children.Add(cards[selectedModule]);
        int index = visible.IndexOf(selectedModule);
        var back = ActionButton("Seção anterior", () => { ShowModule(visible[index - 1]); Resize(true); });
        back.Content = Glyph("\uE76B", 12); back.Width = 32; back.Height = 30; back.Padding = new Thickness(0); back.IsEnabled = index > 0;
        var next = ActionButton("Próxima seção", () => { ShowModule(visible[index + 1]); Resize(true); });
        next.Content = Glyph("\uE76C", 12); next.Width = 32; next.Height = 30; next.Padding = new Thickness(0); next.IsEnabled = index + 1 < visible.Count;
        if (index > 0) LayoutDrag.Attach(back, "modules", visible[index - 1], () => Store.IsEditing, MoveModule);
        if (index + 1 < visible.Count) LayoutDrag.Attach(next, "modules", visible[index + 1], () => Store.IsEditing, MoveModule);
        navigation.Children.Add(back);
        navigation.Children.Add(new TextBlock { Text = $"{LayoutCatalog.ModuleName(selectedModule)}  ·  {index + 1}/{visible.Count}", FontSize = 11, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center });
        navigation.Children.Add(next);
        if (IsVisible && Stage == IslandStage.Full && selectedModule == "background") trayCard.Refresh();
        if (IsVisible && Stage == IslandStage.Full && selectedModule == "connections") brightness.Refresh();
    }
    private void MoveModule(string source, string target, bool after) => Store.Update(p => LayoutCatalog.Move(p.ModuleOrder, source, target, after));
    public override void RefreshAppearance()
    {
        base.RefreshAppearance();
        foreach (var mini in minis.Values) if (mini.IsVisible) mini.RefreshAppearance();
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
    public override void Position() => Resize(false);
    private void Resize(bool animate)
    {
        var p = Store.Value;
        Surface.Padding = new Thickness(12, 5, 12, Stage == IslandStage.Compact ? 5 : 12);
        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double compact = Math.Min(Display.Width - 16, Math.Max(220 * p.IslandScale, header.DesiredSize.Width + 32));
        double width = Stage == IslandStage.Compact ? compact : Stage == IslandStage.Hover ? Math.Min(Display.Width - 16, Math.Max(compact, 340)) : Math.Min(Display.Width - 16, Math.Max(320, Math.Min(640, p.IslandExpandedWidth)));
        headerViewport.Width = Math.Max(1, width - 24);
        double headerHeight = Math.Max(40, 36 * p.IslandScale) + 10;
        double limit = CompactLayout.PanelHeight(Display.Height);
        double inner = Math.Max(1, width - 24);
        navigationViewport.Width = inner;
        hoverViewport.Width = inner; hoverPanel.Width = inner;
        hoverViewport.MaxHeight = Math.Max(24, limit - headerHeight - 12);
        modules.Width = inner; moduleViewport.Width = inner;
        moduleViewport.MaxHeight = Math.Max(24, limit - headerHeight - 64);
        modules.Measure(new Size(inner, double.PositiveInfinity));
        hoverPanel.Measure(new Size(Math.Max(1, width - 24), double.PositiveInfinity));
        double height = Stage == IslandStage.Compact ? headerHeight : Stage == IslandStage.Hover ? Math.Min(limit, headerHeight + hoverPanel.DesiredSize.Height + 12) : Math.Min(limit, headerHeight + Math.Min(moduleViewport.MaxHeight, modules.DesiredSize.Height) + 56);
        Place(width, height, animate);
    }
}
