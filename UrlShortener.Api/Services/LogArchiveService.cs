using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Data;

namespace UrlShortener.Api.Services;

public sealed class LogArchiveService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly LinkServiceOptions _options;
    private readonly ILogger<LogArchiveService> _logger;

    public LogArchiveService(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        IOptions<LinkServiceOptions> options,
        ILogger<LogArchiveService> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var archiveThreshold = now.AddDays(-_options.LogRetentionDays);
        // Полное удаление с двойным запасом по сроку хранения — иначе link_visits растёт без предела.
        var purgeThreshold = now.AddDays(-2 * _options.LogRetentionDays);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var archived = await db.Visits
            .Where(v => !v.IsArchived && v.VisitedAt < archiveThreshold)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.IsArchived, true), ct);

        var purgedVisits = await db.Visits
            .Where(v => v.VisitedAt < purgeThreshold)
            .ExecuteDeleteAsync(ct);

        var removedAudit = await db.AuditLog
            .Where(a => a.CreatedAt < archiveThreshold)
            .ExecuteDeleteAsync(ct);

        if (archived > 0 || purgedVisits > 0 || removedAudit > 0)
            _logger.LogInformation(
                "Обслуживание логов: помечено архивными {Archived}, удалено визитов {PurgedVisits}, удалено аудита {RemovedAudit}",
                archived, purgedVisits, removedAudit);
    }
}
