using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ThinkWind.Services;

namespace ThinkWind.Views;

internal sealed class OverlayToggleButton : ToggleButton
{
    private readonly SettingsStore store;
    private bool syncing;
    internal OverlayToggleButton(SettingsStore settings)
    {
        store = settings;
        Width = 172; Height = 34; Padding = new Thickness(0); Margin = new Thickness(0, 0, 10, 0);
        FontSize = 12; FontWeight = FontWeights.Medium; Cursor = Cursors.Hand;
        BorderThickness = new Thickness(2);
        var circle = new FrameworkElementFactory(typeof(Border));
        // Oblong outline: two semicircular ends joined by straight sides.
        circle.SetValue(Border.CornerRadiusProperty, new CornerRadius(17));
        circle.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        circle.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(BorderBrushProperty));
        circle.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(BorderThicknessProperty));
        var layout = new FrameworkElementFactory(typeof(Grid));
        circle.AppendChild(layout);
        var thumb = new FrameworkElementFactory(typeof(Border), "Thumb");
        thumb.SetValue(WidthProperty, 30.0);
        thumb.SetValue(HeightProperty, 30.0);
        thumb.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        thumb.SetValue(Border.CornerRadiusProperty, new CornerRadius(15));
        thumb.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        thumb.SetResourceReference(Border.BackgroundProperty, "Think.Accent");
        layout.AppendChild(thumb);
        var content = new FrameworkElementFactory(typeof(ContentPresenter), "Label");
        content.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(MarginProperty, new Thickness(30, 0, 0, 0));
        content.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center); layout.AppendChild(content);
        var template = new ControlTemplate(typeof(ToggleButton)) { VisualTree = circle };
        var on = new Trigger { Property = IsCheckedProperty, Value = true };
        on.Setters.Add(new Setter(HorizontalAlignmentProperty, HorizontalAlignment.Right, "Thumb"));
        on.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension("Think.Background"), "Thumb"));
        on.Setters.Add(new Setter(MarginProperty, new Thickness(0, 0, 30, 0), "Label"));
        template.Triggers.Add(on);
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(OpacityProperty, .8)); template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(2))); template.Triggers.Add(focus);
        Template = template;
        Checked += (_, _) => Save(true); Unchecked += (_, _) => Save(false);
        Loaded += (_, _) => { store.Changed += Refresh; Refresh(null, EventArgs.Empty); };
        Unloaded += (_, _) => store.Changed -= Refresh;
        Refresh(null, EventArgs.Empty);
    }
    private void Save(bool enabled)
    { if (!syncing && store.Value.HardwareOverlayEnabled != enabled) store.Update(p => p.HardwareOverlayEnabled = enabled); }
    private void Refresh(object? sender, EventArgs e)
    {
        syncing = true;
        bool enabled = store.Value.HardwareOverlayEnabled;
        IsChecked = enabled; Content = enabled ? "OVERLAY ON" : "OVERLAY OFF";
        SetResourceReference(BackgroundProperty, enabled ? "Think.Accent" : "Think.Background");
        SetResourceReference(BorderBrushProperty, "Think.Accent");
        SetResourceReference(ForegroundProperty, enabled ? "Think.Background" : "Think.Text");
        string label = enabled ? "Overlay habilitado · aparece somente em jogos reconhecidos" : "Overlay desabilitado";
        ToolTip = label; AutomationProperties.SetName(this, label);
        syncing = false;
    }
}
