using GeopoliticalMonitor.Application.Firms;

namespace GeopoliticalMonitor.Application.Tests;

public class FirmsRefreshServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidatesNormalLimitAndAntimeridianViewports()
    {
        var normal = FirmsRefreshService.ValidateAndSplit(new FirmsViewport(10, -20, 100, 40));
        Assert.Equal(FirmsRefreshStatus.Succeeded, normal.Status);
        Assert.Single(normal.Bounds);

        var crossing = FirmsRefreshService.ValidateAndSplit(new FirmsViewport(170, -10, -170, 20));
        Assert.Equal(FirmsRefreshStatus.Succeeded, crossing.Status);
        Assert.Equal(2, crossing.Bounds.Count);
        Assert.Equal(new FirmsBoundingBox(170, -10, 180, 20), crossing.Bounds[0]);
        Assert.Equal(new FirmsBoundingBox(-180, -10, -170, 20), crossing.Bounds[1]);
    }

    [Theory]
    [InlineData(-100, 0, 1, 10)]
    [InlineData(0, -31, 10, 31)]
    public async Task OversizedViewportFailsBeforeProviderCall(double west, double south, double east, double north)
    {
        var feed = new RecordingFeed();
        var result = await Service(feed).RefreshAsync(new FirmsViewport(west, south, east, north));

        Assert.Equal(FirmsRefreshStatus.AreaTooLarge, result.Status);
        Assert.Empty(feed.Calls);
    }

    [Theory]
    [InlineData(double.NaN, 0, 10, 10)]
    [InlineData(-181, 0, 10, 10)]
    [InlineData(0, 20, 10, 10)]
    [InlineData(10, 0, 10, 10)]
    public async Task InvalidViewportFailsBeforeProviderCall(double west, double south, double east, double north)
    {
        var feed = new RecordingFeed();
        var result = await Service(feed).RefreshAsync(new FirmsViewport(west, south, east, north));

        Assert.Equal(FirmsRefreshStatus.InvalidViewport, result.Status);
        Assert.Empty(feed.Calls);
    }

    [Fact]
    public async Task NormalRefreshRequestsBothProductsAndAppliesRollingWindow()
    {
        var atCutoff = Observation(FirmsSource.ViirsNoaa20Nrt, Now.AddHours(-24), 1, 1);
        var tooOld = Observation(FirmsSource.ViirsNoaa20Nrt, Now.AddHours(-24).AddTicks(-1), 2, 2);
        var newest = Observation(FirmsSource.ViirsNoaa21Nrt, Now.AddMinutes(-5), 3, 3);
        var feed = new RecordingFeed((source, _) => new FirmsObservationBatch(
            source == FirmsSource.ViirsNoaa20Nrt ? [atCutoff, tooOld] : [newest], 0));

        var result = await Service(feed).RefreshAsync(new FirmsViewport(0, 0, 20, 20));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, feed.Calls.Count);
        Assert.Equal(FirmsSources.All, feed.Calls.Select(call => call.Source).ToArray());
        Assert.Equal([newest.Id, atCutoff.Id], result.Snapshot!.Observations.Select(item => item.Id).ToArray());
        Assert.Equal(Now, result.Snapshot.RefreshedAtUtc);
    }

    [Fact]
    public async Task AntimeridianRefreshRequestsBothSegmentsForBothProducts()
    {
        var feed = new RecordingFeed();
        var result = await Service(feed).RefreshAsync(new FirmsViewport(170, -10, -170, 10));

        Assert.True(result.IsSuccess);
        Assert.Equal(4, feed.Calls.Count);
        Assert.Equal(2, feed.Calls.Count(call => call.Source == FirmsSource.ViirsNoaa20Nrt));
        Assert.Equal(2, feed.Calls.Count(call => call.Source == FirmsSource.ViirsNoaa21Nrt));
    }

    [Fact]
    public async Task ExactDuplicatesCollapseButProductsRemainDistinct()
    {
        var noaa20 = Observation(FirmsSource.ViirsNoaa20Nrt, Now.AddHours(-1), 10, 20);
        var noaa21 = Observation(FirmsSource.ViirsNoaa21Nrt, Now.AddHours(-1), 10, 20);
        var feed = new RecordingFeed((source, _) => new FirmsObservationBatch(
            source == FirmsSource.ViirsNoaa20Nrt ? [noaa20, noaa20] : [noaa21], 0));

        var result = await Service(feed).RefreshAsync(new FirmsViewport(0, 0, 20, 20));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Snapshot!.Observations.Count);
        Assert.NotEqual(noaa20.Id, noaa21.Id);
    }

    [Fact]
    public async Task TwentyThousandAcceptedAndMoreFailsWithoutSnapshot()
    {
        var boundaryFeed = new RecordingFeed((source, _) => new FirmsObservationBatch(
            CreateMany(source, 10_000), 0));
        var boundary = await Service(boundaryFeed).RefreshAsync(new FirmsViewport(0, 0, 20, 20));
        Assert.True(boundary.IsSuccess);
        Assert.Equal(20_000, boundary.Snapshot!.Observations.Count);

        var oversizedFeed = new RecordingFeed((source, _) => new FirmsObservationBatch(
            CreateMany(source, 10_001), 0));
        var oversized = await Service(oversizedFeed).RefreshAsync(new FirmsViewport(0, 0, 20, 20));
        Assert.Equal(FirmsRefreshStatus.TooManyObservations, oversized.Status);
        Assert.Null(oversized.Snapshot);
    }

    [Fact]
    public async Task OneProviderFailureFailsWholeSnapshot()
    {
        var feed = new RecordingFeed((source, _) => source == FirmsSource.ViirsNoaa20Nrt
            ? new FirmsObservationBatch([Observation(source, Now.AddMinutes(-10), 1, 1)], 0)
            : throw new FirmsFeedException(FirmsFeedFailureKind.RequestFailed, "failed"));

        var result = await Service(feed).RefreshAsync(new FirmsViewport(0, 0, 20, 20));

        Assert.Equal(FirmsRefreshStatus.RequestFailed, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task ClearlyFutureObservationFailsWholeSnapshot()
    {
        var feed = new RecordingFeed((source, _) => new FirmsObservationBatch(
            source == FirmsSource.ViirsNoaa20Nrt
                ? [Observation(source, Now.AddMinutes(6), 1, 1)]
                : [], 0));

        var result = await Service(feed).RefreshAsync(new FirmsViewport(0, 0, 20, 20));

        Assert.Equal(FirmsRefreshStatus.InvalidProviderResponse, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task CallerCancellationReturnsCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        var feed = new RecordingFeed(async (_, _, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new FirmsObservationBatch([], 0);
        });

        var result = await Service(feed).RefreshAsync(new FirmsViewport(0, 0, 20, 20), cancellation.Token);

        Assert.Equal(FirmsRefreshStatus.Cancelled, result.Status);
    }

    private static FirmsRefreshService Service(IFirmsObservationFeed feed)
        => new(feed, new FixedTimeProvider(Now));

    private static FirmsObservation Observation(FirmsSource source, DateTimeOffset acquired, double latitude, double longitude)
    {
        var providerSource = source.ToProviderValue();
        var satellite = source == FirmsSource.ViirsNoaa20Nrt ? "1" : "2";
        return new FirmsObservation(
            FirmsObservationIdentity.Create(providerSource, satellite, acquired, latitude, longitude),
            latitude, longitude, acquired, providerSource, satellite, "Nominal", 4.2, 330.5, 290.2,
            0.4, 0.5, "2.0NRT", "Day");
    }

    private static IReadOnlyList<FirmsObservation> CreateMany(FirmsSource source, int count)
        => Enumerable.Range(0, count)
            .Select(index => Observation(source, Now.AddSeconds(-(index % 3600)), index / 10_000d, index / 10_000d))
            .ToArray();

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingFeed : IFirmsObservationFeed
    {
        private readonly Func<FirmsSource, FirmsBoundingBox, CancellationToken, Task<FirmsObservationBatch>> response;

        public RecordingFeed(Func<FirmsSource, FirmsBoundingBox, FirmsObservationBatch>? response = null)
            : this((source, bounds, _) => Task.FromResult(response?.Invoke(source, bounds) ?? new FirmsObservationBatch([], 0)))
        {
        }

        public RecordingFeed(Func<FirmsSource, FirmsBoundingBox, CancellationToken, Task<FirmsObservationBatch>> response)
        {
            this.response = response;
        }

        public List<(FirmsSource Source, FirmsBoundingBox Bounds)> Calls { get; } = [];

        public Task<FirmsObservationBatch> FetchAsync(FirmsSource source, FirmsBoundingBox bounds, CancellationToken cancellationToken = default)
        {
            Calls.Add((source, bounds));
            return response(source, bounds, cancellationToken);
        }
    }
}
