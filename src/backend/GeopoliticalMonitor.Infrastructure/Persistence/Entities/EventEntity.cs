namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class EventEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset LastUpdatedAtUtc { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationDisplayName { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Severity { get; set; } = string.Empty;
    public string SeverityRule { get; set; } = string.Empty;
    public string SeverityInputs { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? ProviderKey { get; set; }
    public int? ProviderLastSeenRelease { get; set; }
    public ICollection<EventSourceAssociationEntity> SourceAssociations { get; } = new List<EventSourceAssociationEntity>();
    public ICollection<WatchlistEventEntity> WatchlistMemberships { get; } = new List<WatchlistEventEntity>();
    public ICollection<AlertOccurrenceEntity> AlertOccurrences { get; } = new List<AlertOccurrenceEntity>();
}
