using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Events;

public interface IEventStore
{
    Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken = default);
    Task<Event?> GetEventByIdAsync(EventId eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SourceItem>> GetSourcesForEventAsync(EventId eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<EventId, int>> GetSourceCountsAsync(CancellationToken cancellationToken = default);
    Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default);
    Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default);
    Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default);
    Task<SourceAttachmentStoreResult> AttachSourceToEventAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default);
}
