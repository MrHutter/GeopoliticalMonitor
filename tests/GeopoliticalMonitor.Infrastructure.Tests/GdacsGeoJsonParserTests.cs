using System.Text;
using System.Text.Json;
using System.Globalization;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Infrastructure.Gdacs;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class GdacsGeoJsonParserTests
{
    private readonly GdacsGeoJsonParser parser = new();

    [Fact]
    public void ParsesFixtureEarthquakeFeaturesAndOptionalMetadata()
    {
        var payload = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gdacs-earthquakes.json"));

        var batch = parser.Parse(payload);

        Assert.Equal(2, batch.Records.Count);
        var first = batch.Records[0];
        Assert.Equal(9000001, first.EventId);
        Assert.Equal(9100001, first.EpisodeId);
        Assert.Equal(DateTimeOffset.Parse("2026-01-15T10:00:00Z"), first.StartedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2026-01-16T11:30:00Z"), first.ProviderUpdatedAtUtc);
        Assert.Equal(-140.25, first.Longitude);
        Assert.Equal(-45.5, first.Latitude);
        Assert.Equal("Orange", first.AlertLevel);
        Assert.Equal(6.5, first.Magnitude);
        Assert.Equal("Testland", first.Country);
        Assert.Equal("TST", first.Iso3);
        Assert.Equal("https://www.gdacs.org/report.aspx?eventid=9000001&episodeid=9100001&eventtype=EQ", first.CanonicalUrl.AbsoluteUri);
        Assert.Null(batch.Records[1].Magnitude);
        Assert.Null(batch.Records[1].Country);
        Assert.Equal("Earthquake near Test Island", batch.Records[1].Description);
    }

    [Theory]
    [InlineData("TC", "[160.5,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,91]", "9000001", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[181,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "0", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "9000001", "9100001", "not-a-date", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "http://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "https://example.com/report.aspx?eventid=1")]
    public void RejectsCompletelyInvalidFeature(string eventType, string coordinates, string eventId, string episodeId, string startedAt, string url)
    {
        var json = CreateFeatureCollection(eventType, coordinates, eventId, episodeId, startedAt, url);

        var exception = Assert.Throws<GdacsFeedException>(() => parser.Parse(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(GdacsFeedFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void SkipsMalformedSiblingAndRetainsValidRecord()
    {
        var valid = CreateFeatureCollection("EQ", "[160.5,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1");
        var json = valid.Insert(valid.LastIndexOf("]}", StringComparison.Ordinal), ",{\"type\":\"Feature\",\"geometry\":null,\"properties\":{}}");

        var batch = parser.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Single(batch.Records);
        Assert.Equal(1, batch.SkippedMalformedRecords);
    }

    [Fact]
    public void RejectsMarkupAndOverlongText()
    {
        var markup = CreateFeatureCollection("EQ", "[160.5,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")
            .Replace("Earthquake in Testland", "<b>Earthquake</b>", StringComparison.Ordinal);
        Assert.Throws<GdacsFeedException>(() => parser.Parse(Encoding.UTF8.GetBytes(markup)));

        var tooLong = CreateFeatureCollection("EQ", "[160.5,53.1]", "9000001", "9100001", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")
            .Replace("Earthquake in Testland", new string('x', 221), StringComparison.Ordinal);
        Assert.Throws<GdacsFeedException>(() => parser.Parse(Encoding.UTF8.GetBytes(tooLong)));
    }

    [Fact]
    public void RejectsMalformedJson()
    {
        var exception = Assert.Throws<GdacsFeedException>(() => parser.Parse(Encoding.UTF8.GetBytes("not json")));
        Assert.Equal(GdacsFeedFailureKind.InvalidResponse, exception.Kind);
    }

    private static string CreateFeatureCollection(string eventType, string coordinates, string eventId, string episodeId, string startedAt, string url)
    {
        var coordinateValues = coordinates.Trim('[', ']').Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        var response = new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    geometry = new { type = "Point", coordinates = coordinateValues },
                    properties = new
                    {
                        eventtype = eventType,
                        eventid = long.Parse(eventId),
                        episodeid = long.Parse(episodeId),
                        name = "Earthquake in Testland",
                        description = "Earthquake in Testland",
                        alertlevel = "Orange",
                        fromdate = startedAt,
                        datemodified = "2025-09-19T20:01:59",
                        country = "Russia",
                        iso3 = "RUS",
                        url = new { report = url },
                        severitydata = new { severity = 7.8 },
                    },
                },
            },
        };
        return JsonSerializer.Serialize(response);
    }
}
