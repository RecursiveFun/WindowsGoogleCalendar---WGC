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

    public static string LogDirectory { get; private set; } = AppPaths.LogsDirectory;

    public static void Initialize()
    {
        LogDirectory = AppPaths.LogsDirectory;
        Directory.CreateDirectory(LogDirectory);
        var logPath = Path.Combine(LogDirectory, "wgc-.log");

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
        Logger.Information("WGC starting (version {Version})",
            typeof(AppLog).Assembly.GetName().Version?.ToString() ?? "unknown");
    }

    public static void Close()
    {
        Logger.Information("WGC shutting down");
        Log.CloseAndFlush();
    }
}
