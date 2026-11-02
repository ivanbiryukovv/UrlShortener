using UrlShortener.Api.Domain;
using UrlShortener.Api.Services;
using UrlShortener.Contracts;

namespace UrlShortener.Api.Endpoints;

public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/links", CreateLink).RequireAuthorization();
        app.MapGet("/links", GetAll).RequireAuthorization();
        app.MapGet("/l/{code}", Redirect);
        app.MapGet("/links/{code}", GetInfo).RequireAuthorization();
        app.MapGet("/links/{code}/stats", GetStats).RequireAuthorization();
        app.MapDelete("/links/{code}", Deactivate).RequireAuthorization();
    }

    private static async Task<IResult> CreateLink(
        CreateLinkRequest request, LinkService service, RateGuard guard, HttpContext http, CancellationToken ct)
    {
        var ip = ClientIp(http);
        if (await guard.RejectAsync(ip, RateScope.Create, ct) is { } limited)
            return limited;

        if (request.OrderId == Guid.Empty)
            return Results.BadRequest(new { error = "orderId обязателен" });

        if (request.TtlDays is { } ttl && (ttl < 1 || ttl > LinkService.MaxTtlDays))
            return Results.BadRequest(new { error = $"ttlDays должен быть в диапазоне 1..{LinkService.MaxTtlDays}" });

        var response = await service.CreateAsync(request, ip, ct);
        return Results.Created(response.ShortUrl, response);
    }

    private static async Task<IResult> Redirect(
        string code, LinkService service, RateGuard guard, HttpContext http, CancellationToken ct)
    {
        var ip = ClientIp(http);
        if (await guard.RejectAsync(ip, RateScope.Redirect, ct) is { } limited)
            return limited;

        var (linkCode, signature) = SplitCode(code);
        var outcome = await service.VisitAsync(
            linkCode, signature, ip, http.Request.Headers.UserAgent, ct);

        return outcome.Result switch
        {
            VisitResult.Success => Results.Redirect($"/order/{outcome.OrderId}", permanent: false),
            VisitResult.Expired => Results.StatusCode(StatusCodes.Status410Gone),
            VisitResult.InvalidSignature => Results.StatusCode(StatusCodes.Status403Forbidden),
            VisitResult.Inactive => Results.StatusCode(StatusCodes.Status410Gone),
            _ => Results.NotFound()
        };
    }

    private static async Task<IResult> GetAll(LinkService service, CancellationToken ct, int? page = null, int? pageSize = null)
    {
        var p = page ?? 1;
        var ps = pageSize ?? 100;
        var items = await service.GetAllAsync(ct, p, ps);
        return Results.Ok(items);
    }

    private static async Task<IResult> GetInfo(string code, LinkService service, CancellationToken ct)
    {
        var info = await service.GetInfoAsync(code, ct);
        return info is null ? Results.NotFound() : Results.Ok(info);
    }

    private static async Task<IResult> GetStats(string code, LinkService service, CancellationToken ct)
    {
        var stats = await service.GetStatsAsync(code, ct);
        return stats is null ? Results.NotFound() : Results.Ok(stats);
    }

    private static async Task<IResult> Deactivate(
        string code, LinkService service, HttpContext http, CancellationToken ct)
    {
        var found = await service.DeactivateAsync(code, ClientIp(http), ct);
        return found ? Results.NoContent() : Results.NotFound();
    }

    private static (string Code, string? Signature) SplitCode(string raw)
    {
        var dot = raw.IndexOf('.');
        return dot < 0 ? (raw, null) : (raw[..dot], raw[(dot + 1)..]);
    }

    private static string ClientIp(HttpContext http)
        => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
