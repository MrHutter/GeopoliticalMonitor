namespace GeopoliticalMonitor.Application.Gdacs;

public interface IGdacsEventFeed
{
    Task<GdacsEarthquakeFeedBatch> FetchRecentEarthquakesAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
}
