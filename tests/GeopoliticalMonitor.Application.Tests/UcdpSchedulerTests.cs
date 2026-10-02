using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.Refresh;
using GeopoliticalMonitor.Application.Ucdp;
using static GeopoliticalMonitor.Application.Tests.UcdpRefreshServiceTests;
using static GeopoliticalMonitor.Application.Tests.UcdpTestData;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class UcdpSchedulerTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderUsesTheApprovedIntervalAndFirstRunDelay()
    {
        var provider = new UcdpScheduledProvider(new UcdpRefreshService(new FakeFeed(), new FakeStore()));

        Assert.Equal(ScheduledProviderId.UcdpCandidate, provider.Id);
        Assert.Equal("UCDP Candidate", provider.DisplayName);
        Assert.Equal(TimeSpan.FromHours(6), provider.Interval);
        Assert.Equal(TimeSpan.FromMinutes(2), ((IScheduledProvider)provider).FirstRunDelay);
    }

    [Fact]
    public async Task UcdpFirstRunsTwoMinutesAfterStartWhileGdacsKeepsSixtySeconds()
    {
        var clock = new ManualTimeProvider(Start);
        var feed = new FakeFeed();
        var ucdp = new UcdpScheduledProvider(new UcdpRefreshService(feed, new FakeStore { StoredNewest = 202608 }, clock));
        var gdacs = new CountingProvider(ScheduledProviderId.Gdacs);
        var scheduler = new ProviderRefreshScheduler([gdacs, ucdp], clock, () => 0.5);
        using var lifetime = new CancellationTokenSource();
        scheduler.Start(lifetime.Token);

        var statuses = scheduler.GetStatuses();
        Assert.Equal(Start.AddSeconds(60), statuses.Single(item => item.Provider == ScheduledProviderId.Gdacs).NextRefreshAtUtc);
        Assert.Equal(Start.AddMinutes(2), statuses.Single(item => item.Provider == ScheduledProviderId.UcdpCandidate).NextRefreshAtUtc);

        clock.Advance(TimeSpan.FromSeconds(90));
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, gdacs.Calls);
        Assert.Empty(feed.Requests);

        clock.Advance(TimeSpan.FromSeconds(30));
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal([202609], feed.Requests);
        scheduler.Stop(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task NoNewReleaseIsASuccessWithAFixedMessageAndTheNextCheckInSixHours()
    {
        var clock = new ManualTimeProvider(Start);
        var ucdp = new UcdpScheduledProvider(new UcdpRefreshService(new FakeFeed(), new FakeStore { StoredNewest = 202608 }, clock));
        var scheduler = new ProviderRefreshScheduler([ucdp], clock, () => 0.5);
        using var lifetime = new CancellationTokenSource();
        scheduler.Start(lifetime.Token);

        clock.Advance(TimeSpan.FromMinutes(2));
        await scheduler.TickAsync(CancellationToken.None);

        var status = Assert.Single(scheduler.GetStatuses());
        Assert.Equal(ProviderRefreshState.Idle, status.State);
        Assert.Equal(clock.GetUtcNow(), status.LastSuccessAtUtc);
        Assert.Equal(clock.GetUtcNow().AddHours(6), status.NextRefreshAtUtc);
        Assert.Equal(ProviderStatusMessage.NoNewRelease, status.Message);
        Assert.Equal("No new release", ProviderStatusMessages.ToText(status.Message));
        Assert.Equal("monthly release", status.CadenceLabel);
        Assert.Equal("2026-08", status.DataThrough);
        scheduler.Stop(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task FailuresBackOffAndKeepTheProviderScheduled()
    {
        var clock = new ManualTimeProvider(Start);
        var feed = new FakeFeed();
        feed.Fail(new UcdpRelease(2026, 9), UcdpFeedFailureKind.ProviderUnavailable);
        var ucdp = new UcdpScheduledProvider(new UcdpRefreshService(feed, new FakeStore { StoredNewest = 202608 }, clock));
        var scheduler = new ProviderRefreshScheduler([ucdp], clock, () => 0.5);
        using var lifetime = new CancellationTokenSource();
        scheduler.Start(lifetime.Token);

        clock.Advance(TimeSpan.FromMinutes(2));
        await scheduler.TickAsync(CancellationToken.None);

        var status = Assert.Single(scheduler.GetStatuses());
        Assert.Equal(ProviderRefreshState.Failed, status.State);
        Assert.Equal(ProviderStatusMessage.ProviderUnavailable, status.Message);
        Assert.Null(status.LastSuccessAtUtc);
        Assert.Equal(clock.GetUtcNow().AddHours(6), status.NextRefreshAtUtc);
        scheduler.Stop(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ManualRefreshSharesTheGateWithTheTimer()
    {
        var clock = new ManualTimeProvider(Start);
        var feed = new FakeFeed { Block = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var service = new UcdpRefreshService(feed, new FakeStore { StoredNewest = 202608 }, clock);
        var scheduler = new ProviderRefreshScheduler([new UcdpScheduledProvider(service)], clock, () => 0.5);
        using var lifetime = new CancellationTokenSource();
        scheduler.Start(lifetime.Token);

        clock.Advance(TimeSpan.FromMinutes(2));
        var timer = scheduler.TickAsync(CancellationToken.None);
        await feed.Started.Task;
        var manual = await scheduler.RefreshManuallyAsync(ScheduledProviderId.UcdpCandidate, service.RefreshAsync, UcdpScheduledProvider.Classify, CancellationToken.None);
        feed.Block.SetResult();
        await timer;

        Assert.Equal(UcdpRefreshStatus.AlreadyInProgress, manual.Status);
        Assert.Single(feed.Requests);
        scheduler.Stop(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DataOlderThanFortyFiveDaysAfterExtractionIsStale()
    {
        var store = new FakeStore { StoredNewest = 202608 };
        var service = new UcdpRefreshService(new FakeFeed(), store);
        await service.LoadStateAsync();
        var provider = new UcdpScheduledProvider(service);
        var extracted = new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);

        Assert.False(provider.GetStatusDetail(extracted.AddDays(45)).DataStale);
        Assert.True(provider.GetStatusDetail(extracted.AddDays(45).AddMinutes(1)).DataStale);
        Assert.Equal("2026-08", provider.GetStatusDetail(extracted).DataThrough);
    }

    [Fact]
    public void NoDataYetMeansNoMonthAndNotDataStale()
    {
        var provider = new UcdpScheduledProvider(new UcdpRefreshService(new FakeFeed(), new FakeStore()));

        var detail = provider.GetStatusDetail(Start);
        Assert.Equal("monthly release", detail.CadenceLabel);
        Assert.Null(detail.DataThrough);
        Assert.False(detail.DataStale);
    }

    [Theory]
    [InlineData(UcdpRefreshStatus.Succeeded, ProviderRefreshOutcomeKind.Succeeded, ProviderStatusMessage.UpToDate)]
    [InlineData(UcdpRefreshStatus.NoNewRelease, ProviderRefreshOutcomeKind.Succeeded, ProviderStatusMessage.NoNewRelease)]
    [InlineData(UcdpRefreshStatus.AlreadyInProgress, ProviderRefreshOutcomeKind.AlreadyInProgress, ProviderStatusMessage.None)]
    [InlineData(UcdpRefreshStatus.Cancelled, ProviderRefreshOutcomeKind.Cancelled, ProviderStatusMessage.None)]
    [InlineData(UcdpRefreshStatus.TimedOut, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.TimedOut)]
    [InlineData(UcdpRefreshStatus.InvalidResponse, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.InvalidResponse)]
    [InlineData(UcdpRefreshStatus.PersistenceFailure, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.PersistenceFailure)]
    public void ClassifiesEveryRefreshStatus(UcdpRefreshStatus status, ProviderRefreshOutcomeKind kind, ProviderStatusMessage message)
    {
        var outcome = UcdpScheduledProvider.Classify(new UcdpRefreshResult(status));

        Assert.Equal(kind, outcome.Kind);
        Assert.Equal(message, outcome.Message);
        Assert.Equal(0, outcome.NewAlerts);
    }

    [Fact]
    public void GdacsKeepsItsSixtySecondFirstRun()
    {
        IScheduledProvider gdacs = new GdacsScheduledProvider(null!);
        Assert.Equal(TimeSpan.FromSeconds(60), gdacs.FirstRunDelay);
        Assert.Equal(ProviderStatusDetail.None, gdacs.GetStatusDetail(Start));
    }

    private sealed class CountingProvider(ScheduledProviderId id) : IScheduledProvider
    {
        private int calls;

        public ScheduledProviderId Id => id;
        public string DisplayName => id.ToString();
        public TimeSpan Interval => TimeSpan.FromMinutes(30);
        public int Calls => Volatile.Read(ref calls);

        public Task<ProviderRefreshOutcome> RefreshAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(ProviderRefreshOutcome.Succeeded(0));
        }
    }
}
