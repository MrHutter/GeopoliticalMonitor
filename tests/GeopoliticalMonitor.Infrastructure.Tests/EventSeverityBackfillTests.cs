using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class EventSeverityBackfillTests : IDisposable
{
    private const string PreviousMigration = "20260927164002_AddAlertsFoundation";
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] DemoEventIds =
    [
        "evt-demo-baltic-security",
        "evt-demo-red-sea-shipping",
        "evt-demo-europe-diplomatic",
        "evt-demo-east-asia-infrastructure",
        "evt-demo-north-africa-civil",
        "evt-demo-humanitarian-crossing",
    ];

    private readonly string directory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorSeverityTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task BackfillAssignsSeverityAndProvenanceToRowsFromThePreviousSchema()
    {
        var initializer = await CreateLegacyDatabaseAsync("backfill.db", async db =>
        {
            await InsertLegacyEventAsync(db, "evt-gdacs-eq-100");
            await InsertGdacsSourceAsync(db, "evt-gdacs-eq-100", "100", 1, GdacsText("Orange", "6.4 M"), Now);

            await InsertLegacyEventAsync(db, "evt-gdacs-eq-200");
            await InsertGdacsSourceAsync(db, "evt-gdacs-eq-200", "200", 1, GdacsText("Green", "5.0 M"), Now.AddHours(-2));
            await InsertGdacsSourceAsync(db, "evt-gdacs-eq-200", "200", 2, GdacsText("Red", "7.6 M"), Now);

            await InsertLegacyEventAsync(db, "evt-gdacs-eq-300");

            await InsertLegacyEventAsync(db, "evt-gdacs-eq-400");
            await InsertGdacsSourceAsync(db, "evt-gdacs-eq-400", "400", 1, "Provider record without structured lines", Now);

            await InsertLegacyEventAsync(db, "evt-gdacs-eq-abc");
            await InsertLegacyEventAsync(db, "evt-demo-red-sea-shipping");
            await InsertLegacyEventAsync(db, "evt-demo-not-in-mapping");
            await InsertLegacyEventAsync(db, "evt-local-note");
        });

        await initializer.InitializeAsync();

        var rows = await ReadEventsAsync(initializer);
        AssertRow(rows["evt-gdacs-eq-100"], "High", GdacsSeverityRule.RuleId, "alert=Orange; magnitude=6.4", "Gdacs", "100");
        AssertRow(rows["evt-gdacs-eq-200"], "Catastrophic", GdacsSeverityRule.RuleId, "alert=Red; magnitude=7.6", "Gdacs", "200");
        AssertRow(rows["evt-gdacs-eq-300"], "Low", EventSeverityAssessment.LegacyDefaultRule, GdacsSeverityRule.BackfillUnavailableInputs, "Gdacs", "300");
        AssertRow(rows["evt-gdacs-eq-400"], "Low", EventSeverityAssessment.LegacyDefaultRule, GdacsSeverityRule.BackfillUnavailableInputs, "Gdacs", "400");
        AssertRow(rows["evt-gdacs-eq-abc"], "Low", EventSeverityAssessment.LegacyDefaultRule, EventSeverityBackfill.NoProviderDataInputs, "Manual", null);
        AssertRow(rows["evt-demo-red-sea-shipping"], "High", EventSeverityAssessment.ManualRule, DemoEventSeverities.Inputs, "Demo", null);
        AssertRow(rows["evt-demo-not-in-mapping"], "Low", EventSeverityAssessment.LegacyDefaultRule, EventSeverityBackfill.NoProviderDataInputs, "Demo", null);
        AssertRow(rows["evt-local-note"], "Low", EventSeverityAssessment.LegacyDefaultRule, EventSeverityBackfill.NoProviderDataInputs, "Manual", null);
        Assert.Equal(Now, rows["evt-gdacs-eq-100"].LastUpdatedAtUtc);
        Assert.Equal(Now, rows["evt-gdacs-eq-100"].UpdatedAtUtc);

        var eventStore = new SqliteEventStore(initializer.CreateDbContext);
        var domainEvents = await eventStore.GetEventsAsync();
        Assert.Equal(8, domainEvents.Count);
        Assert.Equal(EventSeverity.Catastrophic, domainEvents.Single(item => item.Id.Value == "evt-gdacs-eq-200").Severity);
    }

    [Fact]
    public async Task BackfillIsIdempotentAcrossRestarts()
    {
        var initializer = await CreateLegacyDatabaseAsync("idempotent.db", async db =>
        {
            await InsertLegacyEventAsync(db, "evt-gdacs-eq-100");
            await InsertGdacsSourceAsync(db, "evt-gdacs-eq-100", "100", 1, GdacsText("Orange", "6.4 M"), Now);
            await InsertLegacyEventAsync(db, "evt-gdacs-eq-300");
            await InsertLegacyEventAsync(db, "evt-demo-baltic-security");
            await InsertLegacyEventAsync(db, "evt-local-note");
        });

        await initializer.InitializeAsync();
        var first = await ReadEventsAsync(initializer);

        await initializer.InitializeAsync();
        var second = await ReadEventsAsync(initializer);
        await using var db = initializer.CreateDbContext();
        var rerun = await EventSeverityBackfill.RunAsync(db);

        Assert.Equal(0, rerun);
        Assert.Equal(first.Keys.Order(), second.Keys.Order());
        foreach (var (id, row) in first)
        {
            var again = second[id];
            Assert.Equal(row.Severity, again.Severity);
            Assert.Equal(row.SeverityRule, again.SeverityRule);
            Assert.Equal(row.SeverityInputs, again.SeverityInputs);
            Assert.Equal(row.Provider, again.Provider);
            Assert.Equal(row.ProviderKey, again.ProviderKey);
            Assert.Equal(row.UpdatedAtUtc, again.UpdatedAtUtc);
        }

        Assert.DoesNotContain(second.Values, item => item.SeverityInputs == EventSeverityBackfill.PendingInputs);
    }

    [Fact]
    public async Task SeederAndBackfillAssignTheSameDemoSeverities()
    {
        var seeded = new LocalDatabaseInitializer(Path.Combine(directory, "seeded.db"));
        await seeded.InitializeAsync();
        await new DemoDataSeeder(new EventCatalogService(new SqliteEventStore(seeded.CreateDbContext))).SeedAsync();

        var legacy = await CreateLegacyDatabaseAsync("legacy-demo.db", async db =>
        {
            foreach (var id in DemoEventIds)
            {
                await InsertLegacyEventAsync(db, id);
            }
        });
        await legacy.InitializeAsync();

        var seededRows = await ReadEventsAsync(seeded);
        var backfilledRows = await ReadEventsAsync(legacy);
        Assert.Equal(DemoEventIds.Order(), seededRows.Keys.Order());
        foreach (var id in DemoEventIds)
        {
            Assert.Equal(seededRows[id].Severity, backfilledRows[id].Severity);
            AssertRow(seededRows[id], seededRows[id].Severity, EventSeverityAssessment.ManualRule, DemoEventSeverities.Inputs, "Demo", null);
            AssertRow(backfilledRows[id], seededRows[id].Severity, EventSeverityAssessment.ManualRule, DemoEventSeverities.Inputs, "Demo", null);
        }
    }

    [Fact]
    public async Task UniqueIndexRejectsDuplicateProviderKeyButAllowsMissingKeys()
    {
        var initializer = new LocalDatabaseInitializer(Path.Combine(directory, "unique.db"));
        await initializer.InitializeAsync();

        await using (var db = initializer.CreateDbContext())
        {
            db.Events.Add(CreateEntity("evt-manual-1", "Manual", null));
            db.Events.Add(CreateEntity("evt-manual-2", "Manual", null));
            db.Events.Add(CreateEntity("evt-gdacs-eq-42", "Gdacs", "42"));
            await db.SaveChangesAsync();
        }

        await using var duplicate = initializer.CreateDbContext();
        duplicate.Events.Add(CreateEntity("evt-other-42", "Gdacs", "42"));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
    }

    [Fact]
    public async Task StoreRoundTripsSeverityAndProvenanceThroughDomainAndDto()
    {
        var initializer = new LocalDatabaseInitializer(Path.Combine(directory, "roundtrip.db"));
        await initializer.InitializeAsync();
        var store = new SqliteEventStore(initializer.CreateDbContext);
        var geopoliticalEvent = Event.Create(
            new EventId("evt-roundtrip"),
            "Round trip",
            "Summary",
            EventCategory.Security,
            EventStatus.Active,
            Now,
            Now,
            new Location(1, 2, "Place"),
            Now,
            Now,
            new EventSeverityAssessment(EventSeverity.Critical, EventSeverityAssessment.ManualRule, "hand entered"),
            new EventProvenance(EventProvider.Manual));

        await store.CreateEventAsync(geopoliticalEvent);
        var dto = await new EventCatalogService(store).GetEventByIdAsync("evt-roundtrip");

        Assert.NotNull(dto);
        Assert.Equal("Critical", dto!.Severity);
        Assert.Equal(EventSeverityAssessment.ManualRule, dto.SeverityRule);
        Assert.Equal("hand entered", dto.SeverityInputs);
        Assert.Equal("Manual", dto.Provider);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private async Task<LocalDatabaseInitializer> CreateLegacyDatabaseAsync(string fileName, Func<GeopoliticalMonitorDbContext, Task> seed)
    {
        Directory.CreateDirectory(directory);
        var initializer = new LocalDatabaseInitializer(Path.Combine(directory, fileName));
        await using var db = initializer.CreateDbContext();
        await db.Database.GetService<IMigrator>().MigrateAsync(PreviousMigration);
        await seed(db);
        return initializer;
    }

    private static Task InsertLegacyEventAsync(GeopoliticalMonitorDbContext db, string id)
        => LegacyEventRows.InsertEventAsync(
            db,
            id,
            $"Title {id}",
            "Summary",
            EventCategory.NaturalDisaster.ToString(),
            EventStatus.Unverified.ToString(),
            Now.AddHours(-1),
            Now,
            10,
            20,
            "Place",
            null,
            null,
            Now.AddDays(-1),
            Now);

    private static Task InsertGdacsSourceAsync(GeopoliticalMonitorDbContext db, string eventId, string providerKey, int episode, string text, DateTimeOffset publishedAtUtc)
        => LegacyEventRows.InsertSourceForEventAsync(
            db,
            eventId,
            $"src-gdacs-eq-{providerKey}-{episode}",
            "DatasetRecord",
            $"{GdacsRefreshService.ExternalIdPrefix}{providerKey}:{episode}",
            text,
            $"https://www.gdacs.org/report.aspx?eventid={providerKey}&episodeid={episode}&eventtype=EQ",
            publishedAtUtc);

    private static string GdacsText(string alertLevel, string magnitude)
        => string.Join(
            Environment.NewLine,
            $"Provider: {GdacsRefreshService.Attribution}",
            "Record: EQ-1, episode 1",
            $"GDACS alert level: {alertLevel}",
            $"Magnitude: {magnitude}",
            "Country: Example");

    private static async Task<Dictionary<string, EventEntity>> ReadEventsAsync(LocalDatabaseInitializer initializer)
    {
        await using var db = initializer.CreateDbContext();
        return await db.Events.AsNoTracking().ToDictionaryAsync(item => item.Id);
    }

    private static void AssertRow(EventEntity row, string severity, string rule, string inputs, string provider, string? providerKey)
    {
        Assert.Equal(severity, row.Severity);
        Assert.Equal(rule, row.SeverityRule);
        Assert.Equal(inputs, row.SeverityInputs);
        Assert.Equal(provider, row.Provider);
        Assert.Equal(providerKey, row.ProviderKey);
    }

    private static EventEntity CreateEntity(string id, string provider, string? providerKey) => new()
    {
        Id = id,
        Title = "Title",
        Summary = "Summary",
        Category = EventCategory.Other.ToString(),
        Status = EventStatus.Active.ToString(),
        StartedAtUtc = Now,
        LastUpdatedAtUtc = Now,
        Latitude = 1,
        Longitude = 2,
        LocationDisplayName = "Place",
        CreatedAtUtc = Now,
        UpdatedAtUtc = Now,
        Severity = EventSeverity.Low.ToString(),
        SeverityRule = EventSeverityAssessment.ManualRule,
        SeverityInputs = "test",
        Provider = provider,
        ProviderKey = providerKey,
    };
}
