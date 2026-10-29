using System;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ThinkWind.Services;
internal static class AppBrand
{
    internal static BitmapImage WindowIcon => new(new Uri("pack://application:,,,/ThinkWind;component/Assets/ThinkWind.ico"));
    internal static System.Drawing.Icon TrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/ThinkWind;component/Assets/ThinkWind.ico"))!.Stream;
        using var icon = new System.Drawing.Icon(stream, 32, 32);
        return (System.Drawing.Icon)icon.Clone();
    }
}
