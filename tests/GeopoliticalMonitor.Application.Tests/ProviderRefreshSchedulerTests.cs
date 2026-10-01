using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.Refresh;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class ProviderRefreshSchedulerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    [Fact]
    public async Task FirstAutomaticRefreshIsDueSixtySecondsAfterStart()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider();
        var scheduler = CreateScheduler(clock, provider);

        scheduler.Start(CancellationToken.None);
        Assert.Equal(Start.AddSeconds(60), Status(scheduler).NextRefreshAtUtc);

        clock.Advance(TimeSpan.FromSeconds(30));
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(0, provider.Calls);

        clock.Advance(TimeSpan.FromSeconds(30));
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(Start.AddSeconds(60) + Interval, Status(scheduler).NextRefreshAtUtc);
        Assert.Equal(Start.AddSeconds(60), Status(scheduler).LastSuccessAtUtc);

        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public void RepeatedStartDoesNotRestartOrDuplicateTheSchedule()
    {
        var clock = new ManualTimeProvider(Start);
        var scheduler = CreateScheduler(clock, new ScriptedProvider());

        scheduler.Start(CancellationToken.None);
        var loop = scheduler.LoopTask;
        clock.Advance(TimeSpan.FromSeconds(45));
        scheduler.Start(CancellationToken.None);

        Assert.True(scheduler.IsStarted);
        Assert.Same(loop, scheduler.LoopTask);
        Assert.Equal(Start.AddSeconds(60), Status(scheduler).NextRefreshAtUtc);
        scheduler.Stop(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TickLoopRunsEveryThirtySecondsOnTheTimeProvider()
    {
        var clock = new ManualTimeProvider(Start) { FireTimers = true };
        var provider = new ScriptedProvider();
        var scheduler = CreateScheduler(clock, provider);

        scheduler.Start(CancellationToken.None);
        await WaitUntilAsync(() => clock.TimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(30));
        await Task.Delay(50);
        Assert.Equal(0, provider.Calls);

        clock.Advance(TimeSpan.FromSeconds(30));
        await WaitUntilAsync(() => provider.Calls == 1);

        scheduler.Stop(TimeSpan.FromSeconds(2));
        Assert.True(scheduler.LoopTask!.IsCompleted);
    }

    [Fact]
    public async Task ManualAndTimerShareOneInFlightGdacsRefreshAndBusyTickIsSkippedNotQueued()
    {
        var clock = new ManualTimeProvider(Start);
        var feed = new BlockingGdacsFeed();
        var service = new GdacsRefreshService(feed, new CountingGdacsStore(), clock);
        var scheduler = new ProviderRefreshScheduler([new GdacsScheduledProvider(service)], clock, () => 0.5);
        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        var manual = scheduler.RefreshManuallyAsync(ScheduledProviderId.Gdacs, service.RefreshAsync, GdacsScheduledProvider.Classify, CancellationToken.None);
        await feed.Started.Task;
        Assert.Equal(ProviderRefreshState.Refreshing, Status(scheduler).State);

        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, feed.Calls);

        feed.Release.SetResult();
        var result = await manual;
        Assert.Equal(GdacsRefreshStatus.Succeeded, result.Status);
        Assert.Equal(clock.GetUtcNow() + Interval, Status(scheduler).NextRefreshAtUtc);

        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, feed.Calls);
    }

    [Fact]
    public async Task ManualRefreshDuringTimerRefreshReturnsAlreadyInProgressAndLeavesScheduleUnchanged()
    {
        var clock = new ManualTimeProvider(Start);
        var feed = new BlockingGdacsFeed();
        var service = new GdacsRefreshService(feed, new CountingGdacsStore(), clock);
        var scheduler = new ProviderRefreshScheduler([new GdacsScheduledProvider(service)], clock, () => 0.5);
        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        var tick = scheduler.TickAsync(CancellationToken.None);
        await feed.Started.Task;
        var dueDuringRefresh = Status(scheduler).NextRefreshAtUtc;

        var manual = await scheduler.RefreshManuallyAsync(ScheduledProviderId.Gdacs, service.RefreshAsync, GdacsScheduledProvider.Classify, CancellationToken.None);

        Assert.Equal(GdacsRefreshStatus.AlreadyInProgress, manual.Status);
        Assert.Equal(ProviderRefreshState.Refreshing, Status(scheduler).State);
        Assert.Equal(dueDuringRefresh, Status(scheduler).NextRefreshAtUtc);
        Assert.Equal(1, feed.Calls);

        feed.Release.SetResult();
        await tick;
        Assert.Equal(ProviderRefreshState.Idle, Status(scheduler).State);
    }

    [Fact]
    public async Task SuccessfulManualRefreshResetsTheNextDueTime()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider();
        var scheduler = CreateScheduler(clock, provider);
        scheduler.Start(CancellationToken.None);

        clock.Advance(TimeSpan.FromSeconds(20));
        await scheduler.RefreshManuallyAsync(ScheduledProviderId.Gdacs, provider.RefreshAsync, outcome => outcome, CancellationToken.None);

        Assert.Equal(Start.AddSeconds(20) + Interval, Status(scheduler).NextRefreshAtUtc);
        clock.Advance(TimeSpan.FromSeconds(60));
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task BackoffFollowsTheApprovedSequenceAndResetsOnSuccess()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider { Default = ProviderRefreshOutcome.Failed(ProviderStatusMessage.ProviderUnavailable) };
        var scheduler = CreateScheduler(clock, provider);
        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        var delays = new List<double>();
        for (var failure = 1; failure <= 9; failure++)
        {
            await scheduler.TickAsync(CancellationToken.None);
            var status = Status(scheduler);
            Assert.Equal(ProviderRefreshState.Failed, status.State);
            Assert.Equal(ProviderStatusMessage.ProviderUnavailable, status.Message);
            delays.Add((status.NextRefreshAtUtc!.Value - clock.GetUtcNow()).TotalMinutes);
            clock.SetNow(status.NextRefreshAtUtc.Value);
        }

        Assert.Equal([30, 30, 30, 40, 80, 160, 320, 360, 360], delays);

        provider.Default = ProviderRefreshOutcome.Succeeded(0);
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(ProviderRefreshState.Idle, Status(scheduler).State);
        Assert.Equal(clock.GetUtcNow() + Interval, Status(scheduler).NextRefreshAtUtc);

        provider.Default = ProviderRefreshOutcome.Failed(ProviderStatusMessage.TimedOut);
        clock.SetNow(Status(scheduler).NextRefreshAtUtc!.Value);
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(30, (Status(scheduler).NextRefreshAtUtc!.Value - clock.GetUtcNow()).TotalMinutes);
        Assert.Equal(ProviderStatusMessage.TimedOut, Status(scheduler).Message);
    }

    [Theory]
    [InlineData(0.0, 1, 27)]
    [InlineData(0.9999999, 1, 33)]
    [InlineData(0.0, 5, 72)]
    [InlineData(1.0, 5, 88)]
    [InlineData(1.0, 8, 360)]
    [InlineData(0.0, 8, 324)]
    public void BackoffJitterStaysWithinTenPercentAndNeverExceedsTheCap(double random, int failures, double expectedMinutes)
    {
        var scheduler = new ProviderRefreshScheduler([new ScriptedProvider()], new ManualTimeProvider(Start), () => random);

        Assert.Equal(expectedMinutes, Math.Round(scheduler.CalculateBackoff(Interval, failures).TotalMinutes, 3), 2);
    }

    [Fact]
    public async Task CancelledResultIsNotAFailure()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider { Default = ProviderRefreshOutcome.Cancelled };
        var scheduler = CreateScheduler(clock, provider);
        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        for (var cancellation = 0; cancellation < 3; cancellation++)
        {
            await scheduler.TickAsync(CancellationToken.None);
        }

        var status = Status(scheduler);
        Assert.Equal(3, provider.Calls);
        Assert.Equal(ProviderRefreshState.Idle, status.State);
        Assert.Equal(ProviderStatusMessage.None, status.Message);
        Assert.Equal(Start.AddSeconds(60), status.NextRefreshAtUtc);

        provider.Default = ProviderRefreshOutcome.Failed(ProviderStatusMessage.ProviderUnavailable);
        var delays = new List<double>();
        for (var failure = 1; failure <= 4; failure++)
        {
            await scheduler.TickAsync(CancellationToken.None);
            var next = Status(scheduler).NextRefreshAtUtc!.Value;
            delays.Add((next - clock.GetUtcNow()).TotalMinutes);
            clock.SetNow(next);
        }

        Assert.Equal([30, 30, 30, 40], delays);
    }

    [Fact]
    public async Task UnconfiguredProviderStopsBeingScheduledAndMakesNoFurtherRequests()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider { Default = ProviderRefreshOutcome.Unconfigured };
        var scheduler = CreateScheduler(clock, provider);
        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        await scheduler.TickAsync(CancellationToken.None);
        for (var hour = 0; hour < 12; hour++)
        {
            clock.Advance(TimeSpan.FromHours(1));
            await scheduler.TickAsync(CancellationToken.None);
        }

        var status = Status(scheduler);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(ProviderRefreshState.Unconfigured, status.State);
        Assert.Null(status.NextRefreshAtUtc);
        Assert.Equal(ProviderStatusMessage.NotConfigured, status.Message);
    }

    [Fact]
    public async Task ProviderBecomesStaleAfterThreeIntervalsWithoutSuccess()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider { Default = ProviderRefreshOutcome.Cancelled };
        var scheduler = CreateScheduler(clock, provider);
        scheduler.Start(CancellationToken.None);

        clock.Advance(TimeSpan.FromMinutes(90));
        Assert.Equal(ProviderRefreshState.Idle, Status(scheduler).State);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(ProviderRefreshState.Stale, Status(scheduler).State);

        provider.Default = ProviderRefreshOutcome.Succeeded(0);
        await scheduler.TickAsync(CancellationToken.None);
        var succeededAt = clock.GetUtcNow();
        Assert.Equal(ProviderRefreshState.Idle, Status(scheduler).State);

        clock.SetNow(succeededAt + TimeSpan.FromMinutes(90));
        Assert.Equal(ProviderRefreshState.Idle, Status(scheduler).State);
        clock.SetNow(succeededAt + TimeSpan.FromMinutes(90) + TimeSpan.FromSeconds(1));
        Assert.Equal(ProviderRefreshState.Stale, Status(scheduler).State);
    }

    [Fact]
    public async Task StatusChangesAreRaisedOnTransitionsOnly()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider();
        var scheduler = CreateScheduler(clock, provider);
        var observed = new List<ProviderRefreshState>();
        scheduler.StatusChanged += () => observed.Add(Status(scheduler).State);

        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(30));
        await scheduler.TickAsync(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(30));
        provider.OnRefresh = () => observed.Add(Status(scheduler).State);
        await scheduler.TickAsync(CancellationToken.None);
        await scheduler.TickAsync(CancellationToken.None);

        Assert.Equal([ProviderRefreshState.Idle, ProviderRefreshState.Refreshing, ProviderRefreshState.Refreshing, ProviderRefreshState.Idle], observed);

        clock.Advance(TimeSpan.FromMinutes(91));
        provider.Default = ProviderRefreshOutcome.Cancelled;
        var before = observed.Count;
        await scheduler.TickAsync(CancellationToken.None);
        await scheduler.TickAsync(CancellationToken.None);
        Assert.Contains(ProviderRefreshState.Stale, observed.Skip(before));
    }

    [Fact]
    public async Task UnexpectedProviderExceptionOnTimerBecomesAFailureWithoutBreakingTheLoop()
    {
        var clock = new ManualTimeProvider(Start);
        var provider = new ScriptedProvider();
        provider.Script.Enqueue(_ => throw new InvalidOperationException("secret detail"));
        var scheduler = CreateScheduler(clock, provider);
        scheduler.Start(CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(60));

        await scheduler.TickAsync(CancellationToken.None);

        Assert.Equal(ProviderRefreshState.Failed, Status(scheduler).State);
        Assert.Equal(ProviderStatusMessage.ProviderUnavailable, Status(scheduler).Message);
    }

    [Fact]
    public async Task ShutdownWithAnInFlightTimerRefreshCompletesWithoutExceptionAndRaisesNothingAfterStop()
    {
        var clock = new ManualTimeProvider(Start) { FireTimers = true };
        var provider = new ScriptedProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.Script.Enqueue(async token =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return ProviderRefreshOutcome.Succeeded(0);
        });
        var scheduler = CreateScheduler(clock, provider);
        var raisedAfterStop = 0;
        var stoppedFlag = false;
        scheduler.StatusChanged += () =>
        {
            if (Volatile.Read(ref stoppedFlag))
            {
                Interlocked.Increment(ref raisedAfterStop);
            }
        };
        using var lifetime = new CancellationTokenSource();

        scheduler.Start(lifetime.Token);
        await WaitUntilAsync(() => clock.TimerCount == 1);
        clock.Advance(TimeSpan.FromSeconds(60));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(ProviderRefreshState.Refreshing, Status(scheduler).State);

        Volatile.Write(ref stoppedFlag, true);
        scheduler.Stop(TimeSpan.FromSeconds(2));
        lifetime.Cancel();

        Assert.True(scheduler.LoopTask!.IsCompletedSuccessfully);
        Assert.Equal(0, raisedAfterStop);
        scheduler.Start(lifetime.Token);
        Assert.True(scheduler.LoopTask!.IsCompleted);
    }

    [Fact]
    public void RejectsDuplicateProviderRegistration()
    {
        Assert.Throws<ArgumentException>(() => new ProviderRefreshScheduler([new ScriptedProvider(), new ScriptedProvider()]));
    }

    [Theory]
    [InlineData(GdacsRefreshStatus.Succeeded, ProviderRefreshOutcomeKind.Succeeded, ProviderStatusMessage.UpToDate)]
    [InlineData(GdacsRefreshStatus.AlreadyInProgress, ProviderRefreshOutcomeKind.AlreadyInProgress, ProviderStatusMessage.None)]
    [InlineData(GdacsRefreshStatus.Cancelled, ProviderRefreshOutcomeKind.Cancelled, ProviderStatusMessage.None)]
    [InlineData(GdacsRefreshStatus.ProviderUnavailable, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.ProviderUnavailable)]
    [InlineData(GdacsRefreshStatus.TimedOut, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.TimedOut)]
    [InlineData(GdacsRefreshStatus.InvalidResponse, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.InvalidResponse)]
    [InlineData(GdacsRefreshStatus.PersistenceFailure, ProviderRefreshOutcomeKind.Failed, ProviderStatusMessage.PersistenceFailure)]
    public void ClassifiesEveryGdacsResult(GdacsRefreshStatus status, ProviderRefreshOutcomeKind kind, ProviderStatusMessage message)
    {
        var outcome = GdacsScheduledProvider.Classify(new GdacsRefreshResult(status, NewAlerts: 2));

        Assert.Equal(kind, outcome.Kind);
        Assert.Equal(message, outcome.Message);
        Assert.Equal(status == GdacsRefreshStatus.Succeeded ? 2 : 0, outcome.NewAlerts);
    }

    [Fact]
    public void GdacsProviderUsesTheApprovedIdentityAndInterval()
    {
        var provider = new GdacsScheduledProvider(new GdacsRefreshService(new BlockingGdacsFeed(), new CountingGdacsStore()));

        Assert.Equal(ScheduledProviderId.Gdacs, provider.Id);
        Assert.Equal("GDACS", provider.DisplayName);
        Assert.Equal(TimeSpan.FromMinutes(30), provider.Interval);
    }

    private static ProviderRefreshScheduler CreateScheduler(TimeProvider clock, IScheduledProvider provider)
        => new([provider], clock, () => 0.5);

    private static ProviderRefreshStatus Status(ProviderRefreshScheduler scheduler) => Assert.Single(scheduler.GetStatuses());

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class ScriptedProvider : IScheduledProvider
    {
        private int calls;

        public ScheduledProviderId Id => ScheduledProviderId.Gdacs;
        public string DisplayName => "GDACS";
        public TimeSpan Interval => ProviderRefreshSchedulerTests.Interval;
        public ProviderRefreshOutcome Default { get; set; } = ProviderRefreshOutcome.Succeeded(0);
        public Queue<Func<CancellationToken, Task<ProviderRefreshOutcome>>> Script { get; } = new();
        public Action? OnRefresh { get; set; }
        public int Calls => Volatile.Read(ref calls);

        public Task<ProviderRefreshOutcome> RefreshAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            OnRefresh?.Invoke();
            return Script.TryDequeue(out var step) ? step(cancellationToken) : Task.FromResult(Default);
        }
    }

    private sealed class BlockingGdacsFeed : IGdacsEventFeed
    {
        private int calls;

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls => Volatile.Read(ref calls);

        public async Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref calls);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new GdacsEarthquakeFeedBatch([], 0);
        }
    }

    private sealed class CountingGdacsStore : IGdacsEarthquakeStore
    {
        public Task<GdacsEarthquakePersistenceResult> PersistGdacsEarthquakesAsync(
            IReadOnlyList<GdacsEarthquakeImport> imports,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GdacsEarthquakePersistenceResult(0, 0, 0, 0, 0, 0, [], 0));
    }
}
