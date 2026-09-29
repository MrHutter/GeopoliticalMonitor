namespace GeopoliticalMonitor.Application.Alerts;

public sealed record AlertRuleDto(
    string Id,
    string WatchlistId,
    string TriggerType,
    bool IsEnabled,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastEnabledAtUtc);

public enum SetAlertRuleDisposition
{
    CreatedEnabled,
    Enabled,
    Disabled,
    Unchanged,
    RuleNotFound,
    WatchlistNotFound,
}

public sealed record SetAlertRuleResult(
    SetAlertRuleDisposition Disposition,
    AlertRuleDto? Rule = null);

public sealed record AlertOccurrenceDto(
    string Id,
    string AlertRuleId,
    string EventId,
    string WatchlistId,
    string EventTitle,
    string WatchlistName,
    string TriggerType,
    DateTimeOffset ProviderRevisionAtUtc,
    DateTimeOffset TriggeredAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed record AlertListResult(
    IReadOnlyList<AlertOccurrenceDto> Alerts,
    long UnreadCount,
    bool HasMore);

public enum MarkAlertReadDisposition
{
    Updated,
    AlreadyRead,
    NotFound,
}

public sealed record MarkAlertReadResult(
    MarkAlertReadDisposition Disposition,
    string AlertId,
    DateTimeOffset? ReadAtUtc = null);
