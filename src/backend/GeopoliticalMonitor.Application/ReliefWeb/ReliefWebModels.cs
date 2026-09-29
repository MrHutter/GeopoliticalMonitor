using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.ReliefWeb;

public sealed record ReliefWebSearchCommand(
    string? EventId,
    string? QueryText,
    string? FromDateUtc,
    string? ToDateUtc,
    string? CountryIso3);

public sealed record ReliefWebSearchCriteria(
    string QueryText,
    DateTimeOffset FromDateUtc,
    DateTimeOffset ToDateUtc,
    string? CountryIso3);

public sealed record ReliefWebOrganization(long? Id, string DisplayName);

public sealed record ReliefWebCountry(long? Id, string Name, string? Iso3);

public sealed record ReliefWebDisaster(
    long Id,
    string Name,
    string? Glide,
    IReadOnlyList<string> Types);

public sealed record ReliefWebReportRecord(
    long ReportId,
    string Title,
    Uri CanonicalUrl,
    Uri? OriginUrl,
    DateTimeOffset? OriginalPublishedAtUtc,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ChangedAtUtc,
    IReadOnlyList<ReliefWebOrganization> Organizations,
    IReadOnlyList<string> Formats,
    IReadOnlyList<ReliefWebCountry> Countries,
    IReadOnlyList<ReliefWebDisaster> Disasters,
    IReadOnlyList<string> Languages);

public sealed record ReliefWebReportBatch(
    IReadOnlyList<ReliefWebReportRecord> Reports,
    int SkippedMalformedReports);

public sealed record ReliefWebDisasterDto(
    string Id,
    string Name,
    string? Glide,
    IReadOnlyList<string> Types);

public sealed record ReliefWebReportCandidateDto(
    string ReportId,
    string Title,
    string CanonicalUrl,
    DateTimeOffset? PublishedAtUtc,
    IReadOnlyList<string> Organizations,
    IReadOnlyList<string> Formats,
    IReadOnlyList<string> Countries,
    IReadOnlyList<ReliefWebDisasterDto> Disasters,
    IReadOnlyList<string> Languages);

public enum ReliefWebFailureKind
{
    ConfigurationUnavailable,
    ProviderUnavailable,
    TimedOut,
    QuotaExceeded,
    InvalidConfiguration,
    InvalidResponse,
    NotFound,
}

public sealed class ReliefWebClientException : Exception
{
    public ReliefWebClientException(ReliefWebFailureKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public ReliefWebFailureKind Kind { get; }
}

public enum ReliefWebSearchStatus
{
    Succeeded,
    InvalidEventId,
    EventNotFound,
    InvalidQuery,
    InvalidDateRange,
    InvalidCountry,
    ConfigurationUnavailable,
    ProviderUnavailable,
    TimedOut,
    QuotaExceeded,
    InvalidConfiguration,
    InvalidResponse,
    PersistenceFailure,
    Cancelled,
}

public sealed record ReliefWebSearchResult(
    ReliefWebSearchStatus Status,
    IReadOnlyList<ReliefWebReportCandidateDto>? Candidates = null,
    int SkippedMalformedReports = 0)
{
    public bool IsSuccess => Status == ReliefWebSearchStatus.Succeeded;
}

public enum ReliefWebAttachStatus
{
    CreatedAndAttached,
    ExistingSourceAttached,
    AlreadyAttached,
    InvalidEventId,
    InvalidReportId,
    EventNotFound,
    Conflict,
    ConfigurationUnavailable,
    ProviderUnavailable,
    TimedOut,
    QuotaExceeded,
    InvalidConfiguration,
    ReportNotFound,
    InvalidResponse,
    PersistenceFailure,
    Cancelled,
}

public sealed record ReliefWebAttachResult(
    ReliefWebAttachStatus Status,
    SourceItemDto? Source = null)
{
    public bool IsSuccess => Status is ReliefWebAttachStatus.CreatedAndAttached
        or ReliefWebAttachStatus.ExistingSourceAttached
        or ReliefWebAttachStatus.AlreadyAttached;
}

public enum ReliefWebAttachmentStoreDisposition
{
    CreatedAndAttached,
    ExistingSourceAttached,
    AlreadyAttached,
    EventNotFound,
    Conflict,
}

public sealed record ReliefWebAttachmentStoreResult(
    ReliefWebAttachmentStoreDisposition Disposition,
    SourceItem? Source = null);

public interface IReliefWebReportClient
{
    Task<ReliefWebReportBatch> SearchReportsAsync(ReliefWebSearchCriteria criteria, CancellationToken cancellationToken = default);
    Task<ReliefWebReportRecord> GetReportAsync(long reportId, CancellationToken cancellationToken = default);
}

public interface IReliefWebSourceStore
{
    Task<ReliefWebAttachmentStoreResult> AttachReliefWebSourceAsync(
        EventId eventId,
        SourceItem sourceItem,
        CancellationToken cancellationToken = default);
}
