namespace UrlShortener.Contracts;

public sealed class LinkResponse
{
    public string Code { get; set; } = string.Empty;

    public string ShortUrl { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsOneTime { get; set; }

    public bool IsActive { get; set; }

    public int VisitCount { get; set; }
}
