using CalendarDesktop.Models;
using CalendarDesktop.Services;

namespace CalendarDesktop.Tests;

public class EventServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsEvent_AndSetsTimestamps()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EventService(db.Db);

        var created = await service.CreateAsync(new CalendarEvent
        {
            Title = "Standup",
            StartDateTime = new DateTime(2026, 9, 8, 9, 0, 0),
            EndDateTime = new DateTime(2026, 9, 8, 9, 30, 0)
        });

        Assert.True(created.Id > 0);
        Assert.NotEqual(default, created.CreatedAt);
        Assert.NotEqual(default, created.UpdatedAt);

        var all = await service.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Standup", all[0].Title);
    }

    [Fact]
    public async Task UpdateAsync_ChangesFields()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EventService(db.Db);
        var created = await service.CreateAsync(new CalendarEvent
        {
            Title = "Old",
            StartDateTime = DateTime.Today.AddHours(10),
            EndDateTime = DateTime.Today.AddHours(11)
        });

        created.Title = "New";
        var updated = await service.UpdateAsync(created);

        Assert.Equal("New", updated.Title);
        var loaded = (await service.GetAllAsync()).Single();
        Assert.Equal("New", loaded.Title);
    }

    [Fact]
    public async Task DeleteAsync_RemovesExisting_AndReturnsFalseForMissing()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EventService(db.Db);
        var created = await service.CreateAsync(new CalendarEvent
        {
            Title = "Temp",
            StartDateTime = DateTime.Today.AddHours(1),
            EndDateTime = DateTime.Today.AddHours(2)
        });

        Assert.True(await service.DeleteAsync(created.Id));
        Assert.Empty(await service.GetAllAsync());
        Assert.False(await service.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task GetRangeAsync_ReturnsOverlappingEventsOnly()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EventService(db.Db);

        await service.CreateAsync(new CalendarEvent
        {
            Title = "Inside",
            StartDateTime = new DateTime(2026, 9, 10, 9, 0, 0),
            EndDateTime = new DateTime(2026, 9, 10, 10, 0, 0)
        });
        await service.CreateAsync(new CalendarEvent
        {
            Title = "Outside",
            StartDateTime = new DateTime(2026, 8, 1, 9, 0, 0),
            EndDateTime = new DateTime(2026, 8, 1, 10, 0, 0)
        });
        await service.CreateAsync(new CalendarEvent
        {
            Title = "Spans",
            StartDateTime = new DateTime(2026, 9, 1, 0, 0, 0),
            EndDateTime = new DateTime(2026, 9, 30, 23, 59, 0),
            IsAllDay = true
        });

        var rangeStart = new DateTime(2026, 9, 1);
        var rangeEnd = new DateTime(2026, 9, 30, 23, 59, 59);
        var results = await service.GetRangeAsync(rangeStart, rangeEnd);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, e => e.Title == "Inside");
        Assert.Contains(results, e => e.Title == "Spans");
        Assert.DoesNotContain(results, e => e.Title == "Outside");
    }

    [Fact]
    public async Task GetStartingBetweenAsync_UsesExclusiveStartAndInclusiveEnd()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EventService(db.Db);

        var now = new DateTime(2026, 9, 8, 12, 0, 0);
        await service.CreateAsync(new CalendarEvent
        {
            Title = "AtBoundaryStart",
            StartDateTime = now,
            EndDateTime = now.AddHours(1)
        });
        await service.CreateAsync(new CalendarEvent
        {
            Title = "InWindow",
            StartDateTime = now.AddMinutes(10),
            EndDateTime = now.AddHours(1)
        });
        await service.CreateAsync(new CalendarEvent
        {
            Title = "AtBoundaryEnd",
            StartDateTime = now.AddMinutes(15),
            EndDateTime = now.AddHours(1)
        });
        await service.CreateAsync(new CalendarEvent
        {
            Title = "AfterWindow",
            StartDateTime = now.AddMinutes(16),
            EndDateTime = now.AddHours(1)
        });

        var results = await service.GetStartingBetweenAsync(now, now.AddMinutes(15));

        Assert.Equal(2, results.Count);
        Assert.Contains(results, e => e.Title == "InWindow");
        Assert.Contains(results, e => e.Title == "AtBoundaryEnd");
        Assert.DoesNotContain(results, e => e.Title == "AtBoundaryStart");
        Assert.DoesNotContain(results, e => e.Title == "AfterWindow");
    }

    [Fact]
    public async Task NormalizeAllDayEndsAsync_FixesExclusiveMidnightEnds()
    {
        await using var db = await TestDb.CreateAsync();
        var service = new EventService(db.Db);

        var start = new DateTime(2026, 9, 4);
        await service.CreateAsync(new CalendarEvent
        {
            Title = "AllDay",
            StartDateTime = start,
            EndDateTime = start.AddDays(1), // exclusive midnight next day
            IsAllDay = true
        });
        await service.CreateAsync(new CalendarEvent
        {
            Title = "Timed",
            StartDateTime = start.AddHours(9),
            EndDateTime = start.AddHours(10),
            IsAllDay = false
        });

        var changed = await service.NormalizeAllDayEndsAsync();
        Assert.Equal(1, changed);

        var allDay = (await service.GetAllAsync()).Single(e => e.Title == "AllDay");
        Assert.Equal(new TimeSpan(23, 59, 0), allDay.EndDateTime.TimeOfDay);
        Assert.Equal(start.Date, allDay.EndDateTime.Date);
    }
}
