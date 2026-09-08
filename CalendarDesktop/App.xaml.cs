using System.Windows;
using CalendarDesktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarDesktop;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;
    private EventReminderService? _reminders;
    private TrayIconService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppLog.Initialize();
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Logger.Fatal(args.Exception, "Unhandled UI exception");
            MessageBox.Show(
                $"Something went wrong:\n\n{args.Exception.Message}\n\nDetails were written to:\n{AppLog.LogDirectory}",
                "CalendarApp",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLog.Logger.Fatal(ex, "Unhandled domain exception");
            }
        };

        try
        {
            Services = AppServices.Build();
            _tray = Services.GetRequiredService<TrayIconService>();
            _tray.Initialize();
            _reminders = Services.GetRequiredService<EventReminderService>();
            _reminders.Start();
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            AppLog.Logger.Fatal(ex, "Startup failed");
            MessageBox.Show(
                $"CalendarApp failed to start:\n\n{ex.Message}",
                "CalendarApp",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _reminders?.Dispose();
        _tray?.Dispose();
        Services?.Dispose();
        AppLog.Close();
        base.OnExit(e);
    }
}
