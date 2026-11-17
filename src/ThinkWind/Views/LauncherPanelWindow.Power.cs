using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class LauncherPanelWindow
{
    private StackPanel BuildPowerActions()
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        Button PowerAction(string name, string glyph)
        {
            var button = ActionButton(name, () =>
            {
                modal = true;
                try
                {
                    Store.Flush();
                    SystemActions.Power(name, confirm: name is not ("Desligar" or "Atualizar e desligar"));
                }
                finally { modal = false; }
            });
            button.Style = (Style)FindResource("Think.IconButton");
            button.Content = Glyph(glyph, 18);
            button.Width = 38; button.Height = 36;
            button.Padding = new Thickness(4); button.Margin = new Thickness(2, 0, 2, 0);
            button.ToolTip = name;
            AutomationProperties.SetName(button, name);
            actions.Children.Add(button);
            return button;
        }

        PowerAction("Desligar", "\uE7E8");
        PowerAction("Suspender", "\uE708");
        PowerAction("Reiniciar", "\uE777");
        var update = PowerAction("Atualizar e desligar", "\uE895");
        update.SetResourceReference(Control.ForegroundProperty, "Think.Accent");
        void SyncUpdateButton() => update.Visibility = WindowsUpdatePower.UpdatePending ? Visibility.Visible : Visibility.Collapsed;
        // Use only the cached startup/hourly result and the existing visible-menu
        // timer. No Windows Update query is performed when opening or clicking.
        SyncUpdateButton();
        IsVisibleChanged += (_, _) => { if (IsVisible) SyncUpdateButton(); };
        visibility.Tick += (_, _) => { if (IsVisible) SyncUpdateButton(); };
        return actions;
    }
}
