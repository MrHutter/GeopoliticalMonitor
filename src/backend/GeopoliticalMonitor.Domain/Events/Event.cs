using GeopoliticalMonitor.Domain;

namespace GeopoliticalMonitor.Domain.Events;

public sealed class Event
{
    private Event(
        EventId id,
        string title,
        string summary,
        EventCategory category,
        EventStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset lastUpdatedAtUtc,
        Location location,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        Id = id;
        Title = title;
        Summary = summary;
        Category = category;
        Status = status;
        StartedAtUtc = startedAtUtc;
        LastUpdatedAtUtc = lastUpdatedAtUtc;
        Location = location;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public EventId Id { get; }
    public string Title { get; }
    public string Summary { get; }
    public EventCategory Category { get; }
    public EventStatus Status { get; }
    public DateTimeOffset StartedAtUtc { get; }
    public DateTimeOffset LastUpdatedAtUtc { get; }
    public Location Location { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    public static Event Create(
        EventId id,
        string title,
        string summary,
        EventCategory category,
        EventStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset lastUpdatedAtUtc,
        Location location,
        DateTimeOffset createdAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Event title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException("Event summary is required.", nameof(summary));
        }

        return new Event(id, title, summary, category, status, startedAtUtc, lastUpdatedAtUtc, location, createdAtUtc, updatedAtUtc);
    }
}