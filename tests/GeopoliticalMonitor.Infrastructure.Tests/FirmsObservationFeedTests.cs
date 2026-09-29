using System.Net;
using System.Text;
using GeopoliticalMonitor.Application.Firms;
using GeopoliticalMonitor.Infrastructure.Firms;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public class FirmsObservationFeedTests
{
    private const string Csv = "latitude,longitude,bright_ti4,scan,track,acq_date,acq_time,satellite,instrument,confidence,version,bright_ti5,frp,daynight\n60,24,331,0.4,0.5,2026-09-28,1437,1,VIIRS,n,2.0NRT,290,5,D\n";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingOrBlankKeyFailsBeforeHttp(string? mapKey)
    {
        var calls = 0;
        using var client = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }));
        var feed = Create(client, mapKey);

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            feed.FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(10, 20, 30, 40)));

        Assert.Equal(FirmsFeedFailureKind.ConfigurationUnavailable, exception.Kind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(FirmsSource.ViirsNoaa20Nrt, "VIIRS_NOAA20_NRT")]
    [InlineData(FirmsSource.ViirsNoaa21Nrt, "VIIRS_NOAA21_NRT")]
    public async Task UsesFixedHttpsHostSourceAreaAndDayRange(FirmsSource source, string expectedSource)
    {
        Uri? requestedUri = null;
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Csv, Encoding.UTF8, "text/csv"),
            });
        }));

        await Create(client, "  unit-test-key  ").FetchAsync(source, new FirmsBoundingBox(10.25, -20.5, 30.75, 40));

        Assert.NotNull(requestedUri);
        Assert.Equal(Uri.UriSchemeHttps, requestedUri.Scheme);
        Assert.Equal(FirmsObservationFeed.OfficialHost, requestedUri.Host);
        Assert.Contains($"/api/area/csv/unit-test-key/{expectedSource}/10.25,-20.5,30.75,40/2", requestedUri.AbsoluteUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task NonSuccessAndRedirectMapToSafeFailure(HttpStatusCode statusCode)
    {
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode))));

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            Create(client, "unit-test-key").FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(FirmsFeedFailureKind.RequestFailed, exception.Kind);
        Assert.DoesNotContain("unit-test-key", exception.Message);
        Assert.DoesNotContain("api/area", exception.Message);
    }

    [Fact]
    public async Task EnforcesFourMiBResponseLimit()
    {
        var oversized = new byte[FirmsObservationFeed.MaximumResponseBytes + 1];
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(oversized),
        })));

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            Create(client, "unit-test-key").FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(FirmsFeedFailureKind.ResponseTooLarge, exception.Kind);
    }

    [Fact]
    public async Task MapsPerOperationTimeout()
    {
        using var client = new HttpClient(new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            new FirmsObservationFeed(client, new FirmsCsvParser(), new FirmsClientOptions("unit-test-key"), TimeSpan.FromMilliseconds(10))
                .FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(FirmsFeedFailureKind.TimedOut, exception.Kind);
    }

    [Fact]
    public async Task PreservesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new StubHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Create(client, "unit-test-key").FetchAsync(
                FirmsSource.ViirsNoaa20Nrt,
                new FirmsBoundingBox(0, 0, 10, 10),
                cancellation.Token));
    }

    private static FirmsObservationFeed Create(HttpClient client, string? mapKey)
        => new(client, new FirmsCsvParser(), new FirmsClientOptions(mapKey));

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request, cancellationToken);
    }
}
