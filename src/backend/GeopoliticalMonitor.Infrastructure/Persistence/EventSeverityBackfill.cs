using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

/// <summary>
/// One-time, post-migration severity backfill for Events that existed before severity was introduced.
/// The AddEventSeverityAndProvenance migration marks those rows with <see cref="PendingInputs"/>; this step
/// resolves each marked row exactly once, inside one transaction, and never fails on an unusual row.
/// </summary>
public static class EventSeverityBackfill
{
    public const string PendingInputs = "backfill: pending";
    public const string NoProviderDataInputs = "backfill: no provider data";

    public static async Task<int> RunAsync(GeopoliticalMonitorDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var pending = await dbContext.Events
            .Where(item => item.SeverityInputs == PendingInputs && item.SeverityRule == EventSeverityAssessment.LegacyDefaultRule)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return 0;
        }

        var gdacsEventIds = pending
            .Where(item => item.Provider == nameof(EventProvider.Gdacs))
            .Select(item => item.Id)
            .ToArray();
        var gdacsSources = gdacsEventIds.Length == 0
            ? []
            : await dbContext.EventSourceAssociations
                .AsNoTracking()
                .Where(item => gdacsEventIds.Contains(item.EventId))
                .Join(
                    dbContext.SourceItems.AsNoTracking(),
                    association => association.SourceItemId,
                    source => source.Id,
                    (association, source) => new GdacsSourceText(association.EventId, source.SourceType, source.ExternalId, source.Text, source.PublishedAtUtc))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        var sourcesByEvent = gdacsSources.ToLookup(item => item.EventId, StringComparer.Ordinal);

        foreach (var entity in pending)
        {
            var assessment = Resolve(entity.Id, entity.Provider, entity.ProviderKey, sourcesByEvent);
            entity.Severity = assessment.Level.ToString();
            entity.SeverityRule = assessment.Rule;
            entity.SeverityInputs = assessment.Inputs;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return pending.Count;
    }

    private static EventSeverityAssessment Resolve(
        string eventId,
        string provider,
        string? providerKey,
        ILookup<string, GdacsSourceText> sourcesByEvent)
    {
        try
        {
            if (provider == nameof(EventProvider.Gdacs))
            {
                var externalIdPrefix = $"{GdacsRefreshService.ExternalIdPrefix}{providerKey}:";
                var newestFirst = sourcesByEvent[eventId]
                    .Where(item => item.SourceType == nameof(SourceType.DatasetRecord)
                        && item.ExternalId?.StartsWith(externalIdPrefix, StringComparison.Ordinal) == true)
                    .OrderByDescending(item => item.PublishedAtUtc);
                foreach (var source in newestFirst)
                {
                    if (GdacsSeverityRule.TryAssessFromProvenanceText(source.Text, out var parsed))
                    {
                        return parsed!;
                    }
                }

                return Fallback(GdacsSeverityRule.BackfillUnavailableInputs);
            }

            if (provider == nameof(EventProvider.Demo) && DemoEventSeverities.TryGetAssessment(eventId, out var demo))
            {
                return demo!;
            }
        }
        catch (Exception)
        {
            // An unusual row must never block startup; it keeps the explicit legacy default instead.
        }

        return Fallback(NoProviderDataInputs);
    }

    private static EventSeverityAssessment Fallback(string inputs)
        => new(EventSeverity.Low, EventSeverityAssessment.LegacyDefaultRule, inputs);

    private sealed record GdacsSourceText(string EventId, string SourceType, string? ExternalId, string Text, DateTimeOffset PublishedAtUtc);
}
