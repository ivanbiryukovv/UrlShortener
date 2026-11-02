namespace UrlShortener.Api.Domain;

public enum VisitResult
{
    Success,
    Expired,
    NotFound,
    Inactive,
    InvalidSignature
}
