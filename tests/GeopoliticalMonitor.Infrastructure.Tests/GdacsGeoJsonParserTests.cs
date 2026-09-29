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
    public void ParsesRecordedEarthquakeFeaturesAndOptionalMetadata()
    {
        var payload = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gdacs-earthquakes.json"));

        var batch = parser.Parse(payload);

        Assert.Equal(2, batch.Records.Count);
        var first = batch.Records[0];
        Assert.Equal(1500811, first.EventId);
        Assert.Equal(1661062, first.EpisodeId);
        Assert.Equal(DateTimeOffset.Parse("2025-09-18T18:58:12Z"), first.StartedAtUtc);
        Assert.Equal(DateTimeOffset.Parse("2025-09-19T20:01:59Z"), first.ProviderUpdatedAtUtc);
        Assert.Equal(160.5129, first.Longitude);
        Assert.Equal(53.1928, first.Latitude);
        Assert.Equal("Orange", first.AlertLevel);
        Assert.Equal(7.8, first.Magnitude);
        Assert.Equal("Russia", first.Country);
        Assert.Equal("RUS", first.Iso3);
        Assert.Equal("https://www.gdacs.org/report.aspx?eventid=1500811&episodeid=1661062&eventtype=EQ", first.CanonicalUrl.AbsoluteUri);
        Assert.Null(batch.Records[1].Magnitude);
        Assert.Null(batch.Records[1].Country);
        Assert.Equal("Earthquake near Iceland", batch.Records[1].Description);
    }

    [Theory]
    [InlineData("TC", "[160.5,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,91]", "1500811", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[181,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "0", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "1500811", "1661062", "not-a-date", "https://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "http://www.gdacs.org/report.aspx?eventid=1")]
    [InlineData("EQ", "[160.5,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "https://example.com/report.aspx?eventid=1")]
    public void RejectsCompletelyInvalidFeature(string eventType, string coordinates, string eventId, string episodeId, string startedAt, string url)
    {
        var json = CreateFeatureCollection(eventType, coordinates, eventId, episodeId, startedAt, url);

        var exception = Assert.Throws<GdacsFeedException>(() => parser.Parse(Encoding.UTF8.GetBytes(json)));

        Assert.Equal(GdacsFeedFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void SkipsMalformedSiblingAndRetainsValidRecord()
    {
        var valid = CreateFeatureCollection("EQ", "[160.5,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1");
        var json = valid.Insert(valid.LastIndexOf("]}", StringComparison.Ordinal), ",{\"type\":\"Feature\",\"geometry\":null,\"properties\":{}}");

        var batch = parser.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Single(batch.Records);
        Assert.Equal(1, batch.SkippedMalformedRecords);
    }

    [Fact]
    public void RejectsMarkupAndOverlongText()
    {
        var markup = CreateFeatureCollection("EQ", "[160.5,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")
            .Replace("Earthquake in Russia", "<b>Earthquake</b>", StringComparison.Ordinal);
        Assert.Throws<GdacsFeedException>(() => parser.Parse(Encoding.UTF8.GetBytes(markup)));

        var tooLong = CreateFeatureCollection("EQ", "[160.5,53.1]", "1500811", "1661062", "2025-09-18T18:58:12", "https://www.gdacs.org/report.aspx?eventid=1")
            .Replace("Earthquake in Russia", new string('x', 221), StringComparison.Ordinal);
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
                        name = "Earthquake in Russia",
                        description = "Earthquake in Russia",
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
