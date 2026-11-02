using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

public class CodeGeneratorTests
{
    private static CodeGenerator Create(int length = 8)
        => new(Options.Create(new LinkServiceOptions { CodeLength = length }));

    [Fact]
    public void Generate_returns_code_of_configured_length()
    {
        var generator = Create(length: 8);

        var code = generator.Generate();

        Assert.Equal(8, code.Length);
    }

    [Fact]
    public void Generate_uses_only_allowed_alphabet()
    {
        var generator = Create();

        for (var i = 0; i < 100; i++)
        {
            var code = generator.Generate();
            Assert.Matches("^[A-Za-z0-9]+$", code);
        }
    }

    [Fact]
    public void Generate_produces_distinct_codes()
    {
        var generator = Create();

        var codes = Enumerable.Range(0, 200).Select(_ => generator.Generate()).ToHashSet();

        Assert.True(codes.Count > 190, "коды должны быть практически уникальны");
    }
}
