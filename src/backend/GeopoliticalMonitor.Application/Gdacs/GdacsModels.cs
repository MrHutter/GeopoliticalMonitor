using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Gdacs;

public sealed record GdacsEarthquakeRecord(
    long EventId,
    long EpisodeId,
    string Name,
    string Description,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset ProviderUpdatedAtUtc,
    string AlertLevel,
    double? Magnitude,
    string? Country,
    string? Iso3,
    Uri CanonicalUrl,
    double Longitude,
    double Latitude);

public sealed record GdacsEarthquakeFeedBatch(
    IReadOnlyList<GdacsEarthquakeRecord> Records,
    int SkippedMalformedRecords);

public sealed record GdacsEarthquakeImport(
    long ProviderEventId,
    long ProviderEpisodeId,
    Event Event,
    SourceItem Source);

public enum GdacsEventPersistenceDisposition
{
    Created,
    MateriallyUpdated,
    RevisionOnlyUpdated,
    Unchanged,
    Conflict,
}

public sealed record GdacsEventPersistenceOutcome(
    string EventId,
    DateTimeOffset ProviderRevisionAtUtc,
    GdacsEventPersistenceDisposition Disposition);

public sealed record GdacsEarthquakePersistenceResult(
    int NewEvents,
    int UpdatedEvents,
    int NewSources,
    int UpdatedSources,
    int UnchangedRecords,
    int Conflicts,
    IReadOnlyList<GdacsEventPersistenceOutcome> EventOutcomes,
    int NewAlerts);

public enum GdacsFeedFailureKind
{
    ProviderUnavailable,
    TimedOut,
    InvalidResponse,
}

public sealed class GdacsFeedException : Exception
{
    public GdacsFeedException(GdacsFeedFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public GdacsFeedFailureKind Kind { get; }
}

public enum GdacsRefreshStatus
{
    Succeeded,
    AlreadyInProgress,
    ProviderUnavailable,
    TimedOut,
    InvalidResponse,
    PersistenceFailure,
    Cancelled,
}

public sealed record GdacsRefreshResult(
    GdacsRefreshStatus Status,
    int NewEvents = 0,
    int UpdatedEvents = 0,
    int NewSources = 0,
    int UpdatedSources = 0,
    int UnchangedRecords = 0,
    int SkippedMalformedRecords = 0,
    int Conflicts = 0,
    int NewAlerts = 0)
{
    public bool IsSuccess => Status == GdacsRefreshStatus.Succeeded;
}
