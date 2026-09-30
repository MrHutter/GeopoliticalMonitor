using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Tests;

public class EventCatalogServiceTests
{
    private const string ValidPostUrl = "https://x.com/example/status/1759288472993702104";
    private static readonly DateTimeOffset AttachmentTime = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReturnsOnlySourcesAttachedToRequestedEvent()
    {
        var firstEvent = CreateEvent("evt-one");
        var secondEvent = CreateEvent("evt-two");
        var xPost = CreateSource("src-x", SourceType.XPost);
        var article = CreateSource("src-news", SourceType.NewsArticle);
        var store = new InMemoryEventStore([firstEvent, secondEvent], [xPost, article]);
        await store.AttachSourceToEventAsync(firstEvent.Id, xPost.Id);
        await store.AttachSourceToEventAsync(secondEvent.Id, article.Id);
        var service = new EventCatalogService(store);

        var sources = await service.GetSourcesForEventAsync("evt-one");

        var source = Assert.Single(sources);
        Assert.Equal("src-x", source.Id);
        Assert.Equal("XPost", source.SourceType);
    }

    [Fact]
    public async Task DuplicateAttachDoesNotCreateDuplicateAssociation()
    {
        var geopoliticalEvent = CreateEvent("evt-one");
        var source = CreateSource("src-x", SourceType.XPost);
        var store = new InMemoryEventStore([geopoliticalEvent], [source]);
        var service = new EventCatalogService(store);

        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);
        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);

        var sources = await service.GetSourcesForEventAsync("evt-one");
        Assert.Single(sources);
    }

    [Fact]
    public async Task NewXPostCreatesOneSourceAndAssociationWithDerivedMetadata()
    {
        var store = new InMemoryEventStore([CreateEvent("evt-one")], []);
        var service = new EventCatalogService(store, new FixedTimeProvider(AttachmentTime));

        var result = await service.AttachXPostSourceToEventAsync("evt-one", ValidPostUrl);

        Assert.Equal(AttachXPostSourceDisposition.CreatedAndAttached, result.Disposition);
        var source = Assert.Single(store.Sources);
        Assert.Equal(SourceType.XPost, source.SourceType);
        Assert.Equal("1759288472993702104", source.ExternalId);
        Assert.Equal("X post 1759288472993702104", source.Text);
        Assert.Equal(ValidPostUrl, source.Url?.AbsoluteUri);
        Assert.Equal(new DateTimeOffset(2024, 2, 18, 18, 47, 12, 617, TimeSpan.Zero), source.PublishedAtUtc);
        Assert.Equal(AttachmentTime, source.RetrievedAtUtc);
        Assert.Equal(AttachmentTime, source.CreatedAtUtc);
        Assert.Single(store.Associations);
    }

    [Fact]
    public async Task CanonicalEquivalentUrlIsAlreadyAttachedWithoutDuplicates()
    {
        var store = new InMemoryEventStore([CreateEvent("evt-one")], []);
        var service = new EventCatalogService(store, new FixedTimeProvider(AttachmentTime));
        await service.AttachXPostSourceToEventAsync("evt-one", ValidPostUrl);

        var result = await service.AttachXPostSourceToEventAsync(
            "evt-one",
            "https://www.x.com/example/status/1759288472993702104?ref=share#fragment");

        Assert.Equal(AttachXPostSourceDisposition.AlreadyAttached, result.Disposition);
        Assert.Single(store.Sources);
        Assert.Single(store.Associations);
    }

    [Fact]
    public async Task ExistingXPostIsReusedForAnotherEvent()
    {
        var store = new InMemoryEventStore([CreateEvent("evt-one"), CreateEvent("evt-two")], []);
        var service = new EventCatalogService(store, new FixedTimeProvider(AttachmentTime));
        await service.AttachXPostSourceToEventAsync("evt-one", ValidPostUrl);

        var result = await service.AttachXPostSourceToEventAsync("evt-two", ValidPostUrl);

        Assert.Equal(AttachXPostSourceDisposition.ExistingSourceAttached, result.Disposition);
        Assert.Single(store.Sources);
        Assert.Equal(2, store.Associations.Count);
    }

    [Theory]
    [InlineData("missing", ValidPostUrl, AttachXPostSourceDisposition.EventNotFound)]
    [InlineData("evt one", ValidPostUrl, AttachXPostSourceDisposition.InvalidEventId)]
    [InlineData("evt-one", "https://x.com/home", AttachXPostSourceDisposition.InvalidXPostUrl)]
    [InlineData("evt-one", "https://x.com/example/status/18446744073709551615", AttachXPostSourceDisposition.InvalidStatusTimestamp)]
    public async Task InvalidAttachmentDoesNotPersistAnything(string eventId, string url, AttachXPostSourceDisposition expected)
    {
        var store = new InMemoryEventStore([CreateEvent("evt-one")], []);
        var service = new EventCatalogService(store, new FixedTimeProvider(AttachmentTime));

        var result = await service.AttachXPostSourceToEventAsync(eventId, url);

        Assert.Equal(expected, result.Disposition);
        Assert.Empty(store.Sources);
        Assert.Empty(store.Associations);
    }

    private static Event CreateEvent(string id)
    {
        var timestamp = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
        return Event.Create(new EventId(id), "Demo event", "Summary", EventCategory.Security, EventStatus.Active, timestamp, timestamp, new Location(1, 2, "Demo"), timestamp, timestamp, new EventSeverityAssessment(EventSeverity.High, EventSeverityAssessment.ManualRule, "test"), new EventProvenance(EventProvider.Manual));
    }

    private static SourceItem CreateSource(string id, SourceType sourceType)
    {
        var timestamp = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
        return SourceItem.Create(new SourceItemId(id), sourceType, id, "Demo source", "Source text", null, null, null, timestamp, timestamp, null, timestamp);
    }

    private sealed class InMemoryEventStore(IReadOnlyList<Event> events, IReadOnlyList<SourceItem> sources) : IEventStore
    {
        public List<SourceItem> Sources { get; } = [.. sources];
        public HashSet<(EventId EventId, SourceItemId SourceItemId)> Associations { get; } = [];

        public Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken = default) => Task.FromResult(events);
        public Task<Event?> GetEventByIdAsync(EventId eventId, CancellationToken cancellationToken = default) => Task.FromResult(events.SingleOrDefault(item => item.Id == eventId));
        public Task<IReadOnlyDictionary<EventId, int>> GetSourceCountsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<EventId, int>>(Associations.GroupBy(item => item.EventId).ToDictionary(item => item.Key, item => item.Count()));
        public Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default)
        {
            Associations.Add((eventId, sourceItemId));
            return Task.CompletedTask;
        }

        public Task<SourceAttachmentStoreResult> AttachSourceToEventAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default)
        {
            if (events.All(item => item.Id != eventId))
            {
                return Task.FromResult(new SourceAttachmentStoreResult(SourceAttachmentStoreDisposition.EventNotFound));
            }

            var existing = Sources.SingleOrDefault(item => item.SourceType == sourceItem.SourceType && item.Url == sourceItem.Url);
            var persisted = existing ?? sourceItem;
            if (existing is null)
            {
                Sources.Add(sourceItem);
            }

            if (!Associations.Add((eventId, persisted.Id)))
            {
                return Task.FromResult(new SourceAttachmentStoreResult(SourceAttachmentStoreDisposition.AlreadyAttached, persisted));
            }

            return Task.FromResult(new SourceAttachmentStoreResult(
                existing is null ? SourceAttachmentStoreDisposition.CreatedAndAttached : SourceAttachmentStoreDisposition.ExistingSourceAttached,
                persisted));
        }

        public Task<IReadOnlyList<SourceItem>> GetSourcesForEventAsync(EventId eventId, CancellationToken cancellationToken = default)
        {
            var sourceIds = Associations.Where(item => item.EventId == eventId).Select(item => item.SourceItemId).ToHashSet();
            return Task.FromResult<IReadOnlyList<SourceItem>>(Sources.Where(item => sourceIds.Contains(item.Id)).ToArray());
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
