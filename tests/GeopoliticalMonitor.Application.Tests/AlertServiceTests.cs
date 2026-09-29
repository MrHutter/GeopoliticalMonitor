using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class AlertServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);
    private const string WatchlistId = "wl-00000000000000000000000000000001";
    private const string AlertId = "al-00000000000000000000000000000001";

    [Fact]
    public async Task UsesFixedTriggerGeneratedIdUtcClockAndBounds()
    {
        var store = new RecordingAlertStore();
        var service = new AlertService(store, new FixedTimeProvider(Now));

        await service.GetRulesAsync();
        var result = await service.SetRuleEnabledAsync(WatchlistId, true);
        await service.GetAlertsAsync();

        Assert.Equal(SetAlertRuleDisposition.CreatedEnabled, result.Disposition);
        Assert.Equal(AlertService.MaximumRules, store.MaximumRules);
        Assert.Equal(AlertService.MaximumAlerts, store.MaximumAlerts);
        Assert.Equal(AlertTriggerType.GdacsEventMateriallyUpdated, store.TriggerType);
        Assert.Equal(Now, store.TransitionAtUtc);
        Assert.Matches("^ar-[0-9a-f]{32}$", store.NewRuleId.Value);
    }

    [Fact]
    public async Task MalformedIdentifiersReturnOrdinaryNotFoundDispositions()
    {
        var service = new AlertService(new RecordingAlertStore(), new FixedTimeProvider(Now));

        var rule = await service.SetRuleEnabledAsync("bad", true);
        var read = await service.MarkReadAsync("bad");

        Assert.Equal(SetAlertRuleDisposition.WatchlistNotFound, rule.Disposition);
        Assert.Equal(MarkAlertReadDisposition.NotFound, read.Disposition);
    }

    [Fact]
    public async Task MarkReadUsesUtcClockAndIsDelegatedOnce()
    {
        var store = new RecordingAlertStore();
        var service = new AlertService(store, new FixedTimeProvider(Now));

        var result = await service.MarkReadAsync(AlertId);

        Assert.Equal(MarkAlertReadDisposition.Updated, result.Disposition);
        Assert.Equal(Now, store.ReadAtUtc);
        Assert.Equal(AlertId, store.MarkedAlertId.Value);
    }

    private sealed class RecordingAlertStore : IAlertStore
    {
        public int MaximumRules { get; private set; }
        public int MaximumAlerts { get; private set; }
        public AlertTriggerType TriggerType { get; private set; }
        public AlertRuleId NewRuleId { get; private set; }
        public DateTimeOffset TransitionAtUtc { get; private set; }
        public AlertId MarkedAlertId { get; private set; }
        public DateTimeOffset ReadAtUtc { get; private set; }

        public Task<IReadOnlyList<AlertRuleDto>> GetRulesAsync(int maximumRules, CancellationToken cancellationToken = default)
        {
            MaximumRules = maximumRules;
            return Task.FromResult<IReadOnlyList<AlertRuleDto>>([]);
        }

        public Task<SetAlertRuleResult> SetRuleEnabledAsync(
            WatchlistId watchlistId,
            AlertTriggerType triggerType,
            bool enabled,
            AlertRuleId newRuleId,
            DateTimeOffset transitionAtUtc,
            CancellationToken cancellationToken = default)
        {
            TriggerType = triggerType;
            NewRuleId = newRuleId;
            TransitionAtUtc = transitionAtUtc;
            return Task.FromResult(new SetAlertRuleResult(SetAlertRuleDisposition.CreatedEnabled));
        }

        public Task<long> GetUnreadCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0L);

        public Task<AlertListResult> GetAlertsAsync(int maximumAlerts, CancellationToken cancellationToken = default)
        {
            MaximumAlerts = maximumAlerts;
            return Task.FromResult(new AlertListResult([], 0, false));
        }

        public Task<MarkAlertReadResult> MarkReadAsync(AlertId alertId, DateTimeOffset readAtUtc, CancellationToken cancellationToken = default)
        {
            MarkedAlertId = alertId;
            ReadAtUtc = readAtUtc;
            return Task.FromResult(new MarkAlertReadResult(MarkAlertReadDisposition.Updated, alertId.Value, readAtUtc));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
