using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal sealed partial class SettingsWindow
{
    internal void ShowUpdatesSection() => SelectSettingsTab("Atualizações");
    private void BuildUpdates()
    {
        if (Application.Current is not App app) return;
        var updates = app.Updates;
        var page = Section("Atualizações", "Consulta oficial ao GitHub. Downloads e instalações exigem sua autorização.");
        Note(page, "Versão instalada: " + AppBrand.Version + "\nRepositório: " + GithubUpdates.Repository);
        var available = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
        var notes = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 8) };
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, Margin = new Thickness(0, 4, 0, 8) };
        var check = new Button { Content = "Verificar agora", Margin = new Thickness(0, 4, 0, 4) };
        var download = new Button { Content = "Baixar e instalar", Margin = new Thickness(0, 4, 0, 4) };
        var cancel = new Button { Content = "Cancelar download", Margin = new Thickness(0, 4, 0, 4) };
        check.Click += async (_, _) => await updates.CheckAsync();
        cancel.Click += (_, _) => updates.Cancel();
        download.Click += async (_, _) =>
        {
            if (updates.Available is null || updates.Busy) return;
            string tag = updates.Available.Tag;
            if (MessageBox.Show(this, "Baixar e instalar " + tag + "? O launcher será encerrado após validar o arquivo. Suas configurações serão preservadas.", "Atualizar Orby Launcher", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await updates.DownloadAndInstallAsync(true, () =>
            {
                store.Flush(); if (store.SaveError is not null) throw new IOException(store.SaveError);
                string source = Path.GetDirectoryName(store.TutorialStatePath)!;
                string backup = Path.Combine(source, "Updates", "Backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
                Directory.CreateDirectory(backup);
                foreach (string name in new[] { "settings.json", "tutorial.json" })
                { string file = Path.Combine(source, name); if (File.Exists(file)) File.Copy(file, Path.Combine(backup, name)); }
            });
        };
        void Refresh()
        {
            available.Text = "Disponível: " + (updates.Available?.Tag ?? "—"); notes.Text = updates.Available?.Notes ?? "";
            message.Text = updates.Status;
            if (updates.Progress is { } p) { progress.Value = p.Total > 0 ? 100d * p.Bytes / p.Total : 0; message.Text += $"\n{p.Bytes / 1048576d:0.0} / {p.Total / 1048576d:0.0} MB · {p.BytesPerSecond / 1048576d:0.0} MB/s"; }
            check.IsEnabled = !updates.Busy; download.IsEnabled = !updates.Busy && updates.Available is not null;
            cancel.IsEnabled = updates.Busy;
        }
        foreach (UIElement element in new UIElement[] { available, notes, message, progress, check, download, cancel }) page.Children.Add(element);
        Note(page, "Segurança: SHA-256 da API oficial e assinatura Authenticode válida com o mesmo certificado do launcher. Versões sem assinatura são bloqueadas. Nenhum arquivo pessoal é enviado ao GitHub.");
        updates.Changed += Refresh; Closed += (_, _) => updates.Changed -= Refresh; Refresh();
    }
}
