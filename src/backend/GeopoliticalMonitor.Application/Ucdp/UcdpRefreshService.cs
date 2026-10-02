namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>
/// Discovers and imports monthly UCDP Candidate releases. One instance is shared by the timer and the manual action,
/// so its gate allows one in-flight refresh. Every release is fetched before anything is persisted.
/// </summary>
public sealed class UcdpRefreshService
{
    public const int FirstImportProbeCount = 4;

    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly IUcdpCandidateFeed feed;
    private readonly IUcdpCandidateStore store;
    private readonly TimeProvider timeProvider;
    private readonly int maximumEventsPerRelease;

    // Session memory: the newest release imported in this app session, including releases with zero qualifying
    // rows (which leave no Event behind), so such a release is not downloaded again on every check.
    private int sessionNewestRelease;
    private int knownNewestRelease;
    private bool sessionImportedAny;

    public UcdpRefreshService(
        IUcdpCandidateFeed feed,
        IUcdpCandidateStore store,
        TimeProvider? timeProvider = null,
        int maximumEventsPerRelease = UcdpCandidateNormalizer.MaximumEventsPerRelease)
    {
        this.feed = feed;
        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.maximumEventsPerRelease = maximumEventsPerRelease;
    }

    /// <summary>The newest release known to be imported (stored or this session), for status display.</summary>
    public UcdpRelease? NewestImportedRelease
    {
        get
        {
            var ordinal = Volatile.Read(ref knownNewestRelease);
            return ordinal > 0 && UcdpRelease.TryFromOrdinal(ordinal, out var release) ? release : null;
        }
    }

    /// <summary>Reads the stored newest release so status can show it before the first check.</summary>
    public async Task LoadStateAsync(CancellationToken cancellationToken = default)
    {
        Remember(await store.GetNewestUcdpReleaseAsync(cancellationToken).ConfigureAwait(false));
    }

    public async Task<UcdpRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        bool entered;
        try
        {
            entered = await refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new UcdpRefreshResult(UcdpRefreshStatus.Cancelled);
        }

        if (!entered)
        {
            return new UcdpRefreshResult(UcdpRefreshStatus.AlreadyInProgress);
        }

