using UrlShortener.Api.Domain;
using UrlShortener.Api.Domain.AccessRules;

namespace UrlShortener.Api.Services;

public sealed class LinkAccessChecker
{
    private readonly IReadOnlyList<LinkAccessRule> _rules;

    public LinkAccessChecker(IEnumerable<LinkAccessRule> rules) => _rules = rules.ToList();

    public VisitResult Evaluate(Link link, DateTimeOffset now, string? signature)
    {
        var context = new AccessContext(link, now, signature);
        foreach (var rule in _rules)
        {
            var result = rule.Check(context);
            if (result != VisitResult.Success)
                return result;
        }

        return VisitResult.Success;
    }
}
