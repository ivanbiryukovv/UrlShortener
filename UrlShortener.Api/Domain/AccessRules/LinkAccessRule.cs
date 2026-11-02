namespace UrlShortener.Api.Domain.AccessRules;

public readonly record struct AccessContext(Link Link, DateTimeOffset Now, string? Signature);

public abstract class LinkAccessRule
{
    public abstract VisitResult Check(in AccessContext context);
}
