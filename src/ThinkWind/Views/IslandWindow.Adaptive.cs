using System.Windows;
using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Interop;
using OrbyLauncher.Native;

namespace OrbyLauncher.Views;

internal sealed partial class IslandWindow
{
    private bool maximizedOnDisplay, edgeReduced;
    private bool fullscreenReveal;
    internal event Action? FullscreenRevealClosed;
    internal void RevealOverFullscreen()
    {
        fullscreenReveal = true;
        maximizedOnDisplay = false;
        interaction.Compact();
        ShowActivated = false;
        Show();
        Transition(() => interaction.Enter());
    }
    internal void EndFullscreenReveal()
    {
        fullscreenReveal = false;
        DismissMinis(); interaction.Compact();
        CancelCollapse();
    }

    protected override bool FullWidthTop => !fullscreenReveal && edgeReduced && Stage == Services.IslandStage.Compact;
    protected override bool SuppressMaterial => edgeReduced || preparingFullReveal;
    protected override string? TopPanelSide => Stage == Services.IslandStage.Full ? (!fullscreenReveal && maximizedOnDisplay ? Store.Value.IslandPanelSide : "Center") : null;
    protected override double TopEdgeOffset => !fullscreenReveal && Stage == Services.IslandStage.Full && maximizedOnDisplay ? 36 : 0;
    private IslandStatusWindow? statusWindow;
    private readonly MaximizedWindowInset applicationInset = new();
    private StatusBarReservation? fixedReservation;
    private void RefreshApplicationInset()
    {
        if (fullscreenReveal) return;
        if (fixedReservation is null && Store.Value.IslandReserveTop)
        {
            nint handle = new WindowInteropHelper(this).EnsureHandle();
            fixedReservation = new StatusBarReservation(HwndSource.FromHwnd(handle));
        }
        // Fixed reservation does not follow hover, visibility or app mode.
        // Native fullscreen still uses monitor bounds rather than this work area.
        if (Store.Value.IslandReserveTop)
        {
            applicationInset.Apply(Display, 0);
            fixedReservation?.Set(Display, (int)Math.Ceiling(28 * Display.Scale));
        }
        else
        {
            fixedReservation?.Set(Display, 0);
            applicationInset.Apply(Display, IsVisible && maximizedOnDisplay && Store.Value.IslandReduceOverMaximized
                ? (int)Math.Ceiling(28 * Display.Scale) : 0);
        }
    }

