using System.IO;
using CalendarDesktop.Data;
using CalendarDesktop.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CalendarDesktop;

public static class AppServices
{
    public static ServiceProvider Build()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var dbPath = AppPaths.DatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);

        var googleOptions = GoogleConfigStore.Load();
        services.AddSingleton(Options.Create(googleOptions));
        AppLog.Logger.Information(
            "Google OAuth configured={Configured}",
            GoogleConfigStore.IsUsable(googleOptions));

        services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<EventService>();
        services.AddScoped<GoogleCalendarService>();
        services.AddSingleton<EventReminderService>();
        services.AddSingleton<TrayIconService>();

        var provider = services.BuildServiceProvider();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        }

        return provider;
    }
}
