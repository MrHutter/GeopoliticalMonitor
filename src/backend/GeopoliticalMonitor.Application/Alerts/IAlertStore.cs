using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Alerts;

public interface IAlertStore
{
    Task<IReadOnlyList<AlertRuleDto>> GetRulesAsync(
        int maximumRules,
        CancellationToken cancellationToken = default);

    Task<SetAlertRuleResult> SetRuleEnabledAsync(
        WatchlistId watchlistId,
        AlertTriggerType triggerType,
        bool enabled,
        AlertRuleId newRuleId,
        DateTimeOffset transitionAtUtc,
        CancellationToken cancellationToken = default);

    Task<long> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    Task<AlertListResult> GetAlertsAsync(
        int maximumAlerts,
        CancellationToken cancellationToken = default);

    Task<MarkAlertReadResult> MarkReadAsync(
        AlertId alertId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default);
}
