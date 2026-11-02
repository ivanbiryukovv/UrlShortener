namespace UrlShortener.Contracts;

public sealed class DailyVisits
{
    public DateOnly Date { get; set; }

    public int Count { get; set; }
}

public sealed class LinkStatsResponse
{
    public string Code { get; set; } = string.Empty;

    public int TotalVisits { get; set; }

    public int UniqueVisitors { get; set; }

    public List<DailyVisits> ByDay { get; set; } = new();
}
