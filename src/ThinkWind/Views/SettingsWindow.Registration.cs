using System;
using System.Windows;
using System.Windows.Controls;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    private Action? refreshRegistration;
    private void BuildRegistration()
    {
        if (Application.Current is not App { Registration: { } registration } app) return;
        var page = Section("Cadastro", "Opcional, sem senha. Participe da contagem de usuários do Orby.");
        var state = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };
        void Present() => state.Text = registration.Registered
            ? "Participando como " + registration.Name + " · " + registration.Email
            : registration.Configured ? "Você pode usar o Orby normalmente sem cadastro." : "O serviço online ainda está em preparação. O Orby continua funcionando normalmente.";
        Present(); page.Children.Add(state);
        page.Children.Add(Button("Preencher cadastro", () => app.OpenRegistration()));
        var remove = new Button { Content = "Excluir meu cadastro e parar os envios", Margin = new Thickness(0, 8, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
        remove.SetResourceReference(StyleProperty, "Think.IconButton");
        remove.Click += async (_, _) =>
        {
            remove.IsEnabled = false;
            try { await registration.RemoveAsync(); Present(); }
            catch (Exception error) when (error is System.Net.Http.HttpRequestException or OperationCanceledException or System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
            { state.Text = "Não foi possível confirmar a exclusão. Tente novamente com conexão à internet."; }
            finally { remove.IsEnabled = true; }
        };
        page.Children.Add(remove);
        Note(page, "O nome permanece neste PC. O servidor recebe o e-mail apenas para gerar um identificador protegido e não guarda o endereço. A participação registra atividade no máximo uma vez ao dia, sem histórico de aplicativos, hardware, arquivos ou localização. Não é uma conta autenticada; o e-mail não é verificado.");
        refreshRegistration = Present;
    }
}
