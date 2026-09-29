namespace GeopoliticalMonitor.Application.Firms;

public sealed class FirmsRefreshService
{
    public const double MaximumLongitudeSpan = 90;
    public const double MaximumLatitudeSpan = 60;
    public const int MaximumObservations = 20_000;
    public static readonly TimeSpan ObservationWindow = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaximumFutureSkew = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private readonly IFirmsObservationFeed feed;
    private readonly TimeProvider timeProvider;

    public FirmsRefreshService(IFirmsObservationFeed feed, TimeProvider? timeProvider = null)
    {
        this.feed = feed;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<FirmsRefreshResult> RefreshAsync(
        FirmsViewport viewport,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateAndSplit(viewport);
        if (validation.Status != FirmsRefreshStatus.Succeeded)
        {
            return new FirmsRefreshResult(validation.Status);
        }

        bool entered;
        try
        {
            entered = await refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new FirmsRefreshResult(FirmsRefreshStatus.Cancelled);
        }

        if (!entered)
        {
            return new FirmsRefreshResult(FirmsRefreshStatus.AlreadyInProgress);
        }

        try
        {
            var observations = new List<FirmsObservation>();
            var skipped = 0;
            foreach (var source in FirmsSources.All)
            {
                foreach (var bounds in validation.Bounds)
                {
                    var batch = await feed.FetchAsync(source, bounds, cancellationToken).ConfigureAwait(false);
                    observations.AddRange(batch.Observations);
                    skipped += batch.SkippedMalformedRecords;
                }
            }

            var refreshedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
            var cutoff = refreshedAtUtc - ObservationWindow;
            var latestAccepted = refreshedAtUtc + MaximumFutureSkew;
            if (observations.Any(item => item.AcquiredAtUtc > latestAccepted))
            {
                return new FirmsRefreshResult(FirmsRefreshStatus.InvalidProviderResponse);
            }

            var accepted = observations
                .Where(item => item.AcquiredAtUtc >= cutoff && item.AcquiredAtUtc <= latestAccepted)
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderByDescending(item => item.AcquiredAtUtc)
                .ThenBy(item => item.Id, StringComparer.Ordinal)
                .ToArray();

            if (accepted.Length > MaximumObservations)
            {
                return new FirmsRefreshResult(FirmsRefreshStatus.TooManyObservations);
            }

            var snapshot = new FirmsSnapshot(
                accepted,
                refreshedAtUtc,
                accepted.Length == 0 ? null : accepted[^1].AcquiredAtUtc,
                accepted.Length == 0 ? null : accepted[0].AcquiredAtUtc,
                viewport,
                skipped);
            return new FirmsRefreshResult(FirmsRefreshStatus.Succeeded, snapshot);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new FirmsRefreshResult(FirmsRefreshStatus.Cancelled);
        }
        catch (FirmsFeedException exception)
        {
            return new FirmsRefreshResult(exception.Kind switch
            {
                FirmsFeedFailureKind.ConfigurationUnavailable => FirmsRefreshStatus.ConfigurationUnavailable,
                FirmsFeedFailureKind.ResponseTooLarge => FirmsRefreshStatus.ResponseTooLarge,
                FirmsFeedFailureKind.InvalidProviderResponse => FirmsRefreshStatus.InvalidProviderResponse,
                FirmsFeedFailureKind.TooManyObservations => FirmsRefreshStatus.TooManyObservations,
                _ => FirmsRefreshStatus.RequestFailed,
            });
        }
        catch (Exception)
        {
            return new FirmsRefreshResult(FirmsRefreshStatus.RequestFailed);
        }
        finally
        {
            refreshGate.Release();
        }
    }

    public static FirmsViewportValidation ValidateAndSplit(FirmsViewport viewport)
    {
        if (!double.IsFinite(viewport.West)
            || !double.IsFinite(viewport.South)
            || !double.IsFinite(viewport.East)
            || !double.IsFinite(viewport.North)
            || viewport.West < -180 || viewport.West > 180
            || viewport.East < -180 || viewport.East > 180
            || viewport.South < -90 || viewport.South > 90
            || viewport.North < -90 || viewport.North > 90
            || viewport.South >= viewport.North)
        {
            return new FirmsViewportValidation(FirmsRefreshStatus.InvalidViewport, []);
        }

        var longitudeSpan = viewport.West <= viewport.East
            ? viewport.East - viewport.West
            : (180 - viewport.West) + (viewport.East + 180);
        var latitudeSpan = viewport.North - viewport.South;
        if (longitudeSpan <= 0)
        {
            return new FirmsViewportValidation(FirmsRefreshStatus.InvalidViewport, []);
        }

        if (longitudeSpan > MaximumLongitudeSpan || latitudeSpan > MaximumLatitudeSpan)
        {
            return new FirmsViewportValidation(FirmsRefreshStatus.AreaTooLarge, []);
        }

        IReadOnlyList<FirmsBoundingBox> bounds;
        if (viewport.West <= viewport.East)
        {
            bounds = [new FirmsBoundingBox(viewport.West, viewport.South, viewport.East, viewport.North)];
        }
        else
        {
            var split = new List<FirmsBoundingBox>(2);
            if (viewport.West < 180)
            {
                split.Add(new FirmsBoundingBox(viewport.West, viewport.South, 180, viewport.North));
            }
            if (viewport.East > -180)
            {
                split.Add(new FirmsBoundingBox(-180, viewport.South, viewport.East, viewport.North));
            }
            bounds = split;
        }
        return new FirmsViewportValidation(FirmsRefreshStatus.Succeeded, bounds);
    }
}

public sealed record FirmsViewportValidation(
    FirmsRefreshStatus Status,
    IReadOnlyList<FirmsBoundingBox> Bounds);
