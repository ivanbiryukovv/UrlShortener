using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public class HmacSignerTests
{
    private static HmacSigner Create(string secret = "тестовый-секрет-для-подписи-32-символа")
        => new(Options.Create(new LinkServiceOptions { HmacSecret = secret }));

    [Fact]
    public void Sign_is_deterministic()
    {
        var signer = Create();

        Assert.Equal(signer.Sign("AbC123xY"), signer.Sign("AbC123xY"));
    }

    [Fact]
    public void Verify_accepts_valid_signature()
    {
        var signer = Create();
        var signature = signer.Sign("AbC123xY");

        Assert.True(signer.Verify("AbC123xY", signature));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("подделка")]
    public void Verify_rejects_invalid_signature(string? signature)
    {
        var signer = Create();

        Assert.False(signer.Verify("AbC123xY", signature));
    }

    [Fact]
    public void Verify_rejects_signature_from_other_key()
    {
        var signature = Create("первый-ключ-первый-ключ-первый-ключ").Sign("AbC123xY");
        var other = Create("второй-ключ-второй-ключ-второй-ключ");

        Assert.False(other.Verify("AbC123xY", signature));
    }
}
