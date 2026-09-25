using CalendarDesktop.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalendarDesktop.Tests;

public sealed class TestDb : IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection _connection;

    public AppDbContext Db { get; }

    private TestDb(SqliteConnection connection, AppDbContext db)
    {
        _connection = connection;
        Db = db;
    }

    public static async Task<TestDb> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return new TestDb(connection, db);
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
