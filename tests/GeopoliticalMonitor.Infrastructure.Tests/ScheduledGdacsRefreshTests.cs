using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.Refresh;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GeopoliticalMonitor.Infrastructure.Tests;

/// <summary>
/// Timer-driven GDACS refreshes through the real scheduler, service and SQLite store on a temporary database.
/// Alert semantics and the single-transaction guarantee must be identical to the manual path.
/// </summary>
public sealed class ScheduledGdacsRefreshTests : IDisposable
{
    private const string EventId = "evt-gdacs-eq-9000001";
    private static readonly DateTimeOffset Start = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstRevision = DateTimeOffset.Parse("2026-09-29T20:01:59Z");
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorSchedulerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task TimerDrivenMaterialUpdateOfAWatchedEventCreatesExactlyOneAlert()
    {
        var harness = await CreateHarnessAsync();
        await harness.ImportFirstRevisionAndWatchAsync();

        harness.Feed.Record = CreateRecord(FirstRevision.AddHours(1), "Corrected earthquake title");
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        await harness.Scheduler.TickAsync(CancellationToken.None);

        var status = Assert.Single(harness.Scheduler.GetStatuses());
        Assert.Equal(ProviderRefreshState.Idle, status.State);
        Assert.Equal(1, status.NewAlerts);
        await using (var db = harness.Initializer.CreateDbContext())
        {
            var alert = Assert.Single(await db.AlertOccurrences.ToListAsync());
            Assert.Equal(EventId, alert.EventId);
            Assert.Equal(FirstRevision.AddHours(1), alert.ProviderRevisionAtUtc);
            Assert.Equal("Corrected earthquake title", (await db.Events.SingleAsync()).Title);
        }

        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        await harness.Scheduler.TickAsync(CancellationToken.None);

        Assert.Equal(0, Assert.Single(harness.Scheduler.GetStatuses()).NewAlerts);
        await using var verification = harness.Initializer.CreateDbContext();
        Assert.Single(await verification.AlertOccurrences.ToListAsync());
    }

    [Fact]
    public async Task CancellationMidPersistLeavesTheDatabaseUnchangedAndTheNextRefreshAlertsOnce()
    {
        var harness = await CreateHarnessAsync();
        await harness.ImportFirstRevisionAndWatchAsync();
        var before = await harness.ReadEventAsync();

        harness.Feed.Record = CreateRecord(FirstRevision.AddHours(1), "Corrected earthquake title");
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        using var cancellation = new CancellationTokenSource();
        harness.Interceptor.CancelOnSave = cancellation;
        await harness.Scheduler.TickAsync(cancellation.Token);

        Assert.True(harness.Interceptor.Intercepted);
        var afterCancel = await harness.ReadEventAsync();
        Assert.Equal(before.Title, afterCancel.Title);
        Assert.Equal(before.LastUpdatedAtUtc, afterCancel.LastUpdatedAtUtc);
        Assert.Equal(before.UpdatedAtUtc, afterCancel.UpdatedAtUtc);
        Assert.Equal(0, await harness.CountAlertsAsync());
        Assert.Equal(1, await harness.CountSourcesAsync());
        var cancelledStatus = Assert.Single(harness.Scheduler.GetStatuses());
        Assert.Equal(ProviderRefreshState.Idle, cancelledStatus.State);
        Assert.Equal(ProviderStatusMessage.UpToDate, cancelledStatus.Message);

        harness.Interceptor.CancelOnSave = null;
        await harness.Scheduler.TickAsync(CancellationToken.None);

        Assert.Equal(1, await harness.CountAlertsAsync());
        Assert.Equal("Corrected earthquake title", (await harness.ReadEventAsync()).Title);
        Assert.Equal(1, Assert.Single(harness.Scheduler.GetStatuses()).NewAlerts);
    }

