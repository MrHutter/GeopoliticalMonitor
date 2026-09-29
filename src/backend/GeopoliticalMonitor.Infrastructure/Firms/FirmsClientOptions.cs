namespace GeopoliticalMonitor.Infrastructure.Firms;

public sealed record FirmsClientOptions(string? MapKey)
{
    public string? NormalizedMapKey => string.IsNullOrWhiteSpace(MapKey) ? null : MapKey.Trim();
}
