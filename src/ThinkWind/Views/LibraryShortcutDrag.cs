using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using OrbyLauncher.Native;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal static class LibraryShortcutDrag
{
    private sealed record Payload(string Name, string Target);
    internal static bool Active { get; private set; }
    internal static bool IsShortcut(IDataObject data) => data.GetDataPresent(typeof(Payload));
    internal static bool Pin(IDataObject data, SettingsStore store)
    {
        if (data.GetData(typeof(Payload)) is not Payload app) return false;
        store.Update(p => { if (!p.Shortcuts.Exists(item => item.Target.Equals(app.Target, StringComparison.OrdinalIgnoreCase))) p.Shortcuts.Add(new(app.Name, app.Target)); });
        return true;
    }
    internal static void Attach(FrameworkElement element, AppEntry app, Func<bool> enabled)
    {
        Point? origin = null;
        element.PreviewMouseLeftButtonDown += (_, e) => { if (enabled()) origin = e.GetPosition(element); };
        element.PreviewMouseLeftButtonUp += (_, _) => origin = null;
        element.PreviewMouseMove += (_, e) =>
        {
            if (!enabled() || origin is null || e.LeftButton != MouseButtonState.Pressed) return;
            Vector delta = e.GetPosition(element) - origin.Value;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null;
            string directory = Path.Combine(Path.GetTempPath(), "OrbyShortcuts", Guid.NewGuid().ToString("N"));
            object? shell = null, shortcut = null;
            try
            {
                Directory.CreateDirectory(directory);
                string name = string.Join("_", app.Name.Split(Path.GetInvalidFileNameChars())).Trim().TrimEnd('.');
                if (name.Length == 0) name = "Aplicativo";
                if (name.Length > 80) name = name[..80];
                string path = Path.Combine(directory, name + ".lnk");
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                shortcut = ((dynamic)shell!).CreateShortcut(path);
                if (app.Target.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
                {
                    ((dynamic)shortcut).TargetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                    ((dynamic)shortcut).Arguments = app.Target;
                }
                else ((dynamic)shortcut).TargetPath = app.Target;
                ((dynamic)shortcut).Description = app.Name;
                ((dynamic)shortcut).Save();
                var data = new DataObject(); data.SetData(typeof(Payload), new Payload(app.Name, app.Target));
                data.SetData(DataFormats.FileDrop, new[] { path });
                Active = true;
                DragDrop.DoDragDrop(element, data, DragDropEffects.Copy | DragDropEffects.Link);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
            { DiagnosticLog.Record("Arrastar atalho", error); MessageBox.Show("Não foi possível criar o atalho.", "Orby Launcher"); }
            finally
            {
                Active = false; CoreAudio.Release(shortcut); CoreAudio.Release(shell);
                // Delete only the one temporary link and its now-empty folder.
                try
                {
                    foreach (string file in Directory.Exists(directory) ? Directory.GetFiles(directory, "*.lnk", SearchOption.TopDirectoryOnly) : Array.Empty<string>()) File.Delete(file);
                    if (Directory.Exists(directory)) Directory.Delete(directory, false);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
            e.Handled = true;
        };
    }
}
