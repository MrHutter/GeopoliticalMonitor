using System.Globalization;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>
/// One monthly UCDP Candidate Events release, identified by the month its events cover (v26.0.8 covers August 2026).
/// File names are derived only from this value, never from web input.
/// </summary>
public readonly record struct UcdpRelease
{
    public const int ExtractionDay = 20;

    public UcdpRelease(int year, int month)
    {
        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "UCDP release year is out of range.");
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "UCDP release month is out of range.");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }
    public int Month { get; }

    /// <summary>Year times 100 plus month, for example 202608. Used as the UCDP provider revision.</summary>
    public int Ordinal => (Year * 100) + Month;

    /// <summary>Monthly file name, for example GEDEvent_v26_0_8.csv (no zero padding on the month).</summary>
    public string FileName => string.Create(CultureInfo.InvariantCulture, $"GEDEvent_v{Year % 100:00}_0_{Month}.csv");

    /// <summary>UCDP version label, for example 26.0.8.</summary>
    public string VersionLabel => string.Create(CultureInfo.InvariantCulture, $"{Year % 100:00}.0.{Month}");

    /// <summary>Machine month key, for example 2026-08.</summary>
    public string MonthKey => string.Create(CultureInfo.InvariantCulture, $"{Year:0000}-{Month:00}");

    /// <summary>Human month, for example August 2026.</summary>
    public string MonthText => FirstDay.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => FirstDay.AddMonths(1).AddDays(-1);

    /// <summary>UCDP extracts each monthly release on the 20th of the following month.</summary>
    public DateTimeOffset ExtractedAtUtc
    {
        get
        {
            var next = Next();
            return new DateTimeOffset(next.Year, next.Month, ExtractionDay, 0, 0, 0, TimeSpan.Zero);
        }
    }

    public UcdpRelease Next() => Month == 12 ? new UcdpRelease(Year + 1, 1) : new UcdpRelease(Year, Month + 1);

    public UcdpRelease Previous() => Month == 1 ? new UcdpRelease(Year - 1, 12) : new UcdpRelease(Year, Month - 1);

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    public static UcdpRelease ForMonthOf(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new UcdpRelease(utc.Year, utc.Month);
    }

    public static bool TryFromOrdinal(int ordinal, out UcdpRelease release)
    {
        release = default;
        if (!EventProvenance.IsValidReleaseOrdinal(ordinal))
        {
            return false;
        }

        release = new UcdpRelease(ordinal / 100, ordinal % 100);
        return true;
    }

    public static UcdpRelease FromOrdinal(int ordinal)
        => TryFromOrdinal(ordinal, out var release)
            ? release
            : throw new ArgumentOutOfRangeException(nameof(ordinal), "UCDP release ordinal is invalid.");

    public override string ToString() => VersionLabel;
}
