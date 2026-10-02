using System.Diagnostics;
using System.Text.Json;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class EventWindowQueryTests(ITestOutputHelper output) : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly string directory = Path.Combine(Path.GetTempPath(), "GeopoliticalMonitorWindowTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task EmptyDatabaseReturnsNothing()
    {
        var (_, service) = await CreateAsync(cap: 3);

        var result = await service.QueryAsync(new EventWindowRequest(KeepEventId: "evt-missing"));

        Assert.Empty(result.Events);
        Assert.Equal(0, result.TotalCount);
        Assert.False(result.Capped);
    }

    [Theory]
    [InlineData(EventTimeRange.Last24Hours, 24)]
    [InlineData(EventTimeRange.Last7Days, 7 * 24)]
    [InlineData(EventTimeRange.Last30Days, 30 * 24)]
    [InlineData(EventTimeRange.Last90Days, 90 * 24)]
    public async Task TimeRangeCutoffIsInclusiveOnStartedAt(EventTimeRange range, int hours)
    {
        var (store, service) = await CreateAsync(cap: 10);
        var cutoff = Now.AddHours(-hours);
        await store.CreateEventAsync(CreateEvent("evt-at-cutoff", startedAt: cutoff));
        await store.CreateEventAsync(CreateEvent("evt-inside", startedAt: cutoff.AddSeconds(1)));
        await store.CreateEventAsync(CreateEvent("evt-outside", startedAt: cutoff.AddSeconds(-1)));

        var bounded = await service.QueryAsync(new EventWindowRequest(range));
        var all = await service.QueryAsync(new EventWindowRequest(EventTimeRange.All));

        Assert.Equal(["evt-at-cutoff", "evt-inside"], Ids(bounded).Order());
        Assert.Equal(2, bounded.TotalCount);
        Assert.Equal(3, all.TotalCount);
    }

    [Theory]
    [InlineData("TITLE WORD")]
    [InlineData("summary word")]
    [InlineData("militaryconflict")]
    [InlineData("Military Conflict")]
    [InlineData("DEVELOPING")]
    [InlineData("critical")]
    [InlineData("display place")]
    [InlineData("REGION NAME")]
    public async Task SearchIsCaseInsensitiveAcrossEverySearchedField(string search)
    {
        var (store, service) = await CreateAsync(cap: 10);
        await store.CreateEventAsync(CreateEvent(
            "evt-target",
            title: "Title Word",
            summary: "Summary Word here",
            category: EventCategory.MilitaryConflict,
            status: EventStatus.Developing,
            severity: EventSeverity.Critical,
            display: "Display Place",
            region: "Region Name"));
        await store.CreateEventAsync(CreateEvent("evt-other", title: "Unrelated", summary: "Nothing", category: EventCategory.Economic, status: EventStatus.Resolved, severity: EventSeverity.Low, display: "Elsewhere", region: null));

        var result = await service.QueryAsync(new EventWindowRequest(Search: search));

        Assert.Equal(["evt-target"], Ids(result));
    }

    [Fact]
    public async Task SearchDoesNotMatchFieldsTheFrontendNeverSearched()
    {
        var (store, service) = await CreateAsync(cap: 10);
        await store.CreateEventAsync(CreateEvent("evt-country", title: "Plain title", countryCode: "XQZ"));

        Assert.Empty((await service.QueryAsync(new EventWindowRequest(Search: "xqz"))).Events);
        Assert.Empty((await service.QueryAsync(new EventWindowRequest(Search: "evt-country"))).Events);
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("50%_off")]
    [InlineData("[a]")]
    [InlineData("o'brien")]
    [InlineData("back\\slash")]
    [InlineData("\" OR 1=1 --")]
    public async Task WildcardsQuotesAndSqlSyntaxInSearchAreLiteralText(string literal)
    {
        var (store, service) = await CreateAsync(cap: 10);
        await store.CreateEventAsync(CreateEvent("evt-literal", title: $"Contains {literal} literally"));
        await store.CreateEventAsync(CreateEvent("evt-plain", title: "Plain title without specials"));

        var result = await service.QueryAsync(new EventWindowRequest(Search: literal));

        Assert.Equal(["evt-literal"], Ids(result));
    }

    [Theory]
    [InlineData(EventMinimumSeverity.All, 5)]
    [InlineData(EventMinimumSeverity.Medium, 4)]
    [InlineData(EventMinimumSeverity.High, 3)]
    [InlineData(EventMinimumSeverity.Critical, 2)]
    public async Task MinimumSeverityKeepsThatLevelAndAbove(EventMinimumSeverity minimum, int expected)
    {
        var (store, service) = await CreateAsync(cap: 10);
        foreach (var severity in Enum.GetValues<EventSeverity>())
        {
            await store.CreateEventAsync(CreateEvent($"evt-{severity.ToString().ToLowerInvariant()}", severity: severity));
        }

        var result = await service.QueryAsync(new EventWindowRequest(MinimumSeverity: minimum));

        Assert.Equal(expected, result.TotalCount);
        Assert.All(result.Events, item => Assert.True(EventWindowRules.SeverityRank(item.Severity) >= EventWindowRules.MinimumRank(minimum)));
    }

    [Fact]
    public async Task WatchlistScopeUsesMembershipAndAnUnknownWatchlistIsEmpty()
    {
        var (store, service) = await CreateAsync(cap: 10);
        await store.CreateEventAsync(CreateEvent("evt-member"));
        await store.CreateEventAsync(CreateEvent("evt-outsider"));
        const string watchlistId = "wl-00000000000000000000000000000001";
        await AddMembershipAsync(watchlistId, "evt-member");

        var scoped = await service.QueryAsync(new EventWindowRequest(WatchlistId: watchlistId));
        var unknown = await service.QueryAsync(new EventWindowRequest(WatchlistId: "wl-00000000000000000000000000000009"));

        Assert.Equal(["evt-member"], Ids(scoped));
        Assert.Equal(1, scoped.TotalCount);
        Assert.Empty(unknown.Events);
    }

    [Fact]
    public async Task AllFourCriteriaCombineWithAnd()
    {
        var (store, service) = await CreateAsync(cap: 10);
        const string watchlistId = "wl-00000000000000000000000000000001";
        await store.CreateEventAsync(CreateEvent("evt-all-match", title: "Harbor fire", severity: EventSeverity.High, startedAt: Now.AddDays(-2)));
        await store.CreateEventAsync(CreateEvent("evt-not-member", title: "Harbor fire", severity: EventSeverity.High, startedAt: Now.AddDays(-2)));
        await store.CreateEventAsync(CreateEvent("evt-too-old", title: "Harbor fire", severity: EventSeverity.High, startedAt: Now.AddDays(-9)));
        await store.CreateEventAsync(CreateEvent("evt-too-low", title: "Harbor fire", severity: EventSeverity.Low, startedAt: Now.AddDays(-2)));
        await store.CreateEventAsync(CreateEvent("evt-no-text", title: "Market note", severity: EventSeverity.High, startedAt: Now.AddDays(-2)));
        foreach (var id in new[] { "evt-all-match", "evt-too-old", "evt-too-low", "evt-no-text" })
        {
            await AddMembershipAsync(watchlistId, id);
        }

        var result = await service.QueryAsync(new EventWindowRequest(EventTimeRange.Last7Days, "harbor", EventMinimumSeverity.Medium, watchlistId));

        Assert.Equal(["evt-all-match"], Ids(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task CapSelectsNewestByInstantThenIdAndReportsTotalAndCapped()
    {
        var (store, service) = await CreateAsync(cap: 3);
        await store.CreateEventAsync(CreateEvent("evt-c", lastUpdated: Now.AddHours(-1)));
        await store.CreateEventAsync(CreateEvent("evt-b", lastUpdated: Now.AddHours(-1)));
        await store.CreateEventAsync(CreateEvent("evt-a", lastUpdated: Now.AddHours(-1)));
        await store.CreateEventAsync(CreateEvent("evt-newest-offset", lastUpdated: new DateTimeOffset(2026, 9, 30, 13, 30, 0, TimeSpan.FromHours(2))));
        await store.CreateEventAsync(CreateEvent("evt-old", lastUpdated: Now.AddDays(-5)));

        var first = await service.QueryAsync(new EventWindowRequest());
        var second = await service.QueryAsync(new EventWindowRequest());

        Assert.Equal(["evt-newest-offset", "evt-a", "evt-b"], Ids(first));
        Assert.Equal(Ids(first), Ids(second));
        Assert.Equal(5, first.TotalCount);
        Assert.True(first.Capped);
        Assert.False((await service.QueryAsync(new EventWindowRequest(Search: "evt-old title"))).Capped);
    }

    [Fact]
    public async Task KeepEventIsIncludedWhenItMatchesAndNeverPushesTheWindowAboveTheCap()
    {
        var (store, service) = await CreateAsync(cap: 3);
        for (var index = 1; index <= 5; index++)
        {
            await store.CreateEventAsync(CreateEvent($"evt-{index}", lastUpdated: Now.AddHours(-index), severity: index == 5 ? EventSeverity.Low : EventSeverity.High));
        }

        var kept = await service.QueryAsync(new EventWindowRequest(KeepEventId: "evt-5"));
        Assert.Equal(["evt-1", "evt-2", "evt-5"], Ids(kept));
        Assert.Equal(5, kept.TotalCount);
        Assert.True(kept.Capped);

        var keptInside = await service.QueryAsync(new EventWindowRequest(KeepEventId: "evt-2"));
        Assert.Equal(["evt-1", "evt-2", "evt-3"], Ids(keptInside));

        var excluded = await service.QueryAsync(new EventWindowRequest(MinimumSeverity: EventMinimumSeverity.High, KeepEventId: "evt-5"));
        Assert.DoesNotContain("evt-5", Ids(excluded));
        Assert.Equal(3, excluded.Events.Count);

        var missing = await service.QueryAsync(new EventWindowRequest(KeepEventId: "evt-missing"));
        Assert.Equal(["evt-1", "evt-2", "evt-3"], Ids(missing));

        var underCap = await service.QueryAsync(new EventWindowRequest(EventTimeRange.All, "evt", EventMinimumSeverity.All, KeepEventId: "evt-5"));
        Assert.True(underCap.Events.Count <= 3);
    }

    [Fact]
    public async Task SearchFindsEventsOlderThanTheCapWindow()
    {
        var (store, service) = await CreateAsync(cap: 2);
        await store.CreateEventAsync(CreateEvent("evt-new-1", lastUpdated: Now));
        await store.CreateEventAsync(CreateEvent("evt-new-2", lastUpdated: Now.AddMinutes(-1)));
        await store.CreateEventAsync(CreateEvent("evt-ancient", title: "Ancient archive record", lastUpdated: Now.AddYears(-3), startedAt: Now.AddYears(-3)));

        Assert.DoesNotContain("evt-ancient", Ids(await service.QueryAsync(new EventWindowRequest())));
        Assert.Equal(["evt-ancient"], Ids(await service.QueryAsync(new EventWindowRequest(Search: "ANCIENT"))));
    }

    [Fact]
    public async Task WindowCarriesSourceCountsForReturnedEvents()
    {
        var (store, service) = await CreateAsync(cap: 10);
        await store.CreateEventAsync(CreateEvent("evt-with-sources"));
        await store.CreateEventAsync(CreateEvent("evt-without"));
        for (var index = 0; index < 2; index++)
        {
            var source = SourceItem.Create(new SourceItemId($"src-{index}"), SourceType.Other, null, "Source", "Text", null, null, null, Now, Now, null, Now);
            await store.CreateSourceItemAsync(source);
            await store.AttachSourceToEventAsync(new EventId("evt-with-sources"), source.Id);
        }

        var result = await service.QueryAsync(new EventWindowRequest());

        Assert.Equal(2, result.Events.Single(item => item.Id == "evt-with-sources").SourceCount);
        Assert.Equal(0, result.Events.Single(item => item.Id == "evt-without").SourceCount);
    }

    [Fact]
    public async Task RealCapReturnsTheNewestTwoThousandOfTwentyFiveHundred()
    {
        var (_, service) = await CreateAsync(cap: EventWindowRules.DefaultCap);
        await using (var db = new LocalDatabaseInitializer(DatabasePath).CreateDbContext())
        {
            for (var index = 0; index < 2500; index++)
            {
                db.Events.Add(ToEntity(CreateEvent($"evt-bulk-{index:D4}", lastUpdated: Now.AddMinutes(-index), severity: (EventSeverity)(index % 5))));
            }

            await db.SaveChangesAsync();
        }

        var stopwatch = Stopwatch.StartNew();
        var result = await service.QueryAsync(new EventWindowRequest());
        var searched = await service.QueryAsync(new EventWindowRequest(Search: "bulk-2499"));
        stopwatch.Stop();
        output.WriteLine($"Two windowed queries over 2,500 Events took {stopwatch.ElapsedMilliseconds} ms.");

        Assert.Equal(2000, result.Events.Count);
        Assert.Equal(2500, result.TotalCount);
        Assert.True(result.Capped);
        Assert.Equal("evt-bulk-0000", result.Events[0].Id);
        Assert.Equal("evt-bulk-1999", result.Events[^1].Id);
        Assert.Equal(["evt-bulk-2499"], Ids(searched));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task NativeQueryMatchesTheSharedParityFixture()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(FindParityFixture()));
        var now = DateTimeOffset.Parse(fixture.RootElement.GetProperty("nowUtc").GetString()!);
        var (store, _) = await CreateAsync(cap: 100);
        var service = new EventWindowService(new SqliteEventStore(new LocalDatabaseInitializer(DatabasePath).CreateDbContext), new FixedTimeProvider(now), 100);
        foreach (var item in fixture.RootElement.GetProperty("events").EnumerateArray())
        {
            await store.CreateEventAsync(CreateEvent(
                item.GetProperty("id").GetString()!,
                title: item.GetProperty("title").GetString()!,
                summary: item.GetProperty("summary").GetString()!,
                category: Enum.Parse<EventCategory>(item.GetProperty("category").GetString()!),
                status: Enum.Parse<EventStatus>(item.GetProperty("status").GetString()!),
                severity: Enum.Parse<EventSeverity>(item.GetProperty("severity").GetString()!),
                startedAt: DateTimeOffset.Parse(item.GetProperty("startedAtUtc").GetString()!),
                lastUpdated: DateTimeOffset.Parse(item.GetProperty("lastUpdatedAtUtc").GetString()!),
                display: item.GetProperty("locationDisplayName").GetString()!,
                region: item.GetProperty("region").GetString()));
        }

        foreach (var testCase in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            Assert.True(EventWindowRules.TryParseRange(testCase.GetProperty("range").GetString(), out var range));
            Assert.True(EventWindowRules.TryParseMinimumSeverity(testCase.GetProperty("minimumSeverity").GetString(), out var minimum));
            var result = await service.QueryAsync(new EventWindowRequest(range, testCase.GetProperty("search").GetString()!, minimum));
            var expected = testCase.GetProperty("expectedIds").EnumerateArray().Select(item => item.GetString()!).Order().ToArray();
            Assert.True(expected.SequenceEqual(Ids(result).Order()), $"Parity case '{testCase.GetProperty("name").GetString()}' returned [{string.Join(", ", Ids(result).Order())}].");
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private string DatabasePath => Path.Combine(directory, "window.db");

    private async Task<(SqliteEventStore Store, EventWindowService Service)> CreateAsync(int cap)
    {
        Directory.CreateDirectory(directory);
        var initializer = new LocalDatabaseInitializer(DatabasePath);
        await initializer.InitializeAsync();
        var store = new SqliteEventStore(initializer.CreateDbContext);
        return (store, new EventWindowService(store, new FixedTimeProvider(Now), cap));
    }

    private async Task AddMembershipAsync(string watchlistId, string eventId)
    {
        await using var db = new LocalDatabaseInitializer(DatabasePath).CreateDbContext();
        if (!await db.Watchlists.AnyAsync(item => item.Id == watchlistId))
        {
            db.Watchlists.Add(new WatchlistEntity { Id = watchlistId, Name = watchlistId, NormalizedName = watchlistId.ToUpperInvariant() });
        }

        db.WatchlistEvents.Add(new WatchlistEventEntity { WatchlistId = watchlistId, EventId = eventId });
        await db.SaveChangesAsync();
    }

    private static string FindParityFixture()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "tests", "fixtures", "event-window-parity.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Parity fixture not found.");
    }

    private static string[] Ids(EventWindowResult result) => result.Events.Select(item => item.Id).ToArray();

    private static Event CreateEvent(
        string id,
        string? title = null,
        string summary = "Summary",
        EventCategory category = EventCategory.Security,
        EventStatus status = EventStatus.Active,
        EventSeverity severity = EventSeverity.Medium,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? lastUpdated = null,
        string display = "Place",
        string? region = null,
        string? countryCode = null)
        => Event.Create(
            new EventId(id),
            title ?? $"{id} title",
            summary,
            category,
            status,
            startedAt ?? Now.AddHours(-1),
            lastUpdated ?? Now.AddHours(-1),
            new Location(1, 2, display, countryCode, region),
            Now.AddDays(-1),
            Now,
            new EventSeverityAssessment(severity, EventSeverityAssessment.ManualRule, "test"),
            new EventProvenance(EventProvider.Manual));

    private static EventEntity ToEntity(Event item) => new()
    {
        Id = item.Id.Value,
        Title = item.Title,
        Summary = item.Summary,
        Category = item.Category.ToString(),
        Status = item.Status.ToString(),
        StartedAtUtc = item.StartedAtUtc,
        LastUpdatedAtUtc = item.LastUpdatedAtUtc,
        Latitude = item.Location.Latitude,
        Longitude = item.Location.Longitude,
        LocationDisplayName = item.Location.DisplayName,
        CountryCode = item.Location.CountryCode,
        Region = item.Location.Region,
        CreatedAtUtc = item.CreatedAtUtc,
        UpdatedAtUtc = item.UpdatedAtUtc,
        Severity = item.Severity.ToString(),
        SeverityRule = item.SeverityRule,
        SeverityInputs = item.SeverityInputs,
        Provider = item.Provider.ToString(),
        ProviderKey = item.ProviderKey,
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
