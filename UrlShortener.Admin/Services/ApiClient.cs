using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using UrlShortener.Contracts;

namespace UrlShortener.Admin.Services;

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message)
    {
    }
}

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly ITokenProvider _tokens;

    public ApiClient(HttpClient http, ITokenProvider tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public void SetToken(string token) => _tokens.SetToken(token);

    public async Task<string> AuthenticateAsync(string secret, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("admin/login", new { secret }, ct);
        await EnsureSuccessAsync(response);
        var obj = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        if (obj is null || string.IsNullOrEmpty(obj.Token))
            throw new ApiException("Не удалось получить токен от сервера.");

        SetToken(obj.Token);
        return obj.Token;
    }

    public async Task<LinkResponse> CreateLinkAsync(CreateLinkRequest request, CancellationToken ct = default)
    {
        var response = await SendAsync(() => _http.PostAsJsonAsync("links", request, ct));
        return (await response.Content.ReadFromJsonAsync<LinkResponse>(cancellationToken: ct))!;
    }

    public async Task<List<LinkResponse>> GetAllLinksAsync(CancellationToken ct = default)
    {
        const int pageSize = 200;
        var all = new List<LinkResponse>();

        for (var page = 1; ; page++)
        {
            var response = await _http.GetAsync($"links?page={page}&pageSize={pageSize}", ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ApiException("401 Unauthorized: Токен истёк или невалиден. Пожалуйста, перезагрузите приложение.");

            await EnsureSuccessAsync(response);
            var batch = (await response.Content.ReadFromJsonAsync<List<LinkResponse>>(cancellationToken: ct)) ?? new();
            all.AddRange(batch);

            if (batch.Count < pageSize)
                break;
        }

        return all;
    }

    public async Task<LinkResponse?> GetLinkAsync(string code, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"links/{code}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LinkResponse>(cancellationToken: ct);
    }

    public async Task<LinkStatsResponse?> GetStatsAsync(string code, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"links/{code}/stats", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<LinkStatsResponse>(cancellationToken: ct);
    }

    public async Task DeactivateLinkAsync(string code, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"links/{code}", ct);
        await EnsureSuccessAsync(response);
    }

    public async Task<List<AuditLogItem>> GetAuditAsync(
        DateTimeOffset? from, DateTimeOffset? to, string? action, int take, CancellationToken ct = default)
    {
        var query = new List<string> { $"take={take}" };
        if (from is { } f) query.Add($"from={Uri.EscapeDataString(f.ToString("o"))}");
        if (to is { } t) query.Add($"to={Uri.EscapeDataString(t.ToString("o"))}");
        if (!string.IsNullOrWhiteSpace(action)) query.Add($"action={Uri.EscapeDataString(action)}");

        var response = await _http.GetAsync($"audit?{string.Join('&', query)}", ct);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<List<AuditLogItem>>(cancellationToken: ct)) ?? new();
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> call)
    {
        try
        {
            var response = await call();
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ApiException("401 Unauthorized: токен недействителен или истёк.");

            await EnsureSuccessAsync(response);
            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException($"Не удалось связаться с сервером: {ex.Message}");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync();
        throw new ApiException($"Сервер вернул {(int)response.StatusCode}: {body}");
    }

    private record LoginResponse(string Token);
}
