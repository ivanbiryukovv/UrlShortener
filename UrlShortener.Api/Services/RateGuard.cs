using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Services;

public sealed class RateGuard
{
    private readonly RateLimiter _limiter;
    private readonly IAuditLogger _audit;
    private readonly AppDbContext _db;

    public RateGuard(RateLimiter limiter, IAuditLogger audit, AppDbContext db)
    {
        _limiter = limiter;
        _audit = audit;
        _db = db;
    }

    public async Task<IResult?> RejectAsync(string ip, RateScope scope, CancellationToken ct)
    {
        var decision = _limiter.Check(ip, scope);
        if (decision.Allowed)
            return null;

        var retryAfterSeconds = (int)Math.Ceiling(decision.RetryAfter.TotalSeconds);

        if (decision.Blocked)
        {
            _audit.Record(AuditAction.IpBlocked, ip, null, $"заблокирован на {retryAfterSeconds} с");
            await _db.SaveChangesAsync(ct);
        }

        return Results.Json(
            new { error = "слишком много запросов", retryAfter = retryAfterSeconds },
            statusCode: StatusCodes.Status429TooManyRequests,
            contentType: "application/json")
            .WithRetryAfter(retryAfterSeconds);
    }
}

file static class ResultExtensions
{
    public static IResult WithRetryAfter(this IResult inner, int seconds) =>
        new RetryAfterResult(inner, seconds);

    private sealed class RetryAfterResult : IResult
    {
        private readonly IResult _inner;
        private readonly int _seconds;

        public RetryAfterResult(IResult inner, int seconds)
        {
            _inner = inner;
            _seconds = seconds;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.RetryAfter = _seconds.ToString();
            await _inner.ExecuteAsync(httpContext);
        }
    }
}