    private void SyncStatusSurface()
    {
        BuildStatusRow();
        if (!fullscreenReveal && (edgeReduced || (Stage == Services.IslandStage.Full && maximizedOnDisplay)))
        {
            if (ReferenceEquals(Surface.Child, statusRow)) Surface.Child = null;
            if (statusWindow is null)
            {
                // WPF requires the owner's HWND before assigning an owned window,
                // including the startup probe before the island's first Show().
                new WindowInteropHelper(this).EnsureHandle();
                statusWindow = new IslandStatusWindow(Store) { Owner = this, ShowActivated = false, MonitorDeviceOverride = MonitorDeviceOverride };
                statusWindow.MouseEnter += (_, _) => CancelCollapse();
                statusWindow.MouseLeave += (_, _) => ScheduleCollapse();
            }
            statusRow.Width = Math.Max(1, Display.Bounds.Width / Display.Scale - 24); statusRow.Height = 24;
            statusWindow.Attach(statusRow); Surface.Child = stableLayout;
            Surface.Visibility = edgeReduced || preparingFullReveal ? Visibility.Hidden : Visibility.Visible;
            if (IsVisible) { if (!statusWindow.IsVisible) statusWindow.Show(); statusWindow.Position(); statusWindow.RefreshAppTint(); }
            else statusWindow.Hide();
        }
        else
        {
            statusWindow?.Attach(null); statusWindow?.Hide();
            Surface.Child = stableLayout; Surface.Visibility = preparingFullReveal ? Visibility.Hidden : Visibility.Visible;
        }
        RefreshApplicationInset();
    }
    private readonly DockPanel statusRow = new() { LastChildFill = false };
    private readonly TextBlock statusClock = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private bool statusBuilt;
    private string statusControlsSignature = "";
    private void BuildStatusRow()
    {
        string signature = string.Join('|', Store.Value.QuickOrder) + ":" + string.Join('|', Store.Value.HiddenQuickControls) + ":" + string.Join('|', Store.Value.DisabledQuickControls);
        if (statusBuilt && statusControlsSignature == signature) return;
        if (statusBuilt) { DismissMinis(); systemTools?.Close(); }
        statusControlsSignature = signature;
        statusBuilt = true;
        foreach (var old in statusRow.Children.OfType<Button>())
            if (ReferenceEquals(old.Content, statusClock)) old.Content = null;
        statusRow.Children.Clear();
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        statusClock.Margin = new Thickness(0, 0, 8, 0);
        statusClock.Visibility = IsQuickVisible("clock") ? Visibility.Visible : Visibility.Collapsed;
        DockPanel.SetDock(statusClock, Dock.Left);
        var clockButton = ActionButton("Relógio, cronômetro e temporizador", () => { });
        clockButton.Click += (_, _) => { if (!Store.IsEditing) ShowMini(MiniIslandKind.Clock, clockButton); };
        clockButton.Content = statusClock;
        clockButton.Style = (Style)FindResource("Think.IconButton");
        clockButton.Visibility = statusClock.Visibility;
        DockPanel.SetDock(clockButton, Dock.Left); statusRow.Children.Add(MarkPanelIcon(clockButton, "clock"));
        var dateButton = DateButton(true);
        dateButton.Visibility = statusClock.Visibility;
        DockPanel.SetDock(dateButton, Dock.Left); statusRow.Children.Add(dateButton);
        var statusWeather = WeatherButton(true);
        statusWeather.Visibility = Store.Value.ShowWeatherInHeader ? Visibility.Visible : Visibility.Collapsed;
        DockPanel.SetDock(statusWeather, Dock.Left); statusRow.Children.Add(statusWeather);
        var extraArea = ActionButton("Abrir área 2 à esquerda", ToggleLeftArea);
        TutorialGuide.Mark(extraArea, "monitor");
        extraArea.Style = (Style)FindResource("Think.IconButton"); extraArea.Content = Glyph("\uE9D9", 13);
        extraArea.Width = 28; extraArea.Height = 24; extraArea.Padding = new Thickness(3);
        extraArea.Margin = new Thickness(0, 0, 8, 0);
        extraArea.Visibility = IsQuickVisible("monitor") ? Visibility.Visible : Visibility.Collapsed;
        DockPanel.SetDock(extraArea, Dock.Left); statusRow.Children.Add(MarkPanelIcon(extraArea, "monitor"));
        var tools = SystemToolsButton(true);
        tools.Visibility = IsQuickVisible("tools") ? Visibility.Visible : Visibility.Collapsed;
        DockPanel.SetDock(tools, Dock.Left); statusRow.Children.Add(tools);
        void Add(string label, string glyph, Action action, string panelId = "")
        {
            var button = ActionButton(label, action); button.Style = (Style)FindResource("Think.IconButton");
            if (label == "Expandir central de controle") TutorialGuide.Mark(button, "expand");
            button.Content = Glyph(glyph, 13); button.Width = 28; button.Height = 24; button.Padding = new Thickness(3);
            controls.Children.Add(MarkPanelIcon(button, panelId));
        }
        void AddMini(string label, string glyph, MiniIslandKind kind)
        {
            var button = ActionButton(label, () => { }); button.Style = (Style)FindResource("Think.IconButton");
            button.Content = Glyph(glyph, 13); button.Width = 28; button.Height = 24; button.Padding = new Thickness(3);
            button.Click += (_, _) => ShowMini(kind, button);
            controls.Children.Add(MarkPanelIcon(button, kind switch { MiniIslandKind.Volume => "audio", MiniIslandKind.Network => "wifi", MiniIslandKind.Bluetooth => "bluetooth", MiniIslandKind.Calendar => "calendar", MiniIslandKind.Tray => "tray", _ => "clock" }));
        }
        foreach (var id in Store.Value.QuickOrder.Where(id => id is not ("clock" or "monitor" or "tools" or "settings") && IsQuickVisible(id)))
        {
            var control = Services.LayoutCatalog.QuickControls.First(q => q.Id == id);
            switch (id)
            {
                case "wifi": AddMini(control.Name, control.Glyph, MiniIslandKind.Network); break;
                case "audio": AddMini(control.Name, control.Glyph, MiniIslandKind.Volume); break;
                case "bluetooth": AddMini(control.Name, control.Glyph, MiniIslandKind.Bluetooth); break;
                case "calendar": AddMini(control.Name, control.Glyph, MiniIslandKind.Calendar); break;
                case "tray": AddMini(control.Name, control.Glyph, MiniIslandKind.Tray); break;
                case "notifications": Add(control.Name, control.Glyph, () => Desktop.Chord(0x5B, 0x4E)); break;
                default: if (control.Target is string target) Add(control.Name, control.Glyph, () => Services.SystemActions.Run(target), id); break;
            }
        }
        Add("Expandir central de controle", "\uE70D", OpenFull, "expand");
        DockPanel.SetDock(controls, Dock.Right); statusRow.Children.Add(controls);
    }
    private bool AdaptiveCollapse => fullscreenReveal || Store.Value.IslandAutoCollapse
        || (Store.Value.IslandReduceOverMaximized && maximizedOnDisplay);

