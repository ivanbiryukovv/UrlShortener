namespace UrlShortener.Api.Domain;

public class Link
{
    public string Code { get; set; } = string.Empty;

    public Guid OrderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsOneTime { get; set; }

    public bool IsActive { get; set; }

    public string? HmacSignature { get; set; }

    public bool IsExpired(DateTimeOffset now) => now > ExpiresAt;

    public bool RequiresSignature => !string.IsNullOrEmpty(HmacSignature);
}
