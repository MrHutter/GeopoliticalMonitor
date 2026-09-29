namespace GeopoliticalMonitor.Domain.Alerts;

public readonly record struct AlertRuleId
{
    public const int Length = 35;

    public AlertRuleId(string value)
    {
        if (!TryParse(value, out _))
        {
            throw new ArgumentException("Alert rule id is invalid.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static AlertRuleId CreateNew() => new($"ar-{Guid.NewGuid():N}");

    public static bool TryParse(string? value, out AlertRuleId alertRuleId)
    {
        alertRuleId = default;
        if (value is null || value.Length != Length || !value.StartsWith("ar-", StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = 3; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        alertRuleId = new AlertRuleId(value, validated: true);
        return true;
    }

    public override string ToString() => Value;

    private AlertRuleId(string value, bool validated)
    {
        Value = value;
    }
}
