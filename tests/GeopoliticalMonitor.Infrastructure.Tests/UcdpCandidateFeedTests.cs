using System.Net;
using System.Text;
using GeopoliticalMonitor.Application.Ucdp;
using GeopoliticalMonitor.Infrastructure.Ucdp;

namespace GeopoliticalMonitor.Infrastructure.Tests;

public sealed class UcdpCandidateFeedTests
{
    private static readonly UcdpRelease August = new(2026, 8);
    private static readonly string Csv = string.Join(',', UcdpCandidateCsvParser.RequiredColumns) + "\n"
        + "1,Clear,1,A - B,A,B,1,Place,10,20,Testland,Africa,1,2026-08-02,2026-08-03,3,4,2\n";

    [Fact]
    public async Task RequestsOnlyTheFixedHttpsReleaseUrlWithTheProductUserAgent()
    {
        var handler = new StubHandler(_ => Ok(Csv));
        var feed = new UcdpCandidateFeed(new HttpClient(handler), new UcdpCandidateCsvParser());

        var result = await feed.FetchReleaseAsync(August);

        Assert.Equal(UcdpReleaseAvailability.Published, result.Availability);
        Assert.Single(result.Batch!.Records);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv", request.RequestUri!.AbsoluteUri);
        Assert.Equal("GeopoliticalMonitor/0.1.0", request.Headers.UserAgent.ToString());
        Assert.DoesNotContain(request.Headers, header => header.Key is "Authorization" or "Cookie" or "x-ucdp-access-token");
    }

    [Fact]
    public async Task NotFoundIsANormalNotPublishedOutcome()
    {
        var feed = new UcdpCandidateFeed(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), new UcdpCandidateCsvParser());

        var result = await feed.FetchReleaseAsync(new UcdpRelease(2026, 9));

