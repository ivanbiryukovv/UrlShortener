using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using UrlShortener.Contracts;

namespace UrlShortener.Tests;

public class LinkEndpointsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private const string TestAuthKey = "test-auth-key-minimum-32-characters-required-for-jwt-bearer-auth";

    public LinkEndpointsTests(ApiFactory factory) => _factory = factory;

    private HttpClient CreateClient(bool authorized = true)
    {
        var client = _factory.CreateClient(new()
        {
            AllowAutoRedirect = false
        });

        if (authorized)
        {
            var token = GenerateJwtToken();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        }

        return client;
    }

    private static string GenerateJwtToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestAuthKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, "testuser") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static CreateLinkRequest NewRequest(bool oneTime = false, bool hmac = false) => new()
    {
        OrderId = Guid.NewGuid(),
        TtlDays = 30,
        IsOneTime = oneTime,
        UseHmac = hmac
    };

    [Fact]
    public async Task Create_returns_short_url()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/links", NewRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var link = await response.Content.ReadFromJsonAsync<LinkResponse>();
        Assert.NotNull(link);
        Assert.Equal(8, link!.Code.Length);
        Assert.Contains(link.Code, link.ShortUrl);
    }

    [Fact]
    public async Task Create_rejects_empty_order_id()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/links", new CreateLinkRequest { OrderId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_requires_authorization()
    {
        var client = CreateClient(authorized: false);

        var response = await client.PostAsJsonAsync("/links", NewRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Redirect_returns_302_for_active_link()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest())).Content.ReadFromJsonAsync<LinkResponse>())!;

        var publicClient = CreateClient(authorized: false);
        var response = await publicClient.GetAsync($"/l/{link.Code}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(link.OrderId.ToString(), response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Redirect_returns_404_for_unknown_code()
    {
        var client = CreateClient(authorized: false);

        var response = await client.GetAsync("/l/zzzzzzzz");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task One_time_link_is_gone_after_first_visit()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest(oneTime: true))).Content.ReadFromJsonAsync<LinkResponse>())!;

        var publicClient = CreateClient(authorized: false);
        var first = await publicClient.GetAsync($"/l/{link.Code}");
        var second = await publicClient.GetAsync($"/l/{link.Code}");

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, second.StatusCode);
    }

    [Fact]
    public async Task One_time_link_cannot_be_used_concurrently()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest(oneTime: true))).Content.ReadFromJsonAsync<LinkResponse>())!;

        var publicClient = CreateClient(authorized: false);

        var tasks = Enumerable.Range(0, 2).Select(_ => publicClient.GetAsync($"/l/{link.Code}")).ToArray();
        var responses = await Task.WhenAll(tasks);

        var statuses = responses.Select(r => r.StatusCode).ToList();

        Assert.Contains(HttpStatusCode.Redirect, statuses);
        Assert.Contains(HttpStatusCode.Gone, statuses);
    }

    [Fact]
    public async Task GetInfo_requires_authorization()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest())).Content.ReadFromJsonAsync<LinkResponse>())!;

        var publicClient = CreateClient(authorized: false);
        var response = await publicClient.GetAsync($"/links/{link.Code}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signed_link_requires_signature()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest(hmac: true))).Content.ReadFromJsonAsync<LinkResponse>())!;
        var signedSegment = link.ShortUrl[(link.ShortUrl.IndexOf("/l/", StringComparison.Ordinal) + 3)..];

        var publicClient = CreateClient(authorized: false);
        var withSignature = await publicClient.GetAsync($"/l/{signedSegment}");
        var withoutSignature = await publicClient.GetAsync($"/l/{link.Code}");

        Assert.Equal(HttpStatusCode.Redirect, withSignature.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, withoutSignature.StatusCode);
    }

    [Fact]
    public async Task Stats_reflect_visits()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest())).Content.ReadFromJsonAsync<LinkResponse>())!;

        var publicClient = CreateClient(authorized: false);
        await publicClient.GetAsync($"/l/{link.Code}");
        await publicClient.GetAsync($"/l/{link.Code}");

        var stats = await client.GetFromJsonAsync<LinkStatsResponse>($"/links/{link.Code}/stats");
        Assert.NotNull(stats);
        Assert.Equal(2, stats!.TotalVisits);
    }

    [Fact]
    public async Task Deactivate_makes_link_gone()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest())).Content.ReadFromJsonAsync<LinkResponse>())!;

        var deleteResponse = await client.DeleteAsync($"/links/{link.Code}");

        var publicClient = CreateClient(authorized: false);
        var visit = await publicClient.GetAsync($"/l/{link.Code}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, visit.StatusCode);
    }

    [Fact]
    public async Task Audit_contains_created_action()
    {
        var client = CreateClient();
        var link = (await (await client.PostAsJsonAsync("/links", NewRequest())).Content.ReadFromJsonAsync<LinkResponse>())!;

        var audit = await client.GetFromJsonAsync<List<AuditLogItem>>("/audit?action=LinkCreated&take=200");

        Assert.NotNull(audit);
        Assert.Contains(audit!, a => a.Code == link.Code);
    }
}
