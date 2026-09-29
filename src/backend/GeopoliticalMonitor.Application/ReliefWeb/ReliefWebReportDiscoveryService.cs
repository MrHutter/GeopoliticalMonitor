using System.Globalization;
using System.Text;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;

namespace GeopoliticalMonitor.Application.ReliefWeb;

public sealed class ReliefWebReportDiscoveryService
{
    public const int MaximumQueryLength = 200;
    public const int MaximumSearchWindowDays = 366;
    public const string SourceIdPrefix = "src-reliefweb-report-";
    public const string ExternalIdPrefix = "ReliefWeb:Report:";

    private readonly IReliefWebReportClient client;
    private readonly IEventStore eventStore;
    private readonly IReliefWebSourceStore sourceStore;
    private readonly TimeProvider timeProvider;

    public ReliefWebReportDiscoveryService(
        IReliefWebReportClient client,
        IEventStore eventStore,
        IReliefWebSourceStore sourceStore,
        TimeProvider? timeProvider = null)
    {
        this.client = client;
        this.eventStore = eventStore;
        this.sourceStore = sourceStore;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ReliefWebSearchResult> SearchAsync(
        ReliefWebSearchCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!EventCatalogService.TryParseEventId(command.EventId, out var eventId))
        {
            return new ReliefWebSearchResult(ReliefWebSearchStatus.InvalidEventId);
        }

        var query = command.QueryText?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length > MaximumQueryLength)
        {
            return new ReliefWebSearchResult(ReliefWebSearchStatus.InvalidQuery);
        }

        if (!TryParseUtc(command.FromDateUtc, out var fromUtc)
            || !TryParseUtc(command.ToDateUtc, out var toUtc)
            || fromUtc > toUtc
            || toUtc - fromUtc > TimeSpan.FromDays(MaximumSearchWindowDays))
        {
            return new ReliefWebSearchResult(ReliefWebSearchStatus.InvalidDateRange);
        }

        var countryIso3 = NormalizeIso3(command.CountryIso3);
        if (!string.IsNullOrWhiteSpace(command.CountryIso3) && countryIso3 is null)
        {
            return new ReliefWebSearchResult(ReliefWebSearchStatus.InvalidCountry);
        }

