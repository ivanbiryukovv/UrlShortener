namespace UrlShortener.Api.Services;

public interface IHmacSigner
{
    string Sign(string code);

    bool Verify(string code, string? signature);
}
