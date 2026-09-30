using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

/// <summary>
/// Single source of demo Event severities, shared by the seeder and the legacy backfill so they cannot disagree.
/// </summary>
public static class DemoEventSeverities
{
    public const string EventIdPrefix = "evt-demo-";
    public const string Inputs = "demo data";

    private static readonly IReadOnlyDictionary<string, EventSeverity> Levels = new Dictionary<string, EventSeverity>(StringComparer.Ordinal)
    {
        ["evt-demo-baltic-security"] = EventSeverity.Medium,
        ["evt-demo-red-sea-shipping"] = EventSeverity.High,
        ["evt-demo-europe-diplomatic"] = EventSeverity.Low,
        ["evt-demo-east-asia-infrastructure"] = EventSeverity.Medium,
        ["evt-demo-north-africa-civil"] = EventSeverity.Low,
        ["evt-demo-humanitarian-crossing"] = EventSeverity.Medium,
    };

    public static bool TryGetAssessment(string eventId, out EventSeverityAssessment? assessment)
    {
        if (Levels.TryGetValue(eventId, out var level))
        {
            assessment = new EventSeverityAssessment(level, EventSeverityAssessment.ManualRule, Inputs);
            return true;
        }

        assessment = null;
        return false;
    }

    public static EventProvenance Provenance { get; } = new(EventProvider.Demo);
}
