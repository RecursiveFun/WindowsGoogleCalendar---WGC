namespace CalendarDesktop.Models;

public class GoogleCalendarConnection
{
    public int Id { get; set; }
    public string UserId { get; set; } = "desktop-user";
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? CalendarId { get; set; } = "primary";
    public bool IsConnected { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
