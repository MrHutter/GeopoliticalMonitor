using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class DemoDataCleanupTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly LocalDatabaseInitializer initializer;

    public DemoDataCleanupTests()
    {
        initializer = new LocalDatabaseInitializer(Path.Combine(temporaryDirectory, "geopolitical-monitor-test.db"));
    }

    [Fact]
    public async Task RemovesOnlyDemoRowsFromAMixedDatabaseAndCascades()
    {
        await SeedMixedDatabaseAsync();

        var result = await DemoDataCleanup.TryRunAsync(initializer.CreateDbContext, new FixedTimeProvider(Now));

        Assert.Equal(DemoDataCleanupOutcome.Completed, result.Outcome);
        Assert.Equal(6, result.RemovedEvents);
        Assert.Equal(14, result.RemovedSources);
        Assert.True(result.RemovedDemoEvents);

        await using var dbContext = initializer.CreateDbContext();
        Assert.Equal(
            ["EVT-DEMO-uppercase", "evt-demox-no-separator", "evt-gdacs-eq-1001", "evt-manual-x", "evt-ucdp-ged-501", "user-evt-demo-1"],
            (await dbContext.Events.Select(item => item.Id).ToListAsync()).Order(StringComparer.Ordinal));
        var remainingSources = await dbContext.SourceItems.Select(item => item.Id).ToListAsync();
        Assert.DoesNotContain(remainingSources, id => id.StartsWith(DemoDataCleanup.DemoSourceIdPrefix, StringComparison.Ordinal));
        Assert.Contains("SRC-DEMO-uppercase", remainingSources);
        Assert.Contains("src-gdacs-eq-1001", remainingSources);
        // The X post attached to a demo Event is not demo data; only its association goes.
        Assert.Equal(2, remainingSources.Count(id => id.StartsWith("src-x-", StringComparison.Ordinal)));

        var associations = await dbContext.EventSourceAssociations.ToListAsync();
        Assert.DoesNotContain(associations, item => item.EventId.StartsWith(DemoDataCleanup.DemoEventIdPrefix, StringComparison.Ordinal));
        Assert.DoesNotContain(associations, item => item.SourceItemId.StartsWith(DemoDataCleanup.DemoSourceIdPrefix, StringComparison.Ordinal));
        Assert.Contains(associations, item => item.EventId == "evt-gdacs-eq-1001" && item.SourceItemId == "src-gdacs-eq-1001");
        Assert.Contains(associations, item => item.EventId == "evt-manual-x");

        // The Watchlist and its Alert rule stay; only the demo membership and the demo Alert occurrence go.
        Assert.Single(await dbContext.Watchlists.ToListAsync());
        Assert.Single(await dbContext.AlertRules.ToListAsync());
        Assert.Equal(["evt-ucdp-ged-501"], await dbContext.WatchlistEvents.Select(item => item.EventId).ToListAsync());
        Assert.Equal(["evt-ucdp-ged-501"], await dbContext.AlertOccurrences.Select(item => item.EventId).ToListAsync());

        var flag = await dbContext.ApplicationPreferences.SingleAsync(item => item.Key == DemoDataCleanup.CompletedPreferenceKey);
        Assert.Equal(DemoDataCleanup.CompletedPreferenceValue, flag.Value);
        Assert.Equal(Now, flag.UpdatedAt);
    }

    [Fact]
    public async Task RunsOnlyOnceEvenWhenDemoRowsAppearAgain()
    {
        await initializer.InitializeAsync();
        await new DemoDataSeeder(CreateService()).SeedAsync();
        Assert.True((await DemoDataCleanup.TryRunAsync(initializer.CreateDbContext)).RemovedDemoEvents);

        await new DemoDataSeeder(CreateService()).SeedAsync();
        var second = await DemoDataCleanup.TryRunAsync(initializer.CreateDbContext);

        Assert.Equal(DemoDataCleanupOutcome.AlreadyCompleted, second.Outcome);
        Assert.False(second.RemovedDemoEvents);
        await using var dbContext = initializer.CreateDbContext();
        Assert.Equal(6, await dbContext.Events.CountAsync());
    }

    [Fact]
    public async Task ZeroCountRunStillSetsTheFlagAndShowsNoNotice()
    {
        await initializer.InitializeAsync();

        var first = await DemoDataCleanup.TryRunAsync(initializer.CreateDbContext);
        var second = await DemoDataCleanup.TryRunAsync(initializer.CreateDbContext);

        Assert.Equal(new DemoDataCleanupResult(DemoDataCleanupOutcome.Completed, 0, 0), first);
        Assert.False(first.RemovedDemoEvents);
        Assert.Equal(DemoDataCleanupOutcome.AlreadyCompleted, second.Outcome);
        await using var dbContext = initializer.CreateDbContext();
        Assert.True(await dbContext.ApplicationPreferences.AnyAsync(item => item.Key == DemoDataCleanup.CompletedPreferenceKey));
    }

    [Fact]
    public async Task FailureCommitsNothingDoesNotThrowAndRetriesNextLaunch()
    {
        await initializer.InitializeAsync();
        await new DemoDataSeeder(CreateService()).SeedAsync();

        var failed = await DemoDataCleanup.TryRunAsync(() => CreateDbContext(new FailingSaveChangesInterceptor()));

        Assert.Equal(DemoDataCleanupOutcome.Failed, failed.Outcome);
        Assert.False(failed.RemovedDemoEvents);
        await using (var dbContext = initializer.CreateDbContext())
        {
            Assert.Equal(6, await dbContext.Events.CountAsync());
            Assert.Equal(14, await dbContext.SourceItems.CountAsync());
            Assert.False(await dbContext.ApplicationPreferences.AnyAsync(item => item.Key == DemoDataCleanup.CompletedPreferenceKey));
        }

        var retried = await DemoDataCleanup.TryRunAsync(initializer.CreateDbContext);
        Assert.Equal(DemoDataCleanupOutcome.Completed, retried.Outcome);
        Assert.Equal(6, retried.RemovedEvents);
    }

    [Fact]
    public void DesktopStartupNoLongerSeedsDemoDataAndRunsTheCleanup()
    {
        var mainWindow = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "backend", "GeopoliticalMonitor.Desktop", "MainWindow.xaml.cs"));

        Assert.DoesNotContain("DemoDataSeeder", mainWindow, StringComparison.Ordinal);
        Assert.Contains("DemoDataCleanup.TryRunAsync(", mainWindow, StringComparison.Ordinal);
    }

    private async Task SeedMixedDatabaseAsync()
    {
        await initializer.InitializeAsync();
        var service = CreateService();
        await new DemoDataSeeder(service).SeedAsync();

        await service.CreateEventAsync(CreateEvent("evt-ucdp-ged-501", new EventProvenance(EventProvider.UcdpCandidate, "501", 202608)));
        await service.CreateEventAsync(CreateEvent("evt-gdacs-eq-1001", new EventProvenance(EventProvider.Gdacs, "EQ:1001")));
        await service.CreateEventAsync(CreateEvent("evt-manual-x", new EventProvenance(EventProvider.Manual)));
        // Ids that merely resemble the prefixes must survive.
        await service.CreateEventAsync(CreateEvent("EVT-DEMO-uppercase", new EventProvenance(EventProvider.Manual)));
        await service.CreateEventAsync(CreateEvent("evt-demox-no-separator", new EventProvenance(EventProvider.Manual)));
        await service.CreateEventAsync(CreateEvent("user-evt-demo-1", new EventProvenance(EventProvider.Manual)));
        await service.CreateSourceItemAsync(CreateSource("src-gdacs-eq-1001"));
        await service.CreateSourceItemAsync(CreateSource("SRC-DEMO-uppercase"));
        await service.AttachSourceToEventAsync(new EventId("evt-gdacs-eq-1001"), new SourceItemId("src-gdacs-eq-1001"));
        // A non-demo source attached to a demo Event, and manual X attachments.
        await service.AttachSourceToEventAsync(new EventId("evt-demo-baltic-security"), new SourceItemId("src-gdacs-eq-1001"));
        await service.AttachXPostSourceToEventAsync("evt-manual-x", "https://x.com/example/status/1759288472993702104");
        await service.AttachXPostSourceToEventAsync("evt-demo-red-sea-shipping", "https://x.com/example/status/1759288472993702105");

        await using var dbContext = initializer.CreateDbContext();
        dbContext.Watchlists.Add(new WatchlistEntity { Id = "wl-mixed", Name = "Mixed", NormalizedName = "MIXED" });
        dbContext.WatchlistEvents.AddRange(
            new WatchlistEventEntity { WatchlistId = "wl-mixed", EventId = "evt-demo-baltic-security" },
            new WatchlistEventEntity { WatchlistId = "wl-mixed", EventId = "evt-ucdp-ged-501" });
        dbContext.AlertRules.Add(new AlertRuleEntity { Id = "rule-mixed", WatchlistId = "wl-mixed", TriggerType = "GdacsEventMateriallyUpdated", IsEnabled = true, CreatedAtUtc = Now, LastEnabledAtUtc = Now });
        dbContext.AlertOccurrences.AddRange(
            new AlertOccurrenceEntity { Id = "alert-demo", AlertRuleId = "rule-mixed", EventId = "evt-demo-baltic-security", ProviderRevisionAtUtc = Now, TriggeredAtUtc = Now },
            new AlertOccurrenceEntity { Id = "alert-ucdp", AlertRuleId = "rule-mixed", EventId = "evt-ucdp-ged-501", ProviderRevisionAtUtc = Now, TriggeredAtUtc = Now });
        await dbContext.SaveChangesAsync();
    }

    private EventCatalogService CreateService() =>
        new(new SqliteEventStore(initializer.CreateDbContext), new FixedTimeProvider(Now));

    private GeopoliticalMonitorDbContext CreateDbContext(IInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<GeopoliticalMonitorDbContext>()
            .UseSqlite(SqliteConnectionStringFactory.Create(initializer.DatabasePath))
            .AddInterceptors(interceptor)
            .Options);

    private static Event CreateEvent(string id, EventProvenance provenance)
    {
        var timestamp = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        return Event.Create(new EventId(id), "Test event", "Test summary", EventCategory.Security, EventStatus.Active, timestamp, timestamp.AddMinutes(5), new Location(10, 20, "Test place"), timestamp, timestamp.AddMinutes(5), new EventSeverityAssessment(EventSeverity.Medium, EventSeverityAssessment.ManualRule, "test"), provenance);
    }

    private static SourceItem CreateSource(string id)
    {
        var timestamp = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        return SourceItem.Create(new SourceItemId(id), SourceType.Other, id, "Test source", "Test source text", null, "Test Author", null, timestamp, timestamp.AddMinutes(3), null, timestamp);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "backend", "GeopoliticalMonitor.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the GeopoliticalMonitor repository root from the test output directory.");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private sealed class FailingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated failure while recording the cleanup flag.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
