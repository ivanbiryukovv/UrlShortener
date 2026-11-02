namespace UrlShortener.Api.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int CreatePerMinute { get; set; } = 5;

    public int RedirectPerMinute { get; set; } = 10;

    public int AuthPerMinute { get; set; } = 5;

    public int ViolationsBeforeBlock { get; set; } = 3;

    public int BlockHours { get; set; } = 24;
}
