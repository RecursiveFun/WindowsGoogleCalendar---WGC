using CalendarDesktop.Models;
using Microsoft.EntityFrameworkCore;

namespace CalendarDesktop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();
    public DbSet<GoogleCalendarConnection> GoogleCalendarConnections => Set<GoogleCalendarConnection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.GoogleEventId).HasMaxLength(200);
        });

        modelBuilder.Entity<GoogleCalendarConnection>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.AccessToken).HasMaxLength(4000);
            e.Property(x => x.RefreshToken).HasMaxLength(4000);
        });
    }
}
