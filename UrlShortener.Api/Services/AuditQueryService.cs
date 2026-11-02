using System;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;
using UrlShortener.Contracts;

namespace UrlShortener.Api.Services;

public sealed record AuditFilter(
    DateTimeOffset? From,
    DateTimeOffset? To,
    AuditAction? Action,
    int Take);

public sealed class AuditQueryService
{
    private readonly AppDbContext _db;

    public AuditQueryService(AppDbContext db) => _db = db;

    public async Task<List<AuditLogItem>> QueryAsync(AuditFilter filter, CancellationToken ct)
    {
        var query = _db.AuditLog.AsNoTracking();

        if (filter.From is { } from)
            query = query.Where(a => a.CreatedAt >= from);

        if (filter.To is { } to)
            query = query.Where(a => a.CreatedAt <= to);

        if (filter.Action is { } action)
            query = query.Where(a => a.Action == action);

        // Некоторые провайдеры (например, SQLite) не поддерживают DateTimeOffset в ORDER BY.
        // Чтобы "последние N" были консистентны, сортируем И ограничиваем по одному критерию — Id DESC.
        // Для append-only audit-лога Id (autoincrement) монотонен относительно CreatedAt, поэтому
        // Id DESC и есть "новейшие сверху". Пересортировку по CreatedAt в памяти не делаем:
        // иначе лимит берётся по одному ключу, а порядок по другому — ровно тот дефект, что был.
        List<AuditLogEntry> entries;
        // Проверяем имя провайдера, так как DatabaseFacade не имеет IsSqlite расширения здесь.
        var providerName = _db.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            entries = await query
                .OrderByDescending(a => a.Id)
                .Take(filter.Take)
                .ToListAsync(ct);

            return entries
                .Select(a => new AuditLogItem
                {
                    Id = a.Id,
                    Action = a.Action.ToString(),
                    Code = a.Code,
                    Initiator = a.Initiator,
                    Result = a.Result,
                    CreatedAt = a.CreatedAt
                })
                .ToList();
        }

        // Для провайдеров, поддерживающих DateTimeOffset в ORDER BY, делаем корректную выборку в БД
        entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(filter.Take)
            .ToListAsync(ct);

        return entries
            .Select(a => new AuditLogItem
            {
                Id = a.Id,
                Action = a.Action.ToString(),
                Code = a.Code,
                Initiator = a.Initiator,
                Result = a.Result,
                CreatedAt = a.CreatedAt
            })
            .ToList();
    }
}
