using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class AlertPersistenceTests : IDisposable
{
    private const string WatchlistId = "wl-00000000000000000000000000000001";
    private const string AlertRuleId = "ar-00000000000000000000000000000001";
    private const string AlertId = "al-00000000000000000000000000000001";
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorAlertTests", Guid.NewGuid().ToString("N"));
    private readonly string databasePath;

    public AlertPersistenceTests()
    {
        databasePath = Path.Combine(temporaryDirectory, "test.db");
    }

    [Fact]
    public async Task MigrationCreatesLockedTablesKeysForeignKeysAndIndexes()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await using var db = initializer.CreateDbContext();
        var ruleType = db.Model.FindEntityType(typeof(AlertRuleEntity))!;
        var occurrenceType = db.Model.FindEntityType(typeof(AlertOccurrenceEntity))!;

        Assert.Equal([nameof(AlertRuleEntity.Id)], ruleType.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.Equal([nameof(AlertOccurrenceEntity.Id)], occurrenceType.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.All(ruleType.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior));
        Assert.Equal(2, occurrenceType.GetForeignKeys().Count());
        Assert.All(occurrenceType.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior));
        Assert.Contains(ruleType.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(item => item.Name).SequenceEqual([nameof(AlertRuleEntity.WatchlistId), nameof(AlertRuleEntity.TriggerType)]));
        Assert.Contains(occurrenceType.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(item => item.Name).SequenceEqual([
                nameof(AlertOccurrenceEntity.AlertRuleId),
                nameof(AlertOccurrenceEntity.EventId),
                nameof(AlertOccurrenceEntity.ProviderRevisionAtUtc)]));

        var tables = await db.Database.SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();
        Assert.Contains("AlertRules", tables);
        Assert.Contains("AlertOccurrences", tables);
    }

    [Fact]
    public async Task RuleLifecyclePersistsAcrossReopenAndDisablePreservesHistory()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await SeedEventAndWatchlistAsync(initializer);
        var store = new SqliteAlertStore(initializer.CreateDbContext);

        var created = await new AlertService(store, new FixedTimeProvider(Now)).SetRuleEnabledAsync(WatchlistId, true);
        var disabled = await new AlertService(store, new FixedTimeProvider(Now.AddMinutes(1))).SetRuleEnabledAsync(WatchlistId, false);
        await SeedOccurrenceAsync(initializer, created.Rule!.Id);
        var unchanged = await new AlertService(store, new FixedTimeProvider(Now.AddMinutes(2))).SetRuleEnabledAsync(WatchlistId, false);
        var enabled = await new AlertService(store, new FixedTimeProvider(Now.AddMinutes(3))).SetRuleEnabledAsync(WatchlistId, true);

        Assert.Equal(SetAlertRuleDisposition.CreatedEnabled, created.Disposition);
        Assert.Equal(SetAlertRuleDisposition.Disabled, disabled.Disposition);
        Assert.Equal(SetAlertRuleDisposition.Unchanged, unchanged.Disposition);
        Assert.Equal(SetAlertRuleDisposition.Enabled, enabled.Disposition);
        Assert.Equal(Now, disabled.Rule!.LastEnabledAtUtc);
        Assert.Equal(Now, unchanged.Rule!.LastEnabledAtUtc);
        Assert.Equal(Now.AddMinutes(3), enabled.Rule!.LastEnabledAtUtc);

        var reopened = new AlertService(new SqliteAlertStore(initializer.CreateDbContext));
        Assert.Single(await reopened.GetRulesAsync());
        Assert.Single((await reopened.GetAlertsAsync()).Alerts);
    }

    [Fact]
    public async Task RuleOperationsReportUnknownAndMissingWithoutCreatingDisabledRule()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        var service = new AlertService(new SqliteAlertStore(initializer.CreateDbContext), new FixedTimeProvider(Now));

        var unknown = await service.SetRuleEnabledAsync(WatchlistId, true);
        await SeedEventAndWatchlistAsync(initializer);
        var missing = await service.SetRuleEnabledAsync(WatchlistId, false);

        Assert.Equal(SetAlertRuleDisposition.WatchlistNotFound, unknown.Disposition);
        Assert.Equal(SetAlertRuleDisposition.RuleNotFound, missing.Disposition);
        Assert.Empty(await service.GetRulesAsync());
    }

    [Fact]
    public async Task AlertListingIsNewestFirstAndReadStateIsPersistentAndIdempotent()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await SeedEventAndWatchlistAsync(initializer);
        var service = new AlertService(new SqliteAlertStore(initializer.CreateDbContext), new FixedTimeProvider(Now.AddHours(1)));
        var rule = await service.SetRuleEnabledAsync(WatchlistId, true);
        await SeedOccurrenceAsync(initializer, rule.Rule!.Id, AlertId, Now);
        await SeedOccurrenceAsync(initializer, rule.Rule.Id, "al-00000000000000000000000000000002", Now.AddMinutes(1));

        var initial = await service.GetAlertsAsync();
        var firstRead = await service.MarkReadAsync(AlertId);
        var repeated = await service.MarkReadAsync(AlertId);
        var reopened = await new AlertService(new SqliteAlertStore(initializer.CreateDbContext)).GetAlertsAsync();

        Assert.Equal(2, initial.UnreadCount);
        Assert.Equal("al-00000000000000000000000000000002", initial.Alerts[0].Id);
        Assert.Equal(MarkAlertReadDisposition.Updated, firstRead.Disposition);
        Assert.Equal(MarkAlertReadDisposition.AlreadyRead, repeated.Disposition);
        Assert.Equal(firstRead.ReadAtUtc, repeated.ReadAtUtc);
        Assert.Equal(1, reopened.UnreadCount);
        Assert.NotNull(reopened.Alerts.Single(item => item.Id == AlertId).ReadAtUtc);
    }

    [Fact]
    public async Task AlertListingIsBoundedToNewestTwoHundredAndReportsMore()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await SeedEventAndWatchlistAsync(initializer);
        await using (var db = initializer.CreateDbContext())
        {
            db.AlertRules.Add(CreateRuleEntity(AlertRuleId));
            db.AlertOccurrences.AddRange(Enumerable.Range(1, 201).Select(index =>
                CreateOccurrenceEntity($"al-{index:x32}", AlertRuleId, Now.AddMinutes(index))));
            await db.SaveChangesAsync();
        }

        var result = await new AlertService(new SqliteAlertStore(initializer.CreateDbContext)).GetAlertsAsync();

        Assert.Equal(200, result.Alerts.Count);
        Assert.Equal(201, result.UnreadCount);
        Assert.True(result.HasMore);
        Assert.Equal("al-000000000000000000000000000000c9", result.Alerts[0].Id);
        Assert.DoesNotContain(result.Alerts, item => item.Id == "al-00000000000000000000000000000001");
    }

    [Fact]
    public async Task DatabaseEnforcesRuleAndOccurrenceUniqueness()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await SeedEventAndWatchlistAsync(initializer);
        await using var db = initializer.CreateDbContext();
        db.AlertRules.Add(CreateRuleEntity(AlertRuleId));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.AlertRules.Add(CreateRuleEntity("ar-00000000000000000000000000000002"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.AlertOccurrences.Add(CreateOccurrenceEntity(AlertId, AlertRuleId));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.AlertOccurrences.Add(CreateOccurrenceEntity("al-00000000000000000000000000000002", AlertRuleId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task WatchlistAndEventCascadesPreserveUnrelatedData()
    {
        var initializer = await CreateInitializedDatabaseAsync();
        await SeedEventAndWatchlistAsync(initializer);
        await using (var db = initializer.CreateDbContext())
        {
            db.AlertRules.Add(CreateRuleEntity(AlertRuleId));
            db.AlertOccurrences.Add(CreateOccurrenceEntity(AlertId, AlertRuleId));
            await db.SaveChangesAsync();
        }

        await using (var db = initializer.CreateDbContext())
        {
            await db.AlertRules.Where(item => item.Id == AlertRuleId).ExecuteDeleteAsync();
        }

        await using (var db = initializer.CreateDbContext())
        {
            Assert.Empty(await db.AlertRules.ToListAsync());
            Assert.Empty(await db.AlertOccurrences.ToListAsync());
            Assert.Single(await db.Watchlists.ToListAsync());
            Assert.Single(await db.Events.ToListAsync());
            db.AlertRules.Add(CreateRuleEntity(AlertRuleId));
            db.AlertOccurrences.Add(CreateOccurrenceEntity(AlertId, AlertRuleId));
            await db.SaveChangesAsync();
        }

        await using (var db = initializer.CreateDbContext())
        {
            await db.Events.Where(item => item.Id == "evt-one").ExecuteDeleteAsync();
        }

        await using (var db = initializer.CreateDbContext())
        {
            Assert.Single(await db.Watchlists.ToListAsync());
            Assert.Single(await db.AlertRules.ToListAsync());
            Assert.Empty(await db.AlertOccurrences.ToListAsync());
            db.Events.Add(ToEntity(CreateEvent()));
            db.EventSourceAssociations.Add(new EventSourceAssociationEntity
            {
                EventId = "evt-one",
                SourceItemId = "src-one",
                CreatedAtUtc = Now,
            });
            db.AlertOccurrences.Add(CreateOccurrenceEntity(AlertId, AlertRuleId));
            await db.SaveChangesAsync();
            await db.Watchlists.Where(item => item.Id == WatchlistId).ExecuteDeleteAsync();
        }

        await using var verification = initializer.CreateDbContext();
        Assert.Empty(await verification.AlertRules.ToListAsync());
        Assert.Empty(await verification.AlertOccurrences.ToListAsync());
        Assert.Single(await verification.Events.ToListAsync());
        Assert.Single(await verification.SourceItems.ToListAsync());
        Assert.Single(await verification.EventSourceAssociations.ToListAsync());
    }

    [Fact]
    public async Task CurrentWatchlistSchemaUpgradesWithoutDataLossOrAlertSeed()
    {
        var initializer = new LocalDatabaseInitializer(databasePath);
        await using (var db = initializer.CreateDbContext())
        {
            Directory.CreateDirectory(temporaryDirectory);
            await db.Database.GetService<IMigrator>().MigrateAsync("20260926183055_AddWatchlistFoundation");
            await LegacyEventRows.InsertEventAsync(db, CreateEvent());
            db.Watchlists.Add(new WatchlistEntity { Id = WatchlistId, Name = "Before alerts", NormalizedName = "BEFORE ALERTS" });
            await db.SaveChangesAsync();
        }

        await initializer.InitializeAsync();

        await using var upgraded = initializer.CreateDbContext();
        Assert.True(await upgraded.Events.AnyAsync(item => item.Id == "evt-one"));
        Assert.True(await upgraded.Watchlists.AnyAsync(item => item.Id == WatchlistId));
        Assert.Empty(await upgraded.AlertRules.ToListAsync());
        Assert.Empty(await upgraded.AlertOccurrences.ToListAsync());
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

    private static async Task SeedEventAndWatchlistAsync(LocalDatabaseInitializer initializer)
    {
        var eventItem = CreateEvent();
        var source = SourceItem.Create(
            new SourceItemId("src-one"),
            SourceType.Other,
            "src-one",
            "Source",
            "Source text",
            null,
            null,
            null,
            Now,
            Now,
            null,
            Now);
        var eventStore = new SqliteEventStore(initializer.CreateDbContext);
        await eventStore.CreateEventAsync(eventItem);
        await eventStore.CreateSourceItemAsync(source);
        await eventStore.AttachSourceToEventAsync(eventItem.Id, source.Id);
        await using var db = initializer.CreateDbContext();
        db.Watchlists.Add(new WatchlistEntity { Id = WatchlistId, Name = "Monitored", NormalizedName = "MONITORED" });
        await db.SaveChangesAsync();
    }

    private static async Task SeedOccurrenceAsync(
        LocalDatabaseInitializer initializer,
        string ruleId,
        string alertId = AlertId,
        DateTimeOffset? triggeredAt = null)
    {
        await using var db = initializer.CreateDbContext();
        db.AlertOccurrences.Add(CreateOccurrenceEntity(alertId, ruleId, triggeredAt));
        await db.SaveChangesAsync();
    }

    private static AlertRuleEntity CreateRuleEntity(string id) => new()
    {
        Id = id,
        WatchlistId = WatchlistId,
        TriggerType = AlertTriggerType.GdacsEventMateriallyUpdated.ToString(),
        IsEnabled = true,
        CreatedAtUtc = Now,
        LastEnabledAtUtc = Now,
    };

    private static AlertOccurrenceEntity CreateOccurrenceEntity(
        string id,
        string ruleId,
        DateTimeOffset? triggeredAt = null) => new()
    {
        Id = id,
        AlertRuleId = ruleId,
        EventId = "evt-one",
        ProviderRevisionAtUtc = (triggeredAt ?? Now).AddMinutes(-1),
        TriggeredAtUtc = triggeredAt ?? Now,
    };

    private static Event CreateEvent()
        => Event.Create(
            new EventId("evt-one"),
            "Alert event",
            "Alert persistence event",
            EventCategory.Security,
            EventStatus.Active,
            Now,
            Now,
            new Location(1, 2, "Local"),
            Now,
            Now,
            new EventSeverityAssessment(EventSeverity.Low, EventSeverityAssessment.ManualRule, "test"),
            new EventProvenance(EventProvider.Manual));

    private static EventEntity ToEntity(Event eventItem) => new()
    {
        Id = eventItem.Id.Value,
        Title = eventItem.Title,
        Summary = eventItem.Summary,
        Category = eventItem.Category.ToString(),
        Status = eventItem.Status.ToString(),
        StartedAtUtc = eventItem.StartedAtUtc,
        LastUpdatedAtUtc = eventItem.LastUpdatedAtUtc,
        Latitude = eventItem.Location.Latitude,
        Longitude = eventItem.Location.Longitude,
        LocationDisplayName = eventItem.Location.DisplayName,
        CountryCode = eventItem.Location.CountryCode,
        Region = eventItem.Location.Region,
        CreatedAtUtc = eventItem.CreatedAtUtc,
        UpdatedAtUtc = eventItem.UpdatedAtUtc,
        Severity = eventItem.Severity.ToString(),
        SeverityRule = eventItem.SeverityRule,
        SeverityInputs = eventItem.SeverityInputs,
        Provider = eventItem.Provider.ToString(),
        ProviderKey = eventItem.ProviderKey,
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
