namespace GeopoliticalMonitor.Application.Gdacs;

public interface IGdacsEarthquakeStore
{
    Task<GdacsEarthquakePersistenceResult> PersistGdacsEarthquakesAsync(
        IReadOnlyList<GdacsEarthquakeImport> imports,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken = default);
}
