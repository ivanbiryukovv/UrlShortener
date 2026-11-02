using UrlShortener.Api.Domain;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Endpoints;

public static class AuditEndpoints
{
    private const int DefaultTake = 100;
    private const int MaxTake = 500;

    public static void MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/audit", GetAudit).RequireAuthorization();
    }

    private static async Task<IResult> GetAudit(
        AuditQueryService service,
        CancellationToken ct,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? action = null,
        int? take = null)
    {
        AuditAction? parsedAction = null;
        if (!string.IsNullOrWhiteSpace(action))
        {
            if (!Enum.TryParse<AuditAction>(action, ignoreCase: true, out var value))
                return Results.BadRequest(new { error = $"неизвестное действие: {action}" });

            parsedAction = value;
        }

        var limit = Math.Clamp(take ?? DefaultTake, 1, MaxTake);
        var filter = new AuditFilter(from, to, parsedAction, limit);
        var items = await service.QueryAsync(filter, ct);
        return Results.Ok(items);
    }
}
