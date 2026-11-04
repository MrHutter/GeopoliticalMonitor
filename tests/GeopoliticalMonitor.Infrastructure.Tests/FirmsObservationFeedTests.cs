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
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
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
            new FirmsObservationFeed(client, new FirmsCsvParser(), new FixedKeyResolver("unit-test-key"), TimeSpan.FromMilliseconds(10))
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

    [Theory]
    [InlineData("Invalid MAP_KEY.", FirmsFeedFailureKind.KeyRejected)]
    [InlineData("Invalid MAP_KEY.\nInvalid source.", FirmsFeedFailureKind.KeyRejected)]
    [InlineData("Invalid source.", FirmsFeedFailureKind.ProductUnavailable)]
    [InlineData("Something else went wrong.", FirmsFeedFailureKind.RequestFailed)]
    [InlineData("", FirmsFeedFailureKind.RequestFailed)]
    public async Task ObservedBadRequestBodiesMapToBoundedCategories(string body, FirmsFeedFailureKind expected)
    {
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(body, Encoding.UTF8, "text/plain"),
        })));

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            Create(client, DistinctFakeKey).FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(expected, exception.Kind);
        Assert.DoesNotContain(DistinctFakeKey, exception.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api/area", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TooManyRequestsMapsToRateLimited()
    {
        using var client = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            Create(client, DistinctFakeKey).FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(FirmsFeedFailureKind.RateLimited, exception.Kind);
        Assert.DoesNotContain(DistinctFakeKey, exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ErrorBodyIsReadOnlyUpToItsSmallCap()
    {
        var stream = new CountingStream(new byte[FirmsObservationFeed.MaximumErrorBodyBytes * 64]);
        using var client = new HttpClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StreamContent(stream),
        })));

        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            Create(client, DistinctFakeKey).FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(FirmsFeedFailureKind.RequestFailed, exception.Kind);
        Assert.True(stream.BytesRead <= FirmsObservationFeed.MaximumErrorBodyBytes);
    }

    [Fact]
    public async Task ResolvesTheKeyForEveryRequestSoChangesApplyWithoutRestart()
    {
        var requested = new List<string>();
        using var client = new HttpClient(new StubHandler((request, _) =>
        {
            requested.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Csv, Encoding.UTF8, "text/csv"),
            });
        }));
        var resolver = new MutableKeyResolver("first-unit-test-key");
        var feed = new FirmsObservationFeed(client, new FirmsCsvParser(), resolver);

        await feed.FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10));
        resolver.Value = "second-unit-test-key";
        await feed.FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10));
        resolver.Value = null;
        var exception = await Assert.ThrowsAsync<FirmsFeedException>(() =>
            feed.FetchAsync(FirmsSource.ViirsNoaa20Nrt, new FirmsBoundingBox(0, 0, 10, 10)));

        Assert.Equal(3, resolver.Calls);
        Assert.Equal(2, requested.Count);
        Assert.Contains("/first-unit-test-key/", requested[0], StringComparison.Ordinal);
        Assert.Contains("/second-unit-test-key/", requested[1], StringComparison.Ordinal);
        Assert.Equal(FirmsFeedFailureKind.ConfigurationUnavailable, exception.Kind);
    }

    [Fact]
    public void CredentialStoreUsesOneFixedTargetName()
    {
        Assert.Equal("GeopoliticalMonitor/NASA-FIRMS-MAP_KEY", WindowsCredentialFirmsKeyStore.CredentialTargetName);
    }

    // Obviously fake: not hexadecimal, so it can never be mistaken for a real MAP_KEY.
    private const string DistinctFakeKey = "fake-key-zz-never-real-zz-0000";

    private static FirmsObservationFeed Create(HttpClient client, string? mapKey)
        => new(client, new FirmsCsvParser(), new FixedKeyResolver(mapKey));

    private sealed class FixedKeyResolver(string? value) : IFirmsKeyResolver
    {
        public FirmsResolvedKey Resolve()
        {
            var key = FirmsMapKey.FromInput(value);
            return new FirmsResolvedKey(key is null ? FirmsKeySource.None : FirmsKeySource.App, key, false);
        }
    }

    private sealed class MutableKeyResolver(string? value) : IFirmsKeyResolver
    {
        public string? Value { get; set; } = value;

        public int Calls { get; private set; }

        public FirmsResolvedKey Resolve()
        {
            Calls++;
            var key = FirmsMapKey.FromInput(Value);
            return new FirmsResolvedKey(key is null ? FirmsKeySource.None : FirmsKeySource.App, key, false);
        }
    }

    private sealed class CountingStream(byte[] data) : Stream
    {
        private readonly MemoryStream inner = new(data);

        public long BytesRead { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            BytesRead += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = inner.Read(buffer.Span);
            BytesRead += read;
            return ValueTask.FromResult(read);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request, cancellationToken);
    }
}
