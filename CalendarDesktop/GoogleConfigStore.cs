using System.IO;
using System.Text.Json;
using CalendarDesktop.Services;

namespace CalendarDesktop;

public static class GoogleConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string UserConfigPath => AppPaths.GoogleOAuthPath;

    public static string BundledConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public static string CredentialsJsonPath => AppPaths.CredentialsJsonPath;

    public static GoogleCalendarOptions Load()
    {
        // Production: prefer credentials shipped with the app.
        var bundled = TryLoadBundled();
        if (IsUsable(bundled))
        {
            return bundled!;
        }

        // Dev / upgrade path: fall back to machine-local Desktop OAuth settings.
        var fromCredentialsJson = TryLoadCredentialsJson();
        if (IsUsable(fromCredentialsJson))
        {
            return fromCredentialsJson!;
        }

        var fromUserFile = TryLoadUserFile();
        if (IsUsable(fromUserFile))
        {
            return fromUserFile!;
        }

        return bundled ?? new GoogleCalendarOptions();
    }

    public static void Save(string clientId, string clientSecret)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(UserConfigPath)!);
        var payload = new GoogleOAuthFile
        {
            ClientId = clientId.Trim(),
            ClientSecret = clientSecret.Trim()
        };
        File.WriteAllText(UserConfigPath, JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static bool IsUsable(GoogleCalendarOptions? options) =>
        options != null
        && !string.IsNullOrWhiteSpace(options.ClientId)
        && !string.IsNullOrWhiteSpace(options.ClientSecret)
        && !options.ClientId.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    private static GoogleCalendarOptions? TryLoadBundled()
    {
        if (!File.Exists(BundledConfigPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(BundledConfigPath));
            if (!doc.RootElement.TryGetProperty("GoogleCalendar", out var section))
            {
                return null;
            }

            return new GoogleCalendarOptions
            {
                ClientId = section.TryGetProperty("ClientId", out var id) ? id.GetString() ?? "" : "",
                ClientSecret = section.TryGetProperty("ClientSecret", out var secret) ? secret.GetString() ?? "" : ""
            };
        }
        catch
        {
            return null;
        }
    }

    private static GoogleCalendarOptions? TryLoadCredentialsJson()
    {
        if (!File.Exists(CredentialsJsonPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(CredentialsJsonPath));
            if (!doc.RootElement.TryGetProperty("installed", out var installed))
            {
                return null;
            }

            return new GoogleCalendarOptions
            {
                ClientId = installed.GetProperty("client_id").GetString() ?? "",
                ClientSecret = installed.TryGetProperty("client_secret", out var secret)
                    ? secret.GetString() ?? ""
                    : ""
            };
        }
        catch
        {
            return null;
        }
    }

    private static GoogleCalendarOptions? TryLoadUserFile()
    {
        if (!File.Exists(UserConfigPath))
        {
            return null;
        }

        try
        {
            var user = JsonSerializer.Deserialize<GoogleOAuthFile>(File.ReadAllText(UserConfigPath), JsonOptions);
            if (user == null || string.IsNullOrWhiteSpace(user.ClientId))
            {
                return null;
            }

            return new GoogleCalendarOptions
            {
                ClientId = user.ClientId,
                ClientSecret = user.ClientSecret ?? ""
            };
        }
        catch
        {
            return null;
        }
    }

    private sealed class GoogleOAuthFile
    {
        public string ClientId { get; set; } = "";
        public string? ClientSecret { get; set; }
    }
}
