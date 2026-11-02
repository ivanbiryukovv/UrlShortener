namespace UrlShortener.Contracts;

public sealed class CreateLinkRequest
{
    public Guid OrderId { get; set; }

    public int? TtlDays { get; set; }

    public bool IsOneTime { get; set; }

    public bool UseHmac { get; set; }
}
