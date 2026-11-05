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
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed partial class IslandWindow : FloatingWindow
{
    protected override bool AttachedTop => true;
    protected override bool SquareTopCorners => Stage == IslandStage.Compact;
    private readonly IslandInteraction interaction = new();
    internal IslandStage Stage => interaction.Stage;
    private readonly DockPanel root = new();
    private readonly StackPanel header = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly IslandModuleBoard modules = new();
    private readonly IslandLayoutHost stableLayout = new();
    private readonly StackPanel hoverPanel = new();
    private readonly Grid hoverModules = new();
    private readonly Border mediaSlot = new(), outputSlot = new(), microphoneSlot = new(), hoverMediaSlot = new(), hoverOutputSlot = new(), hoverMicrophoneSlot = new();
    private readonly WrapPanel hoverQuick = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Viewbox secondaryQuickViewport = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly DispatcherTimer hoverEnter = new() { Interval = TimeSpan.FromMilliseconds(80) };
    private readonly DispatcherTimer hoverLeave = new() { Interval = TimeSpan.FromMilliseconds(450) };
    private readonly DispatcherTimer activation = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<MiniIslandKind, MiniIslandWindow> minis = new();
    private readonly StackPanel fullPanel = new();
    private readonly IslandModuleViewport moduleViewport = new();
    private readonly Viewbox headerViewport;
    private readonly Border hoverViewport = new();
    private readonly BrightnessControl brightness;
    private readonly MediaCard media = new();
    private readonly WeatherWidget weather;
    private readonly WidgetArea widgetArea;
    private readonly System.Windows.Controls.Calendar calendar = new() { IsTodayHighlighted = true, SelectionMode = CalendarSelectionMode.SingleDate };
    private readonly TextBlock connectionStatus = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
    private string moduleStructure = "";
    private readonly IslandMotion motion = new();
    private bool resizeQueued, updatingLayout, dismissingMinis, contentResizePending, readingNetwork;
    private int expansionRevision;
    private bool preparingFullReveal;
    private bool escapeDismissed;
    private DateTime? outsideSince;
    private OutsideClickObserver? outsidePanelClicks;
    private DateTime keyboardBusyUntil;
    private bool collapseEnabled;
    private double collapseSeconds = -1;
    private readonly TextBlock clock = new() { FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
    private readonly TextBlock headerDate = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock barDate = new() { VerticalAlignment = VerticalAlignment.Center };
    private Button DateButton(bool statusBar)
    {
        var label = statusBar ? barDate : headerDate;
        if (label.Parent is Panel oldParent) oldParent.Children.Remove(label);
        label.FontSize = statusBar ? 12 : 14;
        label.Text = DateTime.Now.ToString("dd/MM", CultureInfo.GetCultureInfo("pt-BR"));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var icon = Glyph("\uE787", statusBar ? 14 : 16);
        icon.Margin = new Thickness(0, 0, 5, 0); row.Children.Add(icon); row.Children.Add(label);
        var button = ActionButton("Abrir calendário", () => { });
        button.Click += (_, _) => { if (!Store.IsEditing) ShowMini(MiniIslandKind.Calendar, statusBar ? button : null); };
        button.Content = row; button.Style = (Style)FindResource("Think.IconButton");
        button.Padding = new Thickness(5, 0, 5, 0);
        button.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
        button.ToolTip = DateTime.Now.ToString("dddd, dd 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("pt-BR"));
        return MarkPanelIcon(button, "calendar");
    }
    private readonly CompactMonitoringWidget metrics;
    private readonly TextBlock calendarDate = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap };
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
        IsVisibleChanged += (_, _) => SyncEditorBackdrop();
        Closed += (_, _) => { editorBackdrop?.Dispose(); editorBackdrop = null; };
        PreserveKeyboardTarget();
        metrics = new CompactMonitoringWidget(store, ((App)Application.Current).Telemetry);
        metrics.LayoutChanged += ModuleSizeChanged;
        stableLayout.Child = root;
        InitializeHeaderMagnification();
        Title = "Orby Launcher — Central de controle"; openSettings = settings;
        weather = new WeatherWidget(store, compact: true);
        widgetArea = new WidgetArea(store);
        weather.ReadingChanged += RefreshHeaderWeather;
        brightness = new BrightnessControl(() => Display.Bounds);
        calendar.SetResourceReference(StyleProperty, "Think.Calendar");
        calendar.FontSize = 14;
        InitializeCalendarAgenda();
        calendar.Loaded += (_, _) => ModuleSizeChanged();
        calendar.DisplayDateChanged += (_, _) => ModuleSizeChanged();
        calendar.SizeChanged += (_, _) => FitCalendarWidth();
        calendar.Language = System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        var audio = new AudioService();
        output = new AudioEndpointControl(audio, CoreAudio.Flow.Render);
        microphone = new AudioEndpointControl(audio, CoreAudio.Flow.Capture);
        output.SetCompact(true); microphone.SetCompact(true);
        output.LayoutChanged += ModuleSizeChanged; microphone.LayoutChanged += ModuleSizeChanged;
        headerViewport = new Viewbox { Child = new Border { Child = header, Padding = new Thickness(8, 4, 8, 4) }, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
        DockPanel.SetDock(headerViewport, Dock.Top); root.Children.Add(headerViewport);
        secondaryQuickViewport.Child = new Border { Child = hoverQuick, Padding = new Thickness(8, 4, 8, 4) };
        DockPanel.SetDock(secondaryQuickViewport, Dock.Top); root.Children.Add(secondaryQuickViewport);
        hoverModules.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        hoverModules.ColumnDefinitions.Add(new ColumnDefinition());
        var hoverAudio = new Grid();
        hoverAudio.RowDefinitions.Add(new RowDefinition()); hoverAudio.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(hoverMicrophoneSlot, 1);
        hoverAudio.Children.Add(hoverOutputSlot); hoverAudio.Children.Add(hoverMicrophoneSlot);
        var hoverMedia = HoverCard("Reprodução", hoverMediaSlot, true); var hoverVolume = HoverCard("Áudio e microfone", hoverAudio);
        Grid.SetColumn(hoverVolume, 1); hoverModules.Children.Add(hoverMedia); hoverModules.Children.Add(hoverVolume);
        hoverPanel.Children.Add(hoverModules);
        hoverViewport.Child = hoverPanel;
        DockPanel.SetDock(hoverViewport, Dock.Top); root.Children.Add(hoverViewport);
        moduleViewport.Child = modules;
        fullPanel.Children.Add(new AdornerDecoratorHost(moduleViewport));
        root.Children.Add(fullPanel); Surface.Child = stableLayout;
        InitializeSecondArea();
        BuildBodies();
        Loaded += (_, _) => { RefreshSettings(); UpdateClock(); timer.Start(); };
        activation.Tick += (_, _) =>
        {
            if (escapeDismissed)
            {
                if (!IsMouseOver && !NearActivationZone && !PointerInsideGroup) escapeDismissed = false;
                else return;
            }
            if (NearActivationZone && !interaction.MiniOpen) { if (!hoverEnter.IsEnabled) hoverEnter.Start(); } else if (!IsMouseOver) hoverEnter.Stop();
        };
        timer.Tick += (_, _) => { RefreshAdaptiveReveal(); UpdateClock(); RefreshAudio(); UpdateHoverStatus(); weather.Poll(); if (contentResizePending && !ControlsBusy) ModuleSizeChanged(); if (IsVisible && Stage == IslandStage.Full && Store.ModuleVisible("connections")) brightness.Refresh(); };
        hoverEnter.Tick += (_, _) => { hoverEnter.Stop(); if (!escapeDismissed && !edgeReduced && (IsMouseOver || NearActivationZone) && !interaction.MiniOpen) Transition(() => interaction.Enter()); };
        hoverLeave.Tick += (_, _) =>
        {
            hoverLeave.Stop();
            if (!AdaptiveCollapse || (Stage == IslandStage.Compact && !interaction.MiniOpen) || !IsVisible) return;
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
            if (!escapeDismissed && !edgeReduced && Stage == IslandStage.Compact && !interaction.MiniOpen) { hoverEnter.Stop(); hoverEnter.Start(); }
        };
        MouseLeave += (_, _) => { if (!NearActivationZone) escapeDismissed = false; hoverEnter.Stop(); ScheduleCollapse(); };
        LostMouseCapture += (_, _) => { if (contentResizePending) Dispatcher.BeginInvoke(new Action(ModuleSizeChanged)); };
        Deactivated += (_, _) => ScheduleCollapse();
        Loaded += (_, _) =>
        {
            outsidePanelClicks ??= new OutsideClickObserver(this, () =>
            {
                if (Store.IsEditing || InteractionInProgress || PointerInsideGroup) return;
                escapeDismissed = true; hoverEnter.Stop(); CancelCollapse();
                systemTools?.Close(); leftArea?.Hide(); DismissMinis();
                Transition(() => interaction.Compact()); RefreshPanelIcons();
            }, () => PointerInsideGroup);
            if (Stage != IslandStage.Compact || interaction.MiniOpen) outsidePanelClicks.Start();
        };
        Closed += (_, _) => outsidePanelClicks?.Dispose();
        IsVisibleChanged += (_, _) => { if (!IsVisible) outsidePanelClicks?.Stop(); };
        PreviewKeyDown += (_, e) => keyboardBusyUntil = DateTime.UtcNow.AddMilliseconds(350);
        // Bubble after controls have had the chance to dismiss their own popup.
        KeyDown += (_, e) =>
        {
            if (e.Handled || e.Key != Key.Escape) return;
            DismissFromEscape(); e.Handled = true;
        };
        media.LayoutChanged += ModuleSizeChanged;
        brightness.LayoutChanged += ModuleSizeChanged;
        LayoutDrag.Completed += DragCompleted;
        Closed += (_, _) => { timer.Stop(); activation.Stop(); hoverEnter.Stop(); hoverLeave.Stop(); motion.Cancel(MotionElements()); LayoutDrag.Completed -= DragCompleted; media.Dispose(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { timer.Stop(); activation.Stop(); hoverEnter.Stop(); CancelCollapse(); DismissMinis(); motion.Cancel(MotionElements()); media.SetActive(false); } else { timer.Start(); UpdateStageVisibility(); } };
        IsVisibleChanged += (_, _) => { if (!IsVisible) statusWindow?.Hide(); else SyncStatusSurface(); };
        Closed += (_, _) => statusWindow?.Close();
        IsVisibleChanged += (_, _) => RefreshApplicationInset();
        Closed += (_, _) => applicationInset.Dispose();
        Closed += (_, _) => fixedReservation?.Dispose();
        Closed += (_, _) => { weather.ReadingChanged -= RefreshHeaderWeather; weather.Dispose(); widgetArea.Dispose(); };
        IsVisibleChanged += (_, _) => { if (!IsVisible) { weather.SetActive(false); weather.SetHeaderActive(false); systemTools?.Close(); } };
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible && Stage == IslandStage.Full)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                { if (IsVisible && Stage == IslandStage.Full && !preparingFullReveal) Resize(false); }));
        };
    }
    private void DragCompleted() { RefreshSettings(); ScheduleCollapse(); }
    private readonly List<Border> hoverCards = new();
    private double WidgetRadius => Math.Clamp(Store.Value.IslandRadius, 0, 28);
    private void RefreshWidgetCorners()
    {
        foreach (var card in cards.Values.Concat(hoverCards)) card.CornerRadius = new CornerRadius(WidgetRadius);
        secondArea.CornerRadius = new CornerRadius(WidgetRadius);
        media.ConfigureWidgetCorners(WidgetRadius);
        if (leftArea?.IsVisible == true) leftArea.RefreshSettings();
    }
    private Border HoverCard(string title, UIElement child, bool fullBleed = false)
    {
        var card = new Border { Child = child, Padding = new Thickness(fullBleed ? 0 : 10), Margin = new Thickness(4), CornerRadius = new CornerRadius(20) };
        System.Windows.Automation.AutomationProperties.SetName(card, title);
        card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard"); hoverCards.Add(card); return card;
    }
    private void BuildBodies()
    {
        var connections = new WrapPanel();
        foreach (var control in LayoutCatalog.QuickControls.Where(q => q.Id is "wifi" or "bluetooth" or "airplane"))
        {
            var tile = ActionButton(control.Name, () => { if (control.Id == "wifi") ShowMini(MiniIslandKind.Network); else if (control.Id == "bluetooth") ShowMini(MiniIslandKind.Bluetooth); else SystemActions.Run(control.Target!); }, control.Id == "wifi" ? "Gerenciar redes Wi-Fi" : "Abrir configurações de " + control.Name);
            var content = new StackPanel(); content.Children.Add(Glyph(control.Glyph, 22));
            content.Children.Add(new TextBlock { Text = control.Name, FontSize = 11, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
            tile.Content = content; tile.MinWidth = 52; tile.MinHeight = 62; tile.Padding = new Thickness(2); tile.Margin = new Thickness(2);
            ((TextBlock)content.Children[1]).FontSize = 11;
            if (control.Id == "airplane") ((TextBlock)content.Children[1]).Text = "Avião";
            connections.Children.Add(tile);
        }
        var connectionPanel = new StackPanel(); connectionPanel.Children.Add(connections); connectionPanel.Children.Add(brightness);
        connectionPanel.Children.Insert(1, connectionStatus);
        var displaySettings = ActionButton("Tela", () => SystemActions.Run("ms-settings:display")); displaySettings.Padding = new Thickness(6, 3, 6, 3); displaySettings.FontSize = 12;
        connectionPanel.Children.Add(displaySettings);
        bodies["connections"] = connectionPanel;
        bodies["media"] = mediaSlot;
        var audio = new Grid();
        audio.RowDefinitions.Add(new RowDefinition());
        audio.RowDefinitions.Add(new RowDefinition());
        audio.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Stretch the real controls across each row. Uniform Viewboxes with a
        // fixed 280-DIP child letterboxed them whenever row height limited scale.
        outputSlot.HorizontalAlignment = microphoneSlot.HorizontalAlignment = HorizontalAlignment.Stretch;
        outputSlot.Margin = new Thickness(0, 2, 0, 4);
        microphoneSlot.Margin = new Thickness(0, 4, 0, 2);
        Grid.SetRow(microphoneSlot, 1);
        audio.Children.Add(outputSlot); audio.Children.Add(microphoneSlot);
        var audioSettings = new WrapPanel();
        var sound = ActionButton("Som", () => SystemActions.Run("ms-settings:sound")); sound.Padding = new Thickness(6, 3, 6, 3); sound.FontSize = 12; audioSettings.Children.Add(sound);
        var privacy = ActionButton("Microfone", () => SystemActions.Run("ms-settings:privacy-microphone")); privacy.Padding = sound.Padding; privacy.FontSize = 12; audioSettings.Children.Add(privacy);
        Grid.SetRow(audioSettings, 2);
        audio.Children.Add(audioSettings);
        bodies["audio"] = audio;
        bodies["metrics"] = metrics;
        var calendarPanel = new StackPanel();
        calendarPanel.Children.Add(calendarDate);
        calendarPanel.Children.Add(calendar);
        calendarPanel.Children.Add(ClockDesign.Button("\uE787", "Ver agenda", () => OpenWidgetAgenda(false)));
        bodies["calendar"] = calendarPanel;
        bodies["calculator"] = new CalculatorWidget();
        bodies["weather"] = weather;
        bodies["notes"] = widgetArea.Notes;
        bodies["favorites"] = widgetArea.Favorites;
    }
    public override void RefreshSettings()
    {
        // Secondary displays keep their applied layout until editing is completed.
        if (Store.IsEditing && !IsPrimaryEditor) return;
        if (!Store.IsEditing) stableFullPanelWidth = stableFullPanelHeight = null;
        RememberEditingPanel();
        if (LayoutDrag.Active) return;
        ApplyAppearance();
        weather.Configure();
        RefreshCollapsePolicy();
        var before = motion.Capture(MotionElements());
        updatingLayout = true;
        // Reuse audio controls so live styling never resets the selected device.
        string structure = Store.IsEditing + ":" + Store.Value.ShowAudio + ":" + string.Join("|", Store.Value.HiddenModules.OrderBy(id => id))
            + ":" + string.Join("|", Store.Value.ModuleColumns.OrderBy(pair => pair.Key).Select(pair => pair.Key + "=" + pair.Value));
        if (moduleStructure != structure)
        {
            modules.Children.Clear();
            foreach (var card in cards.Values) { if (card.Child is Panel panel) panel.Children.Clear(); }
            cards.Clear();
            foreach (var id in Store.Value.ModuleOrder)
            {
                if (!Store.ModuleVisible(id)) continue;
                var body = bodies[id];
                // Disable the real controls, not the card: dragging, resizing,
                // hide actions and the editor's finish button remain available.
                if (Store.IsEditing && body.IsKeyboardFocusWithin) Keyboard.ClearFocus();
                body.IsEnabled = !Store.IsEditing;
                body.IsHitTestVisible = !Store.IsEditing;
                if (body is FrameworkElement element && element.Parent is Panel parent) parent.Children.Remove(element);
                var content = new Grid();
                content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                content.RowDefinitions.Add(new RowDefinition());
                content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var title = new DockPanel { Margin = new Thickness(4, 0, 4, 8) };
                var grip = new TextBlock { Text = Store.IsEditing ? "⋮⋮  " + LayoutCatalog.ModuleName(id) : LayoutCatalog.ModuleName(id), FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Padding = new Thickness(4), Opacity = .9, Cursor = Store.IsEditing ? Cursors.SizeAll : Cursors.Arrow };
                grip.SetResourceReference(TextBlock.ForegroundProperty, Store.IsEditing ? "Think.Accent" : "Think.Text");
                if (Store.IsEditing)
                {
                    var hide = ActionButton("Ocultar", () => Store.Update(p => p.HiddenModules.Add(id)));
                    hide.Padding = new Thickness(8, 3, 8, 3); DockPanel.SetDock(hide, Dock.Right); title.Children.Add(hide);
                }
                title.Children.Add(grip);
                if (Store.IsEditing) content.Children.Add(title);
                Grid.SetRow(body, 1); content.Children.Add(body);
                if (Store.IsEditing) AddWidgetResizeEdges(content, id);
                var card = new Border { Child = content, Tag = id, CornerRadius = new CornerRadius(20), Padding = new Thickness(10), VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, UseLayoutRounding = true };
                card.SetResourceReference(Border.BackgroundProperty, "Think.GlassCard");
                System.Windows.Automation.AutomationProperties.SetName(card, LayoutCatalog.ModuleName(id));
                LayoutDrag.Attach(card, "modules", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.ModuleOrder, from, to, after)), true, grip);
                cards[id] = card;
            }
            moduleStructure = structure;
        }
        var ordered = Store.Value.ModuleOrder.Where(cards.ContainsKey).ToArray();
        for (int i = 0; i < ordered.Length; i++)
        {
            var card = cards[ordered[i]];
            card.SetValue(IslandModuleBoard.ColumnsProperty, Store.Value.ModuleColumns.GetValueOrDefault(ordered[i]));
            card.SetValue(IslandModuleBoard.ExtraHeightProperty, Store.Value.ModuleExtraHeight.GetValueOrDefault(ordered[i]));
            card.Margin = new Thickness(0); card.Padding = new Thickness(ordered[i] is "media" or "notes" ? 0 : 10);
            card.CornerRadius = new CornerRadius(WidgetRadius);
        }
        modules.Gap = Math.Max(6, Store.Value.IslandSpacing + 2);
        RefreshWidgetCorners();
        modules.SetCards(ordered.Select(id => cards[id]));
        if (Store.IsEditing) interaction.OpenFull();
        TutorialGuide.Mark(header, "island");
        BuildHeader(); UpdateStageVisibility();
        UpdateClock(); Position(); RefreshAudio();
        SyncEditorBackdrop();
        updatingLayout = false;
        motion.Arrange(this, before, MotionElements(), Store.Value.AnimationMilliseconds);
        foreach (var mini in minis.Values) if (mini.IsVisible) mini.RefreshSettings();
    }
    private Button? themeToggle;
    private void RefreshThemeToggle()
    {
        if (themeToggle is null) return;
        bool light = ThemeService.IsLight(Store.Value);
        string label = light ? "Ativar tema escuro no Windows e launcher" : "Ativar tema claro no Windows e launcher";
        themeToggle.Content = Glyph(light ? "\uE708" : "\uE706", 17 * Store.Value.IslandScale);
        themeToggle.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(themeToggle, label);
    }
    private void BuildHeader()
    {
        UpdateSecondAreaMode();
        foreach (var old in header.Children.OfType<Button>()) if (ReferenceEquals(old.Content, clock)) old.Content = null;
        header.Children.Clear();
        headerBattery = null;
        themeToggle = null;
        hoverQuick.Children.Clear();
        var scale = Store.Value.IslandScale;
        var standard = OrderBatteryBefore(Store.Value.QuickOrder.Where(id => id is not ("settings" or "monitor" or "tools") && IsQuickVisible(id)), "windows-settings");
        var headerIds = Stage == IslandStage.Full && maximizedOnDisplay ? Array.Empty<string>() : standard;
        header.HorizontalAlignment = Stage == IslandStage.Full && maximizedOnDisplay
            ? Store.Value.IslandPanelSide == "Left" ? HorizontalAlignment.Left : HorizontalAlignment.Right
            : HorizontalAlignment.Center;
        foreach (var id in headerIds)
        {
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            if (id == "battery") { headerBattery = BatteryButton(false); header.Children.Add(headerBattery); continue; }
            if (id == "clock")
            {
                var clockButton = ActionButton("Relógio, cronômetro e temporizador", () =>
                {
                    if (Store.IsEditing) return;
                    ShowMini(MiniIslandKind.Clock);
                });
                clockButton.Style = (Style)FindResource("Think.IconButton"); clockButton.Content = clock;
                clockButton.Padding = new Thickness(0); clockButton.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
                EnableQuickReorder(clockButton, id);
                header.Children.Add(MarkPanelIcon(clockButton, "clock"));
                header.Children.Add(DateButton(false));
                if (Store.Value.ShowWeatherInHeader && WeatherEnabled) header.Children.Add(WeatherButton(false));
                continue;
            }
            var button = ActionButton(control.Name, () =>
            {
                if (Store.IsEditing && id is not ("settings" or "windows-settings")) return;
                if (id == "audio") ShowMini(MiniIslandKind.Volume);
                else if (id == "brightness") ShowMini(MiniIslandKind.Brightness);
                else if (id == "wifi") ShowMini(MiniIslandKind.Network); else if (id == "bluetooth") ShowMini(MiniIslandKind.Bluetooth);
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
            EnableQuickReorder(button, id);
            header.Children.Add(MarkPanelIcon(button, id));
        }
        if (!IsQuickVisible("clock") && Store.Value.ShowWeatherInHeader && WeatherEnabled && !(Stage == IslandStage.Full && maximizedOnDisplay))
            header.Children.Add(WeatherButton(false));
        if (SecondAreaAvailable && IsQuickVisible("monitor"))
        {
            bool monitoringOpen = Stage == IslandStage.Full && secondAreaSelected;
            var monitor = ActionButton(monitoringOpen ? "Voltar à área principal" : "Abrir monitoramento do PC", () =>
            {
                if (Store.IsEditing) return;
                if (Stage == IslandStage.Full) SelectSecondArea(!secondAreaSelected);
                else
                {
                    secondAreaSelected = true;
                    OpenFull();
                }
                TutorialGuide.Notify("monitor-open");
            });
            TutorialGuide.Mark(monitor, "monitor");
            monitor.Style = (Style)FindResource("Think.IconButton");
            monitor.Content = Glyph(monitoringOpen ? "\uE80F" : "\uE9D9", 17 * scale);
            monitor.Width = monitor.Height = 32 * scale;
            monitor.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
            header.Children.Add(MarkPanelIcon(monitor, "monitor"));
        }
        if (IsQuickVisible("tools")) header.Children.Add(SystemToolsButton(false));
        if (Stage == IslandStage.Full) header.Children.Add(WidgetEditorButton(false));
        if (Stage == IslandStage.Full) header.Children.Add(CreateThemeToggle());
        BuildSecondaryQuickControls();
        if (Stage == IslandStage.Full || Store.IsEditing)
        {
            if (Store.IsEditing)
            {
                // Editor-only controls belong below the persistent header.
                // Inserting a drag grip into the header shifted every icon.
                var positionGrip = Glyph("\uE784", 15);
                positionGrip.Width = 22; positionGrip.Height = 32;
                AttachPositionGrip(positionGrip); hoverQuick.Children.Add(positionGrip);
                var widgetsButton = ActionButton("Selecionar widgets", () => ((App)Application.Current).OpenWidgetSettings());
                widgetsButton.Content = Glyph("\uE80A", 17 * scale);
                StyleSecondaryControl(widgetsButton); hoverQuick.Children.Add(widgetsButton);
            }
            if (Stage == IslandStage.Full)
            {
                var settingsButton = ActionButton("Personalizar launcher", openSettings);
                TutorialGuide.Mark(settingsButton, "customize");
                settingsButton.Content = Glyph(LayoutCatalog.QuickControls.First(q => q.Id == "settings").Glyph, 17 * scale);
                StyleSecondaryControl(settingsButton); hoverQuick.Children.Add(MarkPanelIcon(settingsButton, "settings"));
            }
            var close = ActionButton(Store.IsEditing ? "Concluir edição" : "Fechar painel completo", () =>
            { if (Store.IsEditing) Store.SetEditing(false); Transition(() => interaction.CloseFull()); });
            close.Style = (Style)FindResource("Think.IconButton"); close.Content = Glyph("\uE70E", 13);
            close.Width = 32; close.Height = 32; close.Margin = new Thickness(4, 0, 0, 0); header.Children.Add(MarkPanelIcon(close, "expand"));
        }
        else
        {
            var expand = ActionButton("Abrir painel completo", OpenFull);
            TutorialGuide.Mark(expand, "expand");
            expand.Style = (Style)FindResource("Think.IconButton"); expand.Content = Glyph("\uE70D", 13); expand.Width = 28; expand.Height = 32; header.Children.Add(MarkPanelIcon(expand, "expand"));
        }
        hoverQuick.Visibility = hoverQuick.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
    private void UpdateClock()
    {
        RefreshBattery();
        RefreshPanelIcons();
        metrics.RefreshPower();
        clock.Text = DateTime.Now.ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        headerDate.Text = barDate.Text = DateTime.Now.ToString("dd/MM", CultureInfo.GetCultureInfo("pt-BR"));
        clock.ToolTip = DateTime.Now.ToString("dddd, dd 'de' MMMM", CultureInfo.GetCultureInfo("pt-BR"));
        calendarDate.Text = DateTime.Now.ToString("ddd, dd MMM", CultureInfo.GetCultureInfo("pt-BR"));
        if (calendarToday != DateTime.Today)
        {
            if (calendar.DisplayDate.Year == calendarToday.Year && calendar.DisplayDate.Month == calendarToday.Month) calendar.DisplayDate = DateTime.Today;
            calendarToday = DateTime.Today; calendar.InvalidateVisual();
        }
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
        if (Stage == IslandStage.Hover) TutorialGuide.Notify("island-hover");
        if (Stage == IslandStage.Full) TutorialGuide.Notify("island-full");
        if (previous == Stage) return;
        int revision = ++expansionRevision;
        preparingFullReveal = false;
        if (fullscreenReveal && Stage == IslandStage.Compact)
        {
            Hide(); fullscreenReveal = false;
            FullscreenRevealClosed?.Invoke();
            return;
        }
        if (Stage != IslandStage.Compact)
        {
            // Begin the panel's reveal below the persistent strip. Never animate
            // the strip's full-width rectangle into the panel's rectangle.
            preparingFullReveal = true; Surface.Visibility = Visibility.Hidden; ApplyAppearance();
            if (Stage == IslandStage.Full && !fullscreenReveal) Place(Math.Min(Display.Width - 16, Store.Value.IslandExpandedWidth), 6, false);
        }
        else ApplyAppearance();
        UpdateAdaptiveState();
        BuildHeader(); UpdateStageVisibility(); UpdateClock();
        void Reveal()
        {
            if (revision != expansionRevision) return;
            if (!IsVisible) { preparingFullReveal = false; return; }
            FontSizing.PrepareForMeasure(root);
            if (Stage == IslandStage.Full && !Store.IsEditing)
            {
                // Arrange rehosted controls at their final width before the
                // visible reveal; SizeChanged must not finish this afterwards.
                stableFullPanelWidth = stableFullPanelHeight = null;
                Resize(false);
                root.UpdateLayout();
                stableFullPanelWidth = null;
                Resize(false);
                root.UpdateLayout();
                var ready = TargetBounds;
                stableFullPanelWidth = (ready.Right - ready.Left) / Display.Scale;
                stableFullPanelHeight = (ready.Bottom - ready.Top) / Display.Scale;
                Place(stableFullPanelWidth.Value, 6, false);
            }
            contentResizePending = false;
            preparingFullReveal = false;
            Resize(true);
            motion.Arrange(this, before, MotionElements(), Store.Value.AnimationMilliseconds);
            if (!PointerInsideGroup) ScheduleCollapse();
        }
        if (Stage != IslandStage.Compact) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Reveal));
        else Reveal();
    }
    private void OpenFull()
    {
        escapeDismissed = false;
        RefreshForegroundLayout();
        calendar.DisplayMode = CalendarMode.Month; calendar.DisplayDate = DateTime.Today;
        DismissMinis(); hoverEnter.Stop(); CancelCollapse();
        Transition(() => interaction.OpenFull());
        // Only an explicit open requests keyboard focus; hover stays passive.
        VirtualKeyboardInput.RememberTarget();
        Focusable = true; Activate(); Keyboard.Focus(this);
    }
    private void DismissFromEscape()
    {
        escapeDismissed = true;
        hoverEnter.Stop(); CancelCollapse();
        if (Store.IsEditing) Store.SetEditing(false);
        systemTools?.Close(); leftArea?.Hide(); DismissMinis();
        Transition(() => interaction.Compact());
        RefreshPanelIcons();
        VirtualKeyboardInput.RestoreTarget();
    }
    private void UpdateStageVisibility()
    {
        if (IsVisible && (Stage != IslandStage.Compact || interaction.MiniOpen)) outsidePanelClicks?.Start();
        else outsidePanelClicks?.Stop();
        hoverViewport.Visibility = Stage == IslandStage.Hover ? Visibility.Visible : Visibility.Collapsed;
        fullPanel.Visibility = Stage == IslandStage.Full ? Visibility.Visible : Visibility.Collapsed;
        secondaryQuickViewport.Visibility = Stage != IslandStage.Compact && hoverQuick.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
        weather.SetActive(IsVisible && Stage == IslandStage.Full && Store.ModuleVisible("weather"));
        weather.SetHeaderActive(IsVisible && WeatherEnabled && (Store.Value.ShowWeatherInHeader || Stage != IslandStage.Compact));
        RefreshHeaderWeather();
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
        // Live sensor/media refreshes must not refit a panel already revealed.
        // Explicit settings changes clear the snapshot before calling Resize.
        if (Stage == IslandStage.Full && stableFullPanelHeight.HasValue && !Store.IsEditing && !preparingFullReveal)
        { contentResizePending = false; return; }
        if (preparingFullReveal) { contentResizePending = true; return; }
        if (IsPositionAnimating) { contentResizePending = true; return; }
        if (updatingLayout || resizeQueued || !IsVisible || Stage == IslandStage.Compact || LayoutDrag.Active) return;
        if (ControlsBusy) { contentResizePending = true; return; }
        contentResizePending = false;
        resizeQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            resizeQueued = false;
            if (Stage == IslandStage.Full && stableFullPanelHeight.HasValue && !Store.IsEditing && !preparingFullReveal)
            { contentResizePending = false; return; }
            if (!IsVisible || Stage == IslandStage.Compact || LayoutDrag.Active) return;
            if (preparingFullReveal || IsPositionAnimating) { contentResizePending = true; return; }
            if (ControlsBusy) { contentResizePending = true; return; }
            var before = motion.Capture(MotionElements()); Resize(true);
            motion.Arrange(this, before, MotionElements(), Store.Value.AnimationMilliseconds);
        }));
    }
    public override void RefreshAppearance()
    {
        base.RefreshAppearance();
        RefreshWidgetCorners();
        SyncEditorBackdrop();
        RefreshThemeToggle();
        RefreshCollapsePolicy();
        RefreshForegroundLayout();
        statusWindow?.RefreshAppearance();
        foreach (var mini in minis.Values) if (mini.IsVisible) mini.RefreshAppearance();
    }
    private bool PointerInsideGroup => IsMouseOver || statusWindow?.IsMouseOver == true || leftArea?.IsMouseOver == true || minis.Values.Any(mini => mini.IsVisible && mini.IsMouseOver);
    private bool NearActivationZone
    {
        get
        {
            if (!IsVisible || edgeReduced || Stage != IslandStage.Compact) return false;
            var bounds = TargetBounds; var pointer = System.Windows.Forms.Cursor.Position; double margin = 8 * Display.Scale;
            return pointer.X >= bounds.Left - margin && pointer.X <= bounds.Right + margin && pointer.Y >= bounds.Top && pointer.Y <= bounds.Bottom + margin;
        }
    }
    private bool PointerInsideBridge
    {
        get
        {
            var cursor = System.Windows.Forms.Cursor.Position; var main = Bounds;
            if (statusWindow?.IsVisible == true)
            {
                var strip = statusWindow.Bounds;
                if (main.Top >= strip.Bottom && main.Top - strip.Bottom <= 24 * Display.Scale
                    && cursor.Y >= strip.Bottom - 1 && cursor.Y <= main.Top + 1
                    && cursor.X >= Math.Max(strip.Left, main.Left) && cursor.X <= Math.Min(strip.Right, main.Right)) return true;
            }
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
    private bool ControlsBusy => (widgetAgenda?.IsVisible == true || widgetAgenda?.MinimizedToDock == true) || SystemToolsOpen || IsMouseCaptureWithin || widgetArea.IsInteracting || media.IsInteracting || output.IsInteracting || microphone.IsInteracting || brightness.IsInteracting || weather.IsInteracting || minis.Values.Any(mini => mini.IsVisible && mini.IsInteracting);
    private bool InteractionInProgress => TutorialGuide.KeepIslandOpen || Store.IsEditing || Store.IsCustomizing || LayoutDrag.Active || ControlsBusy || DateTime.UtcNow < keyboardBusyUntil;
    private void CancelCollapse() { hoverLeave.Stop(); outsideSince = null; }
    private void WaitForBridge() { outsideSince = null; hoverLeave.Interval = TimeSpan.FromMilliseconds(50); hoverLeave.Start(); }
    private void ScheduleCollapse()
    {
        hoverLeave.Stop();
        if (dismissingMinis || !IsVisible || (Stage == IslandStage.Compact && !interaction.MiniOpen) || !AdaptiveCollapse) { outsideSince = null; return; }
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
    private FrameworkElement? miniTrigger;
    private int miniPresentationRevision;
    private void ShowMini(MiniIslandKind kind, FrameworkElement? trigger = null)
    {
        hoverEnter.Stop(); CancelCollapse();
        if (lastMiniKind == kind && DateTime.UtcNow - lastMiniDismissed < TimeSpan.FromMilliseconds(200)) return;
        if (minis.TryGetValue(kind, out var existing) && existing.IsVisible) { existing.Dismiss(); return; }
        if (Store.IsEditing) return;
        bool secondaryTrigger = trigger is not null && hoverQuick.IsAncestorOf(trigger);
        var previousStage = Stage;
        DismissMinis(); interaction.OpenMini(collapseExpanded: !secondaryTrigger);
        // Secondary icons exist only in the expanded area. Keep their host
        // and anchor alive, otherwise collapsing removes the button mid-click.
        if (previousStage != Stage)
        {
            // The popup replaces the expanded contents. Keep fullscreen reveal
            // ownership alive until the popup closes instead of hiding its owner.
            ++expansionRevision; preparingFullReveal = false;
            stableFullPanelWidth = stableFullPanelHeight = null;
            UpdateAdaptiveState(); BuildHeader(); UpdateStageVisibility();
            Resize(true); SyncEditorBackdrop();
        }
        miniTrigger = trigger;
        if (!minis.TryGetValue(kind, out var mini))
        {
            mini = new MiniIslandWindow(Store, kind, MiniAnchor) { Owner = this, ShowInTaskbar = ShowInTaskbar, MonitorDeviceOverride = MonitorDeviceOverride };
            mini.MouseEnter += (_, _) => CancelCollapse();
            mini.MouseLeave += (_, _) => ScheduleCollapse();
            mini.PreviewKeyDown += (_, _) => { keyboardBusyUntil = DateTime.UtcNow.AddMilliseconds(350); ScheduleCollapse(); };
            mini.Dismissed += () =>
            {
                RefreshPanelIcons();
                lastMiniKind = kind; lastMiniDismissed = DateTime.UtcNow;
                if (minis.Values.Any(m => m.IsVisible)) return;
                interaction.CloseMini();
                if (!dismissingMinis && fullscreenReveal && Stage == IslandStage.Compact)
                {
                    Hide(); fullscreenReveal = false;
                    FullscreenRevealClosed?.Invoke();
                }
                else ScheduleCollapse();
            };
            minis[kind] = mini;
        }
        int presentation = ++miniPresentationRevision;
        keyboardBusyUntil = DateTime.UtcNow.AddMilliseconds(350);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (presentation != miniPresentationRevision || !IsVisible || !interaction.MiniOpen) return;
            // Let the originating button finish mouse-up/capture and the owner
            // finish its reparenting before activating the independent popup.
            mini.Present(); RefreshPanelIcons();
        }));
    }
    private OverlayChrome.PixelRect MiniAnchor()
    {
        if (miniTrigger is { IsVisible: true } source && PresentationSource.FromVisual(source) is not null)
        {
            var top = source.PointToScreen(new Point());
            var bottom = source.PointToScreen(new Point(source.ActualWidth, source.ActualHeight));
            return new OverlayChrome.PixelRect { Left = (int)Math.Floor(top.X), Top = (int)Math.Floor(top.Y), Right = (int)Math.Ceiling(bottom.X), Bottom = (int)Math.Ceiling(bottom.Y) };
        }
        var anchor = TargetBounds;
        if (Stage != IslandStage.Compact) anchor.Bottom = anchor.Top + (int)((Math.Max(40, 36 * Store.Value.IslandScale) + 10) * Display.Scale);
        return anchor;
    }
    private void DismissMinis()
    {
        ++miniPresentationRevision;
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
            if (connectionStatus.Text != text) { connectionStatus.Text = text; ModuleSizeChanged(); }
        }
        catch (System.Net.NetworkInformation.NetworkInformationException)
        { /* Preserve the last connection state until the next successful refresh. */ }
        finally { readingNetwork = false; }
    }
    public override void Position() => Resize(false);
    private void Resize(bool animate)
    {
        UpdateAdaptiveState();
        UpdateSecondAreaMode();
        if (edgeReduced)
        {
            Surface.Padding = new Thickness(12, 2, 12, 2);
            statusRow.Width = Math.Max(1, Display.Bounds.Width / Display.Scale - 24);
            statusRow.Height = 24;
            Place(Display.Bounds.Width / Display.Scale, 28, animate);
            return;
        }
        if (Stage == IslandStage.Full && !Store.IsEditing && !preparingFullReveal
            && stableFullPanelWidth is double readyWidth && stableFullPanelHeight is double readyHeight)
        {
            // Reposition the completed snapshot for monitor/app mode changes,
            // but never remeasure live content after its visible reveal.
            Place(readyWidth, readyHeight, animate);
            return;
        }
        var p = Store.Value;
        if (Stage != IslandStage.Full) stableFullPanelWidth = stableFullPanelHeight = null;
        FontSizing.LimitScale(modules, 1.25);
        FontSizing.LimitScale(hoverPanel, 1.25);
        System.Windows.Documents.TextElement.SetFontSize(modules,
            SystemFonts.MessageFontSize * Math.Clamp(p.FontScale, .8, 1.25));
        FontSizing.PrepareForMeasure(root);
        Surface.Padding = Stage == IslandStage.Compact ? new Thickness(12, 5, 12, 5) : new Thickness(12);
        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double compact = Math.Min(Display.Width - 16, Math.Max(220 * p.IslandScale, header.DesiredSize.Width + 32));
        double width = Stage == IslandStage.Compact ? compact : Stage == IslandStage.Hover ? Math.Min(Display.Width - 16, Math.Max(compact, Math.Min(540, p.IslandExpandedWidth))) : Math.Min(Display.Width - 16, p.IslandExpandedWidth);
        if (Store.IsEditing && Stage == IslandStage.Full && editingWidth is double previousWidth)
            width = Math.Min(Display.Width - 16, previousWidth);
        headerViewport.Width = headerViewport.MaxWidth = Math.Max(1, width - 24);
        double mainRowHeight = Stage == IslandStage.Full ? 32 : Math.Max(40, 36 * p.IslandScale);
        headerViewport.Height = mainRowHeight;
        double headerHeight = mainRowHeight + Surface.Padding.Top + Surface.Padding.Bottom;
        secondaryQuickViewport.Width = secondaryQuickViewport.MaxWidth = headerViewport.Width;
        // Keep the extra controls on one independent row. The Viewbox fits it
        // to the available width instead of wrapping or changing the main row.
        hoverQuick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        hoverQuick.Width = Math.Max(36, hoverQuick.Children.OfType<FrameworkElement>().Sum(item =>
            (double.IsFinite(item.Width) ? item.Width : item.DesiredSize.Width) + item.Margin.Left + item.Margin.Right));
        secondaryQuickViewport.Measure(new Size(headerViewport.Width, double.PositiveInfinity));
        if (Stage != IslandStage.Compact && hoverQuick.Children.Count > 0) headerHeight += secondaryQuickViewport.DesiredSize.Height;
        double maximumHeight = ExpandedHeightLimit(Display);
        double limit = Stage == IslandStage.Full ? Math.Min(maximumHeight, p.IslandExpandedHeight > 0 ? p.IslandExpandedHeight : Math.Max(120, Math.Min(620, Display.Height * .64))) : CompactLayout.PanelHeight(Display.Height);
        double inner = Math.Max(1, width - 24);
        hoverViewport.Width = inner; hoverPanel.Width = inner;
        hoverViewport.MaxHeight = double.PositiveInfinity;
        // Preserve the compact three-column layout before scaling. Reflowing
        // this board into narrow columns made it tall, then the Viewbox shrank
        // it again and left large empty margins on both sides.
        double boardWidth = Store.IsEditing && editingWidth.HasValue ? editingBoardWidth : Math.Max(760, inner);
        modules.Width = boardWidth;
        double indicatorsHeight = SecondAreaAvailable ? 14 : 0;
        double panelLimit = Store.IsEditing && editingHeight is double savedHeight ? savedHeight : Math.Min(stableFullPanelHeight ?? limit, limit);
        double contentLimit = Math.Max(24, panelLimit - headerHeight - indicatorsHeight);
        moduleViewport.MaxHeight = contentLimit;
        if (Stage == IslandStage.Full && !secondAreaSelected)
        {
            modules.InvalidateMeasure();
            modules.Measure(new Size(boardWidth, double.PositiveInfinity));
            double naturalHeight = Math.Max(1, Math.Ceiling(modules.DesiredSize.Height));
            // Fit content once, then size the frame to the exact rendered board.
            // No width search or compensating widening: uniform scaling keeps
            // icons proportional, while the frame always adds 12 DIP per side.
            double fit = Math.Min(1, Math.Min(inner / boardWidth, contentLimit / naturalHeight));
            double renderedWidth = Math.Max(1, boardWidth * fit);
            if (!Store.IsEditing)
            {
                inner = renderedWidth;
                width = inner + Surface.Padding.Left + Surface.Padding.Right;
            }
            moduleViewport.Configure(boardWidth, naturalHeight, fit);
            moduleViewport.Width = renderedWidth;
            moduleViewport.Height = naturalHeight * fit;
            headerViewport.Width = inner;
        }
        else { moduleViewport.Width = inner; moduleViewport.Height = double.NaN; }
        moduleViewport.Measure(new Size(inner, contentLimit));
        hoverModules.InvalidateMeasure(); hoverPanel.InvalidateMeasure();
        hoverPanel.Measure(new Size(Math.Max(1, width - 24), double.PositiveInfinity));
        hoverViewport.Height = Math.Max(1, hoverPanel.DesiredSize.Height);
        secondArea.Width = inner;
        secondArea.Height = System.Math.Max(40, System.Math.Min(520, limit - headerHeight - 38));
        if (Stage == IslandStage.Full && secondAreaSelected && secondArea.Child is Viewbox monitorViewport
            && monitorViewport.Child is FrameworkElement monitorPanel)
        {
            // Fit the window to the scaled monitoring board, instead of placing
            // a fixed 520-DIP board in the middle of a much wider empty island.
            double monitorWidth = Math.Max(760, inner);
            monitorPanel.Width = monitorWidth;
            FontSizing.PrepareForMeasure(monitorPanel);
            monitorPanel.InvalidateMeasure();
            monitorPanel.Measure(new Size(monitorWidth, double.PositiveInfinity));
            double monitorHeight = monitorPanel is HardwareMonitorPanel board
                ? board.MeasureBoard(monitorWidth).Height : Math.Max(1, Math.Ceiling(monitorPanel.DesiredSize.Height));
            double fit = Math.Min(1, Math.Min(inner / monitorWidth, contentLimit / monitorHeight));
            inner = monitorWidth * fit;
            width = inner + Surface.Padding.Left + Surface.Padding.Right;
            monitorViewport.Width = inner; monitorViewport.Height = monitorHeight * fit;
            secondArea.Width = inner; secondArea.Height = monitorViewport.Height;
            headerViewport.Width = inner;
        }
        double fullContentHeight = secondAreaSelected ? secondArea.Height : moduleViewport.Height;
        moduleViewport.Measure(new Size(inner, contentLimit));
        if (!secondAreaSelected) fullContentHeight = Math.Min(contentLimit, moduleViewport.Height);
        double height = Stage == IslandStage.Compact ? headerHeight : Stage == IslandStage.Hover ? headerHeight + hoverViewport.Height : Math.Min(limit, headerHeight + fullContentHeight + indicatorsHeight);
        if (Store.IsEditing && Stage == IslandStage.Full && editingHeight is double previousHeight)
            height = previousHeight;
        if (Stage == IslandStage.Full && !Store.IsEditing && !preparingFullReveal)
        {
            stableFullPanelHeight ??= height;
            height = Math.Min(limit, stableFullPanelHeight.Value);
        }
        // Keep the destination content layout stable while the native frame
        // reveals it; do not reflow the entire module tree on every frame.
        root.Width = inner;
        // Module fitting may have changed the final width after the first
        // header measure. Both quick-access rows must use this final bound.
        headerViewport.Width = headerViewport.MaxWidth = inner;
        secondaryQuickViewport.Width = secondaryQuickViewport.MaxWidth = inner;
        root.Height = Math.Max(1, height - Surface.Padding.Top - Surface.Padding.Bottom);
        root.HorizontalAlignment = HorizontalAlignment.Center;
        root.VerticalAlignment = VerticalAlignment.Top;
        Place(width, height, animate);
    }
}
