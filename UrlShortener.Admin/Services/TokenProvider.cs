namespace UrlShortener.Admin.Services;

public interface ITokenProvider
{
    string? Token { get; }
    void SetToken(string token);
}

public sealed class TokenProvider : ITokenProvider
{
    private string? _token;
    public string? Token => _token;
    public void SetToken(string token) => _token = token;
}
