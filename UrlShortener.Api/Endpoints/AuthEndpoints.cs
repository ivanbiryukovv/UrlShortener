using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using UrlShortener.Api.Services;
using Microsoft.IdentityModel.Tokens;
using UrlShortener.Api.Configuration;

namespace UrlShortener.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/admin/login", async (HttpContext http, [FromServices] RateGuard guard, [FromServices] IConfiguration config, [FromBody] LoginRequest req, CancellationToken ct) =>
        {
            if (req is null || string.IsNullOrEmpty(req.Secret))
                return Results.BadRequest(new { error = "Secret is required" });

            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var reject = await guard.RejectAsync(ip, RateScope.Auth, ct);
            if (reject is not null)
                return reject;

            var adminSecret = config.GetValue<string>("Auth:AdminSecret");
            var signingKey = config.GetValue<string>("Auth:SigningKey");
            if (string.IsNullOrEmpty(adminSecret) || string.IsNullOrEmpty(signingKey))
                return Results.StatusCode(500);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(req.Secret), Encoding.UTF8.GetBytes(adminSecret)))
                return Results.Unauthorized();

            var issuer = config.GetValue<string>("Auth:Issuer") ?? AuthDefaults.Issuer;
            var audience = config.GetValue<string>("Auth:Audience") ?? AuthDefaults.Audience;

            var keyBytes = Encoding.UTF8.GetBytes(signingKey);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: new[] { new Claim(ClaimTypes.NameIdentifier, "admin") },
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new { token = tokenString });
        }).WithTags("Auth");

    }

    public record LoginRequest(string Secret);
}
