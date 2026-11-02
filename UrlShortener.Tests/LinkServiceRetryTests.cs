using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Data;
using UrlShortener.Api.Services;
using UrlShortener.Api.Domain;
using UrlShortener.Contracts;

namespace UrlShortener.Tests;

public class LinkServiceRetryTests
{
    private sealed class FlakyDbContext : AppDbContext
    {
        private int _calls;

        public FlakyDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _calls) == 1)
            {
                // Симулируем DbUpdateException с сообщением о нарушении уникальности
                throw new DbUpdateException("UNIQUE constraint failed: links.Code", new Exception("sqlite3_constraint_unique"));
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static FlakyDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"links-{Guid.NewGuid():N}")
            .Options;

        return new FlakyDbContext(options);
    }

    private sealed class FixedGenerator : ICodeGenerator
    {
        public string Generate() => "fixedcode";
    }

    private sealed class NoopSigner : IHmacSigner
    {
        public string Sign(string code) => string.Empty;
        public bool Verify(string code, string? signature) => true;
    }

    private sealed class DummyAudit : IAuditLogger
    {
        public void Record(AuditAction action, string initiator, string? code, string? result) { }
    }

    [Fact]
    public async Task Create_retries_on_unique_violation_and_succeeds()
    {
        await using var db = NewDb();

        var codeGen = new FixedGenerator();
        var signer = new NoopSigner();
        var audit = new DummyAudit();
        var checker = new LinkAccessChecker(Array.Empty<UrlShortener.Api.Domain.AccessRules.LinkAccessRule>());
        var options = Options.Create(new LinkServiceOptions { BaseUrl = "https://x", DefaultTtlDays = 30 });

        var svc = new LinkService(db, codeGen, signer, checker, audit, TimeProvider.System, options);

        var req = new CreateLinkRequest { OrderId = Guid.NewGuid(), TtlDays = 1 };

        var res = await svc.CreateAsync(req, "tester", default);

        Assert.NotNull(res);
        Assert.Equal("fixedcode", res.Code);
    }
}
