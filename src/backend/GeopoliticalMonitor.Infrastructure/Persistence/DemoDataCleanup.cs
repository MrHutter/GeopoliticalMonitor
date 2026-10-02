using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed record DemoDataCleanupResult(DemoDataCleanupOutcome Outcome, int RemovedEvents, int RemovedSources)
{
    public static DemoDataCleanupResult AlreadyCompleted { get; } = new(DemoDataCleanupOutcome.AlreadyCompleted, 0, 0);

    public static DemoDataCleanupResult Failed { get; } = new(DemoDataCleanupOutcome.Failed, 0, 0);

    /// <summary>True only when this run removed at least one demo Event; drives the one-time UI notice.</summary>
    public bool RemovedDemoEvents => Outcome == DemoDataCleanupOutcome.Completed && RemovedEvents > 0;
}

public enum DemoDataCleanupOutcome
{
    Completed,
    AlreadyCompleted,
    Failed,
}

/// <summary>
/// One-time removal of the formerly seeded demo data (Slice 4). In one transaction it deletes Events whose id starts
/// with <see cref="DemoEventIdPrefix"/> and SourceItems whose id starts with <see cref="DemoSourceIdPrefix"/>
/// (associations, Watchlist memberships and Alert occurrences follow by cascade) and records
/// <see cref="CompletedPreferenceKey"/>, so it never runs again, even when nothing was removed. The key is native-only:
/// the bridge accepts only the AccentTheme preference key.
/// </summary>
public static class DemoDataCleanup
{
    public const string CompletedPreferenceKey = "DemoDataCleanupCompleted";
    public const string CompletedPreferenceValue = "Completed";
    public const string DemoEventIdPrefix = "evt-demo-";
    public const string DemoSourceIdPrefix = "src-demo-";

    /// <summary>Runs the cleanup and never throws: on failure nothing is committed and the next launch retries.</summary>
    public static async Task<DemoDataCleanupResult> TryRunAsync(
        Func<GeopoliticalMonitorDbContext> createDbContext,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = createDbContext();
            return await RunAsync(dbContext, timeProvider ?? TimeProvider.System, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return DemoDataCleanupResult.Failed;
        }
    }

    public static async Task<DemoDataCleanupResult> RunAsync(
        GeopoliticalMonitorDbContext dbContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var completed = await dbContext.ApplicationPreferences
            .AnyAsync(item => item.Key == CompletedPreferenceKey, cancellationToken)
            .ConfigureAwait(false);
        if (completed)
        {
            return DemoDataCleanupResult.AlreadyCompleted;
        }

        // Substring comparison keeps the prefix match case-sensitive; SQLite LIKE would also match "EVT-DEMO-".
        var removedEvents = await dbContext.Events
            .Where(item => item.Id.Substring(0, DemoEventIdPrefix.Length) == DemoEventIdPrefix)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
        var removedSources = await dbContext.SourceItems
            .Where(item => item.Id.Substring(0, DemoSourceIdPrefix.Length) == DemoSourceIdPrefix)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        dbContext.ApplicationPreferences.Add(new ApplicationPreferenceEntity
        {
            Key = CompletedPreferenceKey,
            Value = CompletedPreferenceValue,
            UpdatedAt = timeProvider.GetUtcNow(),
        });
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new DemoDataCleanupResult(DemoDataCleanupOutcome.Completed, removedEvents, removedSources);
    }
}
