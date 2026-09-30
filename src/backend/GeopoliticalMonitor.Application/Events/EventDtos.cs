using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Events;

public sealed record LocationDto(double Latitude, double Longitude, string DisplayName, string? CountryCode, string? Region);

public sealed record EventDto(
    string Id,
    string Title,
    string Summary,
    string Category,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastUpdatedAtUtc,
    LocationDto Location,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    int SourceCount,
    string Severity,
    string SeverityRule,
    string SeverityInputs,
    string Provider);

public sealed record SourceItemDto(
    string Id,
    string SourceType,
    string? ExternalId,
    string? Title,
    string Text,
    string? Url,
    string? AuthorDisplayName,
    string? AuthorHandle,
    DateTimeOffset PublishedAtUtc,
    DateTimeOffset RetrievedAtUtc,
    LocationDto? Location);

public static class EventDtoMapper
{
    public static EventDto ToDto(Event geopoliticalEvent, int sourceCount)
    {
        return new EventDto(
            geopoliticalEvent.Id.Value,
            geopoliticalEvent.Title,
            geopoliticalEvent.Summary,
            geopoliticalEvent.Category.ToString(),
            geopoliticalEvent.Status.ToString(),
            geopoliticalEvent.StartedAtUtc,
            geopoliticalEvent.LastUpdatedAtUtc,
            ToDto(geopoliticalEvent.Location),
            geopoliticalEvent.CreatedAtUtc,
            geopoliticalEvent.UpdatedAtUtc,
            sourceCount,
            geopoliticalEvent.Severity.ToString(),
            geopoliticalEvent.SeverityRule,
            geopoliticalEvent.SeverityInputs,
            geopoliticalEvent.Provider.ToString());
    }

    public static SourceItemDto ToDto(SourceItem sourceItem)
    {
        return new SourceItemDto(
            sourceItem.Id.Value,
            sourceItem.SourceType.ToString(),
            sourceItem.ExternalId,
            sourceItem.Title,
            sourceItem.Text,
            sourceItem.Url?.ToString(),
            sourceItem.AuthorDisplayName,
            sourceItem.AuthorHandle,
            sourceItem.PublishedAtUtc,
            sourceItem.RetrievedAtUtc,
            sourceItem.Location is null ? null : ToDto(sourceItem.Location));
    }

    private static LocationDto ToDto(Domain.Location location)
    {
        return new LocationDto(location.Latitude, location.Longitude, location.DisplayName, location.CountryCode, location.Region);
    }
}