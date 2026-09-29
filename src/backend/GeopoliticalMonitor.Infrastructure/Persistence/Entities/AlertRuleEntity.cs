namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class AlertRuleEntity
{
    public string Id { get; set; } = string.Empty;
    public string WatchlistId { get; set; } = string.Empty;
    public string TriggerType { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastEnabledAtUtc { get; set; }
    public WatchlistEntity? Watchlist { get; set; }
    public ICollection<AlertOccurrenceEntity> Occurrences { get; } = new List<AlertOccurrenceEntity>();
}
