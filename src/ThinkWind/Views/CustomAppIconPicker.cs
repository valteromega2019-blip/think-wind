using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OrbyLauncher.Services;

namespace OrbyLauncher.Views;
internal static class CustomAppIconPicker
{
    internal static async Task PickAsync(Window owner, SettingsStore store, string target, string? appId)
    {
        var dialog = new OpenFileDialog { Title = "Personalizar ícone (experimental) · PNG ou ICO transparente",
            Filter = "Ícones transparentes (*.png;*.ico)|*.png;*.ico", CheckFileExists = true, Multiselect = false };
        try
        {
            if (dialog.ShowDialog(owner) != true) return;
            string file = dialog.FileName;
            string image = await Task.Run(() => CustomAppIcons.Import(file));
            CustomAppIcons.Set(store, target, appId, image);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or ArgumentException
            or InvalidOperationException or NotSupportedException or FormatException or System.Runtime.InteropServices.COMException)
        { MessageBox.Show(owner, error.Message, "Personalizar ícone", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}