        try
        {
            if (await eventStore.GetEventByIdAsync(eventId, cancellationToken).ConfigureAwait(false) is null)
            {
                return new ReliefWebSearchResult(ReliefWebSearchStatus.EventNotFound);
            }

            var batch = await client
                .SearchReportsAsync(new ReliefWebSearchCriteria(query, fromUtc, toUtc, countryIso3), cancellationToken)
                .ConfigureAwait(false);
            var candidates = batch.Reports.Select(ToCandidate).ToArray();
            return new ReliefWebSearchResult(ReliefWebSearchStatus.Succeeded, candidates, batch.SkippedMalformedReports);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ReliefWebSearchResult(ReliefWebSearchStatus.Cancelled);
        }
        catch (ReliefWebClientException exception)
        {
            return new ReliefWebSearchResult(MapSearchFailure(exception.Kind));
        }
        catch (Exception)
        {
            return new ReliefWebSearchResult(ReliefWebSearchStatus.PersistenceFailure);
        }
    }

    public async Task<ReliefWebAttachResult> AttachAsync(
        string? eventIdValue,
        string? reportIdValue,
        CancellationToken cancellationToken = default)
    {
        if (!EventCatalogService.TryParseEventId(eventIdValue, out var eventId))
        {
            return new ReliefWebAttachResult(ReliefWebAttachStatus.InvalidEventId);
        }

        if (!long.TryParse(reportIdValue, NumberStyles.None, CultureInfo.InvariantCulture, out var reportId) || reportId <= 0)
        {
            return new ReliefWebAttachResult(ReliefWebAttachStatus.InvalidReportId);
        }

        try
        {
            if (await eventStore.GetEventByIdAsync(eventId, cancellationToken).ConfigureAwait(false) is null)
            {
                return new ReliefWebAttachResult(ReliefWebAttachStatus.EventNotFound);
            }

            var report = await client.GetReportAsync(reportId, cancellationToken).ConfigureAwait(false);
            if (report.ReportId != reportId || report.OriginalPublishedAtUtc is null)
            {
                return new ReliefWebAttachResult(ReliefWebAttachStatus.InvalidResponse);
            }

            var retrievedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            var sourceItem = NormalizeSource(report, retrievedAtUtc);
            var persisted = await sourceStore
                .AttachReliefWebSourceAsync(eventId, sourceItem, cancellationToken)
                .ConfigureAwait(false);

            return persisted.Disposition switch
            {
                ReliefWebAttachmentStoreDisposition.CreatedAndAttached
                    => Success(ReliefWebAttachStatus.CreatedAndAttached, persisted),
                ReliefWebAttachmentStoreDisposition.ExistingSourceAttached
                    => Success(ReliefWebAttachStatus.ExistingSourceAttached, persisted),
                ReliefWebAttachmentStoreDisposition.AlreadyAttached
                    => Success(ReliefWebAttachStatus.AlreadyAttached, persisted),
                ReliefWebAttachmentStoreDisposition.Conflict
                    => new ReliefWebAttachResult(ReliefWebAttachStatus.Conflict),
                _ => new ReliefWebAttachResult(ReliefWebAttachStatus.EventNotFound),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new ReliefWebAttachResult(ReliefWebAttachStatus.Cancelled);
        }
        catch (ReliefWebClientException exception)
        {
            return new ReliefWebAttachResult(MapAttachFailure(exception.Kind));
        }
        catch (Exception)
        {
            return new ReliefWebAttachResult(ReliefWebAttachStatus.PersistenceFailure);
        }
    }

    private static ReliefWebReportCandidateDto ToCandidate(ReliefWebReportRecord report)
    {
        return new ReliefWebReportCandidateDto(
            report.ReportId.ToString(CultureInfo.InvariantCulture),
            report.Title,
            report.CanonicalUrl.AbsoluteUri,
            report.OriginalPublishedAtUtc,
            report.Organizations.Select(item => item.DisplayName).ToArray(),
            report.Formats,
            report.Countries.Select(item => item.Name).ToArray(),
            report.Disasters.Select(item => new ReliefWebDisasterDto(
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Name,
                item.Glide,
                item.Types)).ToArray(),
            report.Languages);
    }

    private static SourceItem NormalizeSource(ReliefWebReportRecord report, DateTimeOffset retrievedAtUtc)
    {
        var reportId = report.ReportId.ToString(CultureInfo.InvariantCulture);
        return SourceItem.Create(
            new SourceItemId($"{SourceIdPrefix}{reportId}"),
            SourceType.Other,
            $"{ExternalIdPrefix}{reportId}",
            report.Title,
            BuildMetadataText(report),
            report.CanonicalUrl,
            BuildOrganizationDisplay(report.Organizations),
            null,
            report.OriginalPublishedAtUtc!.Value,
            retrievedAtUtc,
            null,
            retrievedAtUtc);
    }

    private static string BuildMetadataText(ReliefWebReportRecord report)
    {
        var builder = new StringBuilder("ReliefWeb report metadata.");
        AppendList(builder, "Format", report.Formats);
        AppendList(builder, "Countries", report.Countries.Select(item => item.Name));
        AppendList(builder, "Related disasters", report.Disasters.Select(item => item.Name));
        AppendList(builder, "Languages", report.Languages);
        var value = builder.ToString();
        return value.Length <= 4000 ? value : value[..4000];
    }

    private static void AppendList(StringBuilder builder, string label, IEnumerable<string> values)
    {
        var items = values.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToArray();
        if (items.Length > 0)
        {
            builder.Append(' ').Append(label).Append(": ").AppendJoin(", ", items).Append('.');
        }
    }

    private static string? BuildOrganizationDisplay(IReadOnlyList<ReliefWebOrganization> organizations)
    {
        const int maximumLength = 256;
        var names = organizations
            .Select(item => item.DisplayName.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (names.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var name in names)
        {
            var separator = builder.Length == 0 ? string.Empty : "; ";
            if (builder.Length + separator.Length + name.Length > maximumLength)
            {
                break;
            }

            builder.Append(separator).Append(name);
        }

        return builder.Length == 0 ? names[0][..Math.Min(names[0].Length, maximumLength)] : builder.ToString();
    }

    private static ReliefWebAttachResult Success(ReliefWebAttachStatus status, ReliefWebAttachmentStoreResult persisted)
        => new(status, EventDtoMapper.ToDto(persisted.Source!));

    private static bool TryParseUtc(string? value, out DateTimeOffset parsed)
    {
        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out parsed);
    }

    private static string? NormalizeIso3(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        return normalized.Length == 3 && normalized.All(char.IsAsciiLetter) ? normalized : null;
    }

    private static ReliefWebSearchStatus MapSearchFailure(ReliefWebFailureKind kind) => kind switch
    {
        ReliefWebFailureKind.ConfigurationUnavailable => ReliefWebSearchStatus.ConfigurationUnavailable,
        ReliefWebFailureKind.TimedOut => ReliefWebSearchStatus.TimedOut,
        ReliefWebFailureKind.QuotaExceeded => ReliefWebSearchStatus.QuotaExceeded,
        ReliefWebFailureKind.InvalidConfiguration => ReliefWebSearchStatus.InvalidConfiguration,
        ReliefWebFailureKind.InvalidResponse => ReliefWebSearchStatus.InvalidResponse,
        _ => ReliefWebSearchStatus.ProviderUnavailable,
    };

    private static ReliefWebAttachStatus MapAttachFailure(ReliefWebFailureKind kind) => kind switch
    {
        ReliefWebFailureKind.ConfigurationUnavailable => ReliefWebAttachStatus.ConfigurationUnavailable,
        ReliefWebFailureKind.TimedOut => ReliefWebAttachStatus.TimedOut,
        ReliefWebFailureKind.QuotaExceeded => ReliefWebAttachStatus.QuotaExceeded,
        ReliefWebFailureKind.InvalidConfiguration => ReliefWebAttachStatus.InvalidConfiguration,
        ReliefWebFailureKind.InvalidResponse => ReliefWebAttachStatus.InvalidResponse,
        ReliefWebFailureKind.NotFound => ReliefWebAttachStatus.ReportNotFound,
        _ => ReliefWebAttachStatus.ProviderUnavailable,
    };
}
