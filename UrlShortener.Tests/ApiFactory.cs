using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;

namespace UrlShortener.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>, IDisposable
{

    private readonly string _dbName = $"memdb-{Guid.NewGuid():N}";
    private readonly SqliteConnection _keeper;

    public ApiFactory()
    {
        _keeper = new SqliteConnection($"DataSource=file:{_dbName}?mode=memory&cache=shared");
        _keeper.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "DataSource=:memory:",
                ["Links:HmacSecret"] = "секрет-для-интеграционных-тестов-32-символа",
                ["Auth:SigningKey"] = "test-auth-key-minimum-32-characters-required-for-jwt-bearer-auth",
                ["Auth:AdminSecret"] = "test-admin-secret-minimum-16-chars",
                ["RateLimit:CreatePerMinute"] = "10000",
                    ["RateLimit:RedirectPerMinute"] = "10000",
                    ["RateLimit:AuthPerMinute"] = "5"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.Single(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            services.Remove(descriptor);

            var connectionString = $"DataSource=file:{_dbName}?mode=memory&cache=shared";
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    public new void Dispose()
    {
        base.Dispose();
        _keeper?.Dispose();
    }
}
