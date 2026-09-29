using System.Security.Cryptography;
using System.Text;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Events;

public sealed class EventCatalogService
{
    private readonly SemaphoreSlim sourceAttachmentGate = new(1, 1);
    private readonly IEventStore store;
    private readonly TimeProvider timeProvider;

    public EventCatalogService(IEventStore store, TimeProvider? timeProvider = null)
    {
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<EventDto>> GetEventsAsync(CancellationToken cancellationToken = default)
    {
        var events = await store.GetEventsAsync(cancellationToken).ConfigureAwait(false);
        var counts = await store.GetSourceCountsAsync(cancellationToken).ConfigureAwait(false);
        return events.Select(item => EventDtoMapper.ToDto(item, counts.GetValueOrDefault(item.Id))).ToArray();
    }

    public async Task<EventDto?> GetEventByIdAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (!TryParseEventId(eventId, out var parsedEventId))
        {
            throw new ArgumentException("Event id is invalid.", nameof(eventId));
        }

        var geopoliticalEvent = await store.GetEventByIdAsync(parsedEventId, cancellationToken).ConfigureAwait(false);
        if (geopoliticalEvent is null)
        {
            return null;
        }

        var sources = await store.GetSourcesForEventAsync(parsedEventId, cancellationToken).ConfigureAwait(false);
        return EventDtoMapper.ToDto(geopoliticalEvent, sources.Count);
    }

    public async Task<IReadOnlyList<SourceItemDto>> GetSourcesForEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (!TryParseEventId(eventId, out var parsedEventId))
        {
            throw new ArgumentException("Event id is invalid.", nameof(eventId));
        }

        var sources = await store.GetSourcesForEventAsync(parsedEventId, cancellationToken).ConfigureAwait(false);
        return sources.Select(EventDtoMapper.ToDto).ToArray();
    }

    public Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default)
    {
        return store.CreateEventAsync(geopoliticalEvent, cancellationToken);
    }

    public Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default)
    {
        return store.CreateSourceItemAsync(sourceItem, cancellationToken);
    }

    public Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default)
    {
        return store.AttachSourceToEventAsync(eventId, sourceItemId, cancellationToken);
    }

    public async Task<AttachXPostSourceResult> AttachXPostSourceToEventAsync(
        string? eventId,
        string? xPostUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseEventId(eventId, out var parsedEventId))
        {
            return new AttachXPostSourceResult(AttachXPostSourceDisposition.InvalidEventId);
        }

        if (!XPostUrl.TryNormalizeWithStatusId(xPostUrl, out var normalizedPost))
        {
            return new AttachXPostSourceResult(AttachXPostSourceDisposition.InvalidXPostUrl);
        }

        var nowUtc = timeProvider.GetUtcNow().ToUniversalTime();
        if (!XPostSnowflakeTimestamp.TryDecode(normalizedPost.StatusId, nowUtc, out var publishedAtUtc))
        {
            return new AttachXPostSourceResult(AttachXPostSourceDisposition.InvalidStatusTimestamp);
        }

        var sourceItem = SourceItem.Create(
            CreateXPostSourceId(normalizedPost.NormalizedUrl),
            SourceType.XPost,
            normalizedPost.StatusId,
            null,
            $"X post {normalizedPost.StatusId}",
            new Uri(normalizedPost.NormalizedUrl, UriKind.Absolute),
            null,
            null,
            publishedAtUtc,
            nowUtc,
            null,
            nowUtc);

        await sourceAttachmentGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var persisted = await store.AttachSourceToEventAsync(parsedEventId, sourceItem, cancellationToken).ConfigureAwait(false);
            return persisted.Disposition switch
            {
                SourceAttachmentStoreDisposition.CreatedAndAttached => Success(AttachXPostSourceDisposition.CreatedAndAttached, persisted),
                SourceAttachmentStoreDisposition.ExistingSourceAttached => Success(AttachXPostSourceDisposition.ExistingSourceAttached, persisted),
                SourceAttachmentStoreDisposition.AlreadyAttached => Success(AttachXPostSourceDisposition.AlreadyAttached, persisted),
                _ => new AttachXPostSourceResult(AttachXPostSourceDisposition.EventNotFound),
            };
        }
        finally
        {
            sourceAttachmentGate.Release();
        }
    }

    private static AttachXPostSourceResult Success(AttachXPostSourceDisposition disposition, SourceAttachmentStoreResult persisted)
    {
        return new AttachXPostSourceResult(disposition, EventDtoMapper.ToDto(persisted.Source!));
    }

    private static SourceItemId CreateXPostSourceId(string normalizedUrl)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"XPost\n{normalizedUrl}"));
        return new SourceItemId($"src-x-{Convert.ToHexString(hash)}");
    }

    public static bool TryParseEventId(string? value, out EventId eventId)
    {
        eventId = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.')
            {
                return false;
            }
        }

        eventId = new EventId(value);
        return true;
    }
}
