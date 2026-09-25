using CalendarDesktop.Models;
using Google.Apis.Calendar.v3.Data;

namespace CalendarDesktop.Services;

/// <summary>
/// Pure date/time mapping between local events and Google Calendar API shapes.
/// </summary>
public static class GoogleEventDateMapper
{
    public static Event MapToGoogle(CalendarEvent local)
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
            googleEvent.Start = ToGoogleDateTime(local.StartDateTime);
            googleEvent.End = ToGoogleDateTime(local.EndDateTime);
        }

        return googleEvent;
    }

    public static EventDateTime ToGoogleDateTime(DateTime local)
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

    public static DateTime NormalizeImportedAllDayEnd(DateTime start, DateTime end, bool allDay)
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

    public static DateTime ResolveDate(EventDateTime? value, DateTime? fallback = null)
    {
        var now = fallback ?? DateTime.Now;
        if (value == null) return now;

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

        return now;
    }
}
