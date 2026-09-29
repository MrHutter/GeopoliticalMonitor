using System.Text;
using System.Text.Json;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Infrastructure.ReliefWeb;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class ReliefWebReportParserTests
{
    private readonly ReliefWebReportParser parser = new();

    [Fact]
    public void ParsesBoundedReportMetadataAndIgnoresBodyAndFiles()
    {
        var payload = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reliefweb-reports.json"));

        var batch = parser.ParseSearch(payload);

        Assert.Equal(2, batch.Reports.Count);
        var report = batch.Reports[0];
        Assert.Equal(5187658, report.ReportId);
        Assert.Equal("Flood response situation report", report.Title);
        Assert.Equal("https://reliefweb.int/report/example-country/flood-response-situation-report", report.CanonicalUrl.AbsoluteUri);
        Assert.Equal(DateTimeOffset.Parse("2026-09-20T08:30:00Z"), report.OriginalPublishedAtUtc);
        Assert.Equal(["EHO", "Example Response Agency"], report.Organizations.Select(item => item.DisplayName));
        Assert.Equal("Situation Report", Assert.Single(report.Formats));
        Assert.Equal("EXA", Assert.Single(report.Countries).Iso3);
        Assert.Equal("FL-2026-000001-EXA", Assert.Single(report.Disasters).Glide);
        Assert.Equal("Flood", Assert.Single(Assert.Single(report.Disasters).Types));
        Assert.Equal("English", Assert.Single(report.Languages));
        Assert.Empty(batch.Reports[1].Organizations);
        Assert.Null(batch.Reports[1].OriginUrl);
    }

    [Fact]
    public void ItemParsingRequiresMatchingIdentityAndOriginalDate()
    {
        var payload = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "reliefweb-reports.json"));
        Assert.Equal(5187658, parser.ParseReport(payload, 5187658).ReportId);
        Assert.Throws<ReliefWebClientException>(() => parser.ParseReport(payload, 999));

        var missingDate = """{"data":[{"id":1,"fields":{"title":"Report","url":"https://reliefweb.int/report/test"}}]}""";
        Assert.Throws<ReliefWebClientException>(() => parser.ParseReport(Encoding.UTF8.GetBytes(missingDate), 1));
    }

    [Fact]
    public void SkipsMalformedSiblingButRejectsCompletelyMalformedPayload()
    {
        var mixed = """{"data":[{"id":1,"fields":{}},{"id":2,"fields":{"title":"Valid","url":"https://reliefweb.int/report/valid","date":{"original":"2026-09-20T00:00:00Z"}}}]}""";
        var batch = parser.ParseSearch(Encoding.UTF8.GetBytes(mixed));
        Assert.Single(batch.Reports);
        Assert.Equal(1, batch.SkippedMalformedReports);

        Assert.Equal(ReliefWebFailureKind.InvalidResponse,
            Assert.Throws<ReliefWebClientException>(() => parser.ParseSearch(Encoding.UTF8.GetBytes("{}"))).Kind);
        Assert.Equal(ReliefWebFailureKind.InvalidResponse,
            Assert.Throws<ReliefWebClientException>(() => parser.ParseSearch(Encoding.UTF8.GetBytes("not json"))).Kind);
        Assert.Throws<ReliefWebClientException>(() => parser.ParseSearch(Encoding.UTF8.GetBytes("""{"data":[{"id":1,"fields":{}}]}""")));
    }

    [Fact]
    public void RejectsUnsafeUrlsAndBoundViolations()
    {
        var unsafeUrl = """{"data":[{"id":1,"fields":{"title":"Report","url":"https://example.com/report"}}]}""";
        Assert.Throws<ReliefWebClientException>(() => parser.ParseSearch(Encoding.UTF8.GetBytes(unsafeUrl)));

        var longTitle = new string('x', 257);
        var oversized = JsonSerializer.Serialize(new
        {
            data = new[] { new { id = 1, fields = new { title = longTitle, url = "https://reliefweb.int/report/test" } } },
        });
        Assert.Throws<ReliefWebClientException>(() => parser.ParseSearch(Encoding.UTF8.GetBytes(oversized)));

        var tooMany = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new
                {
                    id = 1,
                    fields = new
                    {
                        title = "Report",
                        url = "https://reliefweb.int/report/test",
                        format = Enumerable.Range(1, 17).Select(index => new { name = $"Format {index}" }),
                    },
                },
            },
        });
        Assert.Throws<ReliefWebClientException>(() => parser.ParseSearch(Encoding.UTF8.GetBytes(tooMany)));
    }
}
