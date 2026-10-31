using System;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;

namespace OrbyLauncher.Services;
internal static class AppBrand
{
    internal const string Developer = "Think";
    internal const string AppUserModelId = "Think.OrbyLauncher";
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    internal static void InitializeWindowsIdentity() { SetCurrentProcessExplicitAppUserModelID(AppUserModelId); }
    private static readonly Lazy<BitmapImage> applicationImage = new(() =>
    {
        var image = new BitmapImage();
        image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 512;
        image.UriSource = new Uri("pack://application:,,,/OrbyLauncher;component/Assets/OrbyLauncher.png");
        image.EndInit(); image.Freeze(); return image;
    });
    internal static BitmapImage ApplicationImage => applicationImage.Value;
    internal const string Copyright = "© 2026 Think · Orby Launcher. Créditos do projeto: valteromega2019-blip. Marcas e ícones de terceiros pertencem aos respectivos titulares.";
    internal static string Version => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
        System.Reflection.Assembly.GetExecutingAssembly())?.InformationalVersion.Split('+')[0] ?? "Alfa";
    internal static BitmapImage WindowIcon => new(new Uri("pack://application:,,,/OrbyLauncher;component/Assets/OrbyLauncher.ico"));
    internal static System.Drawing.Icon TrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/OrbyLauncher;component/Assets/OrbyLauncher.ico"))!.Stream;
        using var icon = new System.Drawing.Icon(stream, 32, 32);
        return (System.Drawing.Icon)icon.Clone();
    }
}
