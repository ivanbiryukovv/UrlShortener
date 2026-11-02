namespace UrlShortener.Api.Domain.AccessRules;

public sealed class ExpirationRule : LinkAccessRule
{
    public override VisitResult Check(in AccessContext context)
        => context.Link.IsExpired(context.Now) ? VisitResult.Expired : VisitResult.Success;
}
