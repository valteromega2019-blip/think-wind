using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ThinkWind.Services;

internal static class InstallationSetup
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { internal int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct AppBarData
    {
        internal uint Size;
        internal nint Window;
        internal uint Callback, Edge;
        internal Rect Bounds;
        internal nint Parameter;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindow(string className, string? title);
    [DllImport("shell32.dll")] private static extern nuint SHAppBarMessage(uint message, ref AppBarData data);

    // Runs once from the installer, without starting overlays or input services.
    internal static int Run()
    {
        bool changed = false;
        var data = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>(), Window = FindWindow("Shell_TrayWnd", null) };
        nuint original = 0;
        try
        {
            var settings = new SettingsStore();
            if (settings.LoadWarning is not null) throw new IOException(settings.LoadWarning);
            SystemActions.ApplyStartupDefault(settings, installation: true);
            if (data.Window == 0) throw new InvalidOperationException("A barra do Windows não está disponível. Inicie o Explorador e tente novamente.");
            original = SHAppBarMessage(4, ref data); // ABM_GETSTATE
            data.Parameter = (nint)(original | (nuint)1); // Preserve state, enable ABS_AUTOHIDE.
            SHAppBarMessage(10, ref data); // ABM_SETSTATE
            changed = (original & 1) == 0;
            if ((SHAppBarMessage(4, ref data) & 1) == 0)
                throw new InvalidOperationException("O Windows não confirmou a ocultação automática da barra.");
            settings.Value.HideWindowsTaskbar = true;
            settings.Save();
            return 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.Security.SecurityException)
        {
            if (changed) { data.Parameter = (nint)original; SHAppBarMessage(10, ref data); }
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThinkWind");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "installation-setup.log"), error.Message);
            }
            catch (Exception logError) when (logError is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
            return 1;
        }
    }
}
