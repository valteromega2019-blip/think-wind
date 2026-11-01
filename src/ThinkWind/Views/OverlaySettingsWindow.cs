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
    internal OverlaySettingsWindow(SettingsStore store) : base(store)
    {
        settings = store; Title = "Orby Launcher — Personalização do overlay"; ShowActivated = true;
        var root = new DockPanel();
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var close = ActionButton("Fechar personalização do overlay", Close); close.Content = Glyph("\uE711", 13); close.SetResourceReference(StyleProperty, "Think.IconButton"); close.Width = close.Height = 30;
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = "Overlay", FontSize = 21, FontWeight = FontWeights.SemiBold });
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var editor = new OverlaySettingsContent(store);
        var scroll = new ScrollViewer { Content = editor, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer"); root.Children.Add(scroll); Surface.Child = root;
        store.Changed += AppearanceChanged;
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
