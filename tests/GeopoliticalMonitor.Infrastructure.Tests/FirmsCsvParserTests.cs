using System.Text;
using GeopoliticalMonitor.Application.Firms;
using GeopoliticalMonitor.Infrastructure.Firms;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public class FirmsCsvParserTests
{
    private const string Header = "latitude,longitude,bright_ti4,scan,track,acq_date,acq_time,satellite,instrument,confidence,version,bright_ti5,frp,daynight";

    [Theory]
    [InlineData(FirmsSource.ViirsNoaa20Nrt, "1", "n", "Day")]
    [InlineData(FirmsSource.ViirsNoaa21Nrt, "2", "h", "Night")]
    public void ParsesRepresentativeViirsCsv(FirmsSource source, string satellite, string confidence, string expectedDayNight)
    {
        var csv = $"{Header}\n60.1234,24.5678,331.2,0.42,0.51,2026-09-28,1437,{satellite},VIIRS,{confidence},2.0NRT,290.4,18.6,{expectedDayNight[0]}\n";

        var result = new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(csv), source);

        var observation = Assert.Single(result.Observations);
        Assert.Equal(source.ToProviderValue(), observation.Source);
        Assert.Equal(new DateTimeOffset(2026, 9, 28, 14, 37, 0, TimeSpan.Zero), observation.AcquiredAtUtc);
        Assert.Equal(60.1234, observation.Latitude);
        Assert.Equal(18.6, observation.Frp);
        Assert.Equal(expectedDayNight, observation.DayNight);
        Assert.Matches("^firms-[0-9a-f]{32}$", observation.Id);
    }

    [Fact]
    public void HeaderOnlyIsValidEmptyBatch()
    {
        var result = new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(Header + "\n"), FirmsSource.ViirsNoaa20Nrt);
        Assert.Empty(result.Observations);
        Assert.Equal(0, result.SkippedMalformedRecords);
    }

    [Theory]
    [InlineData("latitude,longitude")]
    [InlineData("")]
    [InlineData("latitude,latitude,longitude,bright_ti4,scan,track,acq_date,acq_time,satellite,instrument,confidence,version,bright_ti5,frp,daynight")]
    public void RejectsStructurallyInvalidCsv(string csv)
    {
        var exception = Assert.Throws<FirmsFeedException>(() =>
            new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(csv), FirmsSource.ViirsNoaa20Nrt));
        Assert.Equal(FirmsFeedFailureKind.InvalidProviderResponse, exception.Kind);
    }

    [Fact]
    public void SkipsMalformedIndividualRowsWhenAnotherRowIsValid()
    {
        var csv = string.Join('\n',
            Header,
            "91,24.5,331.2,0.4,0.5,2026-09-28,1437,1,VIIRS,n,2.0NRT,290,5,D",
            "60.1,24.5,331.2,0.4,0.5,2026-09-28,1437,1,VIIRS,n,2.0NRT,290,5,D");

        var result = new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(csv), FirmsSource.ViirsNoaa20Nrt);

        Assert.Single(result.Observations);
        Assert.Equal(1, result.SkippedMalformedRecords);
    }

    [Theory]
    [InlineData("NaN", "24.5", "331", "5")]
    [InlineData("60", "Infinity", "331", "5")]
    [InlineData("60", "24.5", "NaN", "5")]
    [InlineData("60", "24.5", "331", "Infinity")]
    public void RejectsNonFiniteNumericRows(string latitude, string longitude, string brightness, string frp)
    {
        var csv = $"{Header}\n{latitude},{longitude},{brightness},0.4,0.5,2026-09-28,1437,1,VIIRS,n,2.0NRT,290,{frp},D";
        var exception = Assert.Throws<FirmsFeedException>(() =>
            new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(csv), FirmsSource.ViirsNoaa20Nrt));
        Assert.Equal(FirmsFeedFailureKind.InvalidProviderResponse, exception.Kind);
    }

    [Theory]
    [InlineData("2026-13-28", "1437")]
    [InlineData("2026-09-28", "2460")]
    [InlineData("2026-09-28", "time")]
    public void RejectsInvalidAcquisitionTimestamp(string date, string time)
    {
        var csv = $"{Header}\n60,24,331,0.4,0.5,{date},{time},1,VIIRS,n,2.0NRT,290,5,D";
        Assert.Throws<FirmsFeedException>(() =>
            new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(csv), FirmsSource.ViirsNoaa20Nrt));
    }

    [Fact]
    public void RejectsUnboundedOrUnsupportedTextFields()
    {
        var longVersion = new string('a', 33);
        var csv = $"{Header}\n60,24,331,0.4,0.5,2026-09-28,1437,1,VIIRS,unknown,{longVersion},290,5,X";
        Assert.Throws<FirmsFeedException>(() =>
            new FirmsCsvParser().Parse(Encoding.UTF8.GetBytes(csv), FirmsSource.ViirsNoaa20Nrt));
    }
}
