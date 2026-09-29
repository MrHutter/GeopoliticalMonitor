namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class AlertOccurrenceEntity
{
    public string Id { get; set; } = string.Empty;
    public string AlertRuleId { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public DateTimeOffset ProviderRevisionAtUtc { get; set; }
    public DateTimeOffset TriggeredAtUtc { get; set; }
    public DateTimeOffset? ReadAtUtc { get; set; }
    public AlertRuleEntity? AlertRule { get; set; }
    public EventEntity? Event { get; set; }
}
