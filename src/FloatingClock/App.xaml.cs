using System;
using System.Threading.Tasks;
using System.Windows;
using FloatingClock.Models;
using FloatingClock.Services;

namespace FloatingClock;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Global exception handlers ──
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                ErrorLogger.LogError(ex, "Unhandled AppDomain exception");
        };

        DispatcherUnhandledException += (_, args) =>
        {
            ErrorLogger.LogError(args.Exception, "Unhandled Dispatcher exception");
            args.Handled = true;
            MessageBox.Show(
                $"An unexpected error occurred:\n{args.Exception.Message}\n\nCheck the log folder for details.",
                "Floating Clock — Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLogger.LogError(args.Exception, "Unobserved Task exception");
            args.SetObserved();
        };

        ErrorLogger.LogInfo("Application started.");

        // Apply saved theme
        var settings = AppSettings.Load();
        ThemeManager.ApplyTheme(settings.Theme);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ErrorLogger.LogInfo("Application exiting.");
        base.OnExit(e);
    }
}
