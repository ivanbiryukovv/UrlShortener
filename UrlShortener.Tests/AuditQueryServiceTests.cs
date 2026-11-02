using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;


public class AuditQueryServiceTests
{
    private static readonly DateTimeOffset T = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static AppDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static AuditLogEntry Entry(AuditAction action, string code, DateTimeOffset at) => new()
    {
        Action = action,
        Code = code,
        Initiator = "tester",
        Result = "ok",
        CreatedAt = at
    };

    [Fact]
    public async Task Filters_by_from_date()
    {
        await using var db = NewDb();
        db.AuditLog.AddRange(
            Entry(AuditAction.LinkCreated, "old", T.AddDays(-10)),
            Entry(AuditAction.LinkCreated, "fresh", T));
        await db.SaveChangesAsync();

        var result = await new AuditQueryService(db)
            .QueryAsync(new AuditFilter(T.AddDays(-1), null, null, 100), default);

        Assert.Single(result);
        Assert.Equal("fresh", result[0].Code);
    }

    [Fact]
    public async Task Filters_by_to_date()
    {
        await using var db = NewDb();
        db.AuditLog.AddRange(
            Entry(AuditAction.LinkCreated, "old", T.AddDays(-10)),
            Entry(AuditAction.LinkCreated, "fresh", T));
        await db.SaveChangesAsync();

        var result = await new AuditQueryService(db)
            .QueryAsync(new AuditFilter(null, T.AddDays(-1), null, 100), default);

        Assert.Single(result);
        Assert.Equal("old", result[0].Code);
    }

    [Fact]
    public async Task Filters_by_action_and_returns_newest_first()
    {
        await using var db = NewDb();
        db.AuditLog.AddRange(
            Entry(AuditAction.LinkCreated, "a", T),
            Entry(AuditAction.LinkVisited, "b", T.AddMinutes(1)),
            Entry(AuditAction.LinkCreated, "c", T.AddMinutes(2)));
        await db.SaveChangesAsync();

        var result = await new AuditQueryService(db)
            .QueryAsync(new AuditFilter(null, null, AuditAction.LinkCreated, 100), default);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("LinkCreated", r.Action));
        Assert.Equal("c", result[0].Code); // новейшая сверху
        Assert.Equal("a", result[1].Code);
    }

    [Fact]
    public async Task Take_limits_result_count()
    {
        await using var db = NewDb();
        for (var i = 0; i < 5; i++)
            db.AuditLog.Add(Entry(AuditAction.LinkVisited, $"c{i}", T.AddMinutes(i)));
        await db.SaveChangesAsync();

        var result = await new AuditQueryService(db)
            .QueryAsync(new AuditFilter(null, null, null, 3), default);

        Assert.Equal(3, result.Count);
    }
}
