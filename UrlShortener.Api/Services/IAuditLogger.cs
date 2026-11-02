using UrlShortener.Api.Domain;

namespace UrlShortener.Api.Services;

public interface IAuditLogger
{
    void Record(AuditAction action, string initiator, string? code, string result);
}
