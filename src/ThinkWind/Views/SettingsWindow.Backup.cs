using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;

internal sealed partial class SettingsWindow
{
    private void AddBackupPage()
    {
        var page = Section("Backup", "Agenda e personalizações em arquivos separados. Nada é enviado diretamente pelo Orby.");
        Note(page, "Pasta: " + store.BackupFolder);
        Note(page, "agenda.json guarda eventos e notas; configuracoes.json guarda personalizações e aplicativos fixados. HTML e ICS permitem consultar a agenda sem o Orby. O OneDrive sincroniza a pasta quando disponível e ativo.");
        var open = ClockDesign.Button("\uE8B7", "Abrir pasta", () => RunBackupAction(() => { Directory.CreateDirectory(store.BackupFolder); SystemActions.Run(store.BackupFolder); }, "Pasta aberta.")); page.Children.Add(open);
        var agenda = ((App)Application.Current).Alarms;
        page.Children.Add(ClockDesign.Text("Agenda", 16));
        page.Children.Add(ClockDesign.Button("\uE74E", "Salvar backup da agenda", () => RunBackupAction(agenda.ExportBackup, "Backup da agenda salvo.")));
        page.Children.Add(ClockDesign.Button("\uE777", "Restaurar agenda…", () => RestoreFile(true)));
        page.Children.Add(ClockDesign.Text("Personalizações e aplicativos", 16));
        page.Children.Add(ClockDesign.Button("\uE74E", "Salvar backup das configurações", () => RunBackupAction(store.ExportBackup, "Backup das configurações salvo.")));
        page.Children.Add(ClockDesign.Button("\uE777", "Restaurar configurações…", () => RestoreFile(false)));
        Note(page, "Os backups são atualizados ao salvar alterações. Restaurar substitui somente a categoria escolhida. Os alarmes independentes são preservados ao restaurar a agenda. Uma cópia anterior é guardada no perfil local antes da restauração. Um backup pode conter nomes de aplicativos, caminhos e notas pessoais.");
    }
    private void RunBackupAction(Action action, string success)
    {
        try { action(); status.Text = success; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
        { status.Text = "Não foi possível concluir: " + error.Message; }
    }
    private void RestoreFile(bool agenda)
    {
        updateConfirmationOpen = true;
        try
        {
            var picker = new OpenFileDialog
            {
                Title = agenda ? "Restaurar agenda do Orby" : "Restaurar configurações do Orby",
                Filter = "Backup JSON do Orby (*.json)|*.json", CheckFileExists = true,
                InitialDirectory = Directory.Exists(store.BackupFolder) ? store.BackupFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                FileName = agenda ? "agenda.json" : "configuracoes.json"
            };
            if (picker.ShowDialog(this) != true) return;
            // Fully parse before asking to replace any current information.
            var events = agenda ? BackupReader.Agenda(picker.FileName) : null;
            var preferences = agenda ? null : BackupReader.Settings(picker.FileName);
            string category = agenda ? $"os eventos e notas atuais por {events!.Length} itens do backup" : "as personalizações e os aplicativos fixados atuais pelo backup";
            if (MessageBox.Show(this, "Substituir " + category + "? Uma cópia dos dados atuais será guardada antes de restaurar.", "Restaurar backup · Orby", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            RunBackupAction(() =>
            {
                if (agenda) ((App)Application.Current).Alarms.RestoreAgenda(events!);
                else store.RestoreBackup(preferences!);
            }, "Backup restaurado. Suas alterações foram aplicadas.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { status.Text = "Backup recusado: " + error.Message; }
        finally { updateConfirmationOpen = false; }
    }
}
