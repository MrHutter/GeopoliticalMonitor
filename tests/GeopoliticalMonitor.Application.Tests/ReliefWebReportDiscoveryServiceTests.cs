using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class ReliefWebReportDiscoveryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Event ExistingEvent = Event.Create(
        new EventId("evt-demo"), "Flood response", "Summary", EventCategory.Humanitarian,
        EventStatus.Active, Now.AddDays(-2), Now, new Location(1, 2, "Example", "EXA", null), Now, Now,
        new EventSeverityAssessment(EventSeverity.Low, EventSeverityAssessment.ManualRule, "test"), new EventProvenance(EventProvider.Manual));

    [Theory]
    [InlineData(null, "flood", "2026-09-01T00:00:00Z", "2026-09-02T00:00:00Z", null, ReliefWebSearchStatus.InvalidEventId)]
    [InlineData("evt-demo", " ", "2026-09-01T00:00:00Z", "2026-09-02T00:00:00Z", null, ReliefWebSearchStatus.InvalidQuery)]
    [InlineData("evt-demo", "flood", "bad", "2026-09-02T00:00:00Z", null, ReliefWebSearchStatus.InvalidDateRange)]
    [InlineData("evt-demo", "flood", "2026-09-03T00:00:00Z", "2026-09-02T00:00:00Z", null, ReliefWebSearchStatus.InvalidDateRange)]
    [InlineData("evt-demo", "flood", "2025-01-01T00:00:00Z", "2026-09-02T00:00:00Z", null, ReliefWebSearchStatus.InvalidDateRange)]
    [InlineData("evt-demo", "flood", "2026-09-01T00:00:00Z", "2026-09-02T00:00:00Z", "XX", ReliefWebSearchStatus.InvalidCountry)]
    public async Task RejectsInvalidSearchInputWithoutCallingProvider(
        string? eventId,
        string query,
        string from,
        string to,
        string? iso3,
        ReliefWebSearchStatus expected)
    {
        var client = new RecordingClient(CreateReport());
        var result = await CreateService(client).SearchAsync(new(eventId, query, from, to, iso3));

        Assert.Equal(expected, result.Status);
        Assert.Equal(0, client.SearchCalls);
    }

    [Fact]
    public async Task ValidatesEventAndPassesTrimmedBoundedCriteriaToProvider()
    {
        var client = new RecordingClient(CreateReport());
        var result = await CreateService(client).SearchAsync(new(
            "evt-demo", "  flood response  ", "2026-09-01T00:00:00Z", "2026-09-22T00:00:00Z", "exa"));

        Assert.True(result.IsSuccess);
        Assert.Equal("flood response", client.Criteria?.QueryText);
        Assert.Equal("EXA", client.Criteria?.CountryIso3);
        Assert.Equal("5187658", Assert.Single(result.Candidates!).ReportId);
    }

    [Fact]
    public async Task SearchRejectsMissingEventAndOverlongQueryBeforeProviderCall()
    {
        var missingClient = new RecordingClient(CreateReport());
        var missing = await new ReliefWebReportDiscoveryService(
            missingClient, new FakeEventStore(null), new RecordingSourceStore()).SearchAsync(new(
                "evt-demo", "flood", "2026-09-01T00:00:00Z", "2026-09-02T00:00:00Z", null));
        Assert.Equal(ReliefWebSearchStatus.EventNotFound, missing.Status);
        Assert.Equal(0, missingClient.SearchCalls);

        var longClient = new RecordingClient(CreateReport());
        var overlong = await CreateService(longClient).SearchAsync(new(
            "evt-demo", new string('x', 201), "2026-09-01T00:00:00Z", "2026-09-02T00:00:00Z", null));
        Assert.Equal(ReliefWebSearchStatus.InvalidQuery, overlong.Status);
        Assert.Equal(0, longClient.SearchCalls);
    }

    [Fact]
    public async Task MapsAuthoritativeReportToMetadataOnlyOtherSource()
    {
        var client = new RecordingClient(CreateReport());
        var store = new RecordingSourceStore();
        var service = CreateService(client, store);

        var result = await service.AttachAsync("evt-demo", "5187658");

        Assert.Equal(ReliefWebAttachStatus.CreatedAndAttached, result.Status);
        Assert.Equal(5187658, client.RetrievedReportId);
        var source = Assert.IsType<SourceItem>(store.Source);
        Assert.Equal("src-reliefweb-report-5187658", source.Id.Value);
        Assert.Equal("ReliefWeb:Report:5187658", source.ExternalId);
        Assert.Equal(SourceType.Other, source.SourceType);
        Assert.Equal("Flood response situation report", source.Title);
        Assert.Equal("https://reliefweb.int/report/example/flood", source.Url?.AbsoluteUri);
        Assert.Equal("EHO; Example Response Agency", source.AuthorDisplayName);
        Assert.Null(source.AuthorHandle);
        Assert.Null(source.Location);
        Assert.Equal(Now.AddDays(-1), source.PublishedAtUtc);
        Assert.Equal(Now, source.RetrievedAtUtc);
        Assert.Equal(Now, source.CreatedAtUtc);
        Assert.Contains("Format: Situation Report", source.Text);
        Assert.DoesNotContain("body", source.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingEventAndInvalidProviderDateCannotPersist()
    {
        var missingClient = new RecordingClient(CreateReport());
        var missingStore = new RecordingSourceStore();
        var missing = await new ReliefWebReportDiscoveryService(
            missingClient, new FakeEventStore(null), missingStore).AttachAsync("evt-demo", "5187658");
        Assert.Equal(ReliefWebAttachStatus.EventNotFound, missing.Status);
        Assert.Equal(0, missingClient.ItemCalls);
        Assert.Null(missingStore.Source);

        var invalidReport = CreateReport() with { OriginalPublishedAtUtc = null };
        var invalidStore = new RecordingSourceStore();
        var invalid = await CreateService(new RecordingClient(invalidReport), invalidStore).AttachAsync("evt-demo", "5187658");
        Assert.Equal(ReliefWebAttachStatus.InvalidResponse, invalid.Status);
        Assert.Null(invalidStore.Source);
    }

    private static ReliefWebReportDiscoveryService CreateService(
        RecordingClient client,
        RecordingSourceStore? store = null)
        => new(client, new FakeEventStore(ExistingEvent), store ?? new RecordingSourceStore(), new FixedTimeProvider(Now));

    private static ReliefWebReportRecord CreateReport() => new(
        5187658,
        "Flood response situation report",
        new Uri("https://reliefweb.int/report/example/flood"),
        new Uri("https://example.org/report"),
        Now.AddDays(-1),
        Now.AddHours(-20),
        Now.AddHours(-10),
        [new(1, "EHO"), new(2, "Example Response Agency")],
        ["Situation Report"],
        [new(3, "Example Country", "EXA")],
        [new(4, "Example Flood", "FL-2026-EXA", ["Flood"])],
        ["English"]);

    private sealed class RecordingClient(ReliefWebReportRecord report) : IReliefWebReportClient
    {
        public int SearchCalls { get; private set; }
        public int ItemCalls { get; private set; }
        public long? RetrievedReportId { get; private set; }
        public ReliefWebSearchCriteria? Criteria { get; private set; }

        public Task<ReliefWebReportBatch> SearchReportsAsync(ReliefWebSearchCriteria criteria, CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            Criteria = criteria;
            return Task.FromResult(new ReliefWebReportBatch([report], 0));
        }

        public Task<ReliefWebReportRecord> GetReportAsync(long reportId, CancellationToken cancellationToken = default)
        {
            ItemCalls++;
            RetrievedReportId = reportId;
            return Task.FromResult(report);
        }
    }

    private sealed class RecordingSourceStore : IReliefWebSourceStore
    {
        public SourceItem? Source { get; private set; }

        public Task<ReliefWebAttachmentStoreResult> AttachReliefWebSourceAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default)
        {
            Source = sourceItem;
            return Task.FromResult(new ReliefWebAttachmentStoreResult(ReliefWebAttachmentStoreDisposition.CreatedAndAttached, sourceItem));
        }
    }

    private sealed class FakeEventStore(Event? existingEvent) : IEventStore
    {
        public Task<Event?> GetEventByIdAsync(EventId eventId, CancellationToken cancellationToken = default) => Task.FromResult(existingEvent);
        public Task<IReadOnlyList<Event>> GetEventsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceItem>> GetSourcesForEventAsync(EventId eventId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<EventId, int>> GetSourceCountsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateEventAsync(Event geopoliticalEvent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CreateSourceItemAsync(SourceItem sourceItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AttachSourceToEventAsync(EventId eventId, SourceItemId sourceItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<SourceAttachmentStoreResult> AttachSourceToEventAsync(EventId eventId, SourceItem sourceItem, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
