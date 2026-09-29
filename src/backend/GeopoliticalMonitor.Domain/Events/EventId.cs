namespace GeopoliticalMonitor.Domain.Events;

public readonly record struct EventId
{
    public EventId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Event id is required.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}