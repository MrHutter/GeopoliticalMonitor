namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class WatchlistEventEntity
{
    public string WatchlistId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public WatchlistEntity? Watchlist { get; set; }
    public EventEntity? Event { get; set; }
}
