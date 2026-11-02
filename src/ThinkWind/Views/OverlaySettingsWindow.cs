using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed class OverlaySettingsWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override string? MaterialOverride => "Acrylic";
    protected override bool HideOnClose => false;
    private readonly SettingsStore settings;
    private OutsideClickObserver? outsideClicks;
    internal OverlaySettingsWindow(SettingsStore store) : base(store)
    {
        settings = store; Title = "Orby Launcher — Personalização do overlay"; ShowActivated = true;
        KeyDown += (_, e) => { if (!e.Handled && e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; } };
        var root = new DockPanel();
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = ActionButton("Fechar personalização do overlay", Close); close.Content = Glyph("\uE711", 13); close.SetResourceReference(StyleProperty, "Think.IconButton"); close.Width = close.Height = 30;
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        var title = new StackPanel();
        var name = new TextBlock { Text = "Personalizar overlay", FontSize = 21, FontWeight = FontWeights.SemiBold };
        name.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); title.Children.Add(name);
        var subtitle = new TextBlock { Text = "Aparência, posição e informações dos jogos", FontSize = 12, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap };
        subtitle.SetResourceReference(TextBlock.ForegroundProperty, "Think.Muted"); title.Children.Add(subtitle);
        heading.Children.Add(title);
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var editor = new OverlaySettingsContent(store);
        var scroll = new ScrollViewer { Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer"); root.Children.Add(scroll); Surface.Child = root;
        store.Changed += AppearanceChanged;
        Loaded += (_, _) =>
        {
            outsideClicks ??= new OutsideClickObserver(this, () =>
            {
                if (System.Windows.Input.Mouse.Captured is null) Close();
            }, () => IsMouseOver || System.Windows.Input.Mouse.Captured is not null);
            outsideClicks.Start();
        };
        Closed += (_, _) => outsideClicks?.Dispose();
        Closed += (_, _) => { editor.Dispose(); store.Changed -= AppearanceChanged; };
    }
    private void AppearanceChanged(object? sender, EventArgs e) => RefreshAppearance();
    public override void Position()
    {
        var display = OverlayChrome.GetDisplay(settings.Value.MonitorIndex, settings.Value.MonitorDeviceName);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        int gap = (int)Math.Round(16 * display.Scale);
        int width = (int)Math.Round(Math.Min(620, display.Width - 32) * display.Scale);
        int height = (int)Math.Round(Math.Min(640, display.Height - 48) * display.Scale);
        OverlayChrome.Move(handle, new OverlayChrome.PixelRect
        {
            Left = display.Work.Left + gap, Top = display.Work.Top + gap,
            Right = display.Work.Left + gap + width, Bottom = display.Work.Top + gap + height
        });
        RefreshAppearance();
    }
}
