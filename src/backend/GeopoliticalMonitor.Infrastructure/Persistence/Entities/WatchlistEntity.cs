namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class WatchlistEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public ICollection<WatchlistEventEntity> EventMemberships { get; } = new List<WatchlistEventEntity>();
    public ICollection<AlertRuleEntity> AlertRules { get; } = new List<AlertRuleEntity>();
}
