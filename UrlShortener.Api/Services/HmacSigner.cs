using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;

namespace UrlShortener.Api.Services;

public sealed class HmacSigner : IHmacSigner
{
    private readonly byte[] _key;

    public HmacSigner(IOptions<LinkServiceOptions> options)
        => _key = Encoding.UTF8.GetBytes(options.Value.HmacSecret);

    public string Sign(string code)
    {
        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hash)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public bool Verify(string code, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
            return false;

        var expected = Sign(code);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }
}
