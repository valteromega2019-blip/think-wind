using System;
using System.IO;
using System.Windows.Threading;
using OrbyLauncher.Services;
using OrbyLauncher.Views;

namespace OrbyLauncher;
public partial class App
{
    internal UserRegistration? Registration { get; private set; }
    private UserRegistrationWindow? registrationWindow;
    private DispatcherTimer? registrationOffer;
    private void InitializeRegistration()
    {
        Registration = new UserRegistration(Path.Combine(Path.GetDirectoryName(store.TutorialStatePath)!, "registration.json"));
        // No network, timer or prompt until the independent endpoint is configured.
        if (!Registration.Configured) return;
        registrationOffer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        registrationOffer.Tick += async (_, _) =>
        {
            if (IsExiting || gameMode?.Active == true || gameMode?.FullscreenPresentation == true || tutorial?.IsVisible == true) return;
            registrationOffer.Stop();
            if (Registration.ShouldOffer) OpenRegistration();
            else await Registration.ReportActivityAsync();
        };
        registrationOffer.Start();
    }
    internal void OpenRegistration()
    {
        if (Registration is null || IsExiting || gameMode?.Active == true || gameMode?.FullscreenPresentation == true) return;
        if (registrationWindow is not null) { registrationWindow.Activate(); return; }
        registrationWindow = new UserRegistrationWindow(store, Registration);
        registrationWindow.Closed += (_, _) => registrationWindow = null;
        registrationWindow.Show();
    }
    private void CloseRegistration()
    {
        registrationOffer?.Stop(); Registration?.Dispose(); registrationWindow?.Retire();
    }
}
