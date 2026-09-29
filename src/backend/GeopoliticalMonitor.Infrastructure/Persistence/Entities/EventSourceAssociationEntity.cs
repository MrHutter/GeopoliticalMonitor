namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class EventSourceAssociationEntity
{
    public string EventId { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public EventEntity? Event { get; set; }
    public SourceItemEntity? SourceItem { get; set; }
}