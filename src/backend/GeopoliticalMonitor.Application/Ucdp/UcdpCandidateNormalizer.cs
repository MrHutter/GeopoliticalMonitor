using System.Globalization;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>Design section 5.1 inclusion and mapping rules for one UCDP Candidate release.</summary>
public static class UcdpCandidateNormalizer
{
    public const string EventIdPrefix = "evt-ucdp-cand-";
    public const string SourceIdPrefix = "src-ucdp-cand-";
    public const string ExternalIdPrefix = "UCDP:CAND:";
    public const int MaximumEventsPerRelease = 5000;
    public const int MaximumTitleLength = 256;
    public const int MaximumSummaryLength = 2000;
    public const int MaximumSourceTextLength = 4000;
    public const int MaximumLocationLength = 256;
    public const int MaximumRegionLength = 128;

    public static UcdpNormalizationResult Normalize(
        IReadOnlyList<UcdpCandidateRecord> records,
        UcdpRelease release,
        DateTimeOffset retrievedAtUtc,
        int maximumEvents = MaximumEventsPerRelease)
    {
        var invalidCoordinates = 0;
        var impreciseLocation = 0;
        var zeroFatalities = 0;
        var outsideWindow = 0;
        var malformed = 0;
        var seenIds = new HashSet<long>();
        var included = new List<UcdpCandidateRecord>(records.Count);

        foreach (var record in records)
        {
            if (!IsStructurallyValid(record) || !seenIds.Add(record.Id))
            {
                malformed++;
            }
            else if (!HasValidCoordinates(record.Latitude, record.Longitude))
            {
                invalidCoordinates++;
            }
            else if (record.WherePrecision > 4)
            {
                impreciseLocation++;
            }
            else if (record.Best < 1)
            {
                zeroFatalities++;
            }
            else if (!release.Contains(record.DateStart))
            {
                outsideWindow++;
            }
            else
            {
                included.Add(record);
            }
        }

        var ranked = included
            .OrderByDescending(record => record.Best)
            .ThenBy(record => record.Id)
            .Take(Math.Max(0, maximumEvents))
            .ToArray();
        var imports = ranked.Select(record => ToImport(record, release, retrievedAtUtc)).ToArray();

        return new UcdpNormalizationResult(
            imports,
            invalidCoordinates,
            impreciseLocation,
            zeroFatalities,
            outsideWindow,
            malformed,
            included.Count - ranked.Length);
    }

    public static bool HasValidCoordinates(double latitude, double longitude)
        => double.IsFinite(latitude)
            && double.IsFinite(longitude)
            && latitude is >= -90 and <= 90
            && longitude is >= -180 and <= 180
            && !(latitude == 0 && longitude == 0);

    public static EventCategory MapCategory(int typeOfViolence) => typeOfViolence switch
    {
        1 or 2 => EventCategory.MilitaryConflict,
        3 => EventCategory.Security,
        _ => throw new ArgumentOutOfRangeException(nameof(typeOfViolence), "UCDP type of violence is not supported."),
    };

    public static string TypeLabel(int typeOfViolence) => typeOfViolence switch
    {
        1 => "State-based violence",
        2 => "Non-state violence",
        3 => "One-sided violence",
        _ => throw new ArgumentOutOfRangeException(nameof(typeOfViolence), "UCDP type of violence is not supported."),
    };

    /// <summary>D1: the Event revision timestamp is the end of the event's last day (UTC), not the release time.</summary>
    public static DateTimeOffset EndOfDayUtc(DateOnly date)
        => new(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);

    private static bool IsStructurallyValid(UcdpCandidateRecord record)
        => record.Id > 0
            && record.TypeOfViolence is >= 1 and <= 3
            && record.WherePrecision is >= 1 and <= 7
            && record.Best >= 0
            && record.Low >= 0
            && record.High >= 0
            && record.DateEnd >= record.DateStart;

