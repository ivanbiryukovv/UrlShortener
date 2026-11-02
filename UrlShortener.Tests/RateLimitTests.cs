using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using UrlShortener.Contracts;

namespace UrlShortener.Tests;

public class RateLimitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RateLimitTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Redirect_returns_429_after_limit_exceeded()
    {
        var strict = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(config =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimit:RedirectPerMinute"] = "2",
                    ["RateLimit:ViolationsBeforeBlock"] = "100"
                })));

        var authed = strict.CreateClient();
        authed.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestAuth.Jwt()}");
        var created = await authed.PostAsJsonAsync("/links", new CreateLinkRequest { OrderId = Guid.NewGuid(), TtlDays = 30 });
        var link = (await created.Content.ReadFromJsonAsync<LinkResponse>())!;

        var pub = strict.CreateClient(new() { AllowAutoRedirect = false });

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await pub.GetAsync($"/l/{link.Code}")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, statuses[0]);
        Assert.Equal(HttpStatusCode.Redirect, statuses[1]);
        Assert.Equal((HttpStatusCode)429, statuses[3]);
    }
}
