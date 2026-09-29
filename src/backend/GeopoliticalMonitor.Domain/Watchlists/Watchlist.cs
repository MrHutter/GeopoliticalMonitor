using System.Globalization;
using System.Text;

namespace GeopoliticalMonitor.Domain.Watchlists;

public sealed class Watchlist
{
    public const int MaximumNameLength = 80;

    private Watchlist(WatchlistId id, string name)
    {
        Id = id;
        Name = name;
    }

    public WatchlistId Id { get; }
    public string Name { get; }

    public static Watchlist Create(WatchlistId id, string name)
    {
        return new Watchlist(id, NormalizeName(name));
    }

    public Watchlist Rename(string name)
    {
        return new Watchlist(Id, NormalizeName(name));
    }

    public static string NormalizeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var canonical = name.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(canonical.Length);
        var pendingSpace = false;
        foreach (var character in canonical)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (char.GetUnicodeCategory(character) == UnicodeCategory.Control)
            {
                throw new ArgumentException("Watchlist name contains a control character.", nameof(name));
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        var normalized = builder.ToString();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Watchlist name is required.", nameof(name));
        }

        if (normalized.Length > MaximumNameLength)
        {
            throw new ArgumentException($"Watchlist name cannot exceed {MaximumNameLength} characters.", nameof(name));
        }

        return normalized;
    }

    public static string CreateComparisonKey(string normalizedName)
    {
        return normalizedName.ToUpperInvariant();
    }
}
