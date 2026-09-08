using CalendarDesktop.Data;
using CalendarDesktop.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using IOPath = System.IO.Path;

namespace CalendarDesktop.Services;

public class GoogleCalendarService
{
    private const string DesktopUserId = "desktop-user";
    private static readonly string[] Scopes = { CalendarService.Scope.Calendar };

    private readonly AppDbContext _db;
    private readonly GoogleCalendarOptions _options;
    private readonly string _tokenStorePath;

    public GoogleCalendarService(AppDbContext db, IOptions<GoogleCalendarOptions> options)
    {
        _db = db;
        _options = options.Value;
        _tokenStorePath = IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CalendarApp",
            "GoogleAuth");

        // Production: bundled appsettings first; LocalAppData is a fallback for debug/dev.
        var stored = GoogleConfigStore.Load();
        if (!string.IsNullOrWhiteSpace(stored.ClientId))
        {
            _options.ClientId = stored.ClientId;
            _options.ClientSecret = stored.ClientSecret;
        }
    }

    public bool IsConfigured => GoogleConfigStore.IsUsable(_options);

    public void ReloadOptionsFromDisk()
    {
        var stored = GoogleConfigStore.Load();
        _options.ClientId = stored.ClientId;
        _options.ClientSecret = stored.ClientSecret;
    }

    public async Task<bool> IsConnectedAsync()
    {
        var connection = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.UserId == DesktopUserId);
        return connection?.IsConnected == true;
    }

    public async Task<string?> GetConnectedEmailAsync()
    {
        var connection = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.UserId == DesktopUserId && c.IsConnected);
        return connection?.Email;
    }

    public async Task<(bool Success, string Message)> SignInAsync()
    {
        ReloadOptionsFromDisk();

        if (!IsConfigured)
        {
            return (false, "Google OAuth is not configured for this build. Ask the app publisher to embed a Desktop OAuth Client ID/Secret, or use Advanced Google setup.");
        }

        try
        {
            AppLog.Logger.Information("Starting Google sign-in");
            // Always clear stale tokens before a fresh interactive sign-in.
            if (System.IO.Directory.Exists(_tokenStorePath))
            {
                try { System.IO.Directory.Delete(_tokenStorePath, true); } catch { /* ignore */ }
            }

            System.IO.Directory.CreateDirectory(_tokenStorePath);

            // Official installed-app flow. Requires OAuth client type = Desktop app.
            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = _options.ClientId,
                    ClientSecret = _options.ClientSecret
                },
                Scopes,
                DesktopUserId,
                CancellationToken.None,
                new FileDataStore(_tokenStorePath, true));

            if (credential.Token.IsStale)
            {
                await credential.RefreshTokenAsync(CancellationToken.None);
            }

            var email = await TryGetEmailAsync(credential);
            await SaveConnectionAsync(credential, email);
            AppLog.Logger.Information("Google sign-in succeeded for {Email}", email ?? "(unknown)");

            return (true, string.IsNullOrEmpty(email)
                ? "Signed in with Google."
                : $"Signed in as {email}.");
        }
        catch (Exception ex)
        {
            AppLog.Logger.Error(ex, "Google sign-in failed");
            return (false,
                "Google sign-in failed.\n\n" +
                "Confirm the OAuth client is type Desktop app and Calendar API is enabled.\n\n" +
                $"Details: {ex.Message}");
        }
    }

    public async Task SignOutAsync()
    {
        var connection = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.UserId == DesktopUserId);
        if (connection != null)
        {
            connection.IsConnected = false;
            connection.AccessToken = null;
            connection.RefreshToken = null;
            connection.Email = null;
            connection.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        if (System.IO.Directory.Exists(_tokenStorePath))
        {
            try { System.IO.Directory.Delete(_tokenStorePath, true); } catch { /* ignore */ }
        }
    }

    public async Task<(bool Success, string Message, int Count)> PullAsync()
    {
        try
        {
            var service = await CreateCalendarServiceAsync();
            if (service == null)
            {
                return (false, "Not signed in to Google. Sign in first.", 0);
            }

            var windowStart = DateTimeOffset.Now.AddYears(-1);
            var windowEnd = DateTimeOffset.Now.AddYears(1);
            var seenGoogleIds = new HashSet<string>(StringComparer.Ordinal);
            var imported = 0;
            string? pageToken = null;

            do
            {
                var request = service.Events.List("primary");
                request.TimeMinDateTimeOffset = windowStart;
                request.TimeMaxDateTimeOffset = windowEnd;
                request.SingleEvents = true;
                request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                request.MaxResults = 250;
                request.ShowDeleted = true;
                request.PageToken = pageToken;

                var result = await request.ExecuteAsync();
                if (result.Items != null)
                {
                    foreach (var googleEvent in result.Items)
                    {
                        if (string.IsNullOrEmpty(googleEvent.Id))
                        {
                            continue;
                        }

                        if (string.Equals(googleEvent.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                        {
                            var cancelled = await _db.Events
                                .FirstOrDefaultAsync(e => e.GoogleEventId == googleEvent.Id);
                            if (cancelled != null)
                            {
                                _db.Events.Remove(cancelled);
                            }
                            continue;
                        }

                        seenGoogleIds.Add(googleEvent.Id);

                        var existing = await _db.Events
                            .FirstOrDefaultAsync(e => e.GoogleEventId == googleEvent.Id);

                        var start = ResolveDate(googleEvent.Start);
                        var end = googleEvent.End == null ? start.AddHours(1) : ResolveDate(googleEvent.End);
                        var allDay = !string.IsNullOrEmpty(googleEvent.Start?.Date);
                        // Google all-day ends are exclusive (midnight of the next day).
                        end = NormalizeImportedAllDayEnd(start, end, allDay);

                        if (existing == null)
                        {
                            _db.Events.Add(new CalendarEvent
                            {
                                Title = googleEvent.Summary ?? "(No title)",
                                Description = googleEvent.Description,
                                Location = googleEvent.Location,
                                StartDateTime = start,
                                EndDateTime = end,
                                IsAllDay = allDay,
                                GoogleEventId = googleEvent.Id,
                                IsSynced = true
                            });
                        }
                        else
                        {
                            existing.Title = googleEvent.Summary ?? existing.Title;
                            existing.Description = googleEvent.Description;
                            existing.Location = googleEvent.Location;
                            existing.StartDateTime = start;
                            existing.EndDateTime = end;
                            existing.IsAllDay = allDay;
                            existing.IsSynced = true;
                            existing.UpdatedAt = DateTime.UtcNow;
                        }

                        imported++;
                    }
                }

                pageToken = result.NextPageToken;
            }
            while (!string.IsNullOrEmpty(pageToken));

            // Remove local copies of Google events that were deleted (or no longer in range).
            var localStart = windowStart.LocalDateTime;
            var localEnd = windowEnd.LocalDateTime;
            var stale = await _db.Events
                .Where(e => e.GoogleEventId != null && e.GoogleEventId != "")
                .Where(e => e.StartDateTime <= localEnd && e.EndDateTime >= localStart)
                .ToListAsync();

            var removed = 0;
            foreach (var item in stale)
            {
                if (!seenGoogleIds.Contains(item.GoogleEventId!))
                {
                    _db.Events.Remove(item);
                    removed++;
                }
            }

            await _db.SaveChangesAsync();

            if (imported == 0 && removed == 0)
            {
                AppLog.Logger.Information("Google sync found no changes");
                return (true, "Connected, but no events found in the last/next year.", 0);
            }

            var parts = new List<string>();
            if (imported > 0) parts.Add($"pulled {imported}");
            if (removed > 0) parts.Add($"removed {removed} deleted");
            AppLog.Logger.Information("Google sync complete imported={Imported} removed={Removed}", imported, removed);
            return (true, $"Sync complete: {string.Join(", ", parts)} event(s).", imported);
        }
        catch (Exception ex)
        {
            AppLog.Logger.Error(ex, "Google pull failed");
            return (false, $"Google pull failed: {ex.Message}", 0);
        }
    }

    public async Task<(bool Success, string Message)> PushEventAsync(CalendarEvent localEvent)
    {
        try
        {
            var (service, authError) = await TryCreateCalendarServiceAsync();
            if (service == null)
            {
                return (false, authError ?? "Not signed in to Google. Sign in to sync events.");
            }

            var googleEvent = MapToGoogle(localEvent);

            if (string.IsNullOrEmpty(localEvent.GoogleEventId))
            {
                var created = await service.Events.Insert(googleEvent, "primary").ExecuteAsync();
                localEvent.GoogleEventId = created.Id;
            }
            else
            {
                await service.Events.Update(googleEvent, "primary", localEvent.GoogleEventId).ExecuteAsync();
            }

            localEvent.IsSynced = true;
            localEvent.UpdatedAt = DateTime.UtcNow;
            _db.Events.Update(localEvent);
            await _db.SaveChangesAsync();
            AppLog.Logger.Information("Pushed event {EventId} to Google as {GoogleEventId}", localEvent.Id, localEvent.GoogleEventId);
            return (true, "Event synced to Google Calendar.");
        }
        catch (Exception ex)
        {
            AppLog.Logger.Error(ex, "Google push failed for event {EventId}", localEvent.Id);
            return (false, $"Google sync failed: {ex.Message}");
        }
    }

    public async Task DeleteGoogleEventAsync(string? googleEventId)
    {
        if (string.IsNullOrEmpty(googleEventId)) return;

        var service = await CreateCalendarServiceAsync();
        if (service == null) return;

        try
        {
            await service.Events.Delete("primary", googleEventId).ExecuteAsync();
        }
        catch
        {
            // ignore remote delete failures
        }
    }

    private async Task<CalendarService?> CreateCalendarServiceAsync()
    {
        var (service, _) = await TryCreateCalendarServiceAsync();
        return service;
    }

    private async Task<(CalendarService? Service, string? Error)> TryCreateCalendarServiceAsync()
    {
        ReloadOptionsFromDisk();
        if (!IsConfigured)
        {
            return (null, "Google OAuth is not configured.");
        }

        try
        {
            if (!System.IO.Directory.Exists(_tokenStorePath))
            {
                return (null, "Not signed in to Google. Sign in first.");
            }

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                new ClientSecrets
                {
                    ClientId = _options.ClientId,
                    ClientSecret = _options.ClientSecret
                },
                Scopes,
                DesktopUserId,
                CancellationToken.None,
                new FileDataStore(_tokenStorePath, true));

            if (credential.Token.IsStale)
            {
                if (!await credential.RefreshTokenAsync(CancellationToken.None))
                {
                    return (null, "Google session expired. Sign in again.");
                }
            }

            await SaveConnectionAsync(credential, await TryGetEmailAsync(credential));

            return (new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "CalendarApp"
            }), null);
        }
        catch (Exception ex)
        {
            return (null, $"Could not reach Google Calendar: {ex.Message}");
        }
    }

    private async Task SaveConnectionAsync(UserCredential credential, string? email)
    {
        var connection = await _db.GoogleCalendarConnections
            .FirstOrDefaultAsync(c => c.UserId == DesktopUserId);

        if (connection == null)
        {
            connection = new GoogleCalendarConnection { UserId = DesktopUserId };
            _db.GoogleCalendarConnections.Add(connection);
        }

        connection.AccessToken = credential.Token.AccessToken;
        connection.RefreshToken = credential.Token.RefreshToken ?? connection.RefreshToken;
        connection.ExpiryDate = credential.Token.IssuedUtc.AddSeconds(credential.Token.ExpiresInSeconds ?? 3600);
        connection.IsConnected = true;
        connection.CalendarId = "primary";
        connection.Email = email ?? connection.Email;
        connection.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private static async Task<string?> TryGetEmailAsync(UserCredential credential)
    {
        try
        {
            var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "CalendarApp"
            });
            var calendar = await service.Calendars.Get("primary").ExecuteAsync();
            return calendar.Id;
        }
        catch
        {
            return null;
        }
    }

    private static Event MapToGoogle(CalendarEvent local)
    {
        var googleEvent = new Event
        {
            Summary = local.Title,
            Description = local.Description,
            Location = local.Location
        };

        if (local.IsAllDay)
        {
            googleEvent.Start = new EventDateTime { Date = local.StartDateTime.ToString("yyyy-MM-dd") };
            // Google expects an exclusive end date for all-day events.
            var exclusiveEnd = local.EndDateTime.Date.AddDays(1);
            googleEvent.End = new EventDateTime { Date = exclusiveEnd.ToString("yyyy-MM-dd") };
        }
        else
        {
            // Google requires IANA time zones (e.g. America/New_York), not Windows IDs.
            googleEvent.Start = ToGoogleDateTime(local.StartDateTime);
            googleEvent.End = ToGoogleDateTime(local.EndDateTime);
        }

        return googleEvent;
    }

    private static EventDateTime ToGoogleDateTime(DateTime local)
    {
        var dto = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local));
        var value = new EventDateTime { DateTimeDateTimeOffset = dto };

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var ianaId)
            && !string.IsNullOrEmpty(ianaId))
        {
            value.TimeZone = ianaId;
        }

        return value;
    }

    private static DateTime NormalizeImportedAllDayEnd(DateTime start, DateTime end, bool allDay)
    {
        if (!allDay)
        {
            return end;
        }

        // Exclusive end at 00:00 of the following day → last inclusive moment of prior day.
        if (end.TimeOfDay == TimeSpan.Zero && end > start.Date)
        {
            return end.Date.AddDays(-1).AddHours(23).AddMinutes(59);
        }

        if (end.Date == start.Date)
        {
            return start.Date.AddHours(23).AddMinutes(59);
        }

        return end.TimeOfDay == TimeSpan.Zero
            ? end.Date.AddHours(23).AddMinutes(59)
            : end;
    }

    private static DateTime ResolveDate(EventDateTime? value)
    {
        if (value == null) return DateTime.Now;

        if (value.DateTimeDateTimeOffset.HasValue)
        {
            return value.DateTimeDateTimeOffset.Value.LocalDateTime;
        }

        if (!string.IsNullOrEmpty(value.Date) && DateTime.TryParse(value.Date, out var dateOnly))
        {
            return dateOnly.Date;
        }

#pragma warning disable CS0618
        if (value.DateTime.HasValue)
        {
            return DateTime.SpecifyKind(value.DateTime.Value, DateTimeKind.Local);
        }
#pragma warning restore CS0618

        return DateTime.Now;
    }
}