        Assert.Equal(UcdpReleaseAvailability.NotPublished, result.Availability);
        Assert.Null(result.Batch);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently, UcdpFeedFailureKind.InvalidResponse)]
    [InlineData(HttpStatusCode.Found, UcdpFeedFailureKind.InvalidResponse)]
    [InlineData(HttpStatusCode.TemporaryRedirect, UcdpFeedFailureKind.InvalidResponse)]
    [InlineData(HttpStatusCode.NoContent, UcdpFeedFailureKind.ProviderUnavailable)]
    [InlineData(HttpStatusCode.Forbidden, UcdpFeedFailureKind.ProviderUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError, UcdpFeedFailureKind.ProviderUnavailable)]
    [InlineData(HttpStatusCode.ServiceUnavailable, UcdpFeedFailureKind.ProviderUnavailable)]
    public async Task RedirectsAndOtherNonOkStatusesAreTypedFailures(HttpStatusCode status, UcdpFeedFailureKind expected)
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent("secret body https://elsewhere.invalid/") };
            response.Headers.Location = new Uri("https://elsewhere.invalid/file.csv");
            return response;
        });
        var feed = new UcdpCandidateFeed(new HttpClient(handler), new UcdpCandidateCsvParser());

        var exception = await Assert.ThrowsAsync<UcdpFeedException>(() => feed.FetchReleaseAsync(August));

        Assert.Equal(expected, exception.Kind);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain("http", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionClientDoesNotFollowRedirects()
    {
        using var client = UcdpCandidateFeed.CreateProductionHttpClient();
        var handlerField = typeof(HttpMessageInvoker).GetField("_handler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var handler = Assert.IsType<HttpClientHandler>(handlerField!.GetValue(client));

        Assert.False(handler.AllowAutoRedirect);
    }

    [Fact]
    public async Task DeclaredLengthOverTheCapIsRejectedBeforeReading()
    {
        var handler = new StubHandler(_ =>
        {
            var response = Ok("small");
            response.Content.Headers.ContentLength = 2048;
            return response;
        });
        var feed = new UcdpCandidateFeed(new HttpClient(handler), new UcdpCandidateCsvParser(), maximumResponseBytes: 1024);

        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, (await Assert.ThrowsAsync<UcdpFeedException>(() => feed.FetchReleaseAsync(August))).Kind);
    }

    [Fact]
    public async Task StreamedBodyOverTheCapIsRejected()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new UnknownLengthStream(new byte[4096])),
        });
        var feed = new UcdpCandidateFeed(new HttpClient(handler), new UcdpCandidateCsvParser(), maximumResponseBytes: 1024);

        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, (await Assert.ThrowsAsync<UcdpFeedException>(() => feed.FetchReleaseAsync(August))).Kind);
    }

    [Fact]
    public async Task ATimeoutIsATypedFailureAndCallerCancellationIsNot()
    {
        var slow = new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Ok(Csv);
        });
        var timedFeed = new UcdpCandidateFeed(new HttpClient(slow), new UcdpCandidateCsvParser(), requestTimeout: TimeSpan.FromMilliseconds(50));
        Assert.Equal(UcdpFeedFailureKind.TimedOut, (await Assert.ThrowsAsync<UcdpFeedException>(() => timedFeed.FetchReleaseAsync(August))).Kind);

        using var cancellation = new CancellationTokenSource();
        var cancelledFeed = new UcdpCandidateFeed(new HttpClient(slow), new UcdpCandidateCsvParser());
        var pending = cancelledFeed.FetchReleaseAsync(August, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task NetworkErrorsAreProviderUnavailable()
    {
        var feed = new UcdpCandidateFeed(new HttpClient(new StubHandler(_ => throw new HttpRequestException("socket"))), new UcdpCandidateCsvParser());

        Assert.Equal(UcdpFeedFailureKind.ProviderUnavailable, (await Assert.ThrowsAsync<UcdpFeedException>(() => feed.FetchReleaseAsync(August))).Kind);
    }

    [Fact]
    public async Task AResponseFromAnotherTargetIsRejected()
    {
        var handler = new StubHandler(request =>
        {
            var response = Ok(Csv);
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.invalid/downloads/candidateged/x.csv");
            return response;
        });
        var feed = new UcdpCandidateFeed(new HttpClient(handler), new UcdpCandidateCsvParser());

        Assert.Equal(UcdpFeedFailureKind.InvalidResponse, (await Assert.ThrowsAsync<UcdpFeedException>(() => feed.FetchReleaseAsync(August))).Kind);
    }

    [Theory]
    [InlineData("https://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv", true)]
    [InlineData("http://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv", false)]
    [InlineData("https://ucdp.uu.se.evil.invalid/downloads/candidateged/GEDEvent_v26_0_8.csv", false)]
    [InlineData("https://ucdpapi.pcr.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv", false)]
    [InlineData("https://ucdp.uu.se:8443/downloads/candidateged/GEDEvent_v26_0_8.csv", false)]
    [InlineData("https://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv?x=1", false)]
    [InlineData("https://user@ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv", false)]
    [InlineData("https://ucdp.uu.se/other/GEDEvent_v26_0_8.csv", false)]
    public void OnlyTheOfficialHttpsReleasePathIsPermitted(string url, bool permitted)
    {
        Assert.Equal(permitted, UcdpCandidateFeed.IsPermittedTarget(new Uri(url)));
    }

    [Fact]
    public void ReleaseUrlsAreBuiltFromTheReleaseValueOnly()
    {
        Assert.Equal("https://ucdp.uu.se/downloads/candidateged/GEDEvent_v27_0_1.csv", UcdpCandidateFeed.BuildReleaseUri(new UcdpRelease(2027, 1)).AbsoluteUri);
    }

    private static HttpResponseMessage Ok(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/csv") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            : this((request, _) => Task.FromResult(respond(request)))
        {
        }

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        {
            this.respond = respond;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return respond(request, cancellationToken);
        }
    }

    private sealed class UnknownLengthStream(byte[] data) : MemoryStream(data)
    {
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
    }
}
