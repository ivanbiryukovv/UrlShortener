using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public class RateLimiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static (RateLimiter Limiter, FakeTimeProvider Clock) Build(RateLimitOptions? options = null)
    {
        var clock = new FakeTimeProvider(Start);
        var opts = options ?? new RateLimitOptions
        {
            CreatePerMinute = 3,
            RedirectPerMinute = 5,
            ViolationsBeforeBlock = 3,
            BlockHours = 24
        };
        return (new RateLimiter(clock, Options.Create(opts)), clock);
    }

    [Fact]
    public void Allows_requests_within_limit()
    {
        var (limiter, _) = Build();

        for (var i = 0; i < 3; i++)
            Assert.True(limiter.Check("1.1.1.1", RateScope.Create).Allowed);
    }

    [Fact]
    public void Rejects_request_over_limit()
    {
        var (limiter, _) = Build();

        for (var i = 0; i < 3; i++)
            limiter.Check("1.1.1.1", RateScope.Create);

        var decision = limiter.Check("1.1.1.1", RateScope.Create);
        Assert.False(decision.Allowed);
        Assert.True(decision.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void Window_resets_after_a_minute()
    {
        var (limiter, clock) = Build();

        for (var i = 0; i < 3; i++)
            limiter.Check("1.1.1.1", RateScope.Create);
        Assert.False(limiter.Check("1.1.1.1", RateScope.Create).Allowed);

        clock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));

        Assert.True(limiter.Check("1.1.1.1", RateScope.Create).Allowed);
    }

    [Fact]
    public void Blocks_ip_after_repeated_violations()
    {
        var (limiter, _) = Build();

        for (var i = 0; i < 3; i++)
            limiter.Check("9.9.9.9", RateScope.Create);

        RateDecision last = default;
        for (var i = 0; i < 3; i++)
            last = limiter.Check("9.9.9.9", RateScope.Create);

        Assert.True(last.Blocked);
        Assert.True(last.RetryAfter > TimeSpan.FromHours(23));
    }

    [Fact]
    public void Scopes_are_counted_independently()
    {
        var (limiter, _) = Build();

        for (var i = 0; i < 3; i++)
            limiter.Check("1.1.1.1", RateScope.Create);

        Assert.False(limiter.Check("1.1.1.1", RateScope.Create).Allowed);
        Assert.True(limiter.Check("1.1.1.1", RateScope.Redirect).Allowed);
    }
}