    [Fact]
    public async Task RolledBackPersistenceFailureCreatesNoAlertAndTheNextSuccessfulRefreshAlertsOnce()
    {
        var harness = await CreateHarnessAsync();
        await harness.ImportFirstRevisionAndWatchAsync();

        harness.Feed.Record = CreateRecord(FirstRevision.AddHours(1), "Corrected earthquake title");
        harness.Clock.Advance(TimeSpan.FromMinutes(30));
        harness.Interceptor.FailOnSave = true;
        await harness.Scheduler.TickAsync(CancellationToken.None);

        var failed = Assert.Single(harness.Scheduler.GetStatuses());
        Assert.Equal(ProviderRefreshState.Failed, failed.State);
        Assert.Equal(ProviderStatusMessage.PersistenceFailure, failed.Message);
        Assert.Equal(0, await harness.CountAlertsAsync());
        Assert.Equal("Earthquake in Testland", (await harness.ReadEventAsync()).Title);

        harness.Interceptor.FailOnSave = false;
        harness.Clock.SetNow(failed.NextRefreshAtUtc!.Value);
        await harness.Scheduler.TickAsync(CancellationToken.None);

        Assert.Equal(ProviderRefreshState.Idle, Assert.Single(harness.Scheduler.GetStatuses()).State);
        Assert.Equal(1, await harness.CountAlertsAsync());
        await harness.Scheduler.TickAsync(CancellationToken.None);
        Assert.Equal(1, await harness.CountAlertsAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private async Task<Harness> CreateHarnessAsync()
    {
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "scheduler.db");
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();

        var interceptor = new SaveInterceptor();
        var interceptedOptions = new DbContextOptionsBuilder<GeopoliticalMonitorDbContext>()
            .UseSqlite(SqliteConnectionStringFactory.Create(databasePath))
            .AddInterceptors(interceptor)
            .Options;
        var store = new SqliteEventStore(() => new GeopoliticalMonitorDbContext(interceptedOptions));
        var clock = new MutableTimeProvider(Start);
        var feed = new MutableFeed { Record = CreateRecord(FirstRevision, "Earthquake in Testland") };
        var service = new GdacsRefreshService(feed, store, clock);
        var scheduler = new ProviderRefreshScheduler([new GdacsScheduledProvider(service)], clock, () => 0.5);
        return new Harness(initializer, scheduler, service, feed, clock, interceptor);
    }

    private static GdacsEarthquakeRecord CreateRecord(DateTimeOffset revision, string title) => new(
        9000001,
        9100001,
        title,
        "Earthquake in Testland",
        DateTimeOffset.Parse("2026-09-29T18:58:12Z"),
        revision,
        "Orange",
        6.4,
        "Russia",
        "RUS",
        new Uri("https://www.gdacs.org/report.aspx?eventid=9000001&episodeid=9100001&eventtype=EQ"),
        160.5129,
        53.1928);

    private sealed record Harness(
        LocalDatabaseInitializer Initializer,
        ProviderRefreshScheduler Scheduler,
        GdacsRefreshService Service,
        MutableFeed Feed,
        MutableTimeProvider Clock,
        SaveInterceptor Interceptor)
    {
        public async Task ImportFirstRevisionAndWatchAsync()
        {
            var first = await Scheduler.RefreshManuallyAsync(ScheduledProviderId.Gdacs, Service.RefreshAsync, GdacsScheduledProvider.Classify, CancellationToken.None);
            Assert.Equal(GdacsRefreshStatus.Succeeded, first.Status);
            Assert.Equal(1, first.NewEvents);

            const string watchlistId = "wl-00000000000000000000000000000001";
            await using (var db = Initializer.CreateDbContext())
            {
                db.Watchlists.Add(new WatchlistEntity { Id = watchlistId, Name = "Monitor", NormalizedName = "MONITOR" });
                db.WatchlistEvents.Add(new WatchlistEventEntity { WatchlistId = watchlistId, EventId = EventId });
                await db.SaveChangesAsync();
            }

            var alerts = new AlertService(new SqliteAlertStore(Initializer.CreateDbContext), Clock);
            Assert.Equal(SetAlertRuleDisposition.CreatedEnabled, (await alerts.SetRuleEnabledAsync(watchlistId, true)).Disposition);
        }

        public async Task<EventEntity> ReadEventAsync()
        {
            await using var db = Initializer.CreateDbContext();
            return await db.Events.AsNoTracking().SingleAsync();
        }

        public async Task<int> CountAlertsAsync()
        {
            await using var db = Initializer.CreateDbContext();
            return await db.AlertOccurrences.CountAsync();
        }

        public async Task<int> CountSourcesAsync()
        {
            await using var db = Initializer.CreateDbContext();
            return await db.SourceItems.CountAsync();
        }
    }

    private sealed class MutableFeed : IGdacsEventFeed
    {
        public required GdacsEarthquakeRecord Record { get; set; }

        public Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new GdacsEarthquakeFeedBatch([Record], 0));
    }

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;

        public void SetNow(DateTimeOffset value) => now = value;
    }

    private sealed class SaveInterceptor : SaveChangesInterceptor
    {
        public CancellationTokenSource? CancelOnSave { get; set; }
        public bool FailOnSave { get; set; }
        public bool Intercepted { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (CancelOnSave is { } cancellation)
            {
                // Cancels after the store has opened its transaction and staged the Event update and Alert rows.
                Intercepted = true;
                cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (FailOnSave)
            {
                Intercepted = true;
                throw new InvalidOperationException("Simulated persistence failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
