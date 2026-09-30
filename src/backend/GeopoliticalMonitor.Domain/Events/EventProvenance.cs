namespace GeopoliticalMonitor.Domain.Events;

public sealed record EventProvenance
{
    public const int MaximumProviderKeyLength = 256;

    public EventProvenance(EventProvider provider, string? providerKey = null)
    {
        if (!Enum.IsDefined(provider))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), "Event provider is not supported.");
        }

        if (providerKey is not null && providerKey.Length > MaximumProviderKeyLength)
        {
            throw new ArgumentException($"Event provider key must be at most {MaximumProviderKeyLength} characters.", nameof(providerKey));
        }

        Provider = provider;
        ProviderKey = string.IsNullOrWhiteSpace(providerKey) ? null : providerKey;
    }

    public EventProvider Provider { get; }
    public string? ProviderKey { get; }
}
