using CalendarDesktop.Models;
using CalendarDesktop.Services;
using Google.Apis.Calendar.v3.Data;

namespace CalendarDesktop.Tests;

public class GoogleEventDateMapperTests
{
    [Fact]
    public void MapToGoogle_AllDay_UsesExclusiveEndDate()
    {
        var local = new CalendarEvent
        {
            Title = "Birthday",
            IsAllDay = true,
            StartDateTime = new DateTime(2026, 9, 26),
            EndDateTime = new DateTime(2026, 9, 26, 23, 59, 0),
            Description = "desc",
            Location = "Home"
        };

        var google = GoogleEventDateMapper.MapToGoogle(local);

        Assert.Equal("Birthday", google.Summary);
        Assert.Equal("desc", google.Description);
        Assert.Equal("Home", google.Location);
        Assert.Equal("2026-09-26", google.Start.Date);
        Assert.Equal("2026-09-27", google.End.Date);
        Assert.Null(google.Start.DateTimeDateTimeOffset);
    }

    [Fact]
    public void MapToGoogle_Timed_SetsDateTimeOffset()
    {
        var start = new DateTime(2026, 9, 8, 15, 0, 0);
        var end = new DateTime(2026, 9, 8, 16, 0, 0);
        var local = new CalendarEvent
        {
            Title = "Meeting",
            IsAllDay = false,
            StartDateTime = start,
            EndDateTime = end
        };

        var google = GoogleEventDateMapper.MapToGoogle(local);

        Assert.NotNull(google.Start.DateTimeDateTimeOffset);
        Assert.NotNull(google.End.DateTimeDateTimeOffset);
        Assert.Equal(start, google.Start.DateTimeDateTimeOffset!.Value.DateTime);
        Assert.Equal(end, google.End.DateTimeDateTimeOffset!.Value.DateTime);
        Assert.Null(google.Start.Date);
    }

    [Theory]
    [InlineData(false, "2026-09-04", "2026-09-05", "2026-09-05 00:00")] // not all-day → unchanged midnight
    [InlineData(true, "2026-09-04", "2026-09-05", "2026-09-04 23:59")]  // exclusive next day
    [InlineData(true, "2026-09-04", "2026-09-04", "2026-09-04 23:59")]  // same-day midnight → 23:59
    public void NormalizeImportedAllDayEnd_HandlesCommonCases(
        bool allDay, string startText, string endText, string expectedText)
    {
        var start = DateTime.Parse(startText);
        var end = DateTime.Parse(endText);
        var expected = DateTime.Parse(expectedText);

        var actual = GoogleEventDateMapper.NormalizeImportedAllDayEnd(start, end, allDay);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ResolveDate_PrefersDateTimeOffset()
    {
        var offset = new DateTimeOffset(2026, 9, 8, 15, 30, 0, TimeSpan.FromHours(-4));
        var value = new EventDateTime { DateTimeDateTimeOffset = offset };

        var resolved = GoogleEventDateMapper.ResolveDate(value, fallback: new DateTime(2000, 1, 1));

        Assert.Equal(offset.LocalDateTime, resolved);
    }

    [Fact]
    public void ResolveDate_ParsesDateOnly()
    {
        var value = new EventDateTime { Date = "2026-09-26" };
        var resolved = GoogleEventDateMapper.ResolveDate(value, fallback: new DateTime(2000, 1, 1));
        Assert.Equal(new DateTime(2026, 9, 26), resolved);
    }

    [Fact]
    public void ResolveDate_Null_UsesFallback()
    {
        var fallback = new DateTime(2026, 1, 2, 3, 4, 5);
        var resolved = GoogleEventDateMapper.ResolveDate(null, fallback);
        Assert.Equal(fallback, resolved);
    }
}
