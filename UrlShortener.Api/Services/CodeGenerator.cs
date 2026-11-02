using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;

namespace UrlShortener.Api.Services;

public sealed class CodeGenerator : ICodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    private readonly int _length;

    public CodeGenerator(IOptions<LinkServiceOptions> options) => _length = options.Value.CodeLength;

    public string Generate()
    {
        var chars = new char[_length];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];

        return new string(chars);
    }
}
