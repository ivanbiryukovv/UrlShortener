namespace UrlShortener.Contracts;

public sealed class AuditLogItem
{
    public long Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string Initiator { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}
