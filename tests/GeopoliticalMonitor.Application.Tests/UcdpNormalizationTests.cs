using GeopoliticalMonitor.Application.Ucdp;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using static GeopoliticalMonitor.Application.Tests.UcdpTestData;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class UcdpNormalizationTests
{
    [Theory]
    [InlineData(0, EventSeverity.Low)]
    [InlineData(1, EventSeverity.Medium)]
    [InlineData(9, EventSeverity.Medium)]
    [InlineData(10, EventSeverity.High)]
    [InlineData(99, EventSeverity.High)]
    [InlineData(100, EventSeverity.Critical)]
    [InlineData(999, EventSeverity.Critical)]
    [InlineData(1000, EventSeverity.Catastrophic)]
    [InlineData(25000, EventSeverity.Catastrophic)]
    public void SeverityFollowsTheBestEstimateBoundaries(int best, EventSeverity expected)
    {
        var assessment = UcdpSeverityRule.Assess(best, 0, best);

        Assert.Equal(expected, assessment.Level);
        Assert.Equal("ucdp-candidate-v1", assessment.Rule);
        Assert.Equal($"best={best}; low=0; high={best}", assessment.Inputs);
    }

    [Fact]
    public void MapsARecordToAnUnverifiedEventAndADatasetSource()
    {
        var result = UcdpCandidateNormalizer.Normalize([Record(dateStart: "2026-08-03", dateEnd: "2026-08-05")], August2026, RetrievedAt);

        var import = Assert.Single(result.Imports);
        var item = import.Event;
        Assert.Equal("evt-ucdp-cand-1001", item.Id.Value);
        Assert.Equal(EventProvider.UcdpCandidate, item.Provider);
        Assert.Equal("1001", item.ProviderKey);
        Assert.Equal(202608, item.ProviderLastSeenRelease);
        Assert.Equal(EventStatus.Unverified, item.Status);
        Assert.Equal(EventCategory.MilitaryConflict, item.Category);
        Assert.Equal("State-based violence: Khartoum town", item.Title);
        Assert.Contains("Government of Sudan versus RSF", item.Summary);
        Assert.Contains("Date: 2026-08-03 to 2026-08-05", item.Summary);
        Assert.Contains("Fatalities (best/low/high): 12/10/15", item.Summary);
        Assert.Contains("Code status: Clear", item.Summary);
        Assert.Contains("Candidate data may be revised or removed in later UCDP releases.", item.Summary);
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero), item.StartedAtUtc);
        Assert.Equal(new DateTimeOffset(2026, 8, 5, 23, 59, 59, TimeSpan.Zero), item.LastUpdatedAtUtc);
        Assert.Equal("Khartoum town, Sudan", item.Location.DisplayName);
        Assert.Equal("Africa", item.Location.Region);
        Assert.Null(item.Location.CountryCode);
        Assert.Equal(EventSeverity.High, item.Severity);
        Assert.Equal("best=12; low=10; high=15", item.SeverityInputs);

        var source = import.Source;
        Assert.Equal("src-ucdp-cand-1001", source.Id.Value);
        Assert.Equal(SourceType.DatasetRecord, source.SourceType);
        Assert.Equal("UCDP:CAND:1001", source.ExternalId);
        Assert.Equal("https://ucdp.uu.se/downloads/", source.Url!.AbsoluteUri);
        Assert.Contains("release 26.0.8", source.Text);
        Assert.Contains("Code status: Clear", source.Text);
        Assert.Contains("Event clarity: 1", source.Text);
        Assert.Contains("Number of sources: 3", source.Text);
        Assert.Contains("where_prec): 1", source.Text);
        Assert.Contains("date_prec): 1", source.Text);
        Assert.Contains(UcdpAttribution.Citation, source.Text);
        Assert.Contains("DEMSCORE", source.Text);
        Assert.Equal(new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero), source.PublishedAtUtc);
    }

    [Theory]
    [InlineData(1, EventCategory.MilitaryConflict, "State-based violence")]
    [InlineData(2, EventCategory.MilitaryConflict, "Non-state violence")]
    [InlineData(3, EventCategory.Security, "One-sided violence")]
    public void TypeOfViolenceMapsToCategoryAndLabel(int type, EventCategory category, string label)
    {
        var item = Assert.Single(UcdpCandidateNormalizer.Normalize([Record(typeOfViolence: type)], August2026, RetrievedAt).Imports).Event;

        Assert.Equal(category, item.Category);
        Assert.StartsWith(label + ": ", item.Title);
    }

    [Fact]
    public void WherePrecisionFourIsIncludedAndFiveIsSkipped()
    {
        var result = UcdpCandidateNormalizer.Normalize(
            [Record(id: 1, wherePrecision: 4), Record(id: 2, wherePrecision: 5), Record(id: 3, wherePrecision: 7)],
            August2026,
            RetrievedAt);

        Assert.Equal([1L], result.Imports.Select(item => item.ProviderEventId));
        Assert.Equal(2, result.SkippedImpreciseLocation);
    }

    [Fact]
    public void BestOneIsIncludedAndZeroIsSkipped()
    {
        var result = UcdpCandidateNormalizer.Normalize([Record(id: 1, best: 1), Record(id: 2, best: 0)], August2026, RetrievedAt);

        Assert.Equal([1L], result.Imports.Select(item => item.ProviderEventId));
        Assert.Equal(1, result.SkippedZeroFatalities);
        Assert.Equal(EventSeverity.Medium, result.Imports[0].Event.Severity);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(91, 10)]
    [InlineData(-91, 10)]
    [InlineData(10, 181)]
    [InlineData(10, -181)]
    [InlineData(double.NaN, 10)]
    [InlineData(10, double.PositiveInfinity)]
    public void InvalidOrZeroCoordinatesAreSkipped(double latitude, double longitude)
    {
        var result = UcdpCandidateNormalizer.Normalize([Record(latitude: latitude, longitude: longitude)], August2026, RetrievedAt);

        Assert.Empty(result.Imports);
        Assert.Equal(1, result.SkippedInvalidCoordinates);
    }

    [Fact]
    public void ZeroLatitudeAloneIsValid()
    {
        Assert.Single(UcdpCandidateNormalizer.Normalize([Record(latitude: 0, longitude: 12)], August2026, RetrievedAt).Imports);
    }

    [Theory]
    [InlineData("2026-08-01", true)]
    [InlineData("2026-08-31", true)]
    [InlineData("2026-07-31", false)]
    [InlineData("2026-09-01", false)]
    public void DateStartMustFallWithinTheReleaseMonth(string dateStart, bool included)
    {
        var result = UcdpCandidateNormalizer.Normalize([Record(dateStart: dateStart, dateEnd: "2026-09-02")], August2026, RetrievedAt);

        Assert.Equal(included ? 1 : 0, result.Imports.Count);
        Assert.Equal(included ? 0 : 1, result.SkippedOutsideWindow);
    }

    [Fact]
    public void MalformedAndDuplicateRecordsAreCountedSeparately()
    {
        var result = UcdpCandidateNormalizer.Normalize(
            [
                Record(id: 1),
                Record(id: 1),
                Record(id: 0),
                Record(id: 2, typeOfViolence: 4),
                Record(id: 3, wherePrecision: 0),
                Record(id: 4, dateStart: "2026-08-05", dateEnd: "2026-08-04"),
                Record(id: 5, low: -1),
            ],
            August2026,
            RetrievedAt);

        Assert.Single(result.Imports);
        Assert.Equal(6, result.SkippedMalformed);
    }

    [Fact]
    public void FallsBackToTheDyadWhenSidesAreMissing()
    {
        var item = Assert.Single(UcdpCandidateNormalizer.Normalize([Record(sideA: "", sideB: " ")], August2026, RetrievedAt).Imports).Event;

        Assert.StartsWith("Government of Sudan - RSF\n", item.Summary);
    }

    [Fact]
    public void ReleaseCapKeepsTheHighestBestThenTheLowestId()
    {
        var result = UcdpCandidateNormalizer.Normalize(
            [Record(id: 5, best: 3), Record(id: 4, best: 50), Record(id: 2, best: 3), Record(id: 3, best: 50), Record(id: 1, best: 1)],
            August2026,
            RetrievedAt,
            maximumEvents: 3);

        Assert.Equal([3L, 4L, 2L], result.Imports.Select(item => item.ProviderEventId));
        Assert.Equal(2, result.DroppedByCap);
    }

    [Fact]
    public void UntrustedTextIsSanitizedAndTruncatedToDatabaseLimits()
    {
        var hostile = "Villé\u0007   ‮place\t" + new string('x', 400);
        var result = UcdpCandidateNormalizer.Normalize(
            [Record(whereCoordinates: hostile, sideA: new string('a', 1200), sideB: new string('b', 1200), region: new string('r', 300))],
            August2026,
            RetrievedAt);

        var item = Assert.Single(result.Imports).Event;
        Assert.StartsWith("State-based violence: Villé place", item.Title);
        Assert.True(item.Title.Length <= 256);
        Assert.DoesNotContain('\u0007', item.Title);
        Assert.DoesNotContain('‮', item.Title);
        Assert.True(item.Summary.Length <= 2000);
        Assert.True(item.Location.DisplayName.Length <= 256);
        Assert.True(item.Location.Region!.Length <= 128);
        Assert.True(result.Imports[0].Source.Text.Length <= 4000);
    }

    [Fact]
    public void SanitizerNormalizesStripsCollapsesAndKeepsSurrogatePairsWhole()
    {
        Assert.Equal("é a b", ProviderTextSanitizer.SingleLine("é \u0000a\r\n\tb ", 100));
        Assert.Equal("line one\nline two", ProviderTextSanitizer.MultiLine("  line   one \r\n\r\n\u0001line two\r", 100));
        Assert.Equal("ab", ProviderTextSanitizer.SingleLine("ab\U0001F600", 3));
        Assert.Equal("a�b", ProviderTextSanitizer.SingleLine("a\uD800b", 10));
        Assert.Equal(string.Empty, ProviderTextSanitizer.SingleLine(null, 10));
    }

    [Fact]
    public void ReleaseNamesRollOverTheYearBoundary()
    {
        var december = new UcdpRelease(2026, 12);

        Assert.Equal("GEDEvent_v26_0_8.csv", August2026.FileName);
        Assert.Equal("GEDEvent_v26_0_12.csv", december.FileName);
        Assert.Equal("GEDEvent_v27_0_1.csv", december.Next().FileName);
        Assert.Equal(december, new UcdpRelease(2027, 1).Previous());
        Assert.Equal(202608, August2026.Ordinal);
        Assert.Equal("2026-08", August2026.MonthKey);
        Assert.Equal("August 2026", August2026.MonthText);
        Assert.Equal(new DateTimeOffset(2027, 1, 20, 0, 0, 0, TimeSpan.Zero), december.ExtractedAtUtc);
        Assert.False(UcdpRelease.TryFromOrdinal(202613, out _));
        Assert.False(UcdpRelease.TryFromOrdinal(99, out _));
    }
}
