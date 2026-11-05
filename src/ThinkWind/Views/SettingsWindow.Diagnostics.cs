using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    // Keep the user's draft when settings are rebuilt by theme/layout changes.
    // Drafts are memory-only: no personal description is saved automatically.
    private string diagnosticDescription = "", diagnosticPreview = "";
    private int diagnosticViewRevision;
    private bool diagnosticWindowClosed;

    private void AddDiagnosticsPage()
    {
        var page = Section("Diagnóstico e suporte", "Consulte erros locais e prepare um relato. Nada é enviado automaticamente.");
        int viewRevision = ++diagnosticViewRevision;
        var status = DiagnosticText("Use Atualizar registros para consultar o histórico.");
        var history = new StackPanel();
        var historyScroll = new ScrollViewer { Content = history, MaxHeight = 190,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        historyScroll.SetResourceReference(StyleProperty, "Think.SettingsScrollViewer");
        var refresh = DiagnosticAction("\uE72C", "Atualizar registros");
        page.Children.Add(refresh); page.Children.Add(status); page.Children.Add(historyScroll);
        Note(page, "Erros não tratados e falhas instrumentadas ficam apenas neste PC: até três arquivos de 512 KiB. Repetições idênticas em um minuto são reduzidas. Fechamentos forçados ou falhas nativas podem não gerar registro.");
        page.Children.Add(DiagnosticText("Descreva o problema e como reproduzir"));
        var description = DiagnosticInput("Descrição do problema", diagnosticDescription, 80, 4000);
        page.Children.Add(description);
        var prepare = DiagnosticAction("\uE8A5", "Preparar relatório"); page.Children.Add(prepare);
        page.Children.Add(DiagnosticText("Prévia do relatório — revise antes de publicar"));
        var preview = DiagnosticInput("Prévia editável do relatório", diagnosticPreview, 210, 24000); page.Children.Add(preview);
        var reviewed = new CheckBox
        {
            Content = DiagnosticText("Revisei o conteúdo e entendo que a issue será pública no GitHub."),
            Margin = new Thickness(0, 10, 0, 10)
        };
        page.Children.Add(reviewed);
        var actions = new WrapPanel();
        var copy = DiagnosticAction("\uE8C8", "Copiar relatório");
        var save = DiagnosticAction("\uE74E", "Salvar TXT");
        var open = DiagnosticAction("\uE8A7", "Abrir relato no GitHub");
        foreach (var button in new[] { copy, save, open }) { button.Margin = new Thickness(0, 4, 8, 4); actions.Children.Add(button); }
        page.Children.Add(actions);
        var message = DiagnosticText(""); page.Children.Add(message);
        Note(page, "Dados conhecidos de perfil, máquina, caminhos e credenciais são omitidos, mas revise também o que você digitou. Não inclua senhas nem arquivos pessoais. O navegador abre um rascunho; a publicação só acontece após sua confirmação no GitHub. Você precisará de uma conta GitHub.");
        DiagnosticSnapshot snapshot = new(Array.Empty<DiagnosticEntry>(), "");
        bool busy = false;
        bool Current() => !diagnosticWindowClosed && diagnosticViewRevision == viewRevision;
        void RefreshActions()
        {
            bool hasReport = !string.IsNullOrWhiteSpace(preview.Text);
            prepare.IsEnabled = !busy && !string.IsNullOrWhiteSpace(description.Text);
            refresh.IsEnabled = !busy; copy.IsEnabled = save.IsEnabled = !busy && hasReport;
            open.IsEnabled = !busy && hasReport && reviewed.IsChecked == true;
        }
        void ShowSnapshot()
        {
            history.Children.Clear();
            status.Text = snapshot.Entries.Length == 0 ? "Nenhum erro disponível no histórico local."
                : $"{snapshot.Entries.Length} registro(s) recente(s). Mais recentes primeiro.";
            if (snapshot.Notice.Length > 0) status.Text += "\n" + snapshot.Notice;
            foreach (var entry in snapshot.Entries.Take(12))
            {
                var text = DiagnosticText($"{entry.Utc.ToLocalTime():dd/MM HH:mm:ss} · {entry.Area}\n{DiagnosticSanitizer.Limit(entry.Message, 220)}");
                text.Margin = new Thickness(0, 6, 0, 8); history.Children.Add(text);
            }
        }
        async Task Load(bool build)
        {
            if (busy) return;
            busy = true; message.Text = "Lendo registros locais…"; RefreshActions();
            try
            {
                snapshot = await Task.Run(DiagnosticLog.Snapshot);
                if (!Current()) return;
                ShowSnapshot();
                if (build)
                {
                    preview.Text = DiagnosticReport.Build(description.Text, snapshot);
                    message.Text = "Relatório preparado. Revise a prévia e confirme a caixa antes de abrir o GitHub.";
                }
                else message.Text = "Histórico atualizado. Nenhum dado foi enviado.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
            { if (Current()) message.Text = "Não foi possível preparar o relatório. Sua descrição foi mantida."; }
            finally { if (Current()) { busy = false; reviewed.IsChecked = false; RefreshActions(); } }
        }
        description.TextChanged += (_, _) =>
        {
            diagnosticDescription = description.Text; preview.Clear(); reviewed.IsChecked = false; RefreshActions();
        };
        preview.TextChanged += (_, _) => { diagnosticPreview = preview.Text; reviewed.IsChecked = false; RefreshActions(); };
        reviewed.Checked += (_, _) =>
        {
            string sanitized = DiagnosticSanitizer.Redact(preview.Text, 24000);
            if (sanitized != preview.Text)
            {
                preview.Text = sanitized;
                message.Text = "Dados foram omitidos da prévia. Revise o texto atualizado e confirme novamente.";
            }
            RefreshActions();
        };
        reviewed.Unchecked += (_, _) => RefreshActions();
        refresh.Click += async (_, _) => await Load(false);
        prepare.Click += async (_, _) => await Load(true);
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(DiagnosticSanitizer.Redact(preview.Text, 24000)); message.Text = "Relatório copiado. Revise antes de colar em um local público."; }
            catch (System.Runtime.InteropServices.ExternalException) { message.Text = "Área de transferência ocupada. Tente novamente."; }
        };
        save.Click += async (_, _) =>
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Salvar relatório de diagnóstico", Filter = "Relatório de texto (*.txt)|*.txt",
                FileName = "Orby-relatorio-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt", DefaultExt = ".txt", AddExtension = true };
            updateConfirmationOpen = true;
            try
            {
                if (dialog.ShowDialog(this) != true) return;
                string report = DiagnosticSanitizer.Redact(preview.Text, 24000);
                busy = true; RefreshActions();
                await File.WriteAllTextAsync(dialog.FileName, report);
                if (Current()) message.Text = "Relatório salvo no local escolhido. Nenhum envio realizado.";
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
            { if (Current()) message.Text = "Não foi possível salvar. O relatório continua na prévia."; }
            finally { updateConfirmationOpen = false; if (Current()) { busy = false; RefreshActions(); } }
        };
        open.Click += (_, _) =>
        {
            if (reviewed.IsChecked != true || string.IsNullOrWhiteSpace(preview.Text)) return;
            try
            {
                // The public title must not reintroduce text the user removed
                // from the editable report. No raw description in the URL.
                string title = "[Orby " + AppBrand.Version + "] Relato de problema";
                var issue = DiagnosticReport.CreateIssue(title, preview.Text);
                Process.Start(new ProcessStartInfo(issue.Url.AbsoluteUri) { UseShellExecute = true });
                message.Text = issue.Summarized
                    ? "Rascunho aberto com resumo devido ao limite do link. Use Copiar relatório ou Salvar TXT para incluir o restante no GitHub. Confirme a publicação lá."
                    : "Rascunho aberto no navegador. Confirme a publicação no GitHub; abrir o link não envia o relato.";
            }
            catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or UriFormatException or System.Security.SecurityException)
            { message.Text = "Não foi possível abrir o navegador. Copie ou salve o relatório e acesse o GitHub manualmente."; }
        };
        RefreshActions();
    }

    private static TextBlock DiagnosticText(string text) => new()
    { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 5, 0, 7) };

    private static Button DiagnosticAction(string icon, string label)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        var glyph = Glyph(icon, 14); glyph.Margin = new Thickness(0, 0, 7, 0);
        content.Children.Add(glyph); content.Children.Add(DiagnosticText(label));
        var button = new Button { Content = content };
        AutomationProperties.SetName(button, label);
        return button;
    }

    private static TextBox DiagnosticInput(string name, string value, double height, int maximum)
    {
        var input = new TextBox { Text = value, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            Height = height, MaxLength = maximum, Padding = new Thickness(10), Margin = new Thickness(0, 4, 0, 8),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        AutomationProperties.SetName(input, name);
        return input;
    }
}
