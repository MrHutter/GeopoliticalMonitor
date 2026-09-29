using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Domain.Alerts;

public sealed class AlertRule
{
    private AlertRule(
        AlertRuleId id,
        WatchlistId watchlistId,
        AlertTriggerType triggerType,
        bool isEnabled,
        DateTimeOffset createdAtUtc,
        DateTimeOffset lastEnabledAtUtc)
    {
        Id = id;
        WatchlistId = watchlistId;
        TriggerType = triggerType;
        IsEnabled = isEnabled;
        CreatedAtUtc = createdAtUtc;
        LastEnabledAtUtc = lastEnabledAtUtc;
    }

    public AlertRuleId Id { get; }
    public WatchlistId WatchlistId { get; }
    public AlertTriggerType TriggerType { get; }
    public bool IsEnabled { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset LastEnabledAtUtc { get; }

    public static AlertRule CreateEnabled(
        AlertRuleId id,
        WatchlistId watchlistId,
        AlertTriggerType triggerType,
        DateTimeOffset createdAtUtc)
    {
        EnsureUtc(createdAtUtc, nameof(createdAtUtc));
        return new AlertRule(id, watchlistId, triggerType, true, createdAtUtc, createdAtUtc);
    }

    public AlertRule SetEnabled(bool enabled, DateTimeOffset transitionAtUtc)
    {
        EnsureUtc(transitionAtUtc, nameof(transitionAtUtc));
        if (enabled == IsEnabled)
        {
            return this;
        }

        return new AlertRule(
            Id,
            WatchlistId,
            TriggerType,
            enabled,
            CreatedAtUtc,
            enabled ? transitionAtUtc : LastEnabledAtUtc);
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Alert timestamps must use UTC.", parameterName);
        }
    }
}
