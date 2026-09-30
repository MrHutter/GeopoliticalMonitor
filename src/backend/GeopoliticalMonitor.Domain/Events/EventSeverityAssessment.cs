namespace GeopoliticalMonitor.Domain.Events;

public sealed record EventSeverityAssessment
{
    public const int MaximumRuleLength = 64;
    public const int MaximumInputsLength = 256;
    public const string ManualRule = "manual";
    public const string LegacyDefaultRule = "legacy-default";

    public EventSeverityAssessment(EventSeverity level, string rule, string inputs)
    {
        if (!Enum.IsDefined(level))
        {
            throw new ArgumentOutOfRangeException(nameof(level), "Event severity is not supported.");
        }

        if (string.IsNullOrWhiteSpace(rule))
        {
            throw new ArgumentException("Event severity rule is required.", nameof(rule));
        }

        if (rule.Length > MaximumRuleLength)
        {
            throw new ArgumentException($"Event severity rule must be at most {MaximumRuleLength} characters.", nameof(rule));
        }

        if (string.IsNullOrWhiteSpace(inputs))
        {
            throw new ArgumentException("Event severity inputs are required.", nameof(inputs));
        }

        if (inputs.Length > MaximumInputsLength)
        {
            throw new ArgumentException($"Event severity inputs must be at most {MaximumInputsLength} characters.", nameof(inputs));
        }

        Level = level;
        Rule = rule;
        Inputs = inputs;
    }

    public EventSeverity Level { get; }
    public string Rule { get; }
    public string Inputs { get; }
}
