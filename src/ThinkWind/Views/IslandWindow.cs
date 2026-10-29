using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
    private readonly DockPanel root = new();
    private readonly StackPanel header = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly IslandModuleBoard modules = new();
    private readonly StackPanel hoverPanel = new();
    private readonly Grid hoverModules = new();
    private readonly Border mediaSlot = new(), outputSlot = new(), microphoneSlot = new(), hoverMediaSlot = new(), hoverOutputSlot = new(), hoverMicrophoneSlot = new();
    private readonly WrapPanel hoverQuick = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock hoverStatus = new() { FontSize = 11, Margin = new Thickness(8), TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly DispatcherTimer hoverEnter = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer hoverLeave = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer activation = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<MiniIslandKind, MiniIslandWindow> minis = new();
    private readonly StackPanel fullPanel = new();
    private readonly Viewbox moduleViewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly Viewbox headerViewport;
    private readonly Viewbox hoverViewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private readonly BrightnessControl brightness;
    private readonly MediaCard media = new();
    private readonly TrayCard trayCard;
    private readonly System.Windows.Controls.Calendar calendar = new() { IsTodayHighlighted = true, SelectionMode = CalendarSelectionMode.SingleDate };
    private readonly TextBlock connectionStatus = new() { FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
    private string moduleStructure = "";
    private readonly IslandMotion motion = new();
    private bool resizeQueued, updatingLayout, dismissingMinis, contentResizePending, readingNetwork;
    private DateTime? outsideSince;
    private DateTime keyboardBusyUntil;
    private bool collapseEnabled;
    private double collapseSeconds = -1;
    private readonly TextBlock clock = new() { FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
    private readonly TextBlock metrics = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4), FontSize = 13 };
    private readonly TextBlock calendarDate = new() { FontSize = 13, TextWrapping = TextWrapping.Wrap };
    private DateTime calendarToday = DateTime.Today;
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
        trayCard = new TrayCard(store);
        calendar.SetResourceReference(StyleProperty, "Think.Calendar");
        calendar.Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        var audio = new AudioService();
        output = new AudioEndpointControl(audio, CoreAudio.Flow.Render);
        microphone = new AudioEndpointControl(audio, CoreAudio.Flow.Capture);
        output.SetCompact(true); microphone.SetCompact(true);
        output.LayoutChanged += ModuleSizeChanged; microphone.LayoutChanged += ModuleSizeChanged;
        headerViewport = new Viewbox { Child = header, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        DockPanel.SetDock(headerViewport, Dock.Top); root.Children.Add(headerViewport);
        hoverModules.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        hoverModules.ColumnDefinitions.Add(new ColumnDefinition());
        var hoverAudio = new StackPanel(); hoverAudio.Children.Add(hoverOutputSlot); hoverAudio.Children.Add(hoverMicrophoneSlot);
        var hoverMedia = HoverCard("Reprodução", hoverMediaSlot); var hoverVolume = HoverCard("Áudio e microfone", hoverAudio);
        Grid.SetColumn(hoverVolume, 1); hoverModules.Children.Add(hoverMedia); hoverModules.Children.Add(hoverVolume);
        hoverPanel.Children.Add(hoverModules); hoverPanel.Children.Add(hoverQuick); hoverPanel.Children.Add(hoverStatus);
        hoverViewport.Child = hoverPanel;
        DockPanel.SetDock(hoverViewport, Dock.Top); root.Children.Add(hoverViewport);
        moduleViewport.Child = modules;
        fullPanel.Children.Add(new AdornerDecoratorHost(moduleViewport));
        root.Children.Add(fullPanel); Surface.Child = root;
        BuildBodies();
        Loaded += (_, _) => { RefreshSettings(); UpdateClock(); timer.Start(); };
        activation.Tick += (_, _) => { if (NearActivationZone && !interaction.MiniOpen) { if (!hoverEnter.IsEnabled) hoverEnter.Start(); } else if (!IsMouseOver) hoverEnter.Stop(); };
        timer.Tick += (_, _) => { UpdateClock(); RefreshAudio(); UpdateHoverStatus(); if (contentResizePending && !ControlsBusy) ModuleSizeChanged(); if (IsVisible && Stage == IslandStage.Full) { if (Store.ModuleVisible("background")) trayCard.Refresh(); if (Store.ModuleVisible("connections")) brightness.Refresh(); } };
        hoverEnter.Tick += (_, _) => { hoverEnter.Stop(); if ((IsMouseOver || NearActivationZone) && !interaction.MiniOpen) Transition(() => interaction.Enter()); };
        hoverLeave.Tick += (_, _) =>
        {
            hoverLeave.Stop();
            if (!Store.Value.IslandAutoCollapse || (Stage == IslandStage.Compact && !interaction.MiniOpen) || !IsVisible) return;
            if (PointerInsideGroup) { CancelCollapse(); return; }
            if (PointerInsideBridge) { WaitForBridge(); return; }
            if (InteractionInProgress) { outsideSince = null; hoverLeave.Interval = TimeSpan.FromMilliseconds(100); hoverLeave.Start(); return; }
            if (outsideSince is null) { ScheduleCollapse(); return; }
            double remaining = Store.Value.IslandCollapseSeconds - (DateTime.UtcNow - outsideSince.Value).TotalSeconds;
            if (remaining > .001) { ScheduleCollapse(); return; }
            Transition(() => { DismissMinis(); interaction.Compact(); }); outsideSince = null;
        };
        MouseEnter += (_, _) =>
        {
            CancelCollapse();
            if (Stage == IslandStage.Compact && !interaction.MiniOpen) { hoverEnter.Stop(); hoverEnter.Start(); }
        };
        MouseLeave += (_, _) => { hoverEnter.Stop(); ScheduleCollapse(); };
        LostMouseCapture += (_, _) => { if (contentResizePending) Dispatcher.BeginInvoke(new Action(ModuleSizeChanged)); };
        Deactivated += (_, _) => ScheduleCollapse();
        PreviewKeyDown += (_, e) =>
        {
            keyboardBusyUntil = DateTime.UtcNow.AddMilliseconds(350);
            if (e.Key != Key.Escape) return;
            if (Store.IsEditing) Store.SetEditing(false);
            DismissMinis();
            Transition(() => { if (Stage == IslandStage.Full) interaction.CloseFull(); else interaction.Compact(); }); e.Handled = true;
        };
        media.LayoutChanged += ModuleSizeChanged;
        trayCard.LayoutChanged += ModuleSizeChanged;
        brightness.LayoutChanged += ModuleSizeChanged;
        LayoutDrag.Completed += DragCompleted;
        Closed += (_, _) => { timer.Stop(); activation.Stop(); hoverEnter.Stop(); hoverLeave.Stop(); motion.Cancel(MotionElements()); LayoutDrag.Completed -= DragCompleted; media.Dispose(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { activation.Stop(); hoverEnter.Stop(); CancelCollapse(); DismissMinis(); motion.Cancel(MotionElements()); media.SetActive(false); } else UpdateStageVisibility(); };
    }
    private void DragCompleted() { RefreshSettings(); ScheduleCollapse(); }
    private Border HoverCard(string title, UIElement child)
    {
        var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = title, FontSize = 10, Margin = new Thickness(0, 0, 0, 6) }); panel.Children.Add(child);
        var card = new Border { Child = panel, Padding = new Thickness(10), Margin = new Thickness(4), CornerRadius = new CornerRadius(18) };
        card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard"); return card;
    }
    private void BuildBodies()
    {
        var connections = new WrapPanel();
        foreach (var control in LayoutCatalog.QuickControls.Where(q => q.Id is "wifi" or "bluetooth" or "airplane"))
        {
            var tile = ActionButton(control.Name, () => { if (control.Id == "wifi") ShowMini(MiniIslandKind.Network); else SystemActions.Run(control.Target!); }, control.Id == "wifi" ? "Gerenciar redes Wi-Fi" : "Abrir configurações de " + control.Name);
            var content = new StackPanel(); content.Children.Add(Glyph(control.Glyph, 22));
            content.Children.Add(new TextBlock { Text = control.Name, FontSize = 11, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            tile.Content = content; tile.Width = 42; tile.Height = 56; tile.Padding = new Thickness(2); tile.Margin = new Thickness(2);
            ((TextBlock)content.Children[1]).FontSize = 8;
            if (control.Id == "airplane") ((TextBlock)content.Children[1]).Text = "Avião";
            connections.Children.Add(tile);
        }
        var connectionPanel = new StackPanel(); connectionPanel.Children.Add(connections); connectionPanel.Children.Add(brightness);
        connectionPanel.Children.Insert(1, connectionStatus);
        var displaySettings = ActionButton("Tela", () => SystemActions.Run("ms-settings:display")); displaySettings.Padding = new Thickness(6, 3, 6, 3); displaySettings.FontSize = 10;
        connectionPanel.Children.Add(displaySettings);
        bodies["connections"] = connectionPanel;
        bodies["media"] = mediaSlot;
        var audio = new StackPanel(); audio.Children.Add(outputSlot); audio.Children.Add(microphoneSlot);
        var audioSettings = new WrapPanel();
        var sound = ActionButton("Som", () => SystemActions.Run("ms-settings:sound")); sound.Padding = new Thickness(6, 3, 6, 3); sound.FontSize = 10; audioSettings.Children.Add(sound);
        var privacy = ActionButton("Microfone", () => SystemActions.Run("ms-settings:privacy-microphone")); privacy.Padding = sound.Padding; privacy.FontSize = 10; audioSettings.Children.Add(privacy);
        audio.Children.Add(audioSettings);
        bodies["audio"] = audio;
        bodies["metrics"] = metrics;
        var calendarPanel = new StackPanel();
        calendarPanel.Children.Add(calendarDate);
        calendarPanel.Children.Add(calendar); bodies["calendar"] = calendarPanel;
        bodies["background"] = trayCard;
        var notifications = new WrapPanel();
        var alerts = ActionButton("Abrir notificações", () => Desktop.Chord(0x5B, 0x4E)); alerts.Content = Glyph("\uE7F4", 20); alerts.Width = 36; alerts.Height = 36; alerts.Padding = new Thickness(4); notifications.Children.Add(alerts);
        var alertSettings = ActionButton("Configurar notificações", () => SystemActions.Run("ms-settings:notifications")); alertSettings.Content = Glyph("\uE713", 16); alertSettings.Width = 36; alertSettings.Height = 36; alertSettings.Padding = new Thickness(4); notifications.Children.Add(alertSettings);
        bodies["notifications"] = notifications;
    }
    public override void RefreshSettings()
    {
        if (LayoutDrag.Active) return;
        ApplyAppearance();
        RefreshCollapsePolicy();
        var before = motion.Capture(MotionElements());
        updatingLayout = true;
        // Reuse audio controls so live styling never resets the selected device.
        string structure = Store.IsEditing + ":" + Store.Value.ShowAudio + ":" + string.Join("|", Store.Value.HiddenModules.OrderBy(id => id));
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
                var card = new Border { Child = content, Tag = id, CornerRadius = new CornerRadius(20), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, UseLayoutRounding = true };
                card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
                LayoutDrag.Attach(card, "modules", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.ModuleOrder, from, to, after)), true, grip);
                cards[id] = card;
            }
            moduleStructure = structure;
        }
        var ordered = Store.Value.ModuleOrder.Where(cards.ContainsKey).ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            var card = cards[ordered[i]]; bool prominent = i < 3;
            card.Margin = new Thickness(0); card.Padding = new Thickness(prominent ? 10 : 8);
            card.CornerRadius = new CornerRadius(prominent ? 20 : 16);
            if (card.Child is StackPanel content && content.Children[0] is DockPanel title)
                foreach (var label in title.Children.OfType<TextBlock>()) label.FontSize = prominent ? 11 : 10;
        }
        modules.Gap = Math.Max(6, Store.Value.IslandSpacing + 2);
        modules.SetCards(ordered.Select(id => cards[id]));
        if (Store.IsEditing) interaction.OpenFull();
        BuildHeader(); UpdateStageVisibility();
        UpdateClock(); Position(); RefreshAudio();
        updatingLayout = false;
        motion.Arrange(this, before, MotionElements(), Store.Value.AnimationMilliseconds);
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
        var standard = new[] { "clock", "wifi", "audio" }.Concat(Store.Value.HiddenQuickControls.Contains("tray") ? Array.Empty<string>() : new[] { "tray" });
        var headerIds = (Store.IsEditing ? Store.Value.QuickOrder.Where(id => id is not ("settings" or "windows-settings") && !Store.Value.HiddenQuickControls.Contains(id)) : standard)
            .Concat(new[] { "windows-settings" }).Concat(Stage == IslandStage.Full ? new[] { "settings" } : Array.Empty<string>()).Distinct();
        foreach (var id in headerIds)
        {
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            if (id == "clock")
            {
                var clockButton = ActionButton("Relógio e calendário", () =>
                {
                    if (Store.IsEditing) return;
                    if (!Store.ModuleVisible("calendar")) Desktop.Chord(0x5B, 0x4E);
                    else ShowMini(MiniIslandKind.Calendar);
                });
                clockButton.Style = (Style)FindResource("Think.IconButton"); clockButton.Content = clock;
                clockButton.Padding = new Thickness(0); clockButton.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
                LayoutDrag.Attach(clockButton, "quick", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, after)));
                header.Children.Add(clockButton); continue;
            }
            var button = ActionButton(control.Name, () =>
            {
                if (Store.IsEditing && id is not ("settings" or "windows-settings")) return;
                if (id == "audio") ShowMini(MiniIslandKind.Volume);
                else if (id == "wifi") ShowMini(MiniIslandKind.Network);
                else if (id == "tray") ShowMini(MiniIslandKind.Tray);
                else if (id == "calendar") ShowMini(MiniIslandKind.Calendar);
                else if (id == "settings") openSettings();
                else if (id == "notifications") Desktop.Chord(0x5B, 0x4E);
                else if (control.Target is not null) SystemActions.Run(control.Target);
            });
            button.Content = Glyph(control.Glyph, 17 * scale);
            button.Style = (Style)FindResource("Think.IconButton");
            button.Width = 32 * scale; button.Height = 32 * scale;
            button.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
            if (id is not ("settings" or "windows-settings")) LayoutDrag.Attach(button, "quick", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, after)));
            header.Children.Add(button);
        }
        if (Stage == IslandStage.Full || Store.IsEditing)
        {
            var close = ActionButton(Store.IsEditing ? "Concluir edição" : "Fechar painel completo", () =>
            { if (Store.IsEditing) Store.SetEditing(false); Transition(() => interaction.CloseFull()); });
            close.Style = (Style)FindResource("Think.IconButton"); close.Content = Glyph("\uE70E", 13);
            close.Width = 32; close.Height = 32; close.Margin = new Thickness(4, 0, 0, 0); header.Children.Add(close);
        }
        else
        {
            var expand = ActionButton("Abrir painel completo", OpenFull);
            expand.Style = (Style)FindResource("Think.IconButton"); expand.Content = Glyph("\uE70D", 13); expand.Width = 28; expand.Height = 32; header.Children.Add(expand);
        }
        foreach (var id in Store.Value.QuickOrder.Where(id => id is not ("clock" or "wifi" or "audio" or "tray" or "settings" or "windows-settings") && !Store.Value.HiddenQuickControls.Contains(id)))
        {
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            var quick = ActionButton(control.Name, () =>
            {
                if (id == "calendar") ShowMini(MiniIslandKind.Calendar);
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
        calendarDate.Text = DateTime.Now.ToString("ddd, dd MMM", CultureInfo.GetCultureInfo("pt-BR"));
        if (calendarToday != DateTime.Today)
        {
            if (calendar.DisplayDate.Year == calendarToday.Year && calendar.DisplayDate.Month == calendarToday.Month) calendar.DisplayDate = DateTime.Today;
            calendarToday = DateTime.Today; calendar.InvalidateVisual();
        }
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
        if (IsPositionAnimating) return;
        if (!IsVisible || Stage == IslandStage.Compact || !Store.ModuleVisible("audio")) return;
        bool refresh = DateTime.UtcNow >= nextDeviceRefresh;
        if (refresh) nextDeviceRefresh = DateTime.UtcNow.AddSeconds(5);
        output.Poll(refresh); microphone.Poll(refresh);
    }
    private void Transition(Action change)
    {
        var before = motion.Capture(MotionElements());
        var previous = Stage; change();
        if (previous == Stage) return;
        BuildHeader(); UpdateStageVisibility(); Resize(true); UpdateClock();
        motion.Arrange(this, before, MotionElements(), Store.Value.AnimationMilliseconds);
        if (!PointerInsideGroup) ScheduleCollapse();
    }
    private void OpenFull()
    {
        calendar.DisplayMode = CalendarMode.Month; calendar.DisplayDate = DateTime.Today;
        DismissMinis(); hoverEnter.Stop(); CancelCollapse();
        Transition(() => interaction.OpenFull());
    }
    private void UpdateStageVisibility()
    {
        hoverViewport.Visibility = Stage == IslandStage.Hover ? Visibility.Visible : Visibility.Collapsed;
        fullPanel.Visibility = Stage == IslandStage.Full ? Visibility.Visible : Visibility.Collapsed;
        bool hover = Stage == IslandStage.Hover;
        if (IsVisible && Stage == IslandStage.Compact) activation.Start(); else activation.Stop();
        Rehost(media, hover ? hoverMediaSlot : mediaSlot);
        Rehost(output, hover ? hoverOutputSlot : outputSlot);
        Rehost(microphone, hover ? hoverMicrophoneSlot : microphoneSlot);
        hoverModules.Children[0].Visibility = Store.ModuleVisible("media") ? Visibility.Visible : Visibility.Collapsed;
        hoverModules.Children[1].Visibility = Store.ModuleVisible("audio") ? Visibility.Visible : Visibility.Collapsed;
        hoverModules.ColumnDefinitions[0].Width = new GridLength(Store.ModuleVisible("media") ? 1.35 : 0, GridUnitType.Star);
        hoverModules.ColumnDefinitions[1].Width = new GridLength(Store.ModuleVisible("audio") ? 1 : 0, GridUnitType.Star);
        media.SetActive(IsVisible && Stage != IslandStage.Compact && Store.ModuleVisible("media"));
    }
    private static void Rehost(FrameworkElement element, Border slot)
    {
        if (ReferenceEquals(element.Parent, slot)) return;
        if (element.Parent is Decorator old) old.Child = null;
        else if (element.Parent is Panel panel) panel.Children.Remove(element);
        slot.Child = element;
    }
    private IEnumerable<FrameworkElement> MotionElements() => Stage == IslandStage.Full ? cards.Values.Cast<FrameworkElement>() : new FrameworkElement[] { media, output };
    private void ModuleSizeChanged()
    {
        if (IsPositionAnimating) { contentResizePending = true; return; }
        if (updatingLayout || resizeQueued || !IsVisible || Stage == IslandStage.Compact || LayoutDrag.Active) return;
        if (ControlsBusy) { contentResizePending = true; return; }
        contentResizePending = false;
        resizeQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            resizeQueued = false;
            if (!IsVisible || Stage == IslandStage.Compact || LayoutDrag.Active) return;
            if (ControlsBusy) { contentResizePending = true; return; }
            var before = motion.Capture(MotionElements()); Resize(true);
            motion.Arrange(this, before, MotionElements(), Store.Value.AnimationMilliseconds);
        }));
    }
    public override void RefreshAppearance()
    {
        base.RefreshAppearance();
        RefreshCollapsePolicy();
        foreach (var mini in minis.Values) if (mini.IsVisible) mini.RefreshAppearance();
    }
    private bool PointerInsideGroup => IsMouseOver || minis.Values.Any(mini => mini.IsVisible && mini.IsMouseOver);
    private bool NearActivationZone
    {
        get
        {
            if (!IsVisible || Stage != IslandStage.Compact) return false;
            var bounds = TargetBounds; var pointer = System.Windows.Forms.Cursor.Position; double margin = 8 * Display.Scale;
            return pointer.X >= bounds.Left - margin && pointer.X <= bounds.Right + margin && pointer.Y >= bounds.Top && pointer.Y <= bounds.Bottom + margin;
        }
    }
    private bool PointerInsideBridge
    {
        get
        {
            var cursor = System.Windows.Forms.Cursor.Position; var main = Bounds;
            foreach (var mini in minis.Values.Where(mini => mini.IsVisible))
            {
                var popup = mini.Bounds;
                if (popup.Top >= main.Bottom && popup.Top - main.Bottom <= 24 * Display.Scale &&
                    cursor.Y >= main.Bottom - 1 && cursor.Y <= popup.Top + 1 &&
                    cursor.X >= Math.Max(main.Left, popup.Left) && cursor.X <= Math.Min(main.Right, popup.Right)) return true;
            }
            return false;
        }
    }
    private bool ControlsBusy => IsMouseCaptureWithin || media.IsInteracting || output.IsInteracting || microphone.IsInteracting || trayCard.IsInteracting || minis.Values.Any(mini => mini.IsVisible && mini.IsInteracting);
    private bool InteractionInProgress => Store.IsEditing || Store.IsCustomizing || LayoutDrag.Active || ControlsBusy || DateTime.UtcNow < keyboardBusyUntil;
    private void CancelCollapse() { hoverLeave.Stop(); outsideSince = null; }
    private void WaitForBridge() { outsideSince = null; hoverLeave.Interval = TimeSpan.FromMilliseconds(50); hoverLeave.Start(); }
    private void ScheduleCollapse()
    {
        hoverLeave.Stop();
        if (dismissingMinis || !IsVisible || (Stage == IslandStage.Compact && !interaction.MiniOpen) || !Store.Value.IslandAutoCollapse) { outsideSince = null; return; }
        if (PointerInsideGroup) { outsideSince = null; return; }
        if (PointerInsideBridge) { WaitForBridge(); return; }
        if (InteractionInProgress) { outsideSince = null; hoverLeave.Interval = TimeSpan.FromMilliseconds(100); hoverLeave.Start(); return; }
        outsideSince ??= DateTime.UtcNow;
        double remaining = Store.Value.IslandCollapseSeconds - (DateTime.UtcNow - outsideSince.Value).TotalSeconds;
        // Even zero delay yields to the input queue so a popup enter can cancel it.
        hoverLeave.Interval = TimeSpan.FromMilliseconds(Math.Max(1, remaining * 1000)); hoverLeave.Start();
    }
    private void RefreshCollapsePolicy()
    {
        bool changed = collapseEnabled != Store.Value.IslandAutoCollapse || collapseSeconds != Store.Value.IslandCollapseSeconds;
        collapseEnabled = Store.Value.IslandAutoCollapse; collapseSeconds = Store.Value.IslandCollapseSeconds;
        if (changed || (outsideSince is null && !PointerInsideGroup)) ScheduleCollapse();
    }
    private void ShowMini(MiniIslandKind kind)
    {
        hoverEnter.Stop(); CancelCollapse();
        if (lastMiniKind == kind && DateTime.UtcNow - lastMiniDismissed < TimeSpan.FromMilliseconds(200)) return;
        if (minis.TryGetValue(kind, out var existing) && existing.IsVisible) { existing.Dismiss(); return; }
        DismissMinis(); interaction.OpenMini();
        if (!minis.TryGetValue(kind, out var mini))
        {
            mini = new MiniIslandWindow(Store, kind, MiniAnchor) { Owner = this, ShowInTaskbar = ShowInTaskbar };
            mini.MouseEnter += (_, _) => CancelCollapse();
            mini.MouseLeave += (_, _) => ScheduleCollapse();
            mini.PreviewKeyDown += (_, _) => { keyboardBusyUntil = DateTime.UtcNow.AddMilliseconds(350); ScheduleCollapse(); };
            mini.Dismissed += () =>
            {
                lastMiniKind = kind; lastMiniDismissed = DateTime.UtcNow;
                if (minis.Values.Any(m => m.IsVisible)) return;
                interaction.CloseMini(); ScheduleCollapse();
            };
            minis[kind] = mini;
        }
        mini.Present();
    }
    private OverlayChrome.PixelRect MiniAnchor()
    {
        var anchor = TargetBounds;
        if (Stage != IslandStage.Compact) anchor.Bottom = anchor.Top + (int)((Math.Max(40, 36 * Store.Value.IslandScale) + 10) * Display.Scale);
        return anchor;
    }
    private void DismissMinis()
    {
        dismissingMinis = true;
        try { foreach (var mini in minis.Values) mini.Dismiss(); interaction.CloseMini(); }
        finally { dismissingMinis = false; }
    }
    private async void UpdateHoverStatus()
    {
        if (!IsVisible || Stage == IslandStage.Compact || readingNetwork || DateTime.UtcNow < nextNetworkRefresh) return;
        readingNetwork = true;
        nextNetworkRefresh = DateTime.UtcNow.AddSeconds(5);
        try
        {
            var network = await Task.Run(NetworkService.Read);
            if (!IsVisible || Stage == IslandStage.Compact) return;
            string text = (network.Connected ? network.Kind + " · " + network.Name : "Sem rede ativa") + (Store.Value.ShowMemory ? $"    RAM {Desktop.MemoryPercent()}%" : "");
            if (hoverStatus.Text != text) { hoverStatus.Text = text; ModuleSizeChanged(); }
            connectionStatus.Text = text;
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        { if (IsVisible && Stage == IslandStage.Hover) { hoverStatus.Text = "Rede · abra o acesso rápido para conferir a conexão"; ModuleSizeChanged(); } }
        finally { readingNetwork = false; }
    }
    public override void Position() => Resize(false);
    private void Resize(bool animate)
    {
        var p = Store.Value;
        Surface.Padding = new Thickness(12, 5, 12, Stage == IslandStage.Compact ? 5 : 12);
        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double compact = Math.Min(Display.Width - 16, Math.Max(220 * p.IslandScale, header.DesiredSize.Width + 32));
        double width = Stage == IslandStage.Compact ? compact : Stage == IslandStage.Hover ? Math.Min(Display.Width - 16, Math.Max(compact, Math.Min(540, p.IslandExpandedWidth))) : Math.Min(Display.Width - 16, p.IslandExpandedWidth);
        headerViewport.Width = Math.Max(1, width - 24);
        double headerHeight = Math.Max(40, 36 * p.IslandScale) + 10;
        double limit = Stage == IslandStage.Full ? Math.Max(120, Math.Min(620, Display.Height * .64)) : CompactLayout.PanelHeight(Display.Height);
        double inner = Math.Max(1, width - 24);
        hoverViewport.Width = inner; hoverPanel.Width = inner;
        hoverViewport.MaxHeight = Math.Max(24, limit - headerHeight - 12);
        double boardWidth = Math.Max(760, inner);
        modules.Width = boardWidth; moduleViewport.Width = inner;
        moduleViewport.MaxHeight = Math.Max(24, limit - headerHeight - 16);
        modules.Measure(new Size(boardWidth, double.PositiveInfinity));
        moduleViewport.Measure(new Size(inner, moduleViewport.MaxHeight));
        hoverPanel.Measure(new Size(Math.Max(1, width - 24), double.PositiveInfinity));
        double height = Stage == IslandStage.Compact ? headerHeight : Stage == IslandStage.Hover ? Math.Min(limit, headerHeight + hoverPanel.DesiredSize.Height + 12) : Math.Min(limit, headerHeight + moduleViewport.DesiredSize.Height + 16);
        // Keep the destination content layout stable while the native frame
        // reveals it; do not reflow the entire module tree on every frame.
        root.Width = inner;
        root.Height = Math.Max(1, height - Surface.Padding.Top - Surface.Padding.Bottom);
        root.HorizontalAlignment = HorizontalAlignment.Center;
        root.VerticalAlignment = VerticalAlignment.Top;
        Place(width, height, animate);
    }
}
