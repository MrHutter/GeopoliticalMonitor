using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>One parsed UCDP Candidate CSV row. Article text, headlines and geometry are deliberately not represented.</summary>
public sealed record UcdpCandidateRecord(
    long Id,
    string CodeStatus,
    int TypeOfViolence,
    string DyadName,
    string SideA,
    string SideB,
    int WherePrecision,
    string WhereCoordinates,
    double Latitude,
    double Longitude,
    string Country,
    string Region,
    int DatePrecision,
    DateOnly DateStart,
    DateOnly DateEnd,
    int Best,
    int High,
    int Low,
    string? EventClarity = null,
    string? NumberOfSources = null);

public sealed record UcdpCandidateFeedBatch(
    IReadOnlyList<UcdpCandidateRecord> Records,
    int SkippedMalformedRecords);

public enum UcdpReleaseAvailability
{
    Published,
    NotPublished,
}

public sealed record UcdpReleaseFetchResult(
    UcdpRelease Release,
    UcdpReleaseAvailability Availability,
    UcdpCandidateFeedBatch? Batch = null)
{
    public static UcdpReleaseFetchResult NotPublished(UcdpRelease release) => new(release, UcdpReleaseAvailability.NotPublished);

    public static UcdpReleaseFetchResult Published(UcdpRelease release, UcdpCandidateFeedBatch batch) => new(release, UcdpReleaseAvailability.Published, batch);
}

public interface IUcdpCandidateFeed
{
    /// <summary>Downloads one monthly release. A missing (404) release is a normal NotPublished result.</summary>
    Task<UcdpReleaseFetchResult> FetchReleaseAsync(UcdpRelease release, CancellationToken cancellationToken = default);
}

public sealed record UcdpCandidateImport(
    long ProviderEventId,
    Event Event,
    SourceItem Source);

public sealed record UcdpNormalizationResult(
    IReadOnlyList<UcdpCandidateImport> Imports,
    int SkippedInvalidCoordinates,
    int SkippedImpreciseLocation,
    int SkippedZeroFatalities,
    int SkippedOutsideWindow,
    int SkippedMalformed,
    int DroppedByCap);

public sealed record UcdpReleasePersistenceResult(
    int NewEvents,
    int UpdatedEvents,
    int NewSources,
    int UpdatedSources,
    int UnchangedRecords,
    int Conflicts);

public interface IUcdpCandidateStore
{
    /// <summary>The highest ProviderLastSeenRelease among UCDP Candidate Events, or null when none exist.</summary>
    Task<int?> GetNewestUcdpReleaseAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists one release in one transaction. Never creates Alerts.</summary>
    Task<UcdpReleasePersistenceResult> PersistUcdpCandidateReleaseAsync(
        IReadOnlyList<UcdpCandidateImport> imports,
        int releaseOrdinal,
        CancellationToken cancellationToken = default);
}

public enum UcdpFeedFailureKind
{
    ProviderUnavailable,
    TimedOut,
    InvalidResponse,
}

public sealed class UcdpFeedException : Exception
{
    public UcdpFeedException(UcdpFeedFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public UcdpFeedFailureKind Kind { get; }
}

public enum UcdpRefreshStatus
{
    Succeeded,
    NoNewRelease,
    AlreadyInProgress,
    ProviderUnavailable,
    TimedOut,
    InvalidResponse,
    PersistenceFailure,
    Cancelled,
}

public sealed record UcdpRefreshResult(
    UcdpRefreshStatus Status,
    int? NewestRelease = null,
    int ReleasesImported = 0,
    int NewEvents = 0,
    int UpdatedEvents = 0,
    int UnchangedRecords = 0,
    int Conflicts = 0,
    int SkippedMalformedRecords = 0,
    int SkippedInvalidCoordinates = 0,
    int SkippedImpreciseLocation = 0,
    int SkippedZeroFatalities = 0,
    int SkippedOutsideWindow = 0,
    int DroppedByCap = 0)
{
    public bool IsSuccess => Status is UcdpRefreshStatus.Succeeded or UcdpRefreshStatus.NoNewRelease;
}
