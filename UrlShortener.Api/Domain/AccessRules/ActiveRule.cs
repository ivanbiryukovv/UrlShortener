namespace UrlShortener.Api.Domain.AccessRules;

public sealed class ActiveRule : LinkAccessRule
{
    public override VisitResult Check(in AccessContext context)
        => context.Link.IsActive ? VisitResult.Success : VisitResult.Inactive;
}
