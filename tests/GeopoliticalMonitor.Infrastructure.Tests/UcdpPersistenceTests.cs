using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Ucdp;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class UcdpPersistenceTests : IDisposable
{
    private const string PreviousMigration = "20260930120000_AddEventSeverityAndProvenance";
    private const string UcdpMigration = "20261002120000_AddProviderLastSeenRelease";
    private static readonly DateTimeOffset RetrievedAt = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly UcdpRelease July = new(2026, 7);
    private static readonly UcdpRelease August = new(2026, 8);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorUcdpTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InsertsEventsSourcesAndAssociationsAndReimportIsIdempotent()
    {
        var (initializer, store) = await CreateStoreAsync();
        var imports = Imports(August, Record(1, "2026-08-03"), Record(2, "2026-08-04"));

        var first = await store.PersistUcdpCandidateReleaseAsync(imports, August.Ordinal);
        var second = await store.PersistUcdpCandidateReleaseAsync(imports, August.Ordinal);

        Assert.Equal((2, 0, 2, 0, 0, 0), (first.NewEvents, first.UpdatedEvents, first.NewSources, first.UpdatedSources, first.UnchangedRecords, first.Conflicts));
        Assert.Equal((0, 0, 0, 0, 2, 0), (second.NewEvents, second.UpdatedEvents, second.NewSources, second.UpdatedSources, second.UnchangedRecords, second.Conflicts));
        await using var db = initializer.CreateDbContext();
        var row = await db.Events.AsNoTracking().SingleAsync(item => item.Id == "evt-ucdp-cand-1");
        Assert.Equal("UcdpCandidate", row.Provider);
        Assert.Equal("1", row.ProviderKey);
        Assert.Equal(202608, row.ProviderLastSeenRelease);
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 23, 59, 59, TimeSpan.Zero), row.LastUpdatedAtUtc);
        Assert.Equal(2, await db.SourceItems.CountAsync());
        Assert.Equal(2, await db.EventSourceAssociations.CountAsync());
        Assert.Equal(202608, await store.GetNewestUcdpReleaseAsync());
    }

    [Fact]
    public async Task NewerReleaseUpdatesProviderOwnedFieldsButNeverStatus()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.PersistUcdpCandidateReleaseAsync(Imports(July, Record(1, "2026-07-30", best: 5, type: 1)), July.Ordinal);
        await using (var db = initializer.CreateDbContext())
        {
            var row = await db.Events.SingleAsync();
            row.Status = EventStatus.Active.ToString();
            await db.SaveChangesAsync();
        }

        var result = await store.PersistUcdpCandidateReleaseAsync(
            Imports(August, Record(1, "2026-08-01", dateEnd: "2026-08-02", best: 150, type: 3, where: "Revised town")),
            August.Ordinal);

        Assert.Equal(1, result.UpdatedEvents);
        Assert.Equal(1, result.UpdatedSources);
        await using var check = initializer.CreateDbContext();
        var updated = await check.Events.AsNoTracking().SingleAsync();
        Assert.Equal(202608, updated.ProviderLastSeenRelease);
        Assert.Equal("One-sided violence: Revised town", updated.Title);
        Assert.Equal(EventCategory.Security.ToString(), updated.Category);
        Assert.Equal(EventSeverity.Critical.ToString(), updated.Severity);
        Assert.Equal(new DateTimeOffset(2026, 8, 2, 23, 59, 59, TimeSpan.Zero), updated.LastUpdatedAtUtc);
        Assert.Equal(EventStatus.Active.ToString(), updated.Status);
        Assert.Contains("release 26.0.8", (await check.SourceItems.AsNoTracking().SingleAsync()).Text);
    }

    [Fact]
    public async Task EqualOrOlderReleaseIsIgnoredWithoutAConflict()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.PersistUcdpCandidateReleaseAsync(Imports(August, Record(1, "2026-08-03")), August.Ordinal);

        var equal = await store.PersistUcdpCandidateReleaseAsync(Imports(August, Record(1, "2026-08-03", best: 999, where: "Changed")), August.Ordinal);
        var older = await store.PersistUcdpCandidateReleaseAsync(Imports(July, Record(1, "2026-07-03", best: 999, where: "Older")), July.Ordinal);

        Assert.Equal((0, 1), (equal.Conflicts, equal.UnchangedRecords));
        Assert.Equal((0, 1), (older.Conflicts, older.UnchangedRecords));
        await using var db = initializer.CreateDbContext();
        var row = await db.Events.AsNoTracking().SingleAsync();
        Assert.Equal("State-based violence: Place 1", row.Title);
        Assert.Equal(202608, row.ProviderLastSeenRelease);
    }

    [Fact]
    public async Task OwnershipCollisionsAreCountedAndNeverOverwritten()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.CreateEventAsync(ManualEvent("evt-ucdp-cand-1", EventProvider.Manual, null, RetrievedAt));
        await store.CreateEventAsync(ManualEvent("evt-squatter", EventProvider.UcdpCandidate, "2", RetrievedAt));
        await store.CreateSourceItemAsync(SourceItem.Create(new SourceItemId("src-ucdp-cand-3"), SourceType.DatasetRecord, "UCDP:CAND:3", "t", "text", new Uri("https://elsewhere.invalid/"), null, null, RetrievedAt, RetrievedAt, null, RetrievedAt));

        var result = await store.PersistUcdpCandidateReleaseAsync(
            Imports(August, Record(1, "2026-08-03"), Record(2, "2026-08-03"), Record(3, "2026-08-03"), Record(4, "2026-08-03")),
            August.Ordinal);

        Assert.Equal(3, result.Conflicts);
        Assert.Equal(1, result.NewEvents);
        await using var db = initializer.CreateDbContext();
        Assert.Equal("Manual title", (await db.Events.AsNoTracking().SingleAsync(item => item.Id == "evt-ucdp-cand-1")).Title);
        Assert.False(await db.Events.AnyAsync(item => item.Id == "evt-ucdp-cand-2"));
        Assert.False(await db.Events.AnyAsync(item => item.Id == "evt-ucdp-cand-3"));
        Assert.True(await db.Events.AnyAsync(item => item.Id == "evt-ucdp-cand-4"));
    }

    [Fact]
    public async Task UniqueIndexOnProviderAndKeyStaysAuthoritative()
    {
        var (initializer, _) = await CreateStoreAsync();
        await using var db = initializer.CreateDbContext();
        db.Events.Add(EntityFor("evt-a", "UcdpCandidate", "5"));
        db.Events.Add(EntityFor("evt-b", "UcdpCandidate", "5"));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task RecordsAbsentFromALaterReleaseAreNotChanged()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.PersistUcdpCandidateReleaseAsync(Imports(July, Record(1, "2026-07-03"), Record(2, "2026-07-04")), July.Ordinal);

        await store.PersistUcdpCandidateReleaseAsync(Imports(August, Record(2, "2026-08-01")), August.Ordinal);

        await using var db = initializer.CreateDbContext();
        var absent = await db.Events.AsNoTracking().SingleAsync(item => item.Id == "evt-ucdp-cand-1");
        Assert.Equal(202607, absent.ProviderLastSeenRelease);
        Assert.Equal(EventStatus.Unverified.ToString(), absent.Status);
        Assert.Equal(2, await db.Events.CountAsync());
    }

    [Fact]
    public async Task UcdpInsertsAndUpdatesNeverCreateAlertsAndKeepWatchlistMemberships()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.PersistUcdpCandidateReleaseAsync(Imports(July, Record(1, "2026-07-03")), July.Ordinal);
        const string watchlistId = "wl-00000000000000000000000000000077";
        await using (var db = initializer.CreateDbContext())
        {
            db.Watchlists.Add(new WatchlistEntity { Id = watchlistId, Name = "Watched", NormalizedName = "WATCHED" });
            db.WatchlistEvents.Add(new WatchlistEventEntity { WatchlistId = watchlistId, EventId = "evt-ucdp-cand-1" });
            await db.SaveChangesAsync();
        }

        var alerts = new AlertService(new SqliteAlertStore(initializer.CreateDbContext));
        Assert.Equal(SetAlertRuleDisposition.CreatedEnabled, (await alerts.SetRuleEnabledAsync(watchlistId, true)).Disposition);

        var update = await store.PersistUcdpCandidateReleaseAsync(
            Imports(August, Record(1, "2026-08-01", best: 500, where: "Materially different")),
            August.Ordinal);
        await store.PersistUcdpCandidateReleaseAsync(Imports(August, Record(9, "2026-08-02")), August.Ordinal);

        Assert.Equal(1, update.UpdatedEvents);
        await using var check = initializer.CreateDbContext();
        Assert.Empty(await check.AlertOccurrences.ToListAsync());
        Assert.True(await check.WatchlistEvents.AnyAsync(item => item.WatchlistId == watchlistId && item.EventId == "evt-ucdp-cand-1"));
    }

    [Fact]
    public async Task WindowedQueryStillShowsANewerNonUcdpEventAheadOfOlderUcdpEvents()
    {
        var (initializer, store) = await CreateStoreAsync();
        await store.PersistUcdpCandidateReleaseAsync(
            Imports(August, Record(1, "2026-08-01"), Record(2, "2026-08-10"), Record(3, "2026-08-20", dateEnd: "2026-08-25")),
            August.Ordinal);
        await store.CreateEventAsync(ManualEvent("evt-recent-local", EventProvider.Manual, null, new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero)));
        await store.CreateEventAsync(ManualEvent("evt-old-local", EventProvider.Manual, null, new DateTimeOffset(2026, 7, 1, 8, 0, 0, TimeSpan.Zero)));
        var service = new EventWindowService(store, new FixedTimeProvider(RetrievedAt), cap: 2);

        var window = await service.QueryAsync(new EventWindowRequest(EventTimeRange.All));

        Assert.Equal(["evt-recent-local", "evt-ucdp-cand-3"], window.Events.Select(item => item.Id));
        Assert.Equal(5, window.TotalCount);
        Assert.True(window.Capped);
        Assert.Equal(202608, window.Events[1].ProviderLastSeenRelease);
    }

    [Fact]
    public async Task MigrationAddsANullableColumnWithoutDataLossAndModelMatchesSnapshot()
    {
        Directory.CreateDirectory(directory);
        var initializer = new LocalDatabaseInitializer(Path.Combine(directory, "legacy.db"));
        await using (var legacy = initializer.CreateDbContext())
        {
            await legacy.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await LegacyEventRows.InsertEventAsync(legacy, "evt-gdacs-eq-42", "Quake", "Summary", "NaturalDisaster", "Unverified", RetrievedAt, RetrievedAt, 10, 20, "Place", null, null, RetrievedAt, RetrievedAt);
            await legacy.Database.ExecuteSqlRawAsync("""UPDATE "Events" SET "Provider" = 'Gdacs', "ProviderKey" = '42', "Severity" = 'High', "SeverityRule" = 'gdacs-eq-v1', "SeverityInputs" = 'alert=Orange'""");
        }

        await initializer.InitializeAsync();

        await using var db = initializer.CreateDbContext();
        Assert.Contains(UcdpMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        var row = await db.Events.AsNoTracking().SingleAsync();
        Assert.Equal(("Quake", "Gdacs", "42", "High"), (row.Title, row.Provider, row.ProviderKey, row.Severity));
        Assert.Null(row.ProviderLastSeenRelease);

        var column = db.Model.FindEntityType(typeof(EventEntity))!.FindProperty(nameof(EventEntity.ProviderLastSeenRelease))!;
        Assert.True(column.IsNullable);
        Assert.Equal(typeof(int?), column.ClrType);
    }

    [Fact]
    public async Task ImportsWithInconsistentIdentityAreRejected()
    {
        var (_, store) = await CreateStoreAsync();
        var imports = Imports(August, Record(1, "2026-08-03"));

        await Assert.ThrowsAsync<ArgumentException>(() => store.PersistUcdpCandidateReleaseAsync(imports, July.Ordinal));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.PersistUcdpCandidateReleaseAsync(imports, 202613));
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
        var initializer = new LocalDatabaseInitializer(Path.Combine(directory, "ucdp.db"));
        await initializer.InitializeAsync();
        return (initializer, new SqliteEventStore(initializer.CreateDbContext));
    }

    private static IReadOnlyList<UcdpCandidateImport> Imports(UcdpRelease release, params UcdpCandidateRecord[] records)
        => UcdpCandidateNormalizer.Normalize(records, release, RetrievedAt).Imports;

    private static UcdpCandidateRecord Record(long id, string dateStart, string? dateEnd = null, int best = 12, int type = 1, string? where = null)
        => new(
            id,
            "Clear",
            type,
            "A - B",
            "A",
            "B",
            1,
            where ?? $"Place {id}",
            10 + id,
            20 + id,
            "Testland",
            "Africa",
            1,
            DateOnly.Parse(dateStart),
            DateOnly.Parse(dateEnd ?? dateStart),
            best,
            best + 2,
            Math.Max(0, best - 2));

    private static Event ManualEvent(string id, EventProvider provider, string? key, DateTimeOffset lastUpdated)
        => Event.Create(
            new EventId(id),
            "Manual title",
            "Manual summary",
            EventCategory.Security,
            EventStatus.Active,
            lastUpdated.AddHours(-1),
            lastUpdated,
            new Location(1, 2, "Somewhere"),
            lastUpdated,
            lastUpdated,
            new EventSeverityAssessment(EventSeverity.Low, EventSeverityAssessment.ManualRule, "test"),
            new EventProvenance(provider, key));

    private static EventEntity EntityFor(string id, string provider, string key) => new()
    {
        Id = id,
        Title = "T",
        Summary = "S",
        Category = "Security",
        Status = "Active",
        StartedAtUtc = RetrievedAt,
        LastUpdatedAtUtc = RetrievedAt,
        LocationDisplayName = "Place",
        CreatedAtUtc = RetrievedAt,
        UpdatedAtUtc = RetrievedAt,
        Severity = "Low",
        SeverityRule = "manual",
        SeverityInputs = "test",
        Provider = provider,
        ProviderKey = key,
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
