using System.IO;

namespace CalendarDesktop;

/// <summary>
/// App data root under LocalAppData. Migrates once from the legacy "CalendarApp" folder.
/// </summary>
public static class AppPaths
{
    public const string AppFolderName = "WGC";
    private const string LegacyFolderName = "CalendarApp";

    public static string Root
    {
        get
        {
            EnsureMigratedFromLegacy();
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName);
        }
    }

    public static string DatabasePath => Path.Combine(Root, "wgc.db");
    public static string LogsDirectory => Path.Combine(Root, "logs");
    public static string GoogleAuthDirectory => Path.Combine(Root, "GoogleAuth");
    public static string GoogleOAuthPath => Path.Combine(Root, "google-oauth.json");
    public static string CredentialsJsonPath => Path.Combine(Root, "credentials.json");

    private static bool _migrationChecked;

    private static void EnsureMigratedFromLegacy()
    {
        if (_migrationChecked) return;
        _migrationChecked = true;

        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var legacy = Path.Combine(local, LegacyFolderName);
            var current = Path.Combine(local, AppFolderName);

            if (!Directory.Exists(legacy))
            {
                Directory.CreateDirectory(current);
                return;
            }

            if (!Directory.Exists(current))
            {
                Directory.Move(legacy, current);
            }

            // Rename legacy db filename if present.
            var legacyDb = Path.Combine(current, "calendarapp.db");
            var newDb = Path.Combine(current, "wgc.db");
            if (File.Exists(legacyDb) && !File.Exists(newDb))
            {
                File.Move(legacyDb, newDb);
            }

            Directory.CreateDirectory(current);
        }
        catch
        {
            Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolderName));
        }
    }
}
