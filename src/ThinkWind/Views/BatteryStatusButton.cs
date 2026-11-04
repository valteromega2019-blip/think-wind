using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class BatteryStatusButton : Button
{
    private readonly BatteryIcon icon;
    internal BatteryIcon IconElement => icon;
    private readonly TextBlock number;
    private readonly double iconSlotWidth;
    private readonly bool strip;
    private readonly double scale;
    private BatteryReading? last;
    private bool lastNumbers;
    internal BatteryStatusButton(bool strip, double scale)
    {
        this.strip = strip; this.scale = scale;
        iconSlotWidth = strip ? 32 : 40 * scale;
        icon = new BatteryIcon { FontSize = strip ? 19 : 22 * scale, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = false };
        // Reserve the enlarged icon's space before applying RenderTransform.
        // Centering a bare icon in a horizontal StackPanel gave it only its
        // natural height, allowing ancestor layout clips to cut the hover.
        var iconSlot = new Grid { Width = iconSlotWidth, Height = strip ? 24 : 32 * scale, ClipToBounds = false };
        iconSlot.Children.Add(icon);
        number = new TextBlock { FontSize = strip ? 11 : 12 * scale, Width = strip ? 33 : 36 * scale, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, ClipToBounds = false };
        content.Children.Add(iconSlot); content.Children.Add(number); Content = content;
        SetResourceReference(StyleProperty, "Think.IconButton");
        Height = strip ? 24 : 32 * scale; Padding = new Thickness(0); ClipToBounds = false;
        AutomationProperties.SetHelpText(this, "Abrir as configurações de energia e bateria do Windows");
    }
    internal void Present(BatteryReading reading, bool showNumber)
    {
        if (last == reading && lastNumbers == showNumber) return;
        last = reading; lastNumbers = showNumber;
        icon.Charge = reading.Percentage ?? -1; icon.Charging = reading.Charging; icon.Plugged = reading.PluggedIn;
        number.Visibility = showNumber ? Visibility.Visible : Visibility.Collapsed;
        number.Text = reading.Percentage is int percentage ? $"{percentage}%" : "—";
        // Fixed width per preference, not per charge value: no layout movement
        // when the percentage changes between one, two and three digits.
        Width = iconSlotWidth + (showNumber ? number.Width + number.Margin.Left : 0);
        string level = reading.Percentage is int value ? $"{value}%" : "nível indisponível";
        string state = reading.Charging ? "Carregando" : reading.PluggedIn ? "Conectada à tomada" : "Usando bateria";
        string remaining = reading.RemainingMinutes is int minutes && minutes > 0 ? $" · autonomia estimada: {minutes / 60}h {minutes % 60:00}min" : "";
        string description = (BatteryStatusService.IsSimulation ? "[SIMULAÇÃO] " : "") + $"Bateria: {level} · {state}{remaining}" + (reading.Saving ? " · Economia de bateria" : "");
        ToolTip = description + "\nAbrir Energia e bateria";
        AutomationProperties.SetName(this, description);
    }
}
