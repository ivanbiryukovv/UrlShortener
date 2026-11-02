using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain.AccessRules;
using UrlShortener.Api.Endpoints;
using UrlShortener.Api.Services;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsEnvironment("Testing"))
{
    if (builder.Environment.IsDevelopment())
        builder.Configuration.AddUserSecrets<Program>(optional: true);
    builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<LinkServiceOptions>(builder.Configuration.GetSection(LinkServiceOptions.SectionName));
builder.Services.Configure<RateLimitOptions>(builder.Configuration.GetSection(RateLimitOptions.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ICodeGenerator, CodeGenerator>();
builder.Services.AddSingleton<IHmacSigner, HmacSigner>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<LinkService>();
builder.Services.AddScoped<AuditQueryService>();
builder.Services.AddSingleton<RateLimiter>();
builder.Services.AddScoped<RateGuard>();

if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<LogArchiveService>();

builder.Services.AddScoped<LinkAccessRule, ActiveRule>();
builder.Services.AddScoped<LinkAccessRule, ExpirationRule>();
builder.Services.AddScoped<LinkAccessRule, SignatureRule>();
builder.Services.AddScoped<LinkAccessChecker>();

if (!builder.Environment.IsEnvironment("Testing"))
{
    var signingKey = builder.Configuration.GetValue<string>("Auth:SigningKey");
    if (string.IsNullOrEmpty(signingKey))
    {
        if (builder.Environment.IsProduction())
            throw new InvalidOperationException("Auth:SigningKey must be configured in production environment.");
        signingKey = "development-signing-key-please-override-in-production-insecure-min-32chars!!!";
    }

    var issuer = builder.Configuration.GetValue<string>("Auth:Issuer") ?? AuthDefaults.Issuer;
    var audience = builder.Configuration.GetValue<string>("Auth:Audience") ?? AuthDefaults.Audience;

    var keyBytes = Encoding.UTF8.GetBytes(signingKey);
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = builder.Environment.IsProduction();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };
    });
}
else
{
    var testKeyBytes = Encoding.UTF8.GetBytes("test-auth-key-minimum-32-characters-required-for-jwt-bearer-auth");
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(testKeyBytes)
        };
    });
}

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var autoMigrate = config.GetValue<bool>("Database:AutoMigrate");
    if (autoMigrate)
    {
        if (db.Database.IsRelational())
            db.Database.Migrate();
        else
            db.Database.EnsureCreated();
    }

    var linkOptions = config.GetSection(LinkServiceOptions.SectionName).Get<UrlShortener.Api.Configuration.LinkServiceOptions>();
    var signingKey = config.GetValue<string>("Auth:SigningKey");
    var adminSecret = config.GetValue<string>("Auth:AdminSecret");

    if (!env.IsEnvironment("Testing") && env.IsProduction())
    {
        if (string.IsNullOrEmpty(linkOptions?.HmacSecret)
            || linkOptions!.HmacSecret.Contains("development-hmac-secret-please-override")
            || linkOptions.HmacSecret.Length < 32)
            throw new InvalidOperationException("HMAC secret is not configured for production. Set Links:HmacSecret via environment or user-secrets, ensure length >= 32, and that it differs from the bundled development default.");

        if (string.IsNullOrEmpty(signingKey) || signingKey.Contains("development-signing-key-please-override") || signingKey.Length < 32)
            throw new InvalidOperationException("JWT signing key is not configured for production. Set Auth:SigningKey via environment or user-secrets and ensure length >= 32.");

        if (string.IsNullOrEmpty(adminSecret) || adminSecret.Contains("development-admin-secret-please-override") || adminSecret.Length < 16)
            throw new InvalidOperationException("Admin secret is not configured for production. Set Auth:AdminSecret via environment or user-secrets and ensure length >= 16.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapLinkEndpoints();
app.MapAuditEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program;
