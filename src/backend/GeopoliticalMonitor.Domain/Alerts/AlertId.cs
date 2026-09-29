namespace GeopoliticalMonitor.Domain.Alerts;

public readonly record struct AlertId
{
    public const int Length = 35;

    public AlertId(string value)
    {
        if (!TryParse(value, out _))
        {
            throw new ArgumentException("Alert id is invalid.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static AlertId CreateNew() => new($"al-{Guid.NewGuid():N}");

    public static bool TryParse(string? value, out AlertId alertId)
    {
        alertId = default;
        if (value is null || value.Length != Length || !value.StartsWith("al-", StringComparison.Ordinal))
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

        alertId = new AlertId(value, validated: true);
        return true;
    }

    public override string ToString() => Value;

    private AlertId(string value, bool validated)
    {
        Value = value;
    }
}
