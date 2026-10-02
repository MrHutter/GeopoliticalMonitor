namespace GeopoliticalMonitor.Domain.Events;

public sealed record EventProvenance
{
    public const int MaximumProviderKeyLength = 256;
    public const int MinimumReleaseOrdinal = 200001;
    public const int MaximumReleaseOrdinal = 999912;

    public EventProvenance(EventProvider provider, string? providerKey = null, int? providerLastSeenRelease = null)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), "Event provider is not supported.");
        }

        if (providerKey is not null && providerKey.Length > MaximumProviderKeyLength)
        {
            throw new ArgumentException($"Event provider key must be at most {MaximumProviderKeyLength} characters.", nameof(providerKey));
        }

        if (providerLastSeenRelease is { } release && !IsValidReleaseOrdinal(release))
        {
            throw new ArgumentOutOfRangeException(nameof(providerLastSeenRelease), "Provider release ordinal must be year times 100 plus month.");
        }

        Provider = provider;
        ProviderKey = string.IsNullOrWhiteSpace(providerKey) ? null : providerKey;
        ProviderLastSeenRelease = providerLastSeenRelease;
    }

    public EventProvider Provider { get; }
    public string? ProviderKey { get; }

    /// <summary>The newest provider release (year times 100 plus month) that contained this Event, when the provider publishes releases.</summary>
    public int? ProviderLastSeenRelease { get; }

    public static bool IsValidReleaseOrdinal(int value)
        => value is >= MinimumReleaseOrdinal and <= MaximumReleaseOrdinal && value % 100 is >= 1 and <= 12;
}
