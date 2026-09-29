using System.Text.Json;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Gdacs;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class GdacsRefreshBridgeHandlerTests
{
    [Fact]
    public async Task SerializesTypedSuccessResult()
    {
        var handler = new GdacsRefreshBridgeHandler(new GdacsRefreshService(new EmptyFeed(), new ResultStore()));

        using var response = JsonDocument.Parse(await handler.RefreshAsync());

        Assert.Equal(DesktopBridgeProtocol.GdacsRefreshResultMessageType, response.RootElement.GetProperty("type").GetString());
        Assert.Equal("Succeeded", response.RootElement.GetProperty("payload").GetProperty("status").GetString());
        Assert.Equal(1, response.RootElement.GetProperty("payload").GetProperty("newEvents").GetInt32());
        Assert.Equal(7, response.RootElement.GetProperty("payload").GetProperty("newAlerts").GetInt32());
    }

    [Fact]
    public async Task SerializesBoundedFailureWithoutExceptionDetail()
    {
        var handler = new GdacsRefreshBridgeHandler(new GdacsRefreshService(new FailingFeed(), new ResultStore()));

        using var response = JsonDocument.Parse(await handler.RefreshAsync());

        Assert.Equal(DesktopBridgeProtocol.GdacsRefreshErrorMessageType, response.RootElement.GetProperty("type").GetString());
        Assert.Equal("ProviderUnavailable", response.RootElement.GetProperty("payload").GetProperty("status").GetString());
        Assert.DoesNotContain("sensitive detail", response.RootElement.GetProperty("payload").GetProperty("message").GetString());
    }

    private sealed class EmptyFeed : IGdacsEventFeed
    {
        public Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => Task.FromResult(new GdacsEarthquakeFeedBatch([], 0));
    }

    private sealed class FailingFeed : IGdacsEventFeed
    {
        public Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
            => throw new GdacsFeedException(GdacsFeedFailureKind.ProviderUnavailable, "sensitive detail");
    }

    private sealed class ResultStore : IGdacsEarthquakeStore
    {
        public Task<GdacsEarthquakePersistenceResult> PersistGdacsEarthquakesAsync(
            IReadOnlyList<GdacsEarthquakeImport> imports,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GdacsEarthquakePersistenceResult(1, 2, 3, 4, 5, 6, [], 7));
    }
}
