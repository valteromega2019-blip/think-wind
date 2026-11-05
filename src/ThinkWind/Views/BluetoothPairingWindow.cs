using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Windows.Devices.Enumeration;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed record BluetoothPairingReply(bool Accepted, string Pin = "");

internal sealed class BluetoothPairingWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool UsesIslandAppearance => true;
    protected override bool HideOnClose => false;
    protected override string? MaterialOverride => "Acrylic";
    private readonly TaskCompletionSource<BluetoothPairingReply> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly StackPanel panel = new() { Margin = new Thickness(8) };
    private BluetoothPairingReply reply = new(false);
    internal Task<BluetoothPairingReply> Completion => completion.Task;

    internal BluetoothPairingWindow(SettingsStore store, string deviceName, string instruction,
        DevicePairingKinds kind = DevicePairingKinds.ConfirmOnly, string pin = "") : base(store)
    {
        Title = "Orby Launcher — Bluetooth"; ShowActivated = true;
        var heading = new DockPanel();
        var close = ActionButton("Cancelar", Close); close.Content = Glyph("\uE711", 13);
        close.Width = close.Height = 28; close.Padding = new Thickness(4); close.SetResourceReference(StyleProperty, "Think.IconButton");
        DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close);
        heading.Children.Add(new TextBlock { Text = "Bluetooth", FontSize = 18, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(heading);
        var icon = Glyph("\uE702", 30); icon.Margin = new Thickness(0, 12, 0, 10);
        icon.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); panel.Children.Add(icon);
        panel.Children.Add(new TextBlock { Text = DiagnosticSanitizer.Limit(deviceName, 120), FontSize = 17,
            FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
        panel.Children.Add(new TextBlock { Text = instruction, TextWrapping = TextWrapping.Wrap, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) });
        if (kind is DevicePairingKinds.DisplayPin or DevicePairingKinds.ConfirmPinMatch)
        {
            var code = new TextBlock { Text = pin, FontSize = 28, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 14) };
            code.SetResourceReference(TextBlock.ForegroundProperty, "Think.Accent"); panel.Children.Add(code);
        }
        var input = new PasswordBox { MaxLength = 32, MinHeight = 38, Padding = new Thickness(10), Margin = new Thickness(0, 0, 0, 12) };
        System.Windows.Automation.AutomationProperties.SetName(input, "PIN do dispositivo Bluetooth");
        if (kind == DevicePairingKinds.ProvidePin) panel.Children.Add(input);
        var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ActionButton("Cancelar", Close));
        var accept = ActionButton(kind == DevicePairingKinds.ConfirmPinMatch ? "O código confere" : "Confirmar", () =>
        {
            if (kind == DevicePairingKinds.ProvidePin && string.IsNullOrWhiteSpace(input.Password)) return;
            reply = new(true, kind == DevicePairingKinds.ProvidePin ? input.Password : ""); Close();
        });
        accept.SetResourceReference(Control.BackgroundProperty, "Think.AccentSurface");
        accept.SetResourceReference(Control.ForegroundProperty, "Think.AccentInk");
        if (kind == DevicePairingKinds.ProvidePin)
        {
            accept.IsEnabled = false;
            input.PasswordChanged += (_, _) => accept.IsEnabled = !string.IsNullOrWhiteSpace(input.Password);
        }
        buttons.Children.Add(accept); panel.Children.Add(buttons); Surface.Child = panel;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => { Position(); if (kind == DevicePairingKinds.ProvidePin) input.Focus(); };
        Closed += (_, _) => { input.Password = ""; completion.TrySetResult(reply); };
    }

    public override void Position()
    {
        double width = Math.Min(380, Display.Work.Width / Display.Scale - 24);
        panel.Measure(new Size(Math.Max(180, width - 40), double.PositiveInfinity));
        double height = Math.Min(panel.DesiredSize.Height + 40, Display.Work.Height / Display.Scale - 24);
        var anchor = new OverlayChrome.PixelRect { Left = Display.Bounds.Left + Display.Bounds.Width / 2, Right = Display.Bounds.Left + Display.Bounds.Width / 2,
            Top = Display.Bounds.Top + 30, Bottom = Display.Bounds.Top + 30 };
        if (Owner is not null && OverlayChrome.GetWindowRect(new System.Windows.Interop.WindowInteropHelper(Owner).Handle, out var ownerBounds))
            anchor = ownerBounds;
        PlaceNear(width, height, anchor, false);
    }
}
