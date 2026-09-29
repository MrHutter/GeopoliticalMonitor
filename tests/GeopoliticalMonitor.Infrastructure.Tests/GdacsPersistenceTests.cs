using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class GdacsPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorGdacsTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public GdacsPersistenceTests()
    {
        databasePath = Path.Combine(directory, "test.db");
    }

    [Fact]
    public async Task FirstImportIsAtomicAndRepeatIsIdempotent()
    {
        var (initializer, store) = await CreateStoreAsync();
        var import = CreateImport();

        var first = await store.PersistGdacsEarthquakesAsync([import], CreatedAt);
        var second = await store.PersistGdacsEarthquakesAsync([import], CreatedAt);

        Assert.Equal(1, first.NewEvents);
        Assert.Equal(1, first.NewSources);
        Assert.Equal(GdacsEventPersistenceDisposition.Created, Assert.Single(first.EventOutcomes).Disposition);
        Assert.Equal(0, first.NewAlerts);
        Assert.Equal(1, second.UnchangedRecords);
        Assert.Equal(GdacsEventPersistenceDisposition.Unchanged, Assert.Single(second.EventOutcomes).Disposition);
        await using var db = initializer.CreateDbContext();
        Assert.Single(await db.Events.ToListAsync());
        Assert.Single(await db.SourceItems.ToListAsync());
        Assert.Single(await db.EventSourceAssociations.ToListAsync());
    }

    [Fact]
    public async Task NewEpisodeReusesEventAndNewerCorrectionUpdatesOwnedFields()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);

        var nextEpisode = CreateImport(episodeId: 1661063);
        var episodeResult = await store.PersistGdacsEarthquakesAsync([nextEpisode], CreatedAt);
        Assert.Equal(0, episodeResult.NewEvents);
        Assert.Equal(1, episodeResult.NewSources);

        var newer = CreateImport(
            episodeId: 1661062,
            providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1),
            title: "Corrected earthquake title",
            latitude: 54);
        var updateResult = await store.PersistGdacsEarthquakesAsync([newer], CreatedAt);
        Assert.Equal(1, updateResult.UpdatedEvents);
        Assert.Equal(1, updateResult.UpdatedSources);
        Assert.Equal(GdacsEventPersistenceDisposition.MateriallyUpdated, Assert.Single(updateResult.EventOutcomes).Disposition);

        await using var db = initializer.CreateDbContext();
        var persistedEvent = Assert.Single(await db.Events.ToListAsync());
        Assert.Equal("Corrected earthquake title", persistedEvent.Title);
        Assert.Equal(54, persistedEvent.Latitude);
        Assert.Equal("NaturalDisaster", persistedEvent.Category);
        Assert.Equal("Unverified", persistedEvent.Status);
        Assert.Equal(CreatedAt, persistedEvent.CreatedAtUtc);
        Assert.Equal(2, await db.SourceItems.CountAsync());
        Assert.Equal(2, await db.EventSourceAssociations.CountAsync());
    }

    [Fact]
    public async Task StaleAndEqualConflictingRecordsDoNotOverwrite()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);

        var stale = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddMinutes(-1), title: "Stale title");
        await store.PersistGdacsEarthquakesAsync([stale], CreatedAt);
        var equalConflict = CreateImport(title: "Conflicting title");
        var conflict = await store.PersistGdacsEarthquakesAsync([equalConflict], CreatedAt);

        Assert.Equal(1, conflict.Conflicts);
        await using var db = initializer.CreateDbContext();
        Assert.Equal(original.Event.Title, (await db.Events.SingleAsync()).Title);
    }

    [Fact]
    public async Task UnownedDeterministicIdCollisionIsReportedAndUntouched()
    {
        var (initializer, store) = await CreateStoreAsync();
        await using (var db = initializer.CreateDbContext())
        {
            var local = CreateImport().Event;
            db.Events.Add(new GeopoliticalMonitor.Infrastructure.Persistence.Entities.EventEntity
            {
                Id = local.Id.Value,
                Title = "Local owner event",
                Summary = "Local summary",
                Category = EventCategory.Other.ToString(),
                Status = EventStatus.Active.ToString(),
                StartedAtUtc = local.StartedAtUtc,
                LastUpdatedAtUtc = local.LastUpdatedAtUtc,
                Latitude = 1,
                Longitude = 2,
                LocationDisplayName = "Local",
                CreatedAtUtc = CreatedAt,
                UpdatedAtUtc = CreatedAt,
            });
            await db.SaveChangesAsync();
        }

        var result = await store.PersistGdacsEarthquakesAsync([CreateImport()], CreatedAt);

        Assert.Equal(1, result.Conflicts);
        await using var verification = initializer.CreateDbContext();
        Assert.Equal("Local owner event", (await verification.Events.SingleAsync()).Title);
        Assert.Empty(await verification.SourceItems.ToListAsync());
        Assert.Empty(await verification.EventSourceAssociations.ToListAsync());
    }

    [Fact]
    public async Task EmptyLaterFeedDoesNotDeleteExistingProviderData()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.PersistGdacsEarthquakesAsync([CreateImport()], CreatedAt);
        await store.PersistGdacsEarthquakesAsync([], CreatedAt);

        await using var db = initializer.CreateDbContext();
        Assert.Single(await db.Events.ToListAsync());
        Assert.Single(await db.SourceItems.ToListAsync());
    }

    [Fact]
    public async Task MaterialUpdateCreatesOneAlertAndRepeatAcrossReopenIsSilent()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        await AddMonitoringContextAsync(initializer, 1);
        var newer = CreateImport(
            providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1),
            title: "Materially corrected earthquake");

        var updated = await store.PersistGdacsEarthquakesAsync([newer], CreatedAt.AddHours(1));
        var repeated = await new SqliteEventStore(initializer.CreateDbContext)
            .PersistGdacsEarthquakesAsync([newer], CreatedAt.AddHours(2));

        Assert.Equal(1, updated.NewAlerts);
        Assert.Equal(GdacsEventPersistenceDisposition.MateriallyUpdated, Assert.Single(updated.EventOutcomes).Disposition);
        Assert.Equal(0, repeated.NewAlerts);
        await using var db = initializer.CreateDbContext();
        var alert = Assert.Single(await db.AlertOccurrences.ToListAsync());
        Assert.Equal(newer.Event.LastUpdatedAtUtc, alert.ProviderRevisionAtUtc);
        Assert.Equal(CreatedAt.AddHours(1), alert.TriggeredAtUtc);
    }

    [Fact]
    public async Task RevisionOnlyAndSourceOnlyUpdatesDoNotAlert()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        await AddMonitoringContextAsync(initializer, 1);
        var revisionOnly = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1));

        var revisionResult = await store.PersistGdacsEarthquakesAsync([revisionOnly], CreatedAt.AddHours(1));
        var nextEpisode = CreateImport(
            episodeId: 1661063,
            providerUpdatedAt: revisionOnly.Event.LastUpdatedAtUtc);
        var episodeResult = await store.PersistGdacsEarthquakesAsync([nextEpisode], CreatedAt.AddHours(2));

        Assert.Equal(GdacsEventPersistenceDisposition.RevisionOnlyUpdated, Assert.Single(revisionResult.EventOutcomes).Disposition);
        Assert.Equal(0, revisionResult.NewAlerts);
        Assert.Equal(GdacsEventPersistenceDisposition.Unchanged, Assert.Single(episodeResult.EventOutcomes).Disposition);
        Assert.Equal(0, episodeResult.NewAlerts);
        await using var db = initializer.CreateDbContext();
        Assert.Empty(await db.AlertOccurrences.ToListAsync());
    }

    [Fact]
    public async Task NewAndUnwatchedEventsDoNotAlert()
    {
        var (initializer, store) = await CreateStoreAsync();
        const string watchlistId = "wl-00000000000000000000000000000009";
        await using (var db = initializer.CreateDbContext())
        {
            db.Watchlists.Add(new WatchlistEntity
            {
                Id = watchlistId,
                Name = "No membership",
                NormalizedName = "NO MEMBERSHIP",
            });
            await db.SaveChangesAsync();
        }

        var alertService = new AlertService(new SqliteAlertStore(initializer.CreateDbContext), new FixedTimeProvider(CreatedAt));
        await alertService.SetRuleEnabledAsync(watchlistId, true);
        var original = CreateImport();
        var created = await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        var newer = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1), title: "Unwatched correction");
        var updated = await store.PersistGdacsEarthquakesAsync([newer], CreatedAt.AddHours(1));

        Assert.Equal(0, created.NewAlerts);
        Assert.Equal(GdacsEventPersistenceDisposition.Created, Assert.Single(created.EventOutcomes).Disposition);
        Assert.Equal(0, updated.NewAlerts);
        Assert.Equal(GdacsEventPersistenceDisposition.MateriallyUpdated, Assert.Single(updated.EventOutcomes).Disposition);
        await using var verification = initializer.CreateDbContext();
        Assert.Empty(await verification.AlertOccurrences.ToListAsync());
    }

    [Fact]
    public async Task StaleAndEqualTimestampConflictsRemainSilentForMonitoredEvent()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        await AddMonitoringContextAsync(initializer, 1);
        var stale = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddMinutes(-1), title: "Stale correction");
        var conflict = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc, title: "Equal conflict");

        var staleResult = await store.PersistGdacsEarthquakesAsync([stale], CreatedAt.AddHours(1));
        var conflictResult = await store.PersistGdacsEarthquakesAsync([conflict], CreatedAt.AddHours(2));

        Assert.Equal(0, staleResult.NewAlerts);
        Assert.Equal(GdacsEventPersistenceDisposition.Unchanged, Assert.Single(staleResult.EventOutcomes).Disposition);
        Assert.Equal(0, conflictResult.NewAlerts);
        Assert.Equal(GdacsEventPersistenceDisposition.Conflict, Assert.Single(conflictResult.EventOutcomes).Disposition);
        await using var verification = initializer.CreateDbContext();
        Assert.Empty(await verification.AlertOccurrences.ToListAsync());
    }

    [Fact]
    public async Task MaterialUpdateCreatesOneOccurrencePerEnabledWatchlistContext()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        await AddMonitoringContextAsync(initializer, 1);
        await AddMonitoringContextAsync(initializer, 2);
        var newer = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1), latitude: 54);

        var result = await store.PersistGdacsEarthquakesAsync([newer], CreatedAt.AddHours(1));

        Assert.Equal(2, result.NewAlerts);
        await using var db = initializer.CreateDbContext();
        Assert.Equal(2, await db.AlertOccurrences.CountAsync());
        Assert.Equal(2, await db.AlertOccurrences.Select(item => item.AlertRuleId).Distinct().CountAsync());
    }

    [Fact]
    public async Task DisabledRuleAndRemovedMembershipDoNotAlert()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        var watchlistId = await AddMonitoringContextAsync(initializer, 1);
        var alertService = new AlertService(new SqliteAlertStore(initializer.CreateDbContext), new FixedTimeProvider(CreatedAt));
        await alertService.SetRuleEnabledAsync(watchlistId, false);
        var firstUpdate = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1), title: "Disabled update");
        var disabled = await store.PersistGdacsEarthquakesAsync([firstUpdate], CreatedAt.AddHours(1));
        await alertService.SetRuleEnabledAsync(watchlistId, true);
        await using (var db = initializer.CreateDbContext())
        {
            await db.WatchlistEvents.ExecuteDeleteAsync();
        }
        var secondUpdate = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(2), title: "Removed membership update");
        var removed = await store.PersistGdacsEarthquakesAsync([secondUpdate], CreatedAt.AddHours(2));

        Assert.Equal(0, disabled.NewAlerts);
        Assert.Equal(0, removed.NewAlerts);
        await using var verification = initializer.CreateDbContext();
        Assert.Empty(await verification.AlertOccurrences.ToListAsync());
    }

    [Fact]
    public async Task AlertConstraintFailureRollsBackProviderUpdateAndRetryCanSucceed()
    {
        var (initializer, store) = await CreateStoreAsync();
        var original = CreateImport();
        await store.PersistGdacsEarthquakesAsync([original], CreatedAt);
        await AddMonitoringContextAsync(initializer, 1);
        var newer = CreateImport(providerUpdatedAt: original.Event.LastUpdatedAtUtc.AddHours(1), title: "Atomic correction");
        string ruleId;
        await using (var db = initializer.CreateDbContext())
        {
            ruleId = (await db.AlertRules.SingleAsync()).Id;
            db.AlertOccurrences.Add(new AlertOccurrenceEntity
            {
                Id = "al-ffffffffffffffffffffffffffffffff",
                AlertRuleId = ruleId,
                EventId = newer.Event.Id.Value,
                ProviderRevisionAtUtc = newer.Event.LastUpdatedAtUtc,
                TriggeredAtUtc = CreatedAt,
            });
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            store.PersistGdacsEarthquakesAsync([newer], CreatedAt.AddHours(1)));
        await using (var verification = initializer.CreateDbContext())
        {
            Assert.Equal(original.Event.Title, (await verification.Events.SingleAsync()).Title);
            await verification.AlertOccurrences.ExecuteDeleteAsync();
        }

        var retry = await store.PersistGdacsEarthquakesAsync([newer], CreatedAt.AddHours(2));

        Assert.Equal(1, retry.NewAlerts);
        await using var final = initializer.CreateDbContext();
        Assert.Equal("Atomic correction", (await final.Events.SingleAsync()).Title);
        Assert.Single(await final.AlertOccurrences.ToListAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private async Task<(LocalDatabaseInitializer Initializer, SqliteEventStore Store)> CreateStoreAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return (initializer, new SqliteEventStore(initializer.CreateDbContext));
    }

    private static async Task<string> AddMonitoringContextAsync(LocalDatabaseInitializer initializer, int number)
    {
        var watchlistId = $"wl-{number:D32}";
        await using (var db = initializer.CreateDbContext())
        {
            db.Watchlists.Add(new WatchlistEntity
            {
                Id = watchlistId,
                Name = $"Monitor {number}",
                NormalizedName = $"MONITOR {number}",
            });
            db.WatchlistEvents.Add(new WatchlistEventEntity
            {
                WatchlistId = watchlistId,
                EventId = "evt-gdacs-eq-1500811",
            });
            await db.SaveChangesAsync();
        }

        var service = new AlertService(new SqliteAlertStore(initializer.CreateDbContext), new FixedTimeProvider(CreatedAt));
        Assert.Equal(SetAlertRuleDisposition.CreatedEnabled, (await service.SetRuleEnabledAsync(watchlistId, true)).Disposition);
        return watchlistId;
    }

    private static GdacsEarthquakeImport CreateImport(
        long episodeId = 1661062,
        DateTimeOffset? providerUpdatedAt = null,
        string title = "Earthquake in Russia",
        double latitude = 53.1928)
    {
        var updated = providerUpdatedAt ?? DateTimeOffset.Parse("2025-09-19T20:01:59Z");
        var location = new Location(latitude, 160.5129, "Russia", "RUS");
        var eventItem = Event.Create(
            new EventId("evt-gdacs-eq-1500811"),
            title,
            "Earthquake in Russia",
            EventCategory.NaturalDisaster,
            EventStatus.Unverified,
            DateTimeOffset.Parse("2025-09-18T18:58:12Z"),
            updated,
            location,
            CreatedAt,
            CreatedAt);
        var source = SourceItem.Create(
            new SourceItemId($"src-gdacs-eq-1500811-{episodeId}"),
            SourceType.DatasetRecord,
            $"GDACS:EQ:1500811:{episodeId}",
            $"{title} - GDACS episode {episodeId}",
            $"Provider record for {title}",
            new Uri($"https://www.gdacs.org/report.aspx?eventid=1500811&episodeid={episodeId}&eventtype=EQ"),
            GdacsRefreshService.Attribution,
            null,
            updated,
            CreatedAt,
            location,
            CreatedAt);
        return new GdacsEarthquakeImport(1500811, episodeId, eventItem, source);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
