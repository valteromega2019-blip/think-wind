using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ThinkWind.Services;
internal static class AppBrand
{
    internal const string Copyright = "© 2026 Think Wind. Créditos do projeto: valteromega2019-blip. Marcas e ícones de terceiros pertencem aos respectivos titulares.";
    internal static string Version => System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(
        System.Reflection.Assembly.GetExecutingAssembly())?.InformationalVersion.Split('+')[0] ?? "Alfa";
    internal static BitmapImage WindowIcon => new(new Uri("pack://application:,,,/ThinkWind;component/Assets/ThinkWind.ico"));
    internal static System.Drawing.Icon TrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/ThinkWind;component/Assets/ThinkWind.ico"))!.Stream;
        using var icon = new System.Drawing.Icon(stream, 32, 32);
        return (System.Drawing.Icon)icon.Clone();
    }
}
