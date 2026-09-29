namespace GeopoliticalMonitor.Infrastructure.Persistence.Entities;

public sealed class ApplicationPreferenceEntity
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}
