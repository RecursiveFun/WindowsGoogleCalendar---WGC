using CalendarDesktop.Data;
using CalendarDesktop.Models;
using CalendarDesktop.Services;
using Microsoft.Extensions.Options;

namespace CalendarDesktop.Tests;

public class GoogleCalendarServiceConnectionTests
{
    [Fact]
    public async Task IsConnectedAsync_ReflectsConnectionRow()
    {
        await using var db = await TestDb.CreateAsync();
        var service = CreateService(db.Db);

        Assert.False(await service.IsConnectedAsync());

        db.Db.GoogleCalendarConnections.Add(new GoogleCalendarConnection
        {
            UserId = "desktop-user",
            IsConnected = true,
            Email = "user@example.com",
            CalendarId = "primary"
        });
        await db.Db.SaveChangesAsync();

        Assert.True(await service.IsConnectedAsync());
        Assert.Equal("user@example.com", await service.GetConnectedEmailAsync());
    }

    [Fact]
    public async Task SignOutAsync_ClearsConnectionFlags()
    {
        await using var db = await TestDb.CreateAsync();
        db.Db.GoogleCalendarConnections.Add(new GoogleCalendarConnection
        {
            UserId = "desktop-user",
            IsConnected = true,
            Email = "user@example.com",
            AccessToken = "token",
            RefreshToken = "refresh",
            CalendarId = "primary"
        });
        await db.Db.SaveChangesAsync();

        var service = CreateService(db.Db);
        await service.SignOutAsync();

        Assert.False(await service.IsConnectedAsync());
        Assert.Null(await service.GetConnectedEmailAsync());

        var row = db.Db.GoogleCalendarConnections.Single();
        Assert.False(row.IsConnected);
        Assert.Null(row.AccessToken);
        Assert.Null(row.RefreshToken);
        Assert.Null(row.Email);
    }

    [Fact]
    public async Task DeleteGoogleEventAsync_Noops_WhenIdMissing()
    {
        await using var db = await TestDb.CreateAsync();
        var service = CreateService(db.Db);

        // Should not throw when id is null/empty and no Google client is available.
        await service.DeleteGoogleEventAsync(null);
        await service.DeleteGoogleEventAsync("");
    }

    private static GoogleCalendarService CreateService(AppDbContext db) =>
        new(db, Options.Create(new GoogleCalendarOptions
        {
            ClientId = "test-client",
            ClientSecret = "test-secret"
        }));
}
