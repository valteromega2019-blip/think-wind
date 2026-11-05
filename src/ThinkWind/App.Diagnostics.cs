using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using OrbyLauncher.Services;

namespace OrbyLauncher;

public partial class App
{
    private bool diagnosticsAttached;
    private void AttachDiagnostics()
    {
        if (diagnosticsAttached) return;
        diagnosticsAttached = true;
        DispatcherUnhandledException += DiagnosticDispatcherFailure;
        AppDomain.CurrentDomain.UnhandledException += DiagnosticDomainFailure;
        TaskScheduler.UnobservedTaskException += DiagnosticTaskFailure;
    }
    private void DetachDiagnostics()
    {
        if (!diagnosticsAttached) return;
        diagnosticsAttached = false;
        DispatcherUnhandledException -= DiagnosticDispatcherFailure;
        AppDomain.CurrentDomain.UnhandledException -= DiagnosticDomainFailure;
        TaskScheduler.UnobservedTaskException -= DiagnosticTaskFailure;
    }
    private static void DiagnosticDispatcherFailure(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        DiagnosticLog.Record("Interface WPF", args.Exception, fatal: true);
        // Do not set Handled: recording is not recovery from an unknown failure.
    }
    private static void DiagnosticDomainFailure(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception error) DiagnosticLog.Record("Processo", error, args.IsTerminating);
    }
    private static void DiagnosticTaskFailure(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        DiagnosticLog.Record("Tarefa não observada", args.Exception);
    }
}
