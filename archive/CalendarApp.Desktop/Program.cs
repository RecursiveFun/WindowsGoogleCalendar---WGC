using CalendarApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace CalendarApp.Desktop;

internal static class Program
{
    private const string DefaultUrl = "http://127.0.0.1:5002";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        try
        {
            var contentRoot = ResolveContentRoot();
            Directory.SetCurrentDirectory(contentRoot);
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

            Log($"Content root: {contentRoot}");
            Log($"Starting web host at {DefaultUrl}");

            var webApp = CalendarWebHost.CreateAsync(args, contentRoot, DefaultUrl)
                .GetAwaiter()
                .GetResult();

            webApp.StartAsync().GetAwaiter().GetResult();

            var url = GetListeningUrl(webApp) ?? DefaultUrl;
            Log($"Listening on: {url}");

            Application.ApplicationExit += (_, _) =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    webApp.StopAsync(cts.Token).GetAwaiter().GetResult();
                    webApp.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log($"Shutdown error: {ex}");
                }
            };

            Application.Run(new MainForm(url));
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            MessageBox.Show(
                $"Could not start CalendarApp.\n\n{ex.Message}\n\nDetails were written to desktop-startup.log",
                "CalendarApp",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string? GetListeningUrl(WebApplication app)
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        return addresses?.FirstOrDefault();
    }

    private static string ResolveContentRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var nested = Path.Combine(dir.FullName, "CalendarApp");
            if (File.Exists(Path.Combine(nested, "CalendarApp.csproj"))
                && Directory.Exists(Path.Combine(nested, "wwwroot")))
            {
                return nested;
            }

            if (File.Exists(Path.Combine(dir.FullName, "CalendarApp.csproj"))
                && Directory.Exists(Path.Combine(dir.FullName, "wwwroot")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the CalendarApp web content (wwwroot). Run the desktop app from the solution folder.");
    }

    private static void Log(string message)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CalendarApp",
                "desktop-startup.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.AppendAllText(path, $"[{DateTime.Now:u}] {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore logging failures
        }
    }
}
