using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Alerts;

public sealed class AlertService
{
    public const int MaximumRules = 100;
    public const int MaximumAlerts = 200;

    private readonly IAlertStore store;
    private readonly TimeProvider timeProvider;

    public AlertService(IAlertStore store, TimeProvider? timeProvider = null)
    {
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<IReadOnlyList<AlertRuleDto>> GetRulesAsync(CancellationToken cancellationToken = default)
        => store.GetRulesAsync(MaximumRules, cancellationToken);

    public Task<SetAlertRuleResult> SetRuleEnabledAsync(
        string watchlistId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (!WatchlistId.TryParse(watchlistId, out var parsedWatchlistId))
        {
            return Task.FromResult(new SetAlertRuleResult(SetAlertRuleDisposition.WatchlistNotFound));
        }

        return store.SetRuleEnabledAsync(
            parsedWatchlistId,
            AlertTriggerType.GdacsEventMateriallyUpdated,
            enabled,
            AlertRuleId.CreateNew(),
            timeProvider.GetUtcNow().ToUniversalTime(),
            cancellationToken);
    }

    public Task<long> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        => store.GetUnreadCountAsync(cancellationToken);

    public Task<AlertListResult> GetAlertsAsync(CancellationToken cancellationToken = default)
        => store.GetAlertsAsync(MaximumAlerts, cancellationToken);

    public Task<MarkAlertReadResult> MarkReadAsync(
        string alertId,
        CancellationToken cancellationToken = default)
    {
        if (!AlertId.TryParse(alertId, out var parsedAlertId))
        {
            return Task.FromResult(new MarkAlertReadResult(MarkAlertReadDisposition.NotFound, alertId));
        }

        return store.MarkReadAsync(
            parsedAlertId,
            timeProvider.GetUtcNow().ToUniversalTime(),
            cancellationToken);
    }
}
