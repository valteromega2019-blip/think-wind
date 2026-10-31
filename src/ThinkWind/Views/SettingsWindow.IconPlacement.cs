using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace ThinkWind.Views;

internal sealed partial class SettingsWindow
{
    private void AddDisabledIconOption(Panel parent, string id, string name)
    {
        var disabled = Check(parent, "Ocultar nas duas áreas", store.Value.DisabledQuickControls.Contains(id), value => Change(p =>
        {
            if (value) { if (!p.DisabledQuickControls.Contains(id)) p.DisabledQuickControls.Add(id); }
            else p.DisabledQuickControls.Remove(id);
        }));
        disabled.Margin = new Thickness(26, 0, 0, 12); disabled.FontSize = 10;
        disabled.ToolTip = "Desativar " + name + " na barra principal e nos acessos adicionais";
        AutomationProperties.SetName(disabled, "Ocultar " + name + " nas duas áreas");
    }
}
