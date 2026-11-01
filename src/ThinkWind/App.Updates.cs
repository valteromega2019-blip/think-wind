using System;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

namespace OrbyLauncher;
public partial class App
{
    private void InitializeUpdates()
    {
        Updates = new UpdateController(UpdateBlocked);
        Updates.InstallStarting += () => { IsExiting = true; Shutdown(); };
        Updates.Notify += () =>
        {
            if (updateNotice is not null || Updates.Available is null || IsExiting || gameMode?.Active == true) return;
            if (!Updates.MarkNotified()) return;
            updateNotice = new UpdateNotice(store, Updates.Available.Tag, () => { OpenSettings(); settings?.ShowUpdatesSection(); });
            updateNotice.Closed += (_, _) => updateNotice = null;
            updateNotice.Show();
        };
        if (!diagnosing) Updates.Start();
    }
    private bool UpdateBlocked()
    {
        if (IsExiting || gameMode?.Active == true || gameMode?.FullscreenPresentation == true) return true;
        try { return GameModeService.Probe(new Preferences { HideOverFullscreen = true }); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return true; }
    }
}
