using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed class UserRegistrationWindow : FloatingWindow
{
    protected override bool AttachedTop => false;
    protected override bool HideOnClose => false;
    protected override string? MaterialOverride => "Acrylic";
    protected override double TextScale => Math.Clamp(Store.Value.FontScale, .8, 1.2);
    private bool busy;
    internal UserRegistrationWindow(SettingsStore settings, UserRegistration registration) : base(settings)
    {
        Title = "Orby · Cadastro opcional"; ShowActivated = true;
        Width = 420; MaxWidth = Math.Max(320, SystemParameters.WorkArea.Width - 24);
        MaxHeight = Math.Max(320, SystemParameters.WorkArea.Height - 24);
        SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SetResourceReference(ForegroundProperty, "Think.Text");
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(new TextBlock { Text = "Faça parte do Orby", FontSize = 23, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = "Cadastro opcional, sem senha. Ajude a contar quantas pessoas usam o aplicativo.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 16) });
        body.Children.Add(new TextBlock { Text = "Nome" });
        var name = new TextBox { Text = registration.Name, MaxLength = 80, Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(10) };
        body.Children.Add(name);
        body.Children.Add(new TextBlock { Text = "E-mail" });
        var email = new TextBox { Text = registration.Email, MaxLength = 254, Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(10) };
        body.Children.Add(email);
        body.Children.Add(new TextBlock { Text = "Seu nome fica neste PC. O e-mail gera um identificador no servidor, sem guardar o endereço. Ao participar, será enviado um sinal de uso no máximo uma vez ao dia. Sem senha e sem validação do e-mail. Você pode excluir o cadastro em Personalizar → Cadastro.", FontSize = 11, Opacity = .85, TextWrapping = TextWrapping.Wrap });
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 4) };
        message.SetResourceReference(TextBlock.ForegroundProperty, "Think.Text"); body.Children.Add(message);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var skip = ActionButton("Pular", () => { registration.Skip(); Close(); });
        var submit = new Button { Content = "Participar", Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
        submit.SetResourceReference(StyleProperty, "Think.IconButton");
        submit.Click += async (_, _) =>
        {
            if (busy) return; busy = true; submit.IsEnabled = skip.IsEnabled = name.IsEnabled = email.IsEnabled = false;
            message.Text = "Enviando…";
            try { await registration.RegisterAsync(name.Text, email.Text); busy = false; Close(); }
            catch (Exception error) when (error is System.Net.Http.HttpRequestException or OperationCanceledException or System.IO.IOException
                or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
            { message.Text = error is InvalidOperationException ? error.Message : "Não foi possível concluir o cadastro. Confira a conexão ou use Pular."; }
            finally { busy = false; submit.IsEnabled = skip.IsEnabled = name.IsEnabled = email.IsEnabled = true; }
        };
        actions.Children.Add(skip); actions.Children.Add(submit); body.Children.Add(actions);
        Surface.Child = body;
        Loaded += (_, _) => name.Focus();
        Closing += (_, args) => { if (busy) args.Cancel = true; else registration.Skip(); };
        PreviewKeyDown += (_, args) => { if (args.Key == Key.Escape && !busy) { Close(); args.Handled = true; } };
    }
    public override void Position()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + Math.Max(12, (area.Height - (ActualHeight > 0 ? ActualHeight : 440)) / 2);
    }
}
