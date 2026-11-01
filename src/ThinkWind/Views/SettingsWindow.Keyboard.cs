using System;
using System.Windows;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private void BuildKeyboardSettings()
    {
        var page = Section("Teclado", "Teclado personalizado com as cores, transparência e fonte do launcher. Ative o ícone Teclado virtual na seção Ícones para acessá-lo.");
        Check(page, "Usar o teclado original do Windows ao clicar no ícone", store.Value.KeyboardUseNative, value => Change(p => p.KeyboardUseNative = value));
        Check(page, "Exibir painel numérico", store.Value.KeyboardNumberPad, value => Change(p => p.KeyboardNumberPad = value));
        Slider(page, "Tamanho do teclado", .7, 1.3, store.Value.KeyboardScale, value => Change(p => p.KeyboardScale = value), "P0");
        page.Children.Add(Button("Layout português (Brasil)", () => Change(p => p.KeyboardLayout = "PT-BR")));
        page.Children.Add(Button("Layout inglês (US)", () => Change(p => p.KeyboardLayout = "US")));
        page.Children.Add(Button("Abrir / fechar teclado personalizado", () =>
        {
            if (Application.Current is App app) app.ToggleVirtualKeyboard(forceCustom: true);
        }));
        page.Children.Add(Button("Abrir teclado nativo e recursos de acessibilidade", () => SystemActions.Run("osk.exe")));
        Note(page, "Shift, Ctrl, Alt, AltGr e Win se aplicam à próxima tecla; Caps permanece ativo. Use #+= para símbolos e letras acentuadas. Clique primeiro no campo em que deseja escrever. O teclado não lê nem armazena o texto digitado.");
        Note(page, "A interface própria não altera o tema do teclado nativo. Predição, varredura, acesso à tela de entrada e operações protegidas continuam disponíveis no teclado original. O Windows pode bloquear entrada em aplicativos elevados; o launcher não contorna essa proteção.");
    }
}
