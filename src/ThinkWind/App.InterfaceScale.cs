using System;
using System.Linq;
using System.Windows.Threading;
using OrbyLauncher.Native;
using OrbyLauncher.Views;

namespace OrbyLauncher;

public partial class App
{
    private double appliedInterfaceScale = 1;
    private void RefreshInterfaceScaleWindows()
    {
        if (appliedInterfaceScale == store.Value.InterfaceScale) return;
        appliedInterfaceScale = store.Value.InterfaceScale;
        foreach (var window in Windows.OfType<FloatingWindow>().Where(window => window.IsVisible).ToArray())
            window.RefreshSettings();
    }
    private string windowsScaleSignature = "";
    private bool scalePromptPending;
    private DispatcherTimer? scaleChangeDebounce;
    private InterfaceScaleWindow? scalePrompt;
    private string WindowsScaleSignature() => string.Join("|", System.Windows.Forms.Screen.AllScreens.Select((screen, index) =>
        screen.DeviceName + ":" + OverlayChrome.GetDisplay(index, screen.DeviceName).Scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

    internal void NotifyWindowsScaleChanged()
    {
        if (IsExiting || store is null) return;
        string next = WindowsScaleSignature();
        if (windowsScaleSignature.Length == 0) { windowsScaleSignature = next; return; }
        if (next != windowsScaleSignature) { windowsScaleSignature = next; scalePromptPending = true; }
        if (!scalePromptPending || scalePrompt is not null || gameMode?.Active == true) return;
        if (scaleChangeDebounce is null)
        {
            scaleChangeDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            scaleChangeDebounce.Tick += (_, _) =>
            {
                scaleChangeDebounce.Stop();
                if (IsExiting || gameMode?.Active == true || !scalePromptPending || scalePrompt is not null) return;
                scalePromptPending = false;
                scalePrompt = new InterfaceScaleWindow(store);
                scalePrompt.Closed += (_, _) => scalePrompt = null;
                scalePrompt.Show(); scalePrompt.Position(); scalePrompt.Activate();
            };
        }
        scaleChangeDebounce.Stop(); scaleChangeDebounce.Start();
    }
}
