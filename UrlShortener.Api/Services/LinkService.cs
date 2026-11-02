using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;
using UrlShortener.Contracts;

namespace UrlShortener.Api.Services;

public readonly record struct VisitOutcome(VisitResult Result, Guid? OrderId)
{
    public bool IsSuccess => Result == VisitResult.Success;
}

public sealed class LinkService
{
    /// <summary>Верхняя граница срока жизни ссылки в днях — защищает от переполнения DateTimeOffset.AddDays.</summary>
    public const int MaxTtlDays = 3650;

    private const int MaxCodeAttempts = 5;

    // Должно совпадать с HasMaxLength для LinkVisit.UserAgent в AppDbContext.
    private const int UserAgentMaxLength = 512;

    private readonly AppDbContext _db;
    private readonly ICodeGenerator _codeGenerator;
    private readonly IHmacSigner _signer;
    private readonly LinkAccessChecker _accessChecker;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _clock;
    private readonly LinkServiceOptions _options;

    public LinkService(
        AppDbContext db,
        ICodeGenerator codeGenerator,
        IHmacSigner signer,
        LinkAccessChecker accessChecker,
        IAuditLogger audit,
        TimeProvider clock,
        IOptions<LinkServiceOptions> options)
    {
        _db = db;
        _codeGenerator = codeGenerator;
        _signer = signer;
        _accessChecker = accessChecker;
        _audit = audit;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<LinkResponse> CreateAsync(CreateLinkRequest request, string initiator, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var ttlDays = Math.Clamp(
            request.TtlDays is > 0 ? request.TtlDays.Value : _options.DefaultTtlDays, 1, MaxTtlDays);

        for (var attempt = 1; ; attempt++)
        {
            var link = new Link
            {
                Code = await GenerateUniqueCodeAsync(ct),
                OrderId = request.OrderId,
                CreatedAt = now,
                ExpiresAt = now.AddDays(ttlDays),
                IsOneTime = request.IsOneTime,
                IsActive = true
            };

            if (request.UseHmac)
                link.HmacSignature = _signer.Sign(link.Code);

            _db.Links.Add(link);
            _audit.Record(AuditAction.LinkCreated, initiator, link.Code, $"order={request.OrderId}");

            try
            {
                await _db.SaveChangesAsync(ct);
                return ToResponse(link, 0);
            }
            catch (DbUpdateException ex) when (attempt < MaxCodeAttempts)
            {
                // Попытка различить реальную уникальную коллизию от прочих ошибок.
                // Для реляционных баз этот признак доступен через внутренние сообщения провайдера.
                var message = ex.InnerException?.Message ?? ex.Message;
                if (IsUniqueConstraintViolation(message))
                {
                    // Редкая гонка: код заняли между проверкой уникальности и вставкой (PK = Code).
                    // Откатываем добавленные в этот проход сущности и пробуем заново с новым кодом.
                    foreach (var entry in _db.ChangeTracker.Entries()
                                 .Where(e => e.State == EntityState.Added).ToList())
                        entry.State = EntityState.Detached;
                    // Повторим цикл
                }
                else
                {
                    // Не уникальная ошибка — пробрасываем дальше для обнаружения и логирования.
                    throw;
                }
            }
        }
    }

    public async Task<VisitOutcome> VisitAsync(
        string code, string? signature, string ip, string? userAgent, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        var link = await _db.Links.FirstOrDefaultAsync(l => l.Code == code, ct);
        if (link is null)
        {
            // Неизвестный код не логируем: перебор /l/<рандом> иначе бесконтрольно раздувает
            // link_visits и audit_log (амплификация хранилища при сканировании).
            return new VisitOutcome(VisitResult.NotFound, null);
        }

        var result = _accessChecker.Evaluate(link, now, signature);

        if (result == VisitResult.Success && link.IsOneTime && link.IsActive)
        {
            if (_db.Database.IsRelational())
            {
                // Атомарное гашение одноразовой ссылки: гонку выигрывает один UPDATE.
                var rows = await _db.Database.ExecuteSqlRawAsync(
                    "UPDATE links SET IsActive = 0 WHERE Code = {0} AND IsActive = 1", new object[] { link.Code }, ct);
                if (rows != 1)
                {
                    result = VisitResult.Inactive;
                }
                else
                {
                    _audit.Record(AuditAction.LinkDeactivated, "система", link.Code, "одноразовый переход");
                }
            }
            else
            {
                // Нереляционные провайдеры (например, InMemory) не поддерживают raw SQL — гасим в трекере.
                link.IsActive = false;
                _audit.Record(AuditAction.LinkDeactivated, "система", link.Code, "одноразовый переход");
            }
        }

        await LogVisitAsync(code, ip, userAgent, result, now, ct);

        return new VisitOutcome(result, result == VisitResult.Success ? link.OrderId : null);
    }

    public async Task<List<LinkResponse>> GetAllAsync(CancellationToken ct, int page = 1, int pageSize = 100)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        var query = _db.Links.OrderByDescending(l => l.CreatedAt);

        var links = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var codes = links.Select(l => l.Code).ToList();

        var counts = await _db.Visits
            .Where(v => v.Result == VisitResult.Success && codes.Contains(v.Code))
            .GroupBy(v => v.Code)
            .Select(g => new { Code = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Code, x => x.Count, ct);

        return links
            .Select(l => ToResponse(l, counts.GetValueOrDefault(l.Code)))
            .ToList();
    }

    public async Task<LinkResponse?> GetInfoAsync(string code, CancellationToken ct)
    {
        var link = await _db.Links.FirstOrDefaultAsync(l => l.Code == code, ct);
        if (link is null)
            return null;

        var visitCount = await _db.Visits.CountAsync(v => v.Code == code && v.Result == VisitResult.Success, ct);
        return ToResponse(link, visitCount);
    }

    public async Task<LinkStatsResponse?> GetStatsAsync(string code, CancellationToken ct)
    {
        if (!await _db.Links.AnyAsync(l => l.Code == code, ct))
            return null;

        var successful = await _db.Visits
            .Where(v => v.Code == code && v.Result == VisitResult.Success)
            .Select(v => new { v.Ip, v.VisitedAt })
            .ToListAsync(ct);

        var byDay = successful
            .GroupBy(v => DateOnly.FromDateTime(v.VisitedAt.UtcDateTime))
            .OrderBy(g => g.Key)
            .Select(g => new DailyVisits { Date = g.Key, Count = g.Count() })
            .ToList();

        return new LinkStatsResponse
        {
            Code = code,
            TotalVisits = successful.Count,
            UniqueVisitors = successful.Select(v => v.Ip).Distinct().Count(),
            ByDay = byDay
        };
    }

    public async Task<bool> DeactivateAsync(string code, string initiator, CancellationToken ct)
    {
        var link = await _db.Links.FirstOrDefaultAsync(l => l.Code == code, ct);
        if (link is null)
            return false;

        if (link.IsActive)
        {
            link.IsActive = false;
            _audit.Record(AuditAction.LinkDeactivated, initiator, code, "деактивирована вручную");
            await _db.SaveChangesAsync(ct);
        }

        return true;
    }

    private async Task LogVisitAsync(
        string code, string ip, string? userAgent, VisitResult result, DateTimeOffset now, CancellationToken ct)
    {
        _db.Visits.Add(new LinkVisit
        {
            Code = code,
            Ip = ip,
            // Обрезаем до длины колонки: длинный заголовок User-Agent иначе ронял бы вставку
            // (и весь редирект) усечением на SQL Server.
            UserAgent = Truncate(userAgent, UserAgentMaxLength),
            VisitedAt = now,
            Result = result
        });
        _audit.Record(AuditAction.LinkVisited, ip, code, result.ToString());
        await _db.SaveChangesAsync(ct);
    }

    private static string? Truncate(string? value, int maxLength)
        => value is not null && value.Length > maxLength ? value[..maxLength] : value;

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = _codeGenerator.Generate();
            if (!await _db.Links.AnyAsync(l => l.Code == code, ct))
                return code;
        }
    }

    private static bool IsUniqueConstraintViolation(string message)
    {
        if (string.IsNullOrEmpty(message))
            return false;

        // Общие сигнатуры для разных провайдеров
        return message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("unique", StringComparison.OrdinalIgnoreCase)
               || message.Contains("violat", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Cannot insert duplicate key", StringComparison.OrdinalIgnoreCase);
    }

    private LinkResponse ToResponse(Link link, int visitCount) => new()
    {
        Code = link.Code,
        ShortUrl = BuildShortUrl(link),
        OrderId = link.OrderId,
        CreatedAt = link.CreatedAt,
        ExpiresAt = link.ExpiresAt,
        IsOneTime = link.IsOneTime,
        IsActive = link.IsActive,
        VisitCount = visitCount
    };

    private string BuildShortUrl(Link link)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        return link.RequiresSignature
            ? $"{baseUrl}/l/{link.Code}.{link.HmacSignature}"
            : $"{baseUrl}/l/{link.Code}";
    }
}
