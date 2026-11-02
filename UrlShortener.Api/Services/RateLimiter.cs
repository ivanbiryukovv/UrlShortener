using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;

namespace UrlShortener.Api.Services;

public enum RateScope
{
    Create,
    Redirect,
    Auth
}

public readonly record struct RateDecision(bool Allowed, bool Blocked, TimeSpan RetryAfter)
{
    public static RateDecision Allow() => new(true, false, TimeSpan.Zero);
}

public sealed class RateLimiter
{
    private sealed class Counter
    {
        public readonly Queue<DateTimeOffset> Requests = new();
        public readonly Queue<DateTimeOffset> Violations = new();
        public DateTimeOffset? BlockedUntil;
    }

    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, Counter> _counters = new();
    private readonly TimeProvider _clock;
    private readonly RateLimitOptions _options;
    private DateTimeOffset _lastSweep;

    public RateLimiter(TimeProvider clock, IOptions<RateLimitOptions> options)
    {
        _clock = clock;
        _options = options.Value;
        _lastSweep = clock.GetUtcNow();
    }

    public RateDecision Check(string ip, RateScope scope)
    {
        var now = _clock.GetUtcNow();
        Sweep(now);
        var limit = scope == RateScope.Create ? _options.CreatePerMinute
            : scope == RateScope.Redirect ? _options.RedirectPerMinute
            : _options.AuthPerMinute;
        var key = $"{ip}:{scope}";
        var counter = _counters.GetOrAdd(key, _ => new Counter());

        lock (counter)
        {
            if (counter.BlockedUntil is { } until)
            {
                if (now < until)
                    return new RateDecision(false, true, until - now);

                counter.BlockedUntil = null;
            }

            Trim(counter.Requests, now - TimeSpan.FromMinutes(1));
            Trim(counter.Violations, now - TimeSpan.FromHours(1));

            if (counter.Requests.Count < limit)
            {
                counter.Requests.Enqueue(now);
                return RateDecision.Allow();
            }

            counter.Violations.Enqueue(now);

            if (counter.Violations.Count >= _options.ViolationsBeforeBlock)
            {
                var blockedUntil = now.AddHours(_options.BlockHours);
                counter.BlockedUntil = blockedUntil;
                counter.Violations.Clear();
                return new RateDecision(false, true, blockedUntil - now);
            }

            var oldest = counter.Requests.Peek();
            var retryAfter = oldest + TimeSpan.FromMinutes(1) - now;
            return new RateDecision(false, false, retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.FromSeconds(1));
        }
    }

    private static void Trim(Queue<DateTimeOffset> queue, DateTimeOffset threshold)
    {
        while (queue.Count > 0 && queue.Peek() < threshold)
            queue.Dequeue();
    }

    // Периодически вычищаем простаивающие счётчики, иначе словарь растёт с каждым новым IP.
    private void Sweep(DateTimeOffset now)
    {
        if (now - _lastSweep < SweepInterval)
            return;

        _lastSweep = now;
        var requestWindow = now - TimeSpan.FromMinutes(1);
        var violationWindow = now - TimeSpan.FromHours(1);

        foreach (var (key, counter) in _counters)
        {
            lock (counter)
            {
                Trim(counter.Requests, requestWindow);
                Trim(counter.Violations, violationWindow);

                var idle = counter.Requests.Count == 0
                           && counter.Violations.Count == 0
                           && (counter.BlockedUntil is null || counter.BlockedUntil <= now);

                if (idle)
                    _counters.TryRemove(key, out _);
            }
        }
    }
}
