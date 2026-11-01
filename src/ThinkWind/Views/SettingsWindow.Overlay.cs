using System;
using System.Windows;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private void BuildIntegratedOverlay()
    {
        var page = Section("Overlay", "Métricas, FPS, CPU, GPU, posição X/Y, tamanho e aparência. Todas as opções do overlay no mesmo lugar.");
        var editor = new OverlaySettingsContent(store);
        page.Children.Add(editor);
        Closed += (_, _) => editor.Dispose();
    }
}
