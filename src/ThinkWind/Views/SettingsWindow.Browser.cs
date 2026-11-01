using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private void BuildBrowserIntegration(StackPanel panel)
    {
        Note(panel, "Prévias de abas · extensão opcional para Chrome, Edge e navegadores Chromium compatíveis. Instale a pasta BrowserExtension como extensão descompactada, autorize a captura no navegador e cole o ID abaixo. Sem a extensão, permanece a prévia nativa.");
        var extensionId = new TextBox { MaxLength = 32, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(4, 4, 4, 4), ToolTip = "ID da extensão · 32 letras mostrado no navegador" };
        System.Windows.Automation.AutomationProperties.SetName(extensionId, "ID da extensão Orby de abas");
        panel.Children.Add(extensionId);
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(4, 4, 4, 8) };
        void Run(Action action, string success)
        {
            try { action(); result.Text = success; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or System.Security.SecurityException)
            { result.Text = error.Message; }
        }
        panel.Children.Add(Button("Abrir pasta da extensão", () => Run(() =>
        {
            if (!Directory.Exists(BrowserBridge.ExtensionFolder)) throw new IOException("A pasta BrowserExtension não está nesta compilação.");
            SystemActions.Run(BrowserBridge.ExtensionFolder);
        }, "No navegador, abra Extensões, ative o modo de desenvolvedor e escolha Carregar sem compactação.")));
        panel.Children.Add(Button("Conectar ID da extensão", () => Run(() => BrowserBridge.Register(extensionId.Text.Trim()), "Conector registrado neste usuário. Ative as prévias na extensão e visite suas abas.")));
        panel.Children.Add(Button("Apagar capturas locais", () => Run(BrowserBridge.ClearSnapshots, "Capturas apagadas. Desative a extensão se não quiser novas capturas.")));
        panel.Children.Add(Button("Desconectar extensão e apagar capturas", () => Run(BrowserBridge.Disconnect, "Conector desconectado. Você também pode remover a extensão no navegador.")));
        panel.Children.Add(result);
    }
}
