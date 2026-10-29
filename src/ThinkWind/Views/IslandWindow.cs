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
    private enum Expansion { Collapsed, Expanded, Calendar }
    private Expansion expansion;
    private readonly DockPanel root = new();
    private readonly StackPanel header = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel modules = new();
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
        scroll = new ScrollViewer { Content = modules, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed };
        root.Children.Add(new AdornerDecoratorHost(scroll)); Surface.Child = root;
        BuildBodies();
        Loaded += (_, _) => { RefreshSettings(); UpdateClock(); timer.Start(); };
        timer.Tick += (_, _) => { UpdateClock(); RefreshAudio(); };
        Deactivated += (_, _) => { if (!Store.IsEditing && !Store.IsCustomizing && !LayoutDrag.Active) SetExpansion(Expansion.Collapsed); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (Store.IsEditing) Store.SetEditing(false);
            SetExpansion(Expansion.Collapsed); e.Handled = true;
        };
        LayoutDrag.Completed += DragCompleted;
        Closed += (_, _) => { timer.Stop(); LayoutDrag.Completed -= DragCompleted; };
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
        calendarPanel.Children.Add(ActionButton("Abrir / recolher calendário", () => SetExpansion(expansion == Expansion.Calendar ? Expansion.Expanded : Expansion.Calendar)));
        calendarPanel.Children.Add(calendar); bodies["calendar"] = calendarPanel;
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
        if (previewWasOpen && !Store.IsCustomizing && !Store.IsEditing) expansion = Expansion.Collapsed;
        previewWasOpen = Store.IsCustomizing;
        if ((Store.IsEditing || Store.IsCustomizing) && expansion == Expansion.Collapsed) expansion = Expansion.Expanded;
        BuildHeader();
        calendar.Visibility = expansion == Expansion.Calendar ? Visibility.Visible : Visibility.Collapsed;
        scroll.Visibility = expansion != Expansion.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        UpdateClock(); Position(); RefreshAudio();
    }
    private void BuildHeader()
    {
        foreach (var old in header.Children.OfType<Button>()) if (ReferenceEquals(old.Content, clock)) old.Content = null;
        header.Children.Clear();
        var scale = Store.Value.IslandScale;
        if (Store.IsEditing)
        {
            var grip = Glyph("\uE784", 15); grip.Width = 22; AttachPositionGrip(grip); header.Children.Add(grip);
        }
        foreach (var id in Store.Value.QuickOrder.Where(id => !Store.Value.HiddenQuickControls.Contains(id)))
        {
            var control = LayoutCatalog.QuickControls.First(q => q.Id == id);
            if (id == "clock")
            {
                var clockButton = ActionButton("Relógio e calendário", () =>
                {
                    if (Store.IsEditing) return;
                    if (!Store.ModuleVisible("calendar")) Desktop.Chord(0x5B, 0x4E);
                    else SetExpansion(expansion == Expansion.Calendar ? Expansion.Collapsed : Expansion.Calendar);
                });
                clockButton.Style = (Style)FindResource("Think.IconButton"); clockButton.Content = clock;
                clockButton.Padding = new Thickness(0); clockButton.Margin = new Thickness(Store.Value.IslandSpacing / 2, 0, Store.Value.IslandSpacing / 2, 0);
                LayoutDrag.Attach(clockButton, "quick", id, () => Store.IsEditing, (from, to, after) => Store.Update(p => LayoutCatalog.Move(p.QuickOrder, from, to, after)));
                header.Children.Add(clockButton); continue;
            }
            var button = ActionButton(control.Name, () =>
            {
                if (Store.IsEditing) return;
                if (id == "audio")
                {
                    if (!Store.ModuleVisible("audio")) SystemActions.Run("ms-settings:sound");
                    else { SetExpansion(Expansion.Expanded); Dispatcher.BeginInvoke(new Action(() => cards.GetValueOrDefault("audio")?.BringIntoView())); }
                }
                else if (id == "calendar") SetExpansion(Expansion.Calendar);
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
        var expand = ActionButton(Store.IsEditing ? "Concluir edição" : "Expandir ou recolher ilha", () =>
        { if (Store.IsEditing) Store.SetEditing(false); else SetExpansion(expansion == Expansion.Collapsed ? Expansion.Expanded : Expansion.Collapsed); });
        expand.Style = (Style)FindResource("Think.IconButton");
        expand.Content = Glyph(Store.IsEditing ? "\uE73E" : expansion == Expansion.Collapsed ? "\uE70D" : "\uE70E", 13);
        expand.Width = 32; expand.Height = 32; expand.Margin = new Thickness(4, 0, 0, 0); header.Children.Add(expand);
    }
    private void UpdateClock()
    {
        clock.Text = DateTime.Now.ToString("HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
        clock.ToolTip = DateTime.Now.ToString("dddd, dd 'de' MMMM", CultureInfo.GetCultureInfo("pt-BR"));
        if (!IsVisible || expansion == Expansion.Collapsed || !Store.ModuleVisible("metrics")) return;
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
        if (!IsVisible || expansion == Expansion.Collapsed || !Store.ModuleVisible("audio")) return;
        bool refresh = DateTime.UtcNow >= nextDeviceRefresh;
        if (refresh) nextDeviceRefresh = DateTime.UtcNow.AddSeconds(5);
        output.Poll(refresh); microphone.Poll(refresh);
    }
    private void SetExpansion(Expansion next)
    {
        if (next == Expansion.Calendar && !Store.ModuleVisible("calendar")) next = Expansion.Expanded;
        if (expansion == next) return;
        expansion = next;
        calendar.Visibility = next == Expansion.Calendar ? Visibility.Visible : Visibility.Collapsed;
        scroll.Visibility = next == Expansion.Collapsed ? Visibility.Collapsed : Visibility.Visible;
        BuildHeader(); Resize(true);
        if (next != Expansion.Collapsed) { nextDeviceRefresh = DateTime.MinValue; RefreshAudio(); UpdateClock(); }
        if (next == Expansion.Calendar) Dispatcher.BeginInvoke(new Action(() => cards.GetValueOrDefault("calendar")?.BringIntoView()));
    }
    public override void Position() => Resize(false);
    private void Resize(bool animate)
    {
        var p = Store.Value;
        Surface.Padding = new Thickness(12, 5, 12, expansion == Expansion.Collapsed ? 5 : 12);
        header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double compact = Math.Min(Display.Width - 16, header.DesiredSize.Width + 24);
        double width = expansion == Expansion.Collapsed ? compact : Math.Min(Display.Width - 16, Math.Max(compact, p.IslandExpandedWidth));
        double headerHeight = Math.Max(40, 36 * p.IslandScale) + 10;
        if (header.DesiredSize.Width + 24 > width) headerHeight += SystemParameters.HorizontalScrollBarHeight;
        modules.Measure(new Size(Math.Max(1, width - 40), double.PositiveInfinity));
        double height = expansion == Expansion.Collapsed ? headerHeight : Math.Min(Display.Height - 8, headerHeight + modules.DesiredSize.Height + 16);
        scroll.MaxHeight = Math.Max(0, Display.Height - headerHeight - 24);
        Place(width, height, animate);
    }
}
