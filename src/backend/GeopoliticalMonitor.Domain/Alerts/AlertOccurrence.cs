using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Domain.Alerts;

public sealed class AlertOccurrence
{
    private AlertOccurrence(
        AlertId id,
        AlertRuleId alertRuleId,
        EventId eventId,
        DateTimeOffset providerRevisionAtUtc,
        DateTimeOffset triggeredAtUtc,
        DateTimeOffset? readAtUtc)
    {
        Id = id;
        AlertRuleId = alertRuleId;
        EventId = eventId;
        ProviderRevisionAtUtc = providerRevisionAtUtc;
        TriggeredAtUtc = triggeredAtUtc;
        ReadAtUtc = readAtUtc;
    }

    public AlertId Id { get; }
    public AlertRuleId AlertRuleId { get; }
    public EventId EventId { get; }
    public DateTimeOffset ProviderRevisionAtUtc { get; }
    public DateTimeOffset TriggeredAtUtc { get; }
    public DateTimeOffset? ReadAtUtc { get; }
    public bool IsRead => ReadAtUtc is not null;

    public static AlertOccurrence Create(
        AlertId id,
        AlertRuleId alertRuleId,
        EventId eventId,
        DateTimeOffset providerRevisionAtUtc,
        DateTimeOffset triggeredAtUtc)
    {
        EnsureUtc(providerRevisionAtUtc, nameof(providerRevisionAtUtc));
        EnsureUtc(triggeredAtUtc, nameof(triggeredAtUtc));
        return new AlertOccurrence(id, alertRuleId, eventId, providerRevisionAtUtc, triggeredAtUtc, null);
    }

    public AlertOccurrence MarkRead(DateTimeOffset readAtUtc)
    {
        EnsureUtc(readAtUtc, nameof(readAtUtc));
        return IsRead
            ? this
            : new AlertOccurrence(Id, AlertRuleId, EventId, ProviderRevisionAtUtc, TriggeredAtUtc, readAtUtc);
    }

    private static void EnsureUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Alert timestamps must use UTC.", parameterName);
        }
    }
}
