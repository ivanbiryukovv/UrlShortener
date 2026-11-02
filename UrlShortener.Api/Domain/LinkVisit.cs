namespace UrlShortener.Api.Domain;

public class LinkVisit
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Ip { get; set; } = string.Empty;

    public string? UserAgent { get; set; }

    public DateTimeOffset VisitedAt { get; set; }

    public VisitResult Result { get; set; }

    public bool IsArchived { get; set; }
}
