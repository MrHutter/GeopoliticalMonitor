using System.Globalization;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>Design section 4.2: the UCDP best fatality estimate sets the level; low and high are context only.</summary>
public static class UcdpSeverityRule
{
    public const string RuleId = "ucdp-candidate-v1";

    public static EventSeverity Classify(int best) => best switch
    {
        <= 0 => EventSeverity.Low,
        < 10 => EventSeverity.Medium,
        < 100 => EventSeverity.High,
        < 1000 => EventSeverity.Critical,
        _ => EventSeverity.Catastrophic,
    };

    public static EventSeverityAssessment Assess(int best, int low, int high)
        => new(Classify(best), RuleId, FormatInputs(best, low, high));

    public static string FormatInputs(int best, int low, int high)
        => string.Create(CultureInfo.InvariantCulture, $"best={best}; low={low}; high={high}");
}
