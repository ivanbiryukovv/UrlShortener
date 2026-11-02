using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Domain;
using UrlShortener.Api.Domain.AccessRules;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public class AccessRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static LinkAccessChecker BuildChecker(HmacSigner signer) => new(new LinkAccessRule[]
    {
        new ActiveRule(),
        new ExpirationRule(),
        new SignatureRule(signer)
    });

    private static HmacSigner Signer()
        => new(Options.Create(new LinkServiceOptions { HmacSecret = "секрет-для-проверки-подписи-в-тестах" }));

    private static Link ValidLink() => new()
    {
        Code = "AbC123xY",
        IsActive = true,
        CreatedAt = Now.AddDays(-1),
        ExpiresAt = Now.AddDays(10)
    };

    [Fact]
    public void Active_unexpired_link_passes()
    {
        var checker = BuildChecker(Signer());

        Assert.Equal(VisitResult.Success, checker.Evaluate(ValidLink(), Now, null));
    }

    [Fact]
    public void Inactive_link_is_rejected()
    {
        var link = ValidLink();
        link.IsActive = false;

        Assert.Equal(VisitResult.Inactive, BuildChecker(Signer()).Evaluate(link, Now, null));
    }

    [Fact]
    public void Expired_link_is_rejected()
    {
        var link = ValidLink();
        link.ExpiresAt = Now.AddDays(-1);

        Assert.Equal(VisitResult.Expired, BuildChecker(Signer()).Evaluate(link, Now, null));
    }

    [Fact]
    public void Signed_link_requires_matching_signature()
    {
        var signer = Signer();
        var link = ValidLink();
        link.HmacSignature = signer.Sign(link.Code);
        var checker = BuildChecker(signer);

        Assert.Equal(VisitResult.Success, checker.Evaluate(link, Now, link.HmacSignature));
        Assert.Equal(VisitResult.InvalidSignature, checker.Evaluate(link, Now, "подделка"));
        Assert.Equal(VisitResult.InvalidSignature, checker.Evaluate(link, Now, null));
    }
}
