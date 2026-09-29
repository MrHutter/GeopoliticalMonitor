using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Domain.Watchlists;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class WatchlistPersistenceTests : IDisposable
{
    private const string FirstWatchlistId = "wl-00000000000000000000000000000001";
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorWatchlistTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public WatchlistPersistenceTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "test.db");
    }

    [Fact]
    public async Task MigrationCreatesRequiredTablesKeysForeignKeysAndIndexes()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();

        await using var db = initializer.CreateDbContext();
        var watchlistType = db.Model.FindEntityType(typeof(WatchlistEntity))!;
        var membershipType = db.Model.FindEntityType(typeof(WatchlistEventEntity))!;
        Assert.Equal([nameof(WatchlistEntity.Id)], watchlistType.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.Equal(
            [nameof(WatchlistEventEntity.WatchlistId), nameof(WatchlistEventEntity.EventId)],
            membershipType.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.Equal(2, membershipType.GetForeignKeys().Count());
        Assert.All(membershipType.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior));
        Assert.Contains(watchlistType.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(WatchlistEntity.NormalizedName));
        Assert.Contains(membershipType.GetIndexes(), index => index.Properties.Single().Name == nameof(WatchlistEventEntity.EventId));

        var tables = await db.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync();
        Assert.Contains("Watchlists", tables);
        Assert.Contains("WatchlistEvents", tables);
    }

    [Fact]
    public async Task PersistsWatchlistAndIdempotentMembershipAcrossReopen()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        var eventStore = new SqliteEventStore(initializer.CreateDbContext);
        await eventStore.CreateEventAsync(CreateEvent("evt-one"));
        var service = new WatchlistService(new SqliteWatchlistStore(initializer.CreateDbContext));

        var created = await service.CreateWatchlistAsync("Baltic Security");
        var first = await service.SetMembershipAsync(created.Watchlist!.Id, "evt-one", true);
        var duplicate = await service.SetMembershipAsync(created.Watchlist.Id, "evt-one", true);

        Assert.Equal(SetWatchlistMembershipDisposition.Added, first.Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.AlreadyMember, duplicate.Disposition);
        await using (var db = initializer.CreateDbContext())
        {
            Assert.Single(await db.WatchlistEvents.ToListAsync());
        }

        var reopened = new WatchlistService(new SqliteWatchlistStore(initializer.CreateDbContext));
        var persisted = Assert.Single(await reopened.GetWatchlistsAsync());
        Assert.Equal("Baltic Security", persisted.Name);
        Assert.Equal(1, persisted.EventCount);
        Assert.Equal(["evt-one"], (await reopened.GetEventIdsAsync(persisted.Id)).EventIds);
    }

    [Fact]
    public async Task DatabaseEnforcesNormalizedNameAndCompositeMembershipUniqueness()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await using var db = initializer.CreateDbContext();
        db.Events.Add(ToEntity(CreateEvent("evt-one")));
        db.Watchlists.Add(new WatchlistEntity { Id = FirstWatchlistId, Name = "Baltic", NormalizedName = "BALTIC" });
        await db.SaveChangesAsync();

        db.Watchlists.Add(new WatchlistEntity
        {
            Id = "wl-00000000000000000000000000000002",
            Name = "baltic",
            NormalizedName = "BALTIC",
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.WatchlistEvents.Add(new WatchlistEventEntity { WatchlistId = FirstWatchlistId, EventId = "evt-one" });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.WatchlistEvents.Add(new WatchlistEventEntity { WatchlistId = FirstWatchlistId, EventId = "evt-one" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task StoreEnforcesSuppliedCountLimitsWithoutLargeFixtures()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        var store = new SqliteWatchlistStore(initializer.CreateDbContext);
        var first = Watchlist.Create(new WatchlistId(FirstWatchlistId), "First");
        Assert.Equal(CreateWatchlistDisposition.Created, (await store.CreateWatchlistAsync(first, maximumWatchlists: 1)).Disposition);
        var second = Watchlist.Create(new WatchlistId("wl-00000000000000000000000000000002"), "Second");
        Assert.Equal(CreateWatchlistDisposition.WatchlistLimitReached, (await store.CreateWatchlistAsync(second, maximumWatchlists: 1)).Disposition);

        var eventStore = new SqliteEventStore(initializer.CreateDbContext);
        await eventStore.CreateEventAsync(CreateEvent("evt-one"));
        await eventStore.CreateEventAsync(CreateEvent("evt-two"));
        Assert.Equal(SetWatchlistMembershipDisposition.Added, (await store.SetMembershipAsync(first.Id, new EventId("evt-one"), true, maximumEventsPerWatchlist: 1)).Disposition);
        Assert.Equal(SetWatchlistMembershipDisposition.MembershipLimitReached, (await store.SetMembershipAsync(first.Id, new EventId("evt-two"), true, maximumEventsPerWatchlist: 1)).Disposition);
    }

    [Fact]
    public async Task DeletingWatchlistPreservesEventsSourcesAndTheirAssociations()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        var eventStore = new SqliteEventStore(initializer.CreateDbContext);
        var geopoliticalEvent = CreateEvent("evt-one");
        var source = CreateSource("src-one");
        await eventStore.CreateEventAsync(geopoliticalEvent);
        await eventStore.CreateSourceItemAsync(source);
        await eventStore.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);
        var watchlistService = new WatchlistService(new SqliteWatchlistStore(initializer.CreateDbContext));
        var created = await watchlistService.CreateWatchlistAsync("Local watch");
        await watchlistService.SetMembershipAsync(created.Watchlist!.Id, geopoliticalEvent.Id.Value, true);

        var deleted = await watchlistService.DeleteWatchlistAsync(created.Watchlist.Id);

        Assert.Equal(DeleteWatchlistDisposition.Deleted, deleted.Disposition);
        await using var db = initializer.CreateDbContext();
        Assert.Empty(await db.Watchlists.ToListAsync());
        Assert.Empty(await db.WatchlistEvents.ToListAsync());
        Assert.Single(await db.Events.ToListAsync());
        Assert.Single(await db.SourceItems.ToListAsync());
        Assert.Single(await db.EventSourceAssociations.ToListAsync());
    }

    [Fact]
    public async Task DirectFutureEventDeletionCascadesOnlyWatchlistMembership()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await using (var db = initializer.CreateDbContext())
        {
            db.Events.Add(ToEntity(CreateEvent("evt-one")));
            db.Watchlists.Add(new WatchlistEntity { Id = FirstWatchlistId, Name = "Local", NormalizedName = "LOCAL" });
            db.WatchlistEvents.Add(new WatchlistEventEntity { WatchlistId = FirstWatchlistId, EventId = "evt-one" });
            await db.SaveChangesAsync();
        }

        await using (var db = initializer.CreateDbContext())
        {
            await db.Events.Where(item => item.Id == "evt-one").ExecuteDeleteAsync();
        }

        await using var verification = initializer.CreateDbContext();
        Assert.Single(await verification.Watchlists.ToListAsync());
        Assert.Empty(await verification.WatchlistEvents.ToListAsync());
    }

    [Fact]
    public async Task SeedCreatesNoWatchlistsAndSeededEventCanBeAdded()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        var eventCatalog = new EventCatalogService(new SqliteEventStore(initializer.CreateDbContext));
        var seeder = new DemoDataSeeder(eventCatalog);
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        var watchlistService = new WatchlistService(new SqliteWatchlistStore(initializer.CreateDbContext));
        var eventId = (await eventCatalog.GetEventsAsync()).First().Id;
        var created = await watchlistService.CreateWatchlistAsync("Seeded event watch");

        var membership = await watchlistService.SetMembershipAsync(created.Watchlist!.Id, eventId, true);

        Assert.Equal(SetWatchlistMembershipDisposition.Added, membership.Disposition);
        await using var db = initializer.CreateDbContext();
        Assert.Equal(6, await db.Events.CountAsync());
        Assert.Equal(14, await db.SourceItems.CountAsync());
        Assert.Equal(14, await db.EventSourceAssociations.CountAsync());
        Assert.Single(await db.Watchlists.ToListAsync());
    }

    [Fact]
    public async Task ExistingEventSchemaUpgradesWithoutDataLoss()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await using (var db = initializer.CreateDbContext())
        {
            Directory.CreateDirectory(temporaryDirectory);
            await db.Database.GetService<IMigrator>().MigrateAsync("20260917010000_AddEventAndSourceFoundation");
            db.Events.Add(ToEntity(CreateEvent("evt-before-watchlists")));
            await db.SaveChangesAsync();
        }

        await initializer.InitializeAsync();

        await using var upgraded = initializer.CreateDbContext();
        Assert.True(await upgraded.Events.AnyAsync(item => item.Id == "evt-before-watchlists"));
        Assert.Empty(await upgraded.Watchlists.ToListAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<LocalDatabaseInitializer> CreateInitializedDatabaseAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return initializer;
    }

    private static Event CreateEvent(string id)
    {
        var timestamp = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        return Event.Create(
            new EventId(id),
            "Watchlist event",
            "Watchlist persistence event",
            EventCategory.Security,
            EventStatus.Active,
            timestamp,
            timestamp,
            new Location(1, 2, "Local"),
            timestamp,
            timestamp);
    }

    private static SourceItem CreateSource(string id)
    {
        var timestamp = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        return SourceItem.Create(
            new SourceItemId(id),
            SourceType.Other,
            id,
            "Source",
            "Source text",
            null,
            null,
            null,
            timestamp,
            timestamp,
            null,
            timestamp);
    }

    private static EventEntity ToEntity(Event geopoliticalEvent)
    {
        return new EventEntity
        {
            Id = geopoliticalEvent.Id.Value,
            Title = geopoliticalEvent.Title,
            Summary = geopoliticalEvent.Summary,
            Category = geopoliticalEvent.Category.ToString(),
            Status = geopoliticalEvent.Status.ToString(),
            StartedAtUtc = geopoliticalEvent.StartedAtUtc,
            LastUpdatedAtUtc = geopoliticalEvent.LastUpdatedAtUtc,
            Latitude = geopoliticalEvent.Location.Latitude,
            Longitude = geopoliticalEvent.Location.Longitude,
            LocationDisplayName = geopoliticalEvent.Location.DisplayName,
            CountryCode = geopoliticalEvent.Location.CountryCode,
            Region = geopoliticalEvent.Location.Region,
            CreatedAtUtc = geopoliticalEvent.CreatedAtUtc,
            UpdatedAtUtc = geopoliticalEvent.UpdatedAtUtc,
        };
    }
}
