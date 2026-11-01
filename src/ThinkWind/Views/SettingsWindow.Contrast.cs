using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private readonly List<CheckBox> contrastControls = new();
    private bool syncingContrast;
    private void AddContrastControl(Panel parent)
    {
        var check = Check(parent, "Contraste automático do texto", store.Value.AutomaticTextContrast,
            value => { if (!syncingContrast) Change(p => p.AutomaticTextContrast = value); });
        check.ToolTip = "Ajustar a cor do texto quando necessário para facilitar a leitura sobre o fundo escolhido.";
        contrastControls.Add(check);
    }
    private void SyncContrastControls()
    {
        syncingContrast = true;
        try { foreach (var check in contrastControls) check.IsChecked = store.Value.AutomaticTextContrast; }
        finally { syncingContrast = false; }
    }
}
