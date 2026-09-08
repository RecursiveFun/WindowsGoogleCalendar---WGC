using CalendarDesktop.Data;
using CalendarDesktop.Models;
using Microsoft.EntityFrameworkCore;

namespace CalendarDesktop.Services;

public class EventService
{
    private readonly AppDbContext _db;

    public EventService(AppDbContext db) => _db = db;

    public Task<List<CalendarEvent>> GetAllAsync() =>
        _db.Events.OrderBy(e => e.StartDateTime).ToListAsync();

    public Task<List<CalendarEvent>> GetRangeAsync(DateTime start, DateTime end) =>
        _db.Events
            .Where(e => e.StartDateTime <= end && e.EndDateTime >= start)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

    /// <summary>
    /// Events whose start falls in (startExclusive, endInclusive].
    /// </summary>
    public Task<List<CalendarEvent>> GetStartingBetweenAsync(DateTime startExclusive, DateTime endInclusive) =>
        _db.Events
            .Where(e => e.StartDateTime > startExclusive && e.StartDateTime <= endInclusive)
            .OrderBy(e => e.StartDateTime)
            .ToListAsync();

    public async Task<CalendarEvent> CreateAsync(CalendarEvent item)
    {
        item.CreatedAt = DateTime.UtcNow;
        item.UpdatedAt = DateTime.UtcNow;
        _db.Events.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<CalendarEvent> UpdateAsync(CalendarEvent item)
    {
        item.UpdatedAt = DateTime.UtcNow;
        _db.Events.Update(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var item = await _db.Events.FindAsync(id);
        if (item == null) return false;
        _db.Events.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> NormalizeAllDayEndsAsync()
    {
        var events = await _db.Events
            .Where(e => e.IsAllDay && e.EndDateTime.TimeOfDay == TimeSpan.Zero)
            .ToListAsync();

        foreach (var item in events)
        {
            if (item.EndDateTime > item.StartDateTime.Date)
            {
                item.EndDateTime = item.EndDateTime.Date.AddDays(-1).AddHours(23).AddMinutes(59);
            }
            else
            {
                item.EndDateTime = item.StartDateTime.Date.AddHours(23).AddMinutes(59);
            }
            item.UpdatedAt = DateTime.UtcNow;
        }

        if (events.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return events.Count;
    }
}