        try
        {
            return await RefreshCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task<UcdpRefreshResult> RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var retrievedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var current = UcdpRelease.ForMonthOf(retrievedAtUtc);

        int? stored;
        try
        {
            stored = await store.GetNewestUcdpReleaseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new UcdpRefreshResult(UcdpRefreshStatus.Cancelled);
        }
        catch (Exception)
        {
            return new UcdpRefreshResult(UcdpRefreshStatus.PersistenceFailure);
        }

        Remember(stored);
        var newest = Math.Max(stored ?? 0, Volatile.Read(ref sessionNewestRelease));

        List<UcdpReleaseFetchResult> fetched;
        try
        {
            fetched = newest == 0 && !sessionImportedAny
                ? await DiscoverFirstImportAsync(current, cancellationToken).ConfigureAwait(false)
                : await DiscoverNextReleaseAsync(UcdpRelease.FromOrdinal(newest), current, cancellationToken).ConfigureAwait(false);
        }
        catch (UcdpFeedException exception)
        {
            return new UcdpRefreshResult(exception.Kind switch
            {
                UcdpFeedFailureKind.ProviderUnavailable => UcdpRefreshStatus.ProviderUnavailable,
                UcdpFeedFailureKind.TimedOut => UcdpRefreshStatus.TimedOut,
                _ => UcdpRefreshStatus.InvalidResponse,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new UcdpRefreshResult(UcdpRefreshStatus.Cancelled);
        }

        if (fetched.Count == 0)
        {
            return new UcdpRefreshResult(UcdpRefreshStatus.NoNewRelease, NewestImportedRelease?.Ordinal);
        }

        var total = new UcdpRefreshResult(UcdpRefreshStatus.Succeeded);
        foreach (var release in fetched.OrderBy(item => item.Release.Ordinal))
        {
            var batch = release.Batch!;
            var normalized = UcdpCandidateNormalizer.Normalize(batch.Records, release.Release, retrievedAtUtc, maximumEventsPerRelease);
            UcdpReleasePersistenceResult persisted;
            try
            {
                persisted = await store
                    .PersistUcdpCandidateReleaseAsync(normalized.Imports, release.Release.Ordinal, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return total with { Status = UcdpRefreshStatus.Cancelled };
            }
            catch (Exception)
            {
                return total with { Status = UcdpRefreshStatus.PersistenceFailure };
            }

            RememberSessionImport(release.Release.Ordinal);
            total = total with
            {
                NewestRelease = NewestImportedRelease?.Ordinal,
                ReleasesImported = total.ReleasesImported + 1,
                NewEvents = total.NewEvents + persisted.NewEvents,
                UpdatedEvents = total.UpdatedEvents + persisted.UpdatedEvents,
                UnchangedRecords = total.UnchangedRecords + persisted.UnchangedRecords,
                Conflicts = total.Conflicts + persisted.Conflicts,
                SkippedMalformedRecords = total.SkippedMalformedRecords + batch.SkippedMalformedRecords + normalized.SkippedMalformed,
                SkippedInvalidCoordinates = total.SkippedInvalidCoordinates + normalized.SkippedInvalidCoordinates,
                SkippedImpreciseLocation = total.SkippedImpreciseLocation + normalized.SkippedImpreciseLocation,
                SkippedZeroFatalities = total.SkippedZeroFatalities + normalized.SkippedZeroFatalities,
                SkippedOutsideWindow = total.SkippedOutsideWindow + normalized.SkippedOutsideWindow,
                DroppedByCap = total.DroppedByCap + normalized.DroppedByCap,
            };
        }

        return total;
    }

    /// <summary>First import: the newest of up to four releases counting down from the current month, plus the one before it.</summary>
    private async Task<List<UcdpReleaseFetchResult>> DiscoverFirstImportAsync(UcdpRelease current, CancellationToken cancellationToken)
    {
        var newestFound = await ProbeDownwardAsync(current, minimumOrdinalExclusive: 0, cancellationToken).ConfigureAwait(false);
        if (newestFound is null)
        {
            return [];
        }

        var previous = await feed.FetchReleaseAsync(newestFound.Release.Previous(), cancellationToken).ConfigureAwait(false);
        return previous.Availability == UcdpReleaseAvailability.Published && previous.Batch is not null
            ? [previous, newestFound]
            : [newestFound];
    }

    /// <summary>
    /// Later checks probe only the release after the newest imported one. If that release is missing although it is
    /// already two or more months old (for example monthly files replaced by a half-year file while the app was not
    /// opened), the first-import probe finds the newest published release so the provider cannot stall.
    /// </summary>
    private async Task<List<UcdpReleaseFetchResult>> DiscoverNextReleaseAsync(UcdpRelease newest, UcdpRelease current, CancellationToken cancellationToken)
    {
        var next = newest.Next();
        if (next.Ordinal > current.Ordinal)
        {
            return [];
        }

        var result = await feed.FetchReleaseAsync(next, cancellationToken).ConfigureAwait(false);
        if (result.Availability == UcdpReleaseAvailability.Published && result.Batch is not null)
        {
            return [result];
        }

        if (next.Ordinal < current.Previous().Ordinal)
        {
            var found = await ProbeDownwardAsync(current, newest.Ordinal, cancellationToken).ConfigureAwait(false);
            return found is null ? [] : [found];
        }

        return [];
    }

    private async Task<UcdpReleaseFetchResult?> ProbeDownwardAsync(UcdpRelease start, int minimumOrdinalExclusive, CancellationToken cancellationToken)
    {
        var candidate = start;
        for (var probe = 0; probe < FirstImportProbeCount && candidate.Ordinal > minimumOrdinalExclusive; probe++)
        {
            var result = await feed.FetchReleaseAsync(candidate, cancellationToken).ConfigureAwait(false);
            if (result.Availability == UcdpReleaseAvailability.Published && result.Batch is not null)
            {
                return result;
            }

            candidate = candidate.Previous();
        }

        return null;
    }

    private void RememberSessionImport(int ordinal)
    {
        sessionImportedAny = true;
        if (ordinal > Volatile.Read(ref sessionNewestRelease))
        {
            Volatile.Write(ref sessionNewestRelease, ordinal);
        }

        Remember(ordinal);
    }

    private void Remember(int? ordinal)
    {
        if (ordinal is { } value && value > Volatile.Read(ref knownNewestRelease))
        {
            Volatile.Write(ref knownNewestRelease, value);
        }
    }
}
