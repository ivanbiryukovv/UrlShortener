using UrlShortener.Api.Data;
using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Services;

public sealed class AuditLogger : IAuditLogger
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public AuditLogger(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public void Record(AuditAction action, string initiator, string? code, string result)
    {
        _db.AuditLog.Add(new AuditLogEntry
        {
            Action = action,
            Initiator = initiator,
            Code = code,
            Result = result,
            CreatedAt = _clock.GetUtcNow()
        });
    }
}
