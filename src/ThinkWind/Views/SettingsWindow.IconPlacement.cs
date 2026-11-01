using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Linq;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private static string SelectionGlyph(string id, bool module = false)
    {
        if (!module) return id == "weather" ? "\uE753" : LayoutCatalog.QuickControls.FirstOrDefault(control => control.Id == id)?.Glyph ?? "\uE713";
        return id switch
        {
            "connections" => "\uE701", "media" => "\uE8D6", "audio" => "\uE767", "metrics" => "\uE9D9",
            "calendar" => "\uE787", "calculator" => "\uE8EF", "weather" => "\uE753", "notes" => "\uE70B", "favorites" => "\uE734", _ => "\uE713"
        };
    }
    private void AddDisabledIconOption(Panel parent, string id, string name)
    {
        var disabled = Check(parent, "Ocultar nas duas áreas", store.Value.DisabledQuickControls.Contains(id), value => Change(p =>
        {
            if (value) { if (!p.DisabledQuickControls.Contains(id)) p.DisabledQuickControls.Add(id); }
            else p.DisabledQuickControls.Remove(id);
        }));
        disabled.Margin = new Thickness(26, 0, 0, 12); disabled.FontSize = 10;
        disabled.Style = SelectionCircleStyle.Create(SelectionGlyph(id));
        disabled.ToolTip = "Desativar " + name + " na barra principal e nos acessos adicionais";
        AutomationProperties.SetName(disabled, "Ocultar " + name + " nas duas áreas");
    }
}
