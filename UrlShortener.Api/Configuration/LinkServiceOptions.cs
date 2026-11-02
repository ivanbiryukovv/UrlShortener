namespace UrlShortener.Api.Configuration;

public sealed class LinkServiceOptions
{
    public const string SectionName = "Links";

    public string BaseUrl { get; set; } = "https://site.ru";

    public int DefaultTtlDays { get; set; } = 30;

    public int CodeLength { get; set; } = 8;

    public string HmacSecret { get; set; } = string.Empty;

    public int LogRetentionDays { get; set; } = 90;
}
