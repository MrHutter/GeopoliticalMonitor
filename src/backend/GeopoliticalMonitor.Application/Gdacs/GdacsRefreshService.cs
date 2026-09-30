using System.Globalization;
using GeopoliticalMonitor.Domain;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Gdacs;

public sealed class GdacsRefreshService
{
    public const string Attribution = "Global Disaster Awareness and Coordination System, GDACS";
    public const string EventIdPrefix = "evt-gdacs-eq-";
    public const string SourceIdPrefix = "src-gdacs-eq-";
    public const string ExternalIdPrefix = "GDACS:EQ:";

    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly IGdacsEventFeed feed;
    private readonly IGdacsEarthquakeStore store;
    private readonly TimeProvider timeProvider;

    public GdacsRefreshService(
        IGdacsEventFeed feed,
        IGdacsEarthquakeStore store,
        TimeProvider? timeProvider = null)
    {
        this.feed = feed;
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<GdacsRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!await refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new GdacsRefreshResult(GdacsRefreshStatus.AlreadyInProgress);
        }

        try
        {
            var retrievedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            GdacsEarthquakeFeedBatch batch;
            try
            {
                batch = await feed
                    .FetchRecentEarthquakesAsync(retrievedAtUtc.AddDays(-4), retrievedAtUtc, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (GdacsFeedException exception)
            {
                return new GdacsRefreshResult(exception.Kind switch
                {
                    GdacsFeedFailureKind.ProviderUnavailable => GdacsRefreshStatus.ProviderUnavailable,
                    GdacsFeedFailureKind.TimedOut => GdacsRefreshStatus.TimedOut,
                    _ => GdacsRefreshStatus.InvalidResponse,
                });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new GdacsRefreshResult(GdacsRefreshStatus.Cancelled);
            }

            var imports = batch.Records.Select(record => Normalize(record, retrievedAtUtc)).ToArray();
            try
            {
                var persisted = await store.PersistGdacsEarthquakesAsync(imports, retrievedAtUtc, cancellationToken).ConfigureAwait(false);
                return new GdacsRefreshResult(
                    GdacsRefreshStatus.Succeeded,
                    persisted.NewEvents,
                    persisted.UpdatedEvents,
                    persisted.NewSources,
                    persisted.UpdatedSources,
                    persisted.UnchangedRecords,
                    batch.SkippedMalformedRecords,
                    persisted.Conflicts,
                    persisted.NewAlerts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new GdacsRefreshResult(GdacsRefreshStatus.Cancelled);
            }
            catch (Exception)
            {
                return new GdacsRefreshResult(GdacsRefreshStatus.PersistenceFailure);
            }
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private static GdacsEarthquakeImport Normalize(GdacsEarthquakeRecord record, DateTimeOffset retrievedAtUtc)
    {
        var locationName = record.Country ?? record.Name;
        var location = new Location(record.Latitude, record.Longitude, locationName, record.Iso3);
        var eventId = new EventId($"{EventIdPrefix}{record.EventId}");
        var sourceId = new SourceItemId($"{SourceIdPrefix}{record.EventId}-{record.EpisodeId}");
        var externalId = $"{ExternalIdPrefix}{record.EventId}:{record.EpisodeId}";

        var geopoliticalEvent = Event.Create(
            eventId,
            record.Name,
            record.Description,
            EventCategory.NaturalDisaster,
            EventStatus.Unverified,
            record.StartedAtUtc,
            record.ProviderUpdatedAtUtc,
            location,
            retrievedAtUtc,
            retrievedAtUtc,
            GdacsSeverityRule.Assess(record.AlertLevel, record.Magnitude),
            new EventProvenance(EventProvider.Gdacs, record.EventId.ToString(CultureInfo.InvariantCulture)));

        var source = SourceItem.Create(
            sourceId,
            SourceType.DatasetRecord,
            externalId,
            Truncate($"{record.Name} - GDACS episode {record.EpisodeId}", 256),
            BuildProvenanceText(record),
            record.CanonicalUrl,
            Attribution,
            null,
            record.ProviderUpdatedAtUtc,
            retrievedAtUtc,
            location,
            retrievedAtUtc);

        return new GdacsEarthquakeImport(record.EventId, record.EpisodeId, geopoliticalEvent, source);
    }

    private static string BuildProvenanceText(GdacsEarthquakeRecord record)
    {
        var magnitude = record.Magnitude is null
            ? "Not provided"
            : record.Magnitude.Value.ToString("0.0#", CultureInfo.InvariantCulture) + " M";
        var country = record.Country ?? "Not provided";
        var text = string.Join(
            Environment.NewLine,
            $"Provider: {Attribution}",
            $"Record: EQ-{record.EventId}, episode {record.EpisodeId}",
            $"GDACS alert level: {record.AlertLevel}",
            $"Magnitude: {magnitude}",
            $"Country: {country}",
            record.Description,
            "Provider information is not independently verified by Geopolitical Monitor; confirm critical decisions with competent authorities.");

        return text.Length <= 4000 ? text : text[..4000];
    }

    private static string Truncate(string value, int maximumLength)
    {
        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