    private static UcdpCandidateImport ToImport(UcdpCandidateRecord record, UcdpRelease release, DateTimeOffset retrievedAtUtc)
    {
        var providerKey = record.Id.ToString(CultureInfo.InvariantCulture);
        var whereText = ProviderTextSanitizer.SingleLine(record.WhereCoordinates, MaximumLocationLength);
        var country = ProviderTextSanitizer.SingleLine(record.Country, MaximumLocationLength);
        var place = whereText.Length > 0 ? whereText : country.Length > 0 ? country : "Location not named";
        var displayName = ProviderTextSanitizer.SingleLine(
            country.Length > 0 && !string.Equals(place, country, StringComparison.Ordinal) ? $"{place}, {country}" : place,
            MaximumLocationLength);
        var region = ProviderTextSanitizer.SingleLine(record.Region, MaximumRegionLength);

        // UCDP country_id is a Gleditsch-Ward number, not an ISO code, so no country code is stored.
        var location = new Location(record.Latitude, record.Longitude, displayName, countryCode: null, region: region.Length > 0 ? region : null);

        var title = ProviderTextSanitizer.SingleLine($"{TypeLabel(record.TypeOfViolence)}: {place}", MaximumTitleLength);
        var summary = ProviderTextSanitizer.MultiLine(BuildSummary(record), MaximumSummaryLength);

        var geopoliticalEvent = Event.Create(
            new EventId($"{EventIdPrefix}{providerKey}"),
            title,
            summary,
            MapCategory(record.TypeOfViolence),
            EventStatus.Unverified,
            new DateTimeOffset(record.DateStart.Year, record.DateStart.Month, record.DateStart.Day, 0, 0, 0, TimeSpan.Zero),
            EndOfDayUtc(record.DateEnd),
            location,
            retrievedAtUtc,
            retrievedAtUtc,
            UcdpSeverityRule.Assess(record.Best, record.Low, record.High),
            new EventProvenance(EventProvider.UcdpCandidate, providerKey, release.Ordinal));

        var source = SourceItem.Create(
            new SourceItemId($"{SourceIdPrefix}{providerKey}"),
            SourceType.DatasetRecord,
            $"{ExternalIdPrefix}{providerKey}",
            ProviderTextSanitizer.SingleLine($"{UcdpAttribution.DatasetName} {release.VersionLabel}, event {providerKey}", MaximumTitleLength),
            ProviderTextSanitizer.MultiLine(BuildProvenanceText(record, release), MaximumSourceTextLength),
            new Uri(UcdpAttribution.DownloadsPageUrl),
            UcdpAttribution.ProviderName,
            null,
            release.ExtractedAtUtc,
            retrievedAtUtc,
            location,
            retrievedAtUtc);

        return new UcdpCandidateImport(record.Id, geopoliticalEvent, source);
    }

    private static string BuildSummary(UcdpCandidateRecord record)
    {
        var sideA = ProviderTextSanitizer.SingleLine(record.SideA, 300);
        var sideB = ProviderTextSanitizer.SingleLine(record.SideB, 300);
        var dyad = ProviderTextSanitizer.SingleLine(record.DyadName, 600);
        var sides = sideA.Length > 0 && sideB.Length > 0
            ? $"{sideA} versus {sideB}"
            : dyad.Length > 0 ? dyad : "Sides not provided";
        var codeStatus = ProviderTextSanitizer.SingleLine(record.CodeStatus, 64);

        return string.Join(
            '\n',
            sides,
            $"Date: {FormatDateRange(record.DateStart, record.DateEnd)}",
            string.Create(CultureInfo.InvariantCulture, $"Fatalities (best/low/high): {record.Best}/{record.Low}/{record.High}"),
            $"Code status: {(codeStatus.Length > 0 ? codeStatus : "Not provided")}",
            UcdpAttribution.RevisionCaveat);
    }

    private static string BuildProvenanceText(UcdpCandidateRecord record, UcdpRelease release)
    {
        return string.Join(
            '\n',
            $"Dataset: {UcdpAttribution.DatasetName}, release {release.VersionLabel} (events from {release.MonthText})",
            string.Create(CultureInfo.InvariantCulture, $"UCDP event id: {record.Id}"),
            $"Code status: {ValueOrNotProvided(record.CodeStatus)}",
            $"Event clarity: {ValueOrNotProvided(record.EventClarity)}",
            $"Number of sources: {ValueOrNotProvided(record.NumberOfSources)}",
            string.Create(CultureInfo.InvariantCulture, $"Location precision (where_prec): {record.WherePrecision}"),
            string.Create(CultureInfo.InvariantCulture, $"Date precision (date_prec): {record.DatePrecision}"),
            $"Citation: {UcdpAttribution.Citation}",
            UcdpAttribution.Acknowledgement,
            UcdpAttribution.License,
            UcdpAttribution.RevisionCaveat);
    }

    private static string ValueOrNotProvided(string? value)
    {
        var sanitized = ProviderTextSanitizer.SingleLine(value, 64);
        return sanitized.Length > 0 ? sanitized : "Not provided";
    }

    private static string FormatDateRange(DateOnly start, DateOnly end)
    {
        var startText = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return start == end ? startText : $"{startText} to {end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }
}
