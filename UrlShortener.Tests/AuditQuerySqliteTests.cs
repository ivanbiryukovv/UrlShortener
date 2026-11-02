using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;


public class AuditQuerySqliteTests
{
    private static readonly DateTimeOffset T = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static AuditLogEntry Entry(string code, DateTimeOffset at) => new()
    {
        Action = AuditAction.LinkVisited,
        Code = code,
        Initiator = "tester",
        Result = "ok",
        CreatedAt = at
    };


    private static (AppDbContext db, SqliteConnection conn) NewSqliteDb()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(conn)
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return (db, conn);
    }

    [Fact]
    public async Task Sqlite_orders_by_id_desc_not_by_createdat()
    {
        var (db, conn) = NewSqliteDb();
        try
        {
            db.AuditLog.Add(Entry("a", T.AddMinutes(3))); // Id 1
            await db.SaveChangesAsync();
            db.AuditLog.Add(Entry("b", T.AddMinutes(1))); // Id 2
            await db.SaveChangesAsync();
            db.AuditLog.Add(Entry("c", T.AddMinutes(4))); // Id 3
            await db.SaveChangesAsync();
            db.AuditLog.Add(Entry("d", T.AddMinutes(2))); // Id 4
            await db.SaveChangesAsync();

            var result = await new AuditQueryService(db)
                .QueryAsync(new AuditFilter(null, null, null, 100), default);

            Assert.Equal(new[] { "d", "c", "b", "a" }, result.Select(r => r.Code));
        }
        finally
        {
            await db.DisposeAsync();
            conn.Dispose();
        }
    }

    [Fact]
    public async Task Sqlite_take_limits_to_newest_by_id()
    {
        var (db, conn) = NewSqliteDb();
        try
        {
            db.AuditLog.Add(Entry("a", T.AddMinutes(3))); 
            await db.SaveChangesAsync();
            db.AuditLog.Add(Entry("b", T.AddMinutes(1))); 
            await db.SaveChangesAsync();
            db.AuditLog.Add(Entry("c", T.AddMinutes(4))); 
            await db.SaveChangesAsync();
            db.AuditLog.Add(Entry("d", T.AddMinutes(2))); 
            await db.SaveChangesAsync();

            var result = await new AuditQueryService(db)
                .QueryAsync(new AuditFilter(null, null, null, 2), default);

            Assert.Equal(new[] { "d", "c" }, result.Select(r => r.Code));
        }
        finally
        {
            await db.DisposeAsync();
            conn.Dispose();
        }
    }
}
