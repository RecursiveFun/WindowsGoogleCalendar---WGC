using System.IO;
using Serilog;
using Serilog.Events;

namespace CalendarDesktop.Services;

public static class AppLog
{
    public static ILogger Logger { get; private set; } = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.Debug()
        .CreateLogger();

    public static string LogDirectory { get; private set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalendarApp",
            "logs");

    public static void Initialize()
    {
        Directory.CreateDirectory(LogDirectory);
        var logPath = Path.Combine(LogDirectory, "calendarapp-.log");

        Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Debug()
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        Log.Logger = Logger;
        Logger.Information("CalendarApp starting (version {Version})",
            typeof(AppLog).Assembly.GetName().Version?.ToString() ?? "unknown");
    }

    public static void Close()
    {
        Logger.Information("CalendarApp shutting down");
        Log.CloseAndFlush();
    }
}
