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
    private static AppEntry? draggedApp;
    internal static bool Active { get; private set; }
    private static DateTime suppressLaunchUntil;
    internal static bool SuppressLaunch => Active || DateTime.UtcNow < suppressLaunchUntil;
    internal static bool IsShortcut(IDataObject data) => Active && draggedApp is not null;
    internal static bool Pin(IDataObject data, SettingsStore store)
    {
        if (!Active || draggedApp is not AppEntry app) return false;
        store.Update(p => { if (!p.Shortcuts.Exists(item => item.Target.Equals(app.Target, StringComparison.OrdinalIgnoreCase))) p.Shortcuts.Add(new(app.Name, app.Target)); });
        return true;
    }
    internal static void Attach(FrameworkElement element, AppEntry app, Func<bool> enabled)
    {
        Point? origin = null;
        bool suppressClick = false;
        element.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        { if (suppressClick) e.Handled = true; }), true);
        element.PreviewMouseLeftButtonDown += (_, e) => { if (enabled()) origin = e.GetPosition(element); };
        element.PreviewMouseLeftButtonUp += (_, _) => origin = null;
        element.PreviewMouseMove += (_, e) =>
        {
            if (!enabled() || origin is null || e.LeftButton != MouseButtonState.Pressed) return;
            Vector delta = e.GetPosition(element) - origin.Value;
            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            origin = null; suppressClick = true;
            string directory = Path.Combine(Path.GetTempPath(), "OrbyShortcuts", Guid.NewGuid().ToString("N"));
            object? shell = null, shortcut = null;
            try
            {
                Directory.CreateDirectory(directory);
                string name = string.Join("_", app.Name.Split(Path.GetInvalidFileNameChars())).Trim().TrimEnd('.');
                if (name.Length == 0) name = "Aplicativo";
                if (name.Length > 80) name = name[..80];
                string path = Path.Combine(directory, name + ".lnk");
                if (Path.GetExtension(app.Target).Equals(".lnk", StringComparison.OrdinalIgnoreCase) && File.Exists(app.Target))
                    File.Copy(app.Target, path);
                else
                {
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
                }
                // CF_HDROP must contain real files and standard Shell formats,
                // not a managed payload that an external process must deserialize.
                var data = new DataObject(DataFormats.FileDrop, new[] { path });
                data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes((int)DragDropEffects.Copy)));
                draggedApp = app; Active = true;
                if (element.IsMouseCaptured) element.ReleaseMouseCapture();
                DragDrop.DoDragDrop(element, data, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or COMException or InvalidOperationException)
            { DiagnosticLog.Record("Arrastar atalho", error); MessageBox.Show("Não foi possível criar o atalho.", "Orby Launcher"); }
            finally
            {
                Active = false; draggedApp = null; suppressLaunchUntil = DateTime.UtcNow.AddMilliseconds(350);
                CoreAudio.Release(shortcut); CoreAudio.Release(shell);
                element.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => suppressClick = false));
                // Explorer can finish copying after DoDragDrop returns. Keep
                // the link in the Windows temporary folder for that operation.
            }
            e.Handled = true;
        };
    }
}
