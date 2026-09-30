using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class GdacsRefreshServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NormalizesProviderRecordIntoSeparateEventAndDatasetSource()
    {
        var feed = new RecordingFeed([CreateRecord()]);
        var store = new RecordingStore();
        var service = new GdacsRefreshService(feed, store, new FixedTimeProvider(Now));

        var result = await service.RefreshAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddDays(-4), feed.FromUtc);
        Assert.Equal(Now, feed.ToUtc);
        var import = Assert.Single(store.Imports!);
        Assert.Equal("evt-gdacs-eq-1500811", import.Event.Id.Value);
        Assert.Equal(EventCategory.NaturalDisaster, import.Event.Category);
        Assert.Equal(EventStatus.Unverified, import.Event.Status);
        Assert.Equal(CreateRecord().StartedAtUtc, import.Event.StartedAtUtc);
        Assert.Equal(CreateRecord().ProviderUpdatedAtUtc, import.Event.LastUpdatedAtUtc);
        Assert.Equal(53.1928, import.Event.Location.Latitude);
        Assert.Equal(160.5129, import.Event.Location.Longitude);
        Assert.Equal("src-gdacs-eq-1500811-1661062", import.Source.Id.Value);
        Assert.Equal(SourceType.DatasetRecord, import.Source.SourceType);
        Assert.Equal("GDACS:EQ:1500811:1661062", import.Source.ExternalId);
        Assert.Equal(CreateRecord().ProviderUpdatedAtUtc, import.Source.PublishedAtUtc);
        Assert.Equal(Now, import.Source.RetrievedAtUtc);
        Assert.Equal(GdacsRefreshService.Attribution, import.Source.AuthorDisplayName);
        Assert.Contains("GDACS alert level: Orange", import.Source.Text);
        Assert.Contains("Magnitude: 7.8 M", import.Source.Text);
        Assert.Equal(CreateRecord().CanonicalUrl, import.Source.Url);
    }

    [Fact]
    public async Task NormalizationAssignsGdacsSeverityAndProvenanceFromStructuredFields()
    {
        var store = new RecordingStore();
        var service = new GdacsRefreshService(new RecordingFeed([CreateRecord()]), store, new FixedTimeProvider(Now));

        await service.RefreshAsync();

        var import = Assert.Single(store.Imports!);
        Assert.Equal(EventSeverity.High, import.Event.Severity);
        Assert.Equal(GdacsSeverityRule.RuleId, import.Event.SeverityRule);
        Assert.Equal("alert=Orange; magnitude=7.8", import.Event.SeverityInputs);
        Assert.Equal(EventProvider.Gdacs, import.Event.Provider);
        Assert.Equal("1500811", import.Event.ProviderKey);

        Assert.True(GdacsSeverityRule.TryAssessFromProvenanceText(import.Source.Text, out var fromText));
        Assert.Equal(import.Event.SeverityAssessment, fromText);
    }

    [Fact]
    public async Task ReportsMalformedCountAndMapsFeedFailureWithoutPersisting()
    {
        var store = new RecordingStore();
        var malformedService = new GdacsRefreshService(new RecordingFeed([CreateRecord()], skipped: 2), store, new FixedTimeProvider(Now));
        var success = await malformedService.RefreshAsync();
        Assert.Equal(2, success.SkippedMalformedRecords);

        store.Imports = null;
        var failure = await new GdacsRefreshService(new FailingFeed(), store, new FixedTimeProvider(Now)).RefreshAsync();
        Assert.Equal(GdacsRefreshStatus.ProviderUnavailable, failure.Status);
        Assert.Null(store.Imports);
    }

    [Fact]
    public async Task AllowsOnlyOneRefreshAtATime()
    {
        var feed = new BlockingFeed();
        var service = new GdacsRefreshService(feed, new RecordingStore(), new FixedTimeProvider(Now));

        var first = service.RefreshAsync();
        await feed.Started.Task;
        var second = await service.RefreshAsync();
        Assert.Equal(GdacsRefreshStatus.AlreadyInProgress, second.Status);

        feed.Release.SetResult();
        Assert.True((await first).IsSuccess);
    }

    private static GdacsEarthquakeRecord CreateRecord() => new(
        1500811,
        1661062,
        "Earthquake in Russia",
        "Earthquake in Russia",
        DateTimeOffset.Parse("2025-09-18T18:58:12Z"),
        DateTimeOffset.Parse("2025-09-19T20:01:59Z"),
        "Orange",
        7.8,
        "Russia",
        "RUS",
        new Uri("https://www.gdacs.org/report.aspx?eventid=1500811&episodeid=1661062&eventtype=EQ"),
        160.5129,
        53.1928);

    private sealed class RecordingFeed(IReadOnlyList<GdacsEarthquakeRecord> records, int skipped = 0) : IGdacsEventFeed
    {
        public DateTimeOffset FromUtc { get; private set; }
        public DateTimeOffset ToUtc { get; private set; }

        public Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
        {
            FromUtc = fromUtc;
            ToUtc = toUtc;
            return Task.FromResult(new GdacsEarthquakeFeedBatch(records, skipped));
        }
    }

    private sealed class FailingFeed : IGdacsEventFeed
    {
        public Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => throw new GdacsFeedException(GdacsFeedFailureKind.ProviderUnavailable, "offline");
    }

    private sealed class BlockingFeed : IGdacsEventFeed
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new GdacsEarthquakeFeedBatch([], 0);
        }
    }

    private sealed class RecordingStore : IGdacsEarthquakeStore
    {
        public IReadOnlyList<GdacsEarthquakeImport>? Imports { get; set; }

        public Task<GdacsEarthquakePersistenceResult> PersistGdacsEarthquakesAsync(
            IReadOnlyList<GdacsEarthquakeImport> imports,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Imports = imports;
            Assert.Equal(Now, observedAtUtc);
            return Task.FromResult(new GdacsEarthquakePersistenceResult(imports.Count, 0, imports.Count, 0, 0, 0, [], 0));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
