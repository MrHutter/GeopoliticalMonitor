namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class SourceItemEntity
{
    public string Id { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? ExternalId { get; set; }
    public string? Title { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? AuthorDisplayName { get; set; }
    public string? AuthorHandle { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationDisplayName { get; set; }
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public ICollection<EventSourceAssociationEntity> EventAssociations { get; } = new List<EventSourceAssociationEntity>();
}