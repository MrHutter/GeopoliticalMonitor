using GeopoliticalMonitor.Application.Refresh;

namespace GeopoliticalMonitor.Application.Ucdp;

/// <summary>Registers the shared <see cref="UcdpRefreshService"/> with the in-app refresh scheduler.</summary>
public sealed class UcdpScheduledProvider(UcdpRefreshService refreshService) : IScheduledProvider
{
    public const string ProviderDisplayName = "UCDP Candidate";
    public const string CadenceLabel = "monthly release";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan DataStaleAfter = TimeSpan.FromDays(45);

    public ScheduledProviderId Id => ScheduledProviderId.UcdpCandidate;
    public string DisplayName => ProviderDisplayName;
    public TimeSpan Interval => RefreshInterval;
    public TimeSpan FirstRunDelay => InitialDelay;
    public UcdpRefreshService Service { get; } = refreshService;

    public async Task<ProviderRefreshOutcome> RefreshAsync(CancellationToken cancellationToken)
        => Classify(await Service.RefreshAsync(cancellationToken).ConfigureAwait(false));

    /// <summary>Stale when the newest imported release was extracted more than 45 days ago.</summary>
    public ProviderStatusDetail GetStatusDetail(DateTimeOffset now)
    {
        var newest = Service.NewestImportedRelease;
        return new ProviderStatusDetail(
            CadenceLabel,
            newest?.MonthKey,
            newest is { } release && now - release.ExtractedAtUtc > DataStaleAfter);
    }

    public static ProviderRefreshOutcome Classify(UcdpRefreshResult result) => result.Status switch
    {
        UcdpRefreshStatus.Succeeded => ProviderRefreshOutcome.Succeeded(0),
        UcdpRefreshStatus.NoNewRelease => ProviderRefreshOutcome.SucceededWith(ProviderStatusMessage.NoNewRelease),
        UcdpRefreshStatus.AlreadyInProgress => ProviderRefreshOutcome.AlreadyInProgress,
        UcdpRefreshStatus.Cancelled => ProviderRefreshOutcome.Cancelled,
        UcdpRefreshStatus.ProviderUnavailable => ProviderRefreshOutcome.Failed(ProviderStatusMessage.ProviderUnavailable),
        UcdpRefreshStatus.TimedOut => ProviderRefreshOutcome.Failed(ProviderStatusMessage.TimedOut),
        UcdpRefreshStatus.InvalidResponse => ProviderRefreshOutcome.Failed(ProviderStatusMessage.InvalidResponse),
        _ => ProviderRefreshOutcome.Failed(ProviderStatusMessage.PersistenceFailure),
    };
}
