using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using ThinkWind.Native;

namespace ThinkWind.Services;
internal sealed record AppEntry(string Name, string Target, string Category, ImageSource? Icon, string? AppId = null);
internal sealed class ApplicationCatalog : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly StaWorker worker = new("Think Wind — aplicativos");
    private readonly List<FileSystemWatcher> watchers = new();
    private readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private bool loading, refreshAgain, disposed;
    private DateTime refreshed;
    internal IReadOnlyList<AppEntry> Items { get; private set; } = Array.Empty<AppEntry>();
    internal string? Warning { get; private set; }
    internal event Action? Changed;
    internal ApplicationCatalog(Dispatcher ui)
    {
        dispatcher = ui; debounce.Tick += (_, _) => { debounce.Stop(); _ = RefreshAsync(true); };
        foreach (var directory in StartFolders())
        {
            if (!Directory.Exists(directory)) continue;
            try
            {
                var watcher = new FileSystemWatcher(directory) { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
                watchers.Add(watcher); watcher.Created += OnFilesChanged; watcher.Deleted += OnFilesChanged; watcher.Changed += OnFilesChanged; watcher.Renamed += OnFilesChanged;
                watcher.Error += (_, _) => RequestRefresh(); watcher.EnableRaisingEvents = true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
    }
    private static string[] StartFolders() => new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) };
    private void OnFilesChanged(object sender, FileSystemEventArgs args) => RequestRefresh();
    private void RequestRefresh()
    { if (!disposed && !dispatcher.HasShutdownStarted) dispatcher.BeginInvoke(new Action(() => { if (disposed) return; debounce.Stop(); debounce.Start(); })); }
    internal async Task RefreshAsync(bool force = false)
    {
        if (disposed || (!force && DateTime.UtcNow - refreshed < TimeSpan.FromSeconds(30))) return;
        if (loading) { refreshAgain |= force; return; } loading = true;
        try
        {
            var result = await worker.Run(Read);
            if (disposed) return;
            Items = result.Items; Warning = result.Warning; refreshed = DateTime.UtcNow; Changed?.Invoke();
        }
        catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or InvalidOperationException or System.Threading.Tasks.TaskCanceledException)
        { Warning = "Não foi possível atualizar a biblioteca. Os aplicativos já carregados continuam disponíveis."; if (!disposed) Changed?.Invoke(); }
        finally { loading = false; if (refreshAgain && !disposed) { refreshAgain = false; RequestRefresh(); } }
    }
    private static (AppEntry[] Items, string? Warning) Read()
    {
        var result = new Dictionary<string, AppEntry>(StringComparer.OrdinalIgnoreCase); var icons = new ShellIcons();
        object? shell = null; string? warning = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application") ?? throw new COMException("Shell.Application indisponível."));
            foreach (var folder in StartFolders())
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var path in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint }).Where(path => Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".appref-ms", StringComparison.OrdinalIgnoreCase)))
                {
                    object? directory = null, item = null;
                    try
                    {
                        directory = ((dynamic)shell!).NameSpace(Path.GetDirectoryName(path)); item = ((dynamic)directory!).ParseName(Path.GetFileName(path));
                        string id = Convert.ToString(((dynamic)item!).ExtendedProperty("System.AppUserModel.ID")) ?? "";
                        string name = Path.GetFileNameWithoutExtension(path);
                        string category = Path.GetDirectoryName(Path.GetRelativePath(folder, path)) ?? "";
                        string resolved = icons.Resolve(path);
                        string extension = Path.GetExtension(resolved).ToLowerInvariant();
                        if (id.Length == 0 && extension is not (".exe" or ".com" or ".bat" or ".cmd" or ".msc" or ".lnk" or ".appref-ms")) continue;
                        string key = id.Length > 0 ? "id:" + id : "name:" + name + "|" + resolved;
                        result.TryAdd(key, new(name, path, category.Length > 0 ? category : "Aplicativos", icons.Get(path), id.Length > 0 ? id : null));
                    }
                    catch (Exception error) when (error is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or IOException or UnauthorizedAccessException) { }
                    finally { CoreAudio.Release(item); CoreAudio.Release(directory); }
                }
            }
            object? apps = null, entries = null;
            try
            {
                apps = ((dynamic)shell!).NameSpace("shell:AppsFolder"); entries = ((dynamic)apps!).Items();
                int count = ((dynamic)entries!).Count;
                for (int i = 0; i < count; i++)
                {
                    object? item = null;
                    try
                    {
                        item = ((dynamic)entries!).Item(i);
                        string name = ((dynamic)item!).Name;
                        string path = ((dynamic)item!).Path;
                        string id = Convert.ToString(((dynamic)item!).ExtendedProperty("System.AppUserModel.ID")) ?? "";
                        string target = File.Exists(path) ? path : @"shell:AppsFolder\" + (id.Length > 0 ? id : path);
                        string key = id.Length > 0 ? "id:" + id : "name:" + name + "|" + icons.Resolve(target);
                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path)) continue;
                        if (!id.Contains('!') && result.Values.Any(app => app.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))) continue;
                        result.TryAdd(key, new(name, target, id.Contains('!') ? "Microsoft Store / MSIX" : "Aplicativos", icons.Get(target), id.Length > 0 ? id : null));
                    }
                    catch (Exception error) when (error is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or ArgumentException) { }
                    finally { CoreAudio.Release(item); }
                }
            }
            catch (Exception error) when (error is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { warning = "AppsFolder indisponível; exibindo atalhos do Menu Iniciar."; }
            finally { CoreAudio.Release(entries); CoreAudio.Release(apps); }
        }
        finally { CoreAudio.Release(shell); }
        return (result.Values.DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase).ToArray(), warning);
    }
    public void Dispose() { disposed = true; debounce.Stop(); foreach (var watcher in watchers) watcher.Dispose(); worker.Dispose(); }
}
