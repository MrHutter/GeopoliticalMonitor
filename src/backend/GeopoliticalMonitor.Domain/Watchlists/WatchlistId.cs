namespace GeopoliticalMonitor.Domain.Watchlists;

public readonly record struct WatchlistId
{
    public const int Length = 35;

    public WatchlistId(string value)
    {
        if (!TryParse(value, out _))
        {
            throw new ArgumentException("Watchlist id is invalid.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public static WatchlistId CreateNew() => new($"wl-{Guid.NewGuid():N}");

    public static bool TryParse(string? value, out WatchlistId watchlistId)
    {
        watchlistId = default;
        if (value is null || value.Length != Length || !value.StartsWith("wl-", StringComparison.Ordinal))
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

        watchlistId = new WatchlistId(value, validated: true);
        return true;
    }

    public override string ToString() => Value;

    private WatchlistId(string value, bool validated)
    {
        Value = value;
    }
}
