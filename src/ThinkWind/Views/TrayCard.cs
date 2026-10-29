using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ThinkWind.Native;
using ThinkWind.Services;

namespace ThinkWind.Views;
internal sealed class TrayCard : StackPanel
{
    private readonly WrapPanel icons = new();
    private readonly TextBlock status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4) };
    private readonly StackPanel navigation = new() { Orientation = Orientation.Horizontal };
    private List<TrayEntry> entries = new();
    private bool reading;
    private int page;
    private DateTime nextRefresh;
    internal event Action? LayoutChanged;
    internal TrayCard()
    {
        Children.Add(icons); Children.Add(navigation); Children.Add(status);
        var native = new Button { Content = "Bandeja do Windows", Margin = new Thickness(4), ToolTip = "Focar a área de notificação original (Win+B)" };
        native.Click += (_, _) => Desktop.Chord(0x5B, 0x42); Children.Add(native);
        Render();
    }
    internal async void Refresh()
    {
        if (reading || DateTime.UtcNow < nextRefresh) return;
        reading = true; nextRefresh = DateTime.UtcNow.AddSeconds(5);
        try
        {
            // One outstanding provider call at a time, even if Explorer is unresponsive.
            var pending = TrayService.ReadAsync();
            if (await Task.WhenAny(pending, Task.Delay(1800)) != pending) status.Text = "Bandeja ocupada. Use o acesso nativo.";
            entries = await pending; Render();
        }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        { status.Text = "A bandeja está indisponível. Use o acesso nativo."; }
        finally { reading = false; }
    }
    private void Render()
    {
        icons.Children.Clear(); navigation.Children.Clear();
        int pages = Math.Max(1, (entries.Count + 7) / 8); page = Math.Clamp(page, 0, pages - 1);
        foreach (var entry in entries.GetRange(Math.Min(page * 8, entries.Count), Math.Min(8, Math.Max(0, entries.Count - page * 8))))
        {
            var button = new Button { Width = 40, Height = 40, Padding = new Thickness(4), Margin = new Thickness(3), ToolTip = entry.Name, Content = entry.Icon is not null ? new Image { Source = entry.Icon, Width = 24, Height = 24 } : new TextBlock { Text = entry.Name[..1], HorizontalAlignment = HorizontalAlignment.Center } };
            System.Windows.Automation.AutomationProperties.SetName(button, entry.Name);
            button.Click += async (_, _) => { if (!await TrayService.InvokeAsync(entry, false)) status.Text = "O aplicativo não expôs esse comando. Use a bandeja do Windows."; };
            button.PreviewMouseRightButtonUp += async (_, e) => { e.Handled = true; if (!await TrayService.InvokeAsync(entry, true)) status.Text = "Abra o menu pela bandeja do Windows."; };
            icons.Children.Add(button);
        }
        if (pages > 1)
        {
            var back = new Button { Content = "‹", IsEnabled = page > 0 }; back.Click += (_, _) => { page--; Render(); };
            var forward = new Button { Content = "›", IsEnabled = page + 1 < pages }; forward.Click += (_, _) => { page++; Render(); };
            navigation.Children.Add(back); navigation.Children.Add(new TextBlock { Text = $"{page + 1} / {pages}", Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center }); navigation.Children.Add(forward);
        }
        status.Text = entries.Count == 0 ? "O Windows não expôs ícones acessíveis. Abra a bandeja nativa." : "Ícones visíveis da bandeja · botão direito para o menu nativo";
        LayoutChanged?.Invoke();
    }
}
