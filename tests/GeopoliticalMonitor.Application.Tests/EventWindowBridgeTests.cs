using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class EventWindowBridgeTests
{
    private const string WatchlistId = "wl-00000000000000000000000000000001";
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("""{"type":"events.list","payload":{}}""")]
    [InlineData("""{"type":"events.list"}""")]
    [InlineData("""{"type":"events.list","payload":null}""")]
    public void RequestWithNoFieldsKeepsTheOldDefaults(string json)
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest(json, out var request, out _));

        Assert.Equal(DesktopBridgeProtocol.EventsListMessageType, request!.Type);
        Assert.Equal(new EventWindowRequest(), request.EventWindow);
        Assert.Null(request.EventsListRequestId);
    }

    [Fact]
    public void ParsesEveryTypedCriterion()
    {
        var json = JsonSerializer.Serialize(new
        {
            type = "events.list",
            payload = new { requestId = 7, range = "7D", search = "  Harbor ", minimumSeverity = "High", watchlistId = WatchlistId, keepEventId = "evt-demo-baltic-security" },
        });

        Assert.True(DesktopBridgeProtocol.TryParseRequest(json, out var request, out var error), error);

        Assert.Equal(7, request!.EventsListRequestId);
        Assert.Equal(new EventWindowRequest(EventTimeRange.Last7Days, "  Harbor ", EventMinimumSeverity.High, WatchlistId, "evt-demo-baltic-security"), request.EventWindow);
    }

    [Theory]
    [InlineData("ALL", EventTimeRange.All)]
    [InlineData("24H", EventTimeRange.Last24Hours)]
    [InlineData("7D", EventTimeRange.Last7Days)]
    [InlineData("30D", EventTimeRange.Last30Days)]
    [InlineData("90D", EventTimeRange.Last90Days)]
    public void ParsesEachRange(string value, EventTimeRange expected)
    {
        Assert.True(DesktopBridgeProtocol.TryParseRequest(JsonSerializer.Serialize(new { type = "events.list", payload = new { range = value } }), out var request, out _));
        Assert.Equal(expected, request!.EventWindow!.Range);
    }

    [Theory]
    [InlineData("""{"type":"events.list","payload":{"range":"1Y"}}""")]
    [InlineData("""{"type":"events.list","payload":{"range":"all"}}""")]
    [InlineData("""{"type":"events.list","payload":{"range":7}}""")]
    [InlineData("""{"type":"events.list","payload":{"minimumSeverity":"Low"}}""")]
    [InlineData("""{"type":"events.list","payload":{"minimumSeverity":"Catastrophic"}}""")]
    [InlineData("""{"type":"events.list","payload":{"minimumSeverity":"high"}}""")]
    [InlineData("""{"type":"events.list","payload":{"search":null}}""")]
    [InlineData("""{"type":"events.list","payload":{"search":42}}""")]
    [InlineData("""{"type":"events.list","payload":{"requestId":-1}}""")]
    [InlineData("""{"type":"events.list","payload":{"requestId":1.5}}""")]
    [InlineData("""{"type":"events.list","payload":{"requestId":"1"}}""")]
    [InlineData("""{"type":"events.list","payload":{"requestId":2147483648}}""")]
    [InlineData("""{"type":"events.list","payload":{"watchlistId":"Watchlist One"}}""")]
    [InlineData("""{"type":"events.list","payload":{"watchlistId":null}}""")]
    [InlineData("""{"type":"events.list","payload":{"keepEventId":"evt with spaces"}}""")]
    [InlineData("""{"type":"events.list","payload":{"keepEventId":""}}""")]
    [InlineData("""{"type":"events.list","payload":{"query":"SELECT * FROM Events"}}""")]
    [InlineData("""{"type":"events.list","payload":{"sql":"1=1"}}""")]
    [InlineData("""{"type":"events.list","payload":{"cap":100000}}""")]
    [InlineData("""{"type":"events.list","payload":{"range":"7D","range":"ALL"}}""")]
    [InlineData("""{"type":"events.list","payload":{"search":"a","search":"b"}}""")]
    [InlineData("""{"type":"events.list","payload":{},"payload":{}}""")]
    [InlineData("""{"type":"events.list","payload":[]}""")]
    public void RejectsUnknownDuplicateMistypedAndOutOfRangeFields(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out var request, out var error));
        Assert.Null(request);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void RejectsOverLongSearchAndAcceptsTheMaximumLength()
    {
        var maximum = new string('a', EventWindowRules.MaximumSearchLength);
        Assert.True(DesktopBridgeProtocol.TryParseRequest(JsonSerializer.Serialize(new { type = "events.list", payload = new { search = maximum } }), out _, out _));
        Assert.False(DesktopBridgeProtocol.TryParseRequest(JsonSerializer.Serialize(new { type = "events.list", payload = new { search = maximum + "a" } }), out _, out _));
    }

    [Fact]
    public async Task EventListRequestIsDispatchedOnlyForTheTrustedOrigin()
    {
        const string valid = """{"type":"events.list","payload":{"requestId":1,"range":"ALL","search":"","minimumSeverity":"All"}}""";
        var dispatched = new List<DesktopBridgeRequest>();
        Task Record(DesktopBridgeRequest request)
        {
            dispatched.Add(request);
            return Task.CompletedTask;
        }

        Assert.True(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, valid, Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://untrusted.invalid/", valid, Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync("https://x.com/home", valid, Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"events.list","payload":{"unexpected":true}}""", Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"events.list","payload":{"range":"7D","range":"7D"}}""", Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"events.list","payload":{"minimumSeverity":"Extreme"}}""", Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, JsonSerializer.Serialize(new { type = "events.list", payload = new { search = new string('x', 201) } }), Record));
        Assert.False(await PrivilegedBridgeMessageProcessor.TryProcessAsync(PrivilegedWebViewSecurityPolicy.TrustedOrigin, """{"type":"events.window","payload":{}}""", Record));

        var request = Assert.Single(dispatched);
        Assert.Equal(1, request.EventsListRequestId);
    }

    [Fact]
    public void ResponseCarriesTotalCappedAndEchoedRequestId()
    {
        var location = new LocationDto(1, 2, "Place", null, null);
        var dto = new EventDto("evt-one", "Title", "Summary", "Security", "Active", Now, Now, location, Now, Now, 0, "Low", "manual", "test", "Manual");

        using var document = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventsListResultResponseJson(new EventWindowResult([dto], 4812, true), 12));
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal(["capped", "events", "requestId", "totalCount"], payload.EnumerateObject().Select(item => item.Name).Order());
        Assert.Equal(4812, payload.GetProperty("totalCount").GetInt32());
        Assert.True(payload.GetProperty("capped").GetBoolean());
        Assert.Equal(12, payload.GetProperty("requestId").GetInt32());

        using var legacy = JsonDocument.Parse(DesktopBridgeProtocol.CreateEventsListResultResponseJson([dto]));
        Assert.Equal(JsonValueKind.Null, legacy.RootElement.GetProperty("payload").GetProperty("requestId").ValueKind);
        Assert.Equal(1, legacy.RootElement.GetProperty("payload").GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData(EventTimeRange.All, null)]
    [InlineData(EventTimeRange.Last24Hours, 24)]
    [InlineData(EventTimeRange.Last7Days, 168)]
    [InlineData(EventTimeRange.Last30Days, 720)]
    [InlineData(EventTimeRange.Last90Days, 2160)]
    public async Task ServiceResolvesTheCutoffNormalizesSearchAndAppliesTheCap(EventTimeRange range, int? hours)
    {
        var store = new RecordingWindowStore(totalCount: 5);
        var service = new EventWindowService(store, new FixedTimeProvider(Now), cap: 3);

        var result = await service.QueryAsync(new EventWindowRequest(range, "  MIXED Case ", EventMinimumSeverity.High, WatchlistId, "evt-keep"));

        Assert.Equal(hours is null ? null : Now.AddHours(-hours.Value), store.Criteria!.StartedAtOrAfterUtc);
        Assert.Equal("mixed case", store.Criteria.NormalizedSearch);
        Assert.Equal(EventWindowRules.SeverityRank("High"), store.Criteria.MinimumSeverityRank);
        Assert.Equal(WatchlistId, store.Criteria.WatchlistId);
        Assert.Equal("evt-keep", store.Criteria.KeepEventId);
        Assert.Equal(3, store.Cap);
        Assert.True(result.Capped);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task ServiceRejectsOverLongSearchAndANonPositiveCap()
    {
        var service = new EventWindowService(new RecordingWindowStore(0), new FixedTimeProvider(Now));

        Assert.Equal(EventWindowRules.DefaultCap, service.Cap);
        await Assert.ThrowsAsync<ArgumentException>(() => service.QueryAsync(new EventWindowRequest(Search: new string('a', 201))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EventWindowService(new RecordingWindowStore(0), cap: 0));
    }

    [Theory]
    [InlineData("MilitaryConflict", "Military conflict")]
    [InlineData("PoliticalDiplomatic", "Political / diplomatic")]
    [InlineData("CivilUnrest", "Civil unrest")]
    [InlineData("NaturalDisaster", "Natural Disaster")]
    [InlineData("Security", "Security")]
    [InlineData("Economic", "Economic")]
    public void CategoryLabelsMatchTheFrontendFormatter(string category, string label)
    {
        Assert.Equal(label, EventWindowRules.CategoryLabel(category));
    }

    [Theory]
    [InlineData("  Harbor  ", "harbor")]
    [InlineData("﻿École ", "école")]
    [InlineData("\t\n", "")]
    [InlineData(null, "")]
    public void NormalizesSearchLikeTheFrontend(string? input, string expected)
    {
        Assert.Equal(expected, EventWindowRules.NormalizeSearch(input));
    }

    private sealed class RecordingWindowStore(int totalCount) : IEventWindowStore
    {
        public EventWindowCriteria? Criteria { get; private set; }
        public int Cap { get; private set; }

        public Task<EventWindowPage> QueryEventWindowAsync(EventWindowCriteria criteria, int cap, CancellationToken cancellationToken = default)
        {
            Criteria = criteria;
            Cap = cap;
            var item = Event.Create(
                new EventId("evt-one"), "Title", "Summary", EventCategory.Security, EventStatus.Active, Now, Now, new Location(1, 2, "Place"), Now, Now,
                new EventSeverityAssessment(EventSeverity.High, EventSeverityAssessment.ManualRule, "test"), new EventProvenance(EventProvider.Manual));
            return Task.FromResult(new EventWindowPage([item], new Dictionary<EventId, int>(), totalCount));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
