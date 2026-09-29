using System.Text.Json;
using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class AlertBridgeTests
{
    private const string WatchlistId = "wl-0123456789abcdef0123456789abcdef";
    private const string RuleId = "ar-0123456789abcdef0123456789abcdef";
    private const string AlertIdValue = "al-0123456789abcdef0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("""{"type":"alerts.summary","payload":{}}""", "alerts.summary")]
    [InlineData("""{"type":"alerts.rules.list","payload":{}}""", "alerts.rules.list")]
    [InlineData("""{"type":"alerts.rules.set","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","enabled":true}}""", "alerts.rules.set")]
    [InlineData("""{"type":"alerts.list","payload":{}}""", "alerts.list")]
    [InlineData("""{"type":"alerts.markRead","payload":{"alertId":"al-0123456789abcdef0123456789abcdef"}}""", "alerts.markRead")]
    public void ParsesEveryTypedAlertRequest(string json, string expectedType)
    {
        var accepted = DesktopBridgeProtocol.TryParseRequest(json, out var request, out var error);

        Assert.True(accepted, error);
        Assert.Equal(expectedType, request?.Type);
    }

    [Theory]
    [InlineData("""{"type":"alerts.summary","payload":{"all":true}}""")]
    [InlineData("""{"type":"alerts.rules.list","payload":{"limit":100}}""")]
    [InlineData("""{"type":"alerts.list","payload":{"before":"now"}}""")]
    [InlineData("""{"type":"alerts.rules.set","payload":{"enabled":true}}""")]
    [InlineData("""{"type":"alerts.rules.set","payload":{"watchlistId":"bad","enabled":true}}""")]
    [InlineData("""{"type":"alerts.rules.set","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","enabled":"yes"}}""")]
    [InlineData("""{"type":"alerts.rules.set","payload":{"watchlistId":"wl-0123456789abcdef0123456789abcdef","enabled":true,"triggerType":"Other"}}""")]
    [InlineData("""{"type":"alerts.markRead","payload":{}}""")]
    [InlineData("""{"type":"alerts.markRead","payload":{"alertId":"bad"}}""")]
    [InlineData("""{"type":"alerts.markRead","payload":{"alertId":42}}""")]
    [InlineData("""{"type":"alerts.markRead","payload":{"alertId":"al-0123456789abcdef0123456789abcdef","sql":"select 1"}}""")]
    public void RejectsMalformedOrBroadenedAlertRequests(string json)
    {
        Assert.False(DesktopBridgeProtocol.TryParseRequest(json, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void SerializesTypedBoundedAlertResponses()
    {
        var rule = new AlertRuleDto(RuleId, WatchlistId, "GdacsEventMateriallyUpdated", true, Now, Now);
        var alert = new AlertOccurrenceDto(
            AlertIdValue,
            RuleId,
            "evt-one",
            WatchlistId,
            "Event title",
            "Monitor",
            "GdacsEventMateriallyUpdated",
            Now.AddMinutes(-1),
            Now,
            null);
        using var summary = JsonDocument.Parse(DesktopBridgeProtocol.CreateAlertSummaryResultResponseJson(3));
        using var rules = JsonDocument.Parse(DesktopBridgeProtocol.CreateAlertRulesListResultResponseJson([rule]));
        using var list = JsonDocument.Parse(DesktopBridgeProtocol.CreateAlertsListResultResponseJson(new AlertListResult([alert], 3, true)));
        using var marked = JsonDocument.Parse(DesktopBridgeProtocol.CreateAlertMarkReadResultResponseJson(
            new MarkAlertReadResult(MarkAlertReadDisposition.AlreadyRead, AlertIdValue, Now)));

        Assert.Equal(3, summary.RootElement.GetProperty("payload").GetProperty("unreadCount").GetInt64());
        Assert.Equal(RuleId, rules.RootElement.GetProperty("payload").GetProperty("rules")[0].GetProperty("id").GetString());
        Assert.True(list.RootElement.GetProperty("payload").GetProperty("hasMore").GetBoolean());
        Assert.Equal("Event title", list.RootElement.GetProperty("payload").GetProperty("alerts")[0].GetProperty("eventTitle").GetString());
        Assert.Equal("AlreadyRead", marked.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
    }

    [Fact]
    public async Task HandlerReturnsAllRuleDispositionsAndMarkReadNotFoundAsTypedResults()
    {
        var store = new StubAlertStore
        {
            SetResult = new SetAlertRuleResult(SetAlertRuleDisposition.RuleNotFound),
            ReadResult = new MarkAlertReadResult(MarkAlertReadDisposition.NotFound, AlertIdValue),
        };
        var handler = new AlertBridgeHandler(new AlertService(store, new FixedTimeProvider(Now)));

        using var setResponse = JsonDocument.Parse(await handler.HandleAsync(new DesktopBridgeRequest(
            DesktopBridgeProtocol.AlertRulesSetMessageType,
            WatchlistId: WatchlistId,
            AlertRuleEnabled: false)));
        using var readResponse = JsonDocument.Parse(await handler.HandleAsync(new DesktopBridgeRequest(
            DesktopBridgeProtocol.AlertsMarkReadMessageType,
            AlertId: AlertIdValue)));

        Assert.Equal("RuleNotFound", setResponse.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
        Assert.Equal(JsonValueKind.Null, setResponse.RootElement.GetProperty("payload").GetProperty("rule").ValueKind);
        Assert.Equal("NotFound", readResponse.RootElement.GetProperty("payload").GetProperty("disposition").GetString());
    }

    [Fact]
    public async Task HandlerMapsPersistenceFailureWithoutLeakingDetails()
    {
        var handler = new AlertBridgeHandler(new AlertService(new ThrowingAlertStore()));

        var json = await handler.HandleAsync(new DesktopBridgeRequest(DesktopBridgeProtocol.AlertsSummaryMessageType));
        using var response = JsonDocument.Parse(json);

        Assert.Equal("alerts.error", response.RootElement.GetProperty("type").GetString());
        Assert.Equal("PersistenceUnavailable", response.RootElement.GetProperty("payload").GetProperty("code").GetString());
        Assert.DoesNotContain("sensitive-database-path", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubAlertStore : IAlertStore
    {
        public SetAlertRuleResult SetResult { get; init; } = new(SetAlertRuleDisposition.Unchanged);
        public MarkAlertReadResult ReadResult { get; init; } = new(MarkAlertReadDisposition.NotFound, AlertIdValue);
        public Task<IReadOnlyList<AlertRuleDto>> GetRulesAsync(int maximumRules, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AlertRuleDto>>([]);
        public Task<SetAlertRuleResult> SetRuleEnabledAsync(WatchlistId watchlistId, AlertTriggerType triggerType, bool enabled, AlertRuleId newRuleId, DateTimeOffset transitionAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(SetResult);
        public Task<long> GetUnreadCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
        public Task<AlertListResult> GetAlertsAsync(int maximumAlerts, CancellationToken cancellationToken = default)
            => Task.FromResult(new AlertListResult([], 0, false));
        public Task<MarkAlertReadResult> MarkReadAsync(AlertId alertId, DateTimeOffset readAtUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(ReadResult);
    }

    private sealed class ThrowingAlertStore : IAlertStore
    {
        private static InvalidOperationException Failure() => new("sensitive-database-path");
        public Task<IReadOnlyList<AlertRuleDto>> GetRulesAsync(int maximumRules, CancellationToken cancellationToken = default) => throw Failure();
        public Task<SetAlertRuleResult> SetRuleEnabledAsync(WatchlistId watchlistId, AlertTriggerType triggerType, bool enabled, AlertRuleId newRuleId, DateTimeOffset transitionAtUtc, CancellationToken cancellationToken = default) => throw Failure();
        public Task<long> GetUnreadCountAsync(CancellationToken cancellationToken = default) => throw Failure();
        public Task<AlertListResult> GetAlertsAsync(int maximumAlerts, CancellationToken cancellationToken = default) => throw Failure();
        public Task<MarkAlertReadResult> MarkReadAsync(AlertId alertId, DateTimeOffset readAtUtc, CancellationToken cancellationToken = default) => throw Failure();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
