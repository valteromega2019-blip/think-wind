using System.Windows;
using System;
using System.Windows.Controls;
using System.Windows.Interop;
using ThinkWind.Native;

namespace ThinkWind.Views;

internal sealed partial class IslandWindow
{
    private bool maximizedOnDisplay, edgeReduced;

    private bool preparingFullReveal;
    protected override bool FullWidthTop => edgeReduced && Stage == Services.IslandStage.Compact;
    protected override bool SuppressMaterial => edgeReduced || preparingFullReveal;
    protected override string? TopPanelSide => Stage == Services.IslandStage.Full ? (maximizedOnDisplay ? Store.Value.IslandPanelSide : "Center") : null;
    protected override double TopEdgeOffset => Stage == Services.IslandStage.Full && maximizedOnDisplay ? 36 : 0;
    private IslandStatusWindow? statusWindow;
    private readonly MaximizedWindowInset applicationInset = new();
    private void RefreshApplicationInset() => applicationInset.Apply(Display,
        IsVisible && maximizedOnDisplay && Store.Value.IslandReduceOverMaximized
            ? (int)Math.Ceiling(28 * Display.Scale) : 0);

    private void SyncStatusSurface()
    {
        BuildStatusRow();
        if (statusKeyboard is not null) statusKeyboard.Visibility = Store.Value.HiddenQuickControls.Contains("keyboard") ? Visibility.Collapsed : Visibility.Visible;
        if (edgeReduced || (Stage == Services.IslandStage.Full && maximizedOnDisplay))
        {
            if (ReferenceEquals(Surface.Child, statusRow)) Surface.Child = null;
            if (statusWindow is null)
            {
                // WPF requires the owner's HWND before assigning an owned window,
                // including the startup probe before the island's first Show().
                new WindowInteropHelper(this).EnsureHandle();
                statusWindow = new IslandStatusWindow(Store) { Owner = this, ShowActivated = false };
                statusWindow.MouseEnter += (_, _) => CancelCollapse();
                statusWindow.MouseLeave += (_, _) => ScheduleCollapse();
            }
            statusRow.Width = Math.Max(1, Display.Bounds.Width / Display.Scale - 24); statusRow.Height = 24;
            statusWindow.Attach(statusRow); Surface.Child = root;
            Surface.Visibility = edgeReduced ? Visibility.Hidden : Visibility.Visible;
            if (IsVisible) { if (!statusWindow.IsVisible) statusWindow.Show(); statusWindow.Position(); statusWindow.RefreshAppTint(); }
            else statusWindow.Hide();
        }
        else
        {
            statusWindow?.Attach(null); statusWindow?.Hide();
            Surface.Child = root; Surface.Visibility = Visibility.Visible;
        }
        RefreshApplicationInset();
    }
    private readonly DockPanel statusRow = new() { LastChildFill = false };
    private readonly TextBlock statusClock = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private bool statusBuilt;
    private Button? statusKeyboard;
    private void BuildStatusRow()
    {
        if (statusBuilt) return;
        statusBuilt = true;
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        statusClock.Margin = new Thickness(0);
        DockPanel.SetDock(statusClock, Dock.Left);
        statusRow.Children.Add(statusClock);
        var statusWeather = WeatherButton(true);
        statusWeather.Visibility = Store.Value.ShowWeatherInHeader ? Visibility.Visible : Visibility.Collapsed;
        DockPanel.SetDock(statusWeather, Dock.Left); statusRow.Children.Add(statusWeather);
        var tools = SystemToolsButton(true);
        DockPanel.SetDock(tools, Dock.Left); statusRow.Children.Add(tools);
        void Add(string label, string glyph, Action action)
        {
            var button = ActionButton(label, action); button.Style = (Style)FindResource("Think.IconButton");
            button.Content = Glyph(glyph, 13); button.Width = 28; button.Height = 24; button.Padding = new Thickness(3);
            controls.Children.Add(button);
        }
        void AddMini(string label, string glyph, MiniIslandKind kind)
        {
            var button = ActionButton(label, () => { }); button.Style = (Style)FindResource("Think.IconButton");
            button.Content = Glyph(glyph, 13); button.Width = 28; button.Height = 24; button.Padding = new Thickness(3);
            button.Click += (_, _) => ShowMini(kind, button);
            controls.Children.Add(button);
        }
        AddMini("Rede", "\uE701", MiniIslandKind.Network);
        AddMini("Volume", "\uE767", MiniIslandKind.Volume);
        statusKeyboard = ActionButton("Teclado virtual", () => Services.SystemActions.Run("osk.exe"));
        statusKeyboard.Style = (Style)FindResource("Think.IconButton");
        statusKeyboard.Content = Glyph("\uE765", 13); statusKeyboard.Width = 28; statusKeyboard.Height = 24; statusKeyboard.Padding = new Thickness(3);
        statusKeyboard.Visibility = Store.Value.HiddenQuickControls.Contains("keyboard") ? Visibility.Collapsed : Visibility.Visible;
        controls.Children.Add(statusKeyboard);
        Add("Notificações", "\uE7F4", () => Desktop.Chord(0x5B, 0x4E));
        Add("Expandir central de controle", "\uE70D", OpenFull);
        DockPanel.SetDock(controls, Dock.Right); statusRow.Children.Add(controls);
    }
    private bool AdaptiveCollapse => Store.Value.IslandAutoCollapse
        || (Store.Value.IslandReduceOverMaximized && maximizedOnDisplay);

    internal void RefreshForegroundLayout()
    {
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
        bool reduce = Store.Value.IslandReduceOverMaximized && maximizedOnDisplay
            && Stage == Services.IslandStage.Compact && !Store.IsEditing;
        if (reduce == edgeReduced) { SyncStatusSurface(); return false; }
        edgeReduced = reduce;
        SyncStatusSurface();
        ApplyAppearance();
        return true;
    }

    private void RefreshAdaptiveReveal()
    {
        statusClock.Text = DateTime.Now.ToString("HH:mm");
        if (UpdateAdaptiveState() && IsVisible) Resize(true);
    }
}
