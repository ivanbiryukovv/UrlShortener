using System.Net;
using System.Net.Http.Json;

namespace UrlShortener.Tests;

public class AuthEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthEndpointsTests(ApiFactory factory) => _factory = factory;

    private sealed record TokenDto(string Token);

    [Fact]
    public async Task Login_with_correct_secret_returns_token()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/admin/login", new { secret = TestAuth.AdminSecret });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenDto>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.Token));
    }

    [Fact]
    public async Task Login_with_wrong_secret_is_unauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/admin/login", new { secret = "definitely-not-the-secret" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_without_secret_is_bad_request()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/admin/login", new { secret = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Token_from_login_authorizes_protected_endpoint()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/admin/login", new { secret = TestAuth.AdminSecret });
        var token = (await login.Content.ReadFromJsonAsync<TokenDto>())!.Token;

        var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        var response = await authed.GetAsync("/audit?take=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
