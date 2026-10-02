using GeopoliticalMonitor.Application.Ucdp;
using static GeopoliticalMonitor.Application.Tests.UcdpTestData;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class UcdpRefreshServiceTests
{
    private static readonly DateTimeOffset October2 = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FirstImportProbesDownwardThroughA404AndImportsNewestPlusPrevious()
    {
        var feed = new FakeFeed();
        feed.Publish(new UcdpRelease(2026, 8), Record(id: 8, dateStart: "2026-08-10"));
        feed.Publish(new UcdpRelease(2026, 7), Record(id: 7, dateStart: "2026-07-10"));
        var store = new FakeStore();
        var service = new UcdpRefreshService(feed, store, new ManualTimeProvider(October2));

        var result = await service.RefreshAsync();

        Assert.Equal(UcdpRefreshStatus.Succeeded, result.Status);
        Assert.Equal([202610, 202609, 202608, 202607], feed.Requests);
        Assert.Equal([202607, 202608], store.PersistedReleases);
        Assert.Equal(2, result.ReleasesImported);
        Assert.Equal(2, result.NewEvents);
        Assert.Equal(202608, result.NewestRelease);
        Assert.Equal(new UcdpRelease(2026, 8), service.NewestImportedRelease);
    }

    [Fact]
    public async Task FirstImportGivesUpAfterFourProbes()
    {
        var feed = new FakeFeed();
        var service = new UcdpRefreshService(feed, new FakeStore(), new ManualTimeProvider(October2));

        var result = await service.RefreshAsync();

        Assert.Equal(UcdpRefreshStatus.NoNewRelease, result.Status);
        Assert.Equal([202610, 202609, 202608, 202607], feed.Requests);
    }

    [Fact]
    public async Task LaterChecksProbeOnlyTheReleaseAfterTheNewestStoredOne()
    {
        var feed = new FakeFeed();
        var store = new FakeStore { StoredNewest = 202608 };
        var service = new UcdpRefreshService(feed, store, new ManualTimeProvider(October2));

        var none = await service.RefreshAsync();
        feed.Publish(new UcdpRelease(2026, 9), Record(id: 9, dateStart: "2026-09-04"));
        var next = await service.RefreshAsync();

        Assert.Equal(UcdpRefreshStatus.NoNewRelease, none.Status);
        Assert.Equal(UcdpRefreshStatus.Succeeded, next.Status);
        Assert.Equal([202609, 202609], feed.Requests);
        Assert.Equal([202609], store.PersistedReleases);
    }

    [Fact]
    public async Task DecemberIsFollowedByJanuaryOfTheNextYear()
    {
        var feed = new FakeFeed();
        feed.Publish(new UcdpRelease(2027, 1), Record(id: 1, dateStart: "2027-01-05"));
        var store = new FakeStore { StoredNewest = 202612 };
        var service = new UcdpRefreshService(feed, store, new ManualTimeProvider(new DateTimeOffset(2027, 2, 22, 0, 0, 0, TimeSpan.Zero)));

        var result = await service.RefreshAsync();

        Assert.Equal(UcdpRefreshStatus.Succeeded, result.Status);
        Assert.Equal([202701], feed.Requests);
        Assert.Equal("GEDEvent_v27_0_1.csv", feed.RequestedFiles.Single());
    }

    [Fact]
    public async Task AReleaseWithZeroQualifyingRowsIsNotDownloadedAgainThisSession()
    {
        var feed = new FakeFeed();
        feed.Publish(new UcdpRelease(2026, 9), Record(id: 1, best: 0, dateStart: "2026-09-02"));
        var store = new FakeStore();
        var service = new UcdpRefreshService(feed, store, new ManualTimeProvider(October2));

        var first = await service.RefreshAsync();
        feed.Requests.Clear();
        var second = await service.RefreshAsync();

        Assert.Equal(UcdpRefreshStatus.Succeeded, first.Status);
        Assert.Equal(0, first.NewEvents);
        Assert.Equal(1, first.SkippedZeroFatalities);
        Assert.Null(store.StoredNewest);
        Assert.Equal(UcdpRefreshStatus.NoNewRelease, second.Status);
        Assert.Equal([202610], feed.Requests);
    }

    [Fact]
    public async Task AFutureReleaseIsNeverRequested()
    {
        var feed = new FakeFeed();
        var service = new UcdpRefreshService(feed, new FakeStore { StoredNewest = 202610 }, new ManualTimeProvider(October2));

        Assert.Equal(UcdpRefreshStatus.NoNewRelease, (await service.RefreshAsync()).Status);
        Assert.Empty(feed.Requests);
    }

    [Fact]
    public async Task AMissingOldReleaseFallsBackToTheNewestPublishedRelease()
    {
        var feed = new FakeFeed();
        feed.Publish(new UcdpRelease(2026, 8), Record(id: 8, dateStart: "2026-08-10"));
        var store = new FakeStore { StoredNewest = 202605 };
        var service = new UcdpRefreshService(feed, store, new ManualTimeProvider(October2));

        var result = await service.RefreshAsync();

        Assert.Equal(UcdpRefreshStatus.Succeeded, result.Status);
        Assert.Equal([202606, 202610, 202609, 202608], feed.Requests);
        Assert.Equal([202608], store.PersistedReleases);
    }

    [Theory]
    [InlineData(UcdpFeedFailureKind.ProviderUnavailable, UcdpRefreshStatus.ProviderUnavailable)]
    [InlineData(UcdpFeedFailureKind.TimedOut, UcdpRefreshStatus.TimedOut)]
    [InlineData(UcdpFeedFailureKind.InvalidResponse, UcdpRefreshStatus.InvalidResponse)]
    public async Task FeedFailuresMapToTypedStatusesAndPersistNothing(UcdpFeedFailureKind kind, UcdpRefreshStatus expected)
    {
        var feed = new FakeFeed();
        feed.Publish(new UcdpRelease(2026, 9), Record(id: 9, dateStart: "2026-09-01"));
        feed.Fail(new UcdpRelease(2026, 8), kind);
        var store = new FakeStore();
        var service = new UcdpRefreshService(feed, store, new ManualTimeProvider(October2));

        var result = await service.RefreshAsync();

        Assert.Equal(expected, result.Status);
        Assert.Empty(store.PersistedReleases);
    }

    [Fact]
    public async Task PersistenceFailureIsReported()
    {
        var feed = new FakeFeed();
        feed.Publish(new UcdpRelease(2026, 9), Record(id: 9, dateStart: "2026-09-01"));
        var service = new UcdpRefreshService(feed, new FakeStore { FailPersist = true, StoredNewest = 202608 }, new ManualTimeProvider(October2));

        Assert.Equal(UcdpRefreshStatus.PersistenceFailure, (await service.RefreshAsync()).Status);
    }

    [Fact]
    public async Task OnlyOneRefreshRunsAtATime()
    {
        var feed = new FakeFeed { Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = new UcdpRefreshService(feed, new FakeStore { StoredNewest = 202608 }, new ManualTimeProvider(October2));

        var first = service.RefreshAsync();
        await feed.Started.Task;
        var second = await service.RefreshAsync();
        feed.Block.SetResult();

        Assert.Equal(UcdpRefreshStatus.AlreadyInProgress, second.Status);
        Assert.Equal(UcdpRefreshStatus.NoNewRelease, (await first).Status);
    }

    [Fact]
    public async Task CancellationIsNotAFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new UcdpRefreshService(new FakeFeed(), new FakeStore(), new ManualTimeProvider(October2));

        Assert.Equal(UcdpRefreshStatus.Cancelled, (await service.RefreshAsync(cancellation.Token)).Status);
    }

    [Fact]
    public async Task LoadStateExposesTheStoredNewestRelease()
    {
        var service = new UcdpRefreshService(new FakeFeed(), new FakeStore { StoredNewest = 202607 }, new ManualTimeProvider(October2));

        await service.LoadStateAsync();

        Assert.Equal(new UcdpRelease(2026, 7), service.NewestImportedRelease);
    }

    internal sealed class FakeFeed : IUcdpCandidateFeed
    {
        private readonly Dictionary<int, UcdpCandidateRecord[]> published = [];
        private readonly Dictionary<int, UcdpFeedFailureKind> failures = [];

        public List<int> Requests { get; } = [];
        public List<string> RequestedFiles { get; } = [];
        public TaskCompletionSource? Block { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Publish(UcdpRelease release, params UcdpCandidateRecord[] records) => published[release.Ordinal] = records;

        public void Fail(UcdpRelease release, UcdpFeedFailureKind kind) => failures[release.Ordinal] = kind;

        public async Task<UcdpReleaseFetchResult> FetchReleaseAsync(UcdpRelease release, CancellationToken cancellationToken = default)
        {
            Requests.Add(release.Ordinal);
            RequestedFiles.Add(release.FileName);
            Started.TrySetResult();
            if (Block is not null)
            {
                await Block.Task.WaitAsync(cancellationToken);
            }

            if (failures.TryGetValue(release.Ordinal, out var kind))
            {
                throw new UcdpFeedException(kind, "failure");
            }

            return published.TryGetValue(release.Ordinal, out var records)
                ? UcdpReleaseFetchResult.Published(release, new UcdpCandidateFeedBatch(records, 0))
                : UcdpReleaseFetchResult.NotPublished(release);
        }
    }

    internal sealed class FakeStore : IUcdpCandidateStore
    {
        public int? StoredNewest { get; set; }
        public bool FailPersist { get; set; }
        public List<int> PersistedReleases { get; } = [];

        public Task<int?> GetNewestUcdpReleaseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(StoredNewest);
        }

        public Task<UcdpReleasePersistenceResult> PersistUcdpCandidateReleaseAsync(
            IReadOnlyList<UcdpCandidateImport> imports,
            int releaseOrdinal,
            CancellationToken cancellationToken = default)
        {
            if (FailPersist)
            {
                throw new InvalidOperationException("disk full");
            }

            PersistedReleases.Add(releaseOrdinal);
            if (imports.Count > 0)
            {
                StoredNewest = Math.Max(StoredNewest ?? 0, releaseOrdinal);
            }

            return Task.FromResult(new UcdpReleasePersistenceResult(imports.Count, 0, imports.Count, 0, 0, 0));
        }
    }
}
