namespace UrlShortener.Api.Domain;

public enum AuditAction
{
    LinkCreated,
    LinkVisited,
    LinkDeactivated,
    IpBlocked
}

public class AuditLogEntry
{
    public long Id { get; set; }

    public AuditAction Action { get; set; }

    public string? Code { get; set; }

    public string Initiator { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
