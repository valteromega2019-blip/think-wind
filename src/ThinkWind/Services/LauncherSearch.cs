using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ThinkWind.Native;

namespace ThinkWind.Services;
internal sealed record SearchBatch(IReadOnlyList<AppEntry> Items, string? Notice);
internal sealed class LauncherSearch : IDisposable
{
    private readonly StaWorker worker = new("Think Wind — índice do Windows");
    internal static readonly AppEntry[] Settings =
    {
        new("Configurações do Windows", "ms-settings:", "Configurações", null), new("Rede e Wi-Fi", "ms-settings:network-wifi", "Configurações", null),
        new("Bluetooth", "ms-settings:bluetooth", "Configurações", null), new("Som e volume", "ms-settings:sound", "Configurações", null),
        new("Tela e brilho", "ms-settings:display", "Configurações", null), new("Aplicativos instalados", "ms-settings:appsfeatures", "Configurações", null),
        new("Energia, bateria e suspensão", "ms-settings:powersleep", "Configurações", null), new("Notificações", "ms-settings:notifications", "Configurações", null),
        new("Personalização e temas", "ms-settings:personalization", "Configurações", null), new("Conta e informações do usuário", "ms-settings:yourinfo", "Configurações", null),
        new("Privacidade", "ms-settings:privacy", "Configurações", null), new("Armazenamento", "ms-settings:storagesense", "Configurações", null),
        new("Aplicativos padrão", "ms-settings:defaultapps", "Configurações", null), new("Área de transferência", "ms-settings:clipboard", "Configurações", null),
        new("Digitação e teclado", "ms-settings:typing", "Configurações", null), new("Data e hora", "ms-settings:dateandtime", "Configurações", null),
        new("Windows Update", "ms-settings:windowsupdate", "Configurações", null), new("Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Pastas", null),
        new("Imagens", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Pastas", null), new("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "Pastas", null)
    };
    internal Task<SearchBatch> FilesAsync(string query, CancellationToken token) => worker.Run(() => ReadIndex(query, token), token);
    internal Task<AppEntry[]> RecentAsync() => worker.Run(() =>
    {
        var directory = Environment.GetFolderPath(Environment.SpecialFolder.Recent); var icons = new ShellIcons();
        if (!Directory.Exists(directory)) return Array.Empty<AppEntry>();
        return new DirectoryInfo(directory).EnumerateFiles("*.lnk").OrderByDescending(file => file.LastWriteTimeUtc).Take(30)
            .Select(file => new AppEntry(Path.GetFileNameWithoutExtension(file.Name), file.FullName, "Recentes do Windows", icons.Get(file.FullName))).ToArray();
    });
    private static SearchBatch ReadIndex(string query, CancellationToken token)
    {
        var result = new List<AppEntry>(); object? connection = null, records = null;
        try
        {
            token.ThrowIfCancellationRequested();
            connection = Activator.CreateInstance(Type.GetTypeFromProgID("ADODB.Connection")!);
            ((dynamic)connection!).ConnectionTimeout = 2; ((dynamic)connection!).CommandTimeout = 3;
            ((dynamic)connection!).Open("Provider=Search.CollatorDSO;Extended Properties='Application=Windows';");
            string term = query.Replace("'", "''").Trim();
            if (term.Length == 0) return new(result, null);
            string sql = "SELECT TOP 60 System.ItemNameDisplay, System.ItemPathDisplay FROM SYSTEMINDEX WHERE SCOPE='file:' AND System.FileName LIKE '%" + term + "%' ORDER BY System.DateModified DESC";
            records = ((dynamic)connection!).Execute(sql); var icons = new ShellIcons();
            while (!((dynamic)records!).EOF && result.Count < 60)
            {
                token.ThrowIfCancellationRequested(); object? fields = null, nameField = null, pathField = null;
                try
                {
                    fields = ((dynamic)records!).Fields; nameField = ((dynamic)fields!).Item(0); pathField = ((dynamic)fields!).Item(1);
                    string name = Convert.ToString(((dynamic)nameField!).Value) ?? "", path = Convert.ToString(((dynamic)pathField!).Value) ?? "";
                    if (name.Length > 0 && Path.IsPathFullyQualified(path) && (File.Exists(path) || Directory.Exists(path))) result.Add(new(name, path, "Índice do Windows", icons.Get(path)));
                }
                finally { CoreAudio.Release(pathField); CoreAudio.Release(nameField); CoreAudio.Release(fields); }
                ((dynamic)records!).MoveNext();
            }
            return new(result, "Arquivos encontrados no índice do Windows; pastas não indexadas podem não aparecer.");
        }
        catch (Exception error) when (error is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or IOException or UnauthorizedAccessException or ArgumentException)
        { return new(result, "Índice do Windows indisponível. Aplicativos, atalhos recentes e configurações continuam pesquisáveis."); }
        finally
        {
            try { if (records is not null) ((dynamic)records).Close(); } catch (COMException) { }
            try { if (connection is not null) ((dynamic)connection).Close(); } catch (COMException) { }
            CoreAudio.Release(records); CoreAudio.Release(connection);
        }
    }
    public void Dispose() => worker.Dispose();
}
