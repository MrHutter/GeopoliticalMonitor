using System.Text;
using GeopoliticalMonitor.Application.Ucdp;
using GeopoliticalMonitor.Infrastructure.Ucdp;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class UcdpCandidateCsvParserTests
{
    private static readonly string FixtureText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ucdp-candidate-sample.csv"))
        .Replace("\r\n", "\n", StringComparison.Ordinal);

    private readonly UcdpCandidateCsvParser parser = new();

    [Fact]
    public void ParsesTheSyntheticReleaseFixture()
    {
        var batch = parser.Parse(FixtureText);

        Assert.Equal([900001L, 900002L, 900003L, 900004L, 900005L], batch.Records.Select(item => item.Id));
        Assert.Equal(2, batch.SkippedMalformedRecords);

        var first = batch.Records[0];
        Assert.Equal("Northtown, Alpha province", first.WhereCoordinates);
        Assert.Equal("Government of Testland", first.SideA);
        Assert.Equal("Testland Liberation Front", first.SideB);
        Assert.Equal(1, first.TypeOfViolence);
        Assert.Equal(1, first.WherePrecision);
        Assert.Equal(10.5, first.Latitude);
        Assert.Equal(20.25, first.Longitude);
        Assert.Equal(new DateOnly(2026, 8, 3), first.DateStart);
        Assert.Equal(new DateOnly(2026, 8, 4), first.DateEnd);
        Assert.Equal((12, 15, 10), (first.Best, first.High, first.Low));
        Assert.Equal("Clear", first.CodeStatus);
        Assert.Equal("1", first.EventClarity);
        Assert.Equal("2", first.NumberOfSources);

        Assert.Equal(new DateOnly(2026, 8, 10), batch.Records[1].DateEnd);
        Assert.Equal("Check Deaths", batch.Records[1].CodeStatus);
        Assert.Equal(-3.25, batch.Records[1].Latitude);
    }

    [Fact]
    public void TheRecordTypeCarriesNoArticleHeadlineOrGeometryText()
    {
        var names = typeof(UcdpCandidateRecord).GetProperties().Select(item => item.Name).ToArray();

        Assert.DoesNotContain(names, name => name.Contains("Headline", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Article", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Office", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Original", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SourceDate", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Geom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrLfLineEndingsAndAByteOrderMarkGiveTheSameResult()
    {
        var crlf = FixtureText.Replace("\n", "\r\n", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(crlf)).ToArray();

        var batch = parser.Parse(bytes);

        Assert.Equal(5, batch.Records.Count);
        Assert.Equal(2, batch.SkippedMalformedRecords);
        Assert.Equal("Northtown, Alpha province", batch.Records[0].WhereCoordinates);
    }

    [Fact]
    public void QuotedFieldsKeepCommasEscapedQuotesAndCrLfOrCrLineBreaks()
    {
        var csv = Header() + "\n"
            + Row(id: "1", where: "\"Town, \"\"North\"\" side\"", dyad: "\"Line one\r\nline two\rline three\"") + "\n";

        var record = Assert.Single(parser.Parse(csv).Records);

        Assert.Equal("Town, \"North\" side", record.WhereCoordinates);
        Assert.Equal("Line one\r\nline two\rline three", record.DyadName);
    }

    [Fact]
    public void ReorderedAndExtraColumnsAreResolvedByName()
    {
        var columns = UcdpCandidateCsvParser.RequiredColumns.Reverse().Append("extra_column").Prepend("another_extra").ToArray();
        var values = new Dictionary<string, string>
        {
            ["id"] = "77", ["code_status"] = "Clear", ["type_of_violence"] = "2", ["dyad_name"] = "A - B", ["side_a"] = "A", ["side_b"] = "B",
            ["where_prec"] = "2", ["where_coordinates"] = "Place", ["latitude"] = "1.5", ["longitude"] = "2.5", ["country"] = "Testland",
            ["region"] = "Asia", ["date_prec"] = "1", ["date_start"] = "2026-08-01", ["date_end"] = "2026-08-02", ["best"] = "4",
            ["high"] = "5", ["low"] = "3", ["extra_column"] = "ignored", ["another_extra"] = "ignored",
        };
        var csv = string.Join(',', columns) + "\n" + string.Join(',', columns.Select(name => values[name])) + "\n";

        var record = Assert.Single(parser.Parse(csv).Records);

        Assert.Equal(77, record.Id);
        Assert.Equal("Place", record.WhereCoordinates);
        Assert.Equal((4, 5, 3), (record.Best, record.High, record.Low));
        Assert.Null(record.EventClarity);
        Assert.Null(record.NumberOfSources);
    }

    [Theory]
    [InlineData("best")]
    [InlineData("id")]
    [InlineData("where_prec")]
    [InlineData("date_end")]
    public void AMissingRequiredColumnIsAnInvalidResponse(string missing)
    {
        var header = string.Join(',', UcdpCandidateCsvParser.RequiredColumns.Where(name => name != missing));

        var exception = Assert.Throws<UcdpFeedException>(() => parser.Parse(header + "\n"));

        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void ADuplicatedRequiredColumnOrAnEmptyPayloadIsAnInvalidResponse()
    {
        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, Assert.Throws<UcdpFeedException>(() => parser.Parse(Header() + ",best\n")).Kind);
        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, Assert.Throws<UcdpFeedException>(() => parser.Parse("\n\n")).Kind);
        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, Assert.Throws<UcdpFeedException>(() => parser.Parse(string.Empty)).Kind);
    }

    [Fact]
    public void MalformedRowsAreSkippedAndCountedNeverThrown()
    {
        var csv = string.Join('\n',
            Header(),
            Row(id: "1"),
            Row(id: "-5"),
            Row(id: "2", latitude: "north"),
            Row(id: "3", best: "1.5"),
            Row(id: "4", dateStart: "08/03/2026"),
            Row(id: "5", where: "bad\"quote"),
            Row(id: "6", where: "\"closed\"trailing"),
            "7,too,few",
            "",
            Row(id: "8"),
            Row(id: "9", where: "\"never closed"));

        var batch = parser.Parse(csv);

        Assert.Equal([1L, 8L], batch.Records.Select(item => item.Id));
        Assert.Equal(8, batch.SkippedMalformedRecords);
    }

    [Fact]
    public void FieldLongerThanTheLimitSkipsTheRow()
    {
        var csv = Header() + "\n"
            + Row(id: "1", where: new string('a', UcdpCandidateCsvParser.MaximumFieldLength)) + "\n"
            + Row(id: "2", where: new string('a', UcdpCandidateCsvParser.MaximumFieldLength + 1)) + "\n";

        var batch = parser.Parse(csv);

        Assert.Equal([1L], batch.Records.Select(item => item.Id));
        Assert.Equal(1, batch.SkippedMalformedRecords);
    }

    [Fact]
    public void RowLongerThanTheLimitIsSkipped()
    {
        var wide = Header() + string.Concat(Enumerable.Range(0, 10).Select(index => $",pad{index}"));
        var padding = string.Concat(Enumerable.Range(0, 10).Select(_ => "," + new string('p', 7000)));
        var csv = wide + "\n" + Row(id: "1") + padding + "\n" + Row(id: "2") + string.Concat(Enumerable.Range(0, 10).Select(_ => ",x")) + "\n";

        var batch = parser.Parse(csv);

        Assert.Equal([2L], batch.Records.Select(item => item.Id));
        Assert.Equal(1, batch.SkippedMalformedRecords);
    }

    [Fact]
    public void MoreThanTheRowLimitIsAnInvalidResponse()
    {
        var builder = new StringBuilder(Header()).Append('\n');
        var row = Row(id: "1");
        for (var index = 0; index <= UcdpCandidateCsvParser.MaximumRows; index++)
        {
            builder.Append(row).Append('\n');
        }

        var exception = Assert.Throws<UcdpFeedException>(() => parser.Parse(builder.ToString()));

        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, exception.Kind);
    }

    [Fact]
    public void NumbersUseTheInvariantCulture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fi-FI");
            var record = Assert.Single(parser.Parse(Header() + "\n" + Row(id: "1", latitude: "60.25") + "\n").Records);
            Assert.Equal(60.25, record.Latitude);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private static string Header() => string.Join(',', UcdpCandidateCsvParser.RequiredColumns);

    private static string Row(
        string id,
        string where = "Place",
        string dyad = "A - B",
        string latitude = "10",
        string best = "3",
        string dateStart = "2026-08-02")
    {
        var values = new Dictionary<string, string>
        {
            ["id"] = id, ["code_status"] = "Clear", ["type_of_violence"] = "1", ["dyad_name"] = dyad, ["side_a"] = "A", ["side_b"] = "B",
            ["where_prec"] = "1", ["where_coordinates"] = where, ["latitude"] = latitude, ["longitude"] = "20", ["country"] = "Testland",
            ["region"] = "Africa", ["date_prec"] = "1", ["date_start"] = dateStart, ["date_end"] = "2026-08-03", ["best"] = best,
            ["high"] = "4", ["low"] = "2",
        };
        return string.Join(',', UcdpCandidateCsvParser.RequiredColumns.Select(name => values[name]));
    }
}