    internal void RefreshForegroundLayout()
    {
        if (fullscreenReveal) return;
        bool? covered = Desktop.ForegroundCoversDisplay(Display);
        bool modeChanged = covered.HasValue && covered.Value != maximizedOnDisplay;
        if (covered.HasValue) maximizedOnDisplay = covered.Value;
        // Changes on the island's own monitor still matter while expanded.
        // A secondary monitor must not replace that monitor's application state.
        if (Stage == Services.IslandStage.Full)
        {
            if (modeChanged)
            {
                UpdateAdaptiveState(); BuildHeader(); UpdateStageVisibility();
                Resize(true);
            }
            else { RefreshAdaptiveReveal(); }
            return;
        }
        bool enteredMaximized = modeChanged && maximizedOnDisplay;
        if (enteredMaximized && Store.Value.IslandReduceOverMaximized && !PointerInsideGroup
            && !InteractionInProgress && !interaction.MiniOpen && Stage != Services.IslandStage.Compact)
            Transition(() => interaction.Compact());
        RefreshAdaptiveReveal();
    }

    internal void PrepareInitialLayout()
    {
        new WindowInteropHelper(this).EnsureHandle();
        RefreshForegroundLayout();
        RefreshSettings();
        Resize(false);
    }

    private bool UpdateAdaptiveState()
    {
        // Opening a mini-panel must not replace the strip under the clicked icon.
        bool reduce = !fullscreenReveal && Store.Value.IslandReduceOverMaximized && maximizedOnDisplay
            && Stage == Services.IslandStage.Compact && !Store.IsEditing;
        if (reduce == edgeReduced) { SyncStatusSurface(); return false; }
        bool returningToIsland = edgeReduced && !reduce && Stage == Services.IslandStage.Compact && IsVisible;
        edgeReduced = reduce;
        SyncStatusSurface();
        ApplyAppearance();
        if (returningToIsland)
        {
            // The strip belongs to statusWindow; the hidden panel HWND still
            // has a screen-wide rectangle. Do not morph that stale rectangle
            // across the desktop when the maximized application is minimized.
            // Seed a centered, nearly destination-sized island before revealing.
            header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = Math.Min(Display.Width - 16, Math.Max(220 * Store.Value.IslandScale, header.DesiredSize.Width + 32));
            double height = Math.Max(28, header.DesiredSize.Height + 10);
            Place(width * .94, Math.Max(24, height - 4), false);
        }
        return true;
    }

    private void RefreshAdaptiveReveal()
    {
        statusClock.Text = DateTime.Now.ToString("HH:mm");
        if (UpdateAdaptiveState() && IsVisible) Resize(true);
    }
}
