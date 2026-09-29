using System.Net;
using System.Text;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Infrastructure.Gdacs;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class GdacsEventFeedTests
{
    [Fact]
    public async Task SendsBoundedOfficialEarthquakeQueryAndParsesResponse()
    {
        Uri? requestedUri = null;
        var payload = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gdacs-earthquakes.json"));
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            requestedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(payload) });
        }));
        var feed = new GdacsEventFeed(client, new GdacsGeoJsonParser());

        var result = await feed.FetchRecentEarthquakesAsync(
            DateTimeOffset.Parse("2025-09-16T23:12:00Z"),
            DateTimeOffset.Parse("2025-09-20T23:12:00Z"));

        Assert.Equal(2, result.Records.Count);
        Assert.Equal(Uri.UriSchemeHttps, requestedUri?.Scheme);
        Assert.Equal(GdacsEventFeed.OfficialHost, requestedUri?.Host);
        Assert.Contains("eventlist=EQ", requestedUri?.Query);
        Assert.Contains("fromdate=2025-09-16", requestedUri?.Query);
        Assert.Contains("todate=2025-09-20", requestedUri?.Query);
        Assert.Contains("pagesize=100", requestedUri?.Query);
        Assert.Contains("pagenumber=1", requestedUri?.Query);
    }

    [Fact]
    public async Task TreatsNoContentAsEmptyFeed()
    {
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent))));
        var result = await new GdacsEventFeed(client, new GdacsGeoJsonParser())
            .FetchRecentEarthquakesAsync(DateTimeOffset.UtcNow.AddDays(-4), DateTimeOffset.UtcNow);
        Assert.Empty(result.Records);
    }

    [Fact]
    public async Task MapsHttpFailureAndOversizedResponse()
    {
        using var unavailableClient = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        var unavailable = await Assert.ThrowsAsync<GdacsFeedException>(() => new GdacsEventFeed(unavailableClient, new GdacsGeoJsonParser())
            .FetchRecentEarthquakesAsync(DateTimeOffset.UtcNow.AddDays(-4), DateTimeOffset.UtcNow));
        Assert.Equal(GdacsFeedFailureKind.ProviderUnavailable, unavailable.Kind);

        using var oversizedClient = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[GdacsEventFeed.MaximumResponseBytes + 1]),
        })));
        var oversized = await Assert.ThrowsAsync<GdacsFeedException>(() => new GdacsEventFeed(oversizedClient, new GdacsGeoJsonParser())
            .FetchRecentEarthquakesAsync(DateTimeOffset.UtcNow.AddDays(-4), DateTimeOffset.UtcNow));
        Assert.Equal(GdacsFeedFailureKind.InvalidResponse, oversized.Kind);
    }

    [Fact]
    public async Task MapsInternalTimeoutAndPreservesCallerCancellation()
    {
        using var client = new HttpClient(new StubHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var feed = new GdacsEventFeed(client, new GdacsGeoJsonParser(), TimeSpan.FromMilliseconds(20));
        var timedOut = await Assert.ThrowsAsync<GdacsFeedException>(() => feed.FetchRecentEarthquakesAsync(DateTimeOffset.UtcNow.AddDays(-4), DateTimeOffset.UtcNow));
        Assert.Equal(GdacsFeedFailureKind.TimedOut, timedOut.Kind);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => feed.FetchRecentEarthquakesAsync(DateTimeOffset.UtcNow.AddDays(-4), DateTimeOffset.UtcNow, cancellation.Token));
    }

    [Fact]
    public async Task PassesInvalidPayloadToStrictParser()
    {
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        })));
        var exception = await Assert.ThrowsAsync<GdacsFeedException>(() => new GdacsEventFeed(client, new GdacsGeoJsonParser())
            .FetchRecentEarthquakesAsync(DateTimeOffset.UtcNow.AddDays(-4), DateTimeOffset.UtcNow));
        Assert.Equal(GdacsFeedFailureKind.InvalidResponse, exception.Kind);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request, cancellationToken);
    }
}
