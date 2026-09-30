using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.ReliefWeb;
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

public sealed class SqliteEventStoreTests : IDisposable
{
    private static readonly DateTimeOffset AttachmentTime = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public SqliteEventStoreTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "geopolitical-monitor-test.db");
    }

    [Fact]
    public async Task RoundTripsEventSourceAndAssociation()
    {
        var service = await CreateMigratedServiceAsync();
        var geopoliticalEvent = CreateEvent("evt-one", 10, 20, EventCategory.Security, EventStatus.Active);
        var xPost = CreateSource("src-x", SourceType.XPost);
        var article = CreateSource("src-news", SourceType.NewsArticle);

        await service.CreateEventAsync(geopoliticalEvent);
        await service.CreateSourceItemAsync(xPost);
        await service.CreateSourceItemAsync(article);
        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, xPost.Id);
        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, article.Id);

        var events = await service.GetEventsAsync();
        var roundTripped = Assert.Single(events);
        Assert.Equal("evt-one", roundTripped.Id);
        Assert.Equal("Security", roundTripped.Category);
        Assert.Equal("Active", roundTripped.Status);
        Assert.Equal(10, roundTripped.Location.Latitude);
        Assert.Equal(20, roundTripped.Location.Longitude);
        Assert.Equal(2, roundTripped.SourceCount);

        var sources = await service.GetSourcesForEventAsync("evt-one");
        Assert.Equal(2, sources.Count);
        Assert.Contains(sources, item => item.SourceType == "XPost");
        Assert.Contains(sources, item => item.SourceType == "NewsArticle");
    }

    [Fact]
    public async Task DuplicateAttachmentDoesNotCreateDuplicateAssociation()
    {
        var service = await CreateMigratedServiceAsync();
        var geopoliticalEvent = CreateEvent("evt-one", 10, 20, EventCategory.Security, EventStatus.Active);
        var source = CreateSource("src-x", SourceType.XPost);

        await service.CreateEventAsync(geopoliticalEvent);
        await service.CreateSourceItemAsync(source);
        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);
        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);

        var sources = await service.GetSourcesForEventAsync("evt-one");
        Assert.Single(sources);
    }

    [Fact]
    public async Task ReliefWebAttachmentIsTransactionalIdempotentAndReusableAcrossEvents()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var store = new SqliteEventStore(initializer.CreateDbContext);
        var firstEvent = CreateEvent("evt-one", 10, 20, EventCategory.Humanitarian, EventStatus.Active);
        var secondEvent = CreateEvent("evt-two", 30, 40, EventCategory.Humanitarian, EventStatus.Developing);
        await store.CreateEventAsync(firstEvent);
        await store.CreateEventAsync(secondEvent);
        var source = CreateReliefWebSource("Original local metadata");

        var created = await store.AttachReliefWebSourceAsync(firstEvent.Id, source);
        var duplicate = await store.AttachReliefWebSourceAsync(firstEvent.Id, CreateReliefWebSource("Provider changed later"));
        var reused = await store.AttachReliefWebSourceAsync(secondEvent.Id, CreateReliefWebSource("Provider changed later"));

        Assert.Equal(ReliefWebAttachmentStoreDisposition.CreatedAndAttached, created.Disposition);
        Assert.Equal(ReliefWebAttachmentStoreDisposition.AlreadyAttached, duplicate.Disposition);
        Assert.Equal(ReliefWebAttachmentStoreDisposition.ExistingSourceAttached, reused.Disposition);
        await using (var dbContext = initializer.CreateDbContext())
        {
            Assert.Equal(1, await dbContext.SourceItems.CountAsync());
            Assert.Equal(2, await dbContext.EventSourceAssociations.CountAsync());
            Assert.Equal("Original local metadata", (await dbContext.SourceItems.SingleAsync()).Text);
        }

        var restartedStore = new SqliteEventStore(initializer.CreateDbContext);
        Assert.Single(await restartedStore.GetSourcesForEventAsync(secondEvent.Id));
    }

    [Fact]
    public async Task ReliefWebAttachmentRejectsCollisionAndMissingEventWithoutPartialState()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var store = new SqliteEventStore(initializer.CreateDbContext);
        var geopoliticalEvent = CreateEvent("evt-one", 10, 20, EventCategory.Humanitarian, EventStatus.Active);
        await store.CreateEventAsync(geopoliticalEvent);
        await store.CreateSourceItemAsync(SourceItem.Create(
            new SourceItemId("src-reliefweb-report-5187658"), SourceType.NewsArticle, "unrelated",
            "Collision", "Collision", null, null, null, AttachmentTime, AttachmentTime, null, AttachmentTime));

        var conflict = await store.AttachReliefWebSourceAsync(geopoliticalEvent.Id, CreateReliefWebSource("Metadata"));
        var missing = await store.AttachReliefWebSourceAsync(new EventId("evt-missing"),
            CreateReliefWebSource("Metadata", "src-reliefweb-report-5187659", "ReliefWeb:Report:5187659"));

        Assert.Equal(ReliefWebAttachmentStoreDisposition.Conflict, conflict.Disposition);
        Assert.Equal(ReliefWebAttachmentStoreDisposition.EventNotFound, missing.Disposition);
        await using var dbContext = initializer.CreateDbContext();
        Assert.Single(await dbContext.SourceItems.ToListAsync());
        Assert.Empty(await dbContext.EventSourceAssociations.ToListAsync());
    }

    [Fact]
    public async Task SourceRetrievalOnlyReturnsSourcesForRequestedEvent()
    {
        var service = await CreateMigratedServiceAsync();
        var firstEvent = CreateEvent("evt-one", 10, 20, EventCategory.Security, EventStatus.Active);
        var secondEvent = CreateEvent("evt-two", 30, 40, EventCategory.Humanitarian, EventStatus.Developing);
        var firstSource = CreateSource("src-one", SourceType.XPost);
        var secondSource = CreateSource("src-two", SourceType.NewsArticle);

        await service.CreateEventAsync(firstEvent);
        await service.CreateEventAsync(secondEvent);
        await service.CreateSourceItemAsync(firstSource);
        await service.CreateSourceItemAsync(secondSource);
        await service.AttachSourceToEventAsync(firstEvent.Id, firstSource.Id);
        await service.AttachSourceToEventAsync(secondEvent.Id, secondSource.Id);

        var sources = await service.GetSourcesForEventAsync("evt-one");

        var source = Assert.Single(sources);
        Assert.Equal("src-one", source.Id);
    }

    [Fact]
    public async Task ManualXPostAttachmentPersistsOnceAndReusesSourceAcrossEvents()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var service = new EventCatalogService(
            new SqliteEventStore(initializer.CreateDbContext),
            new FixedTimeProvider(AttachmentTime));
        await service.CreateEventAsync(CreateEvent("evt-one", 10, 20, EventCategory.Security, EventStatus.Active));
        await service.CreateEventAsync(CreateEvent("evt-two", 30, 40, EventCategory.Humanitarian, EventStatus.Developing));

        var created = await service.AttachXPostSourceToEventAsync(
            "evt-one",
            "https://x.com/example/status/1759288472993702104");
        var duplicate = await service.AttachXPostSourceToEventAsync(
            "evt-one",
            "https://www.x.com/example/status/1759288472993702104?ref=share");
        var reused = await service.AttachXPostSourceToEventAsync(
            "evt-two",
            "https://x.com/example/status/1759288472993702104");

        Assert.Equal(AttachXPostSourceDisposition.CreatedAndAttached, created.Disposition);
        Assert.Equal(AttachXPostSourceDisposition.AlreadyAttached, duplicate.Disposition);
        Assert.Equal(AttachXPostSourceDisposition.ExistingSourceAttached, reused.Disposition);

        await using (var dbContext = initializer.CreateDbContext())
        {
            var source = Assert.Single(await dbContext.SourceItems.ToListAsync());
            Assert.Equal("XPost", source.SourceType);
            Assert.Equal("1759288472993702104", source.ExternalId);
            Assert.Equal("https://x.com/example/status/1759288472993702104", source.Url);
            Assert.Equal("X post 1759288472993702104", source.Text);
            Assert.Equal(new DateTimeOffset(2024, 2, 18, 18, 47, 12, 617, TimeSpan.Zero), source.PublishedAtUtc);
            Assert.Equal(AttachmentTime, source.RetrievedAtUtc);
            Assert.Equal(AttachmentTime, source.CreatedAtUtc);
            Assert.Equal(2, await dbContext.EventSourceAssociations.CountAsync());
        }

        var restartedService = CreateService(initializer);
        await new DemoDataSeeder(restartedService).SeedAsync();
        var restartedSources = await restartedService.GetSourcesForEventAsync("evt-two");
        var restartedSource = Assert.Single(restartedSources);
        Assert.Equal("1759288472993702104", restartedSource.ExternalId);
    }

    [Fact]
    public async Task MissingEventLeavesNoPartialXPostSource()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var service = new EventCatalogService(
            new SqliteEventStore(initializer.CreateDbContext),
            new FixedTimeProvider(AttachmentTime));

        var result = await service.AttachXPostSourceToEventAsync(
            "evt-missing",
            "https://x.com/example/status/1759288472993702104");

        Assert.Equal(AttachXPostSourceDisposition.EventNotFound, result.Disposition);
        await using var dbContext = initializer.CreateDbContext();
        Assert.Empty(await dbContext.SourceItems.ToListAsync());
        Assert.Empty(await dbContext.EventSourceAssociations.ToListAsync());
    }

    [Fact]
    public async Task SeedIsIdempotent()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        var service = CreateService(initializer);
        var seeder = new DemoDataSeeder(service);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        await using var dbContext = initializer.CreateDbContext();
        Assert.Equal(6, await dbContext.Events.CountAsync());
        Assert.Equal(14, await dbContext.SourceItems.CountAsync());
        Assert.Equal(14, await dbContext.EventSourceAssociations.CountAsync());
    }

    [Fact]
    public async Task FreshDatabaseMigrationCreatesAllPersistenceTablesAndReadWriteWorks()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();

        await using var dbContext = initializer.CreateDbContext();
        var tableNames = await dbContext.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();
        Assert.Contains("ApplicationPreferences", tableNames);
        Assert.Contains("Events", tableNames);
        Assert.Contains("SourceItems", tableNames);
        Assert.Contains("EventSourceAssociations", tableNames);

        var service = CreateService(initializer);
        var geopoliticalEvent = CreateEvent("evt-fresh", 1, 2, EventCategory.Other, EventStatus.Unverified);
        var source = CreateSource("src-fresh", SourceType.DatasetRecord);
        await service.CreateEventAsync(geopoliticalEvent);
        await service.CreateSourceItemAsync(source);
        await service.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);

        Assert.Single(await service.GetSourcesForEventAsync("evt-fresh"));
    }

    [Fact]
    public async Task ExistingTask004DatabaseKeepsAccentPreferenceWhenTask005MigrationApplies()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await using (var dbContext = initializer.CreateDbContext())
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            await dbContext.Database.GetService<IMigrator>().MigrateAsync("20260917000000_InitialLocalPersistence");
            dbContext.ApplicationPreferences.Add(new ApplicationPreferenceEntity
            {
                Key = AccentThemePreferenceService.PreferenceKey,
                Value = AccentTheme.Blue.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        await initializer.InitializeAsync();

        var preferenceService = new AccentThemePreferenceService(new SqliteAccentThemePreferenceStore(initializer.CreateDbContext));
        Assert.Equal(AccentTheme.Blue, await preferenceService.GetAccentThemeAsync());

        var eventService = CreateService(initializer);
        var geopoliticalEvent = CreateEvent("evt-upgrade", 5, 6, EventCategory.Economic, EventStatus.Developing);
        var source = CreateSource("src-upgrade", SourceType.Other);
        await eventService.CreateEventAsync(geopoliticalEvent);
        await eventService.CreateSourceItemAsync(source);
        await eventService.AttachSourceToEventAsync(geopoliticalEvent.Id, source.Id);

        Assert.Single(await eventService.GetSourcesForEventAsync("evt-upgrade"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private async Task<EventCatalogService> CreateMigratedServiceAsync()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await initializer.InitializeAsync();
        return CreateService(initializer);
    }

    private static EventCatalogService CreateService(LocalDatabaseInitializer initializer)
    {
        return new EventCatalogService(new SqliteEventStore(initializer.CreateDbContext));
    }

    private static Event CreateEvent(string id, double latitude, double longitude, EventCategory category, EventStatus status)
    {
        var timestamp = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
        return Event.Create(new EventId(id), "Demo event", "Demo summary", category, status, timestamp, timestamp.AddMinutes(5), new Location(latitude, longitude, "Demo place"), timestamp, timestamp.AddMinutes(5), new EventSeverityAssessment(EventSeverity.Medium, EventSeverityAssessment.ManualRule, "test"), new EventProvenance(EventProvider.Manual));
    }

    private static SourceItem CreateSource(string id, SourceType sourceType)
    {
        var timestamp = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.Zero);
        return SourceItem.Create(new SourceItemId(id), sourceType, id, "Demo source", "Demo source text", null, "Demo Author", "@demo", timestamp, timestamp.AddMinutes(3), new Location(1, 2, "Source place"), timestamp);
    }

    private static SourceItem CreateReliefWebSource(
        string text,
        string id = "src-reliefweb-report-5187658",
        string externalId = "ReliefWeb:Report:5187658")
    {
        return SourceItem.Create(
            new SourceItemId(id),
            SourceType.Other,
            externalId,
            "Flood response situation report",
            text,
            new Uri("https://reliefweb.int/report/example/flood-response"),
            "EHO",
            null,
            AttachmentTime.AddDays(-1),
            AttachmentTime,
            null,
            AttachmentTime);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
