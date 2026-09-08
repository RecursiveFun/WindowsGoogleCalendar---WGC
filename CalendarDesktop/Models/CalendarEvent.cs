namespace CalendarDesktop.Models;

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartDateTime { get; set; }
    public DateTime EndDateTime { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public bool IsAllDay { get; set; }
    public bool IsSynced { get; set; }
    public string? GoogleEventId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
