using GeopoliticalMonitor.Application.Refresh;

namespace GeopoliticalMonitor.Application.Gdacs;

/// <summary>Registers the shared <see cref="GdacsRefreshService"/> with the in-app refresh scheduler.</summary>
public sealed class GdacsScheduledProvider(GdacsRefreshService refreshService) : IScheduledProvider
{
    public const string ProviderDisplayName = "GDACS";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    public ScheduledProviderId Id => ScheduledProviderId.Gdacs;
    public string DisplayName => ProviderDisplayName;
    public TimeSpan Interval => RefreshInterval;
    public GdacsRefreshService Service { get; } = refreshService;

    public async Task<ProviderRefreshOutcome> RefreshAsync(CancellationToken cancellationToken)
        => Classify(await Service.RefreshAsync(cancellationToken).ConfigureAwait(false));

    public static ProviderRefreshOutcome Classify(GdacsRefreshResult result) => result.Status switch
    {
        GdacsRefreshStatus.Succeeded => ProviderRefreshOutcome.Succeeded(result.NewAlerts),
        GdacsRefreshStatus.AlreadyInProgress => ProviderRefreshOutcome.AlreadyInProgress,
        GdacsRefreshStatus.Cancelled => ProviderRefreshOutcome.Cancelled,
        GdacsRefreshStatus.ProviderUnavailable => ProviderRefreshOutcome.Failed(ProviderStatusMessage.ProviderUnavailable),
        GdacsRefreshStatus.TimedOut => ProviderRefreshOutcome.Failed(ProviderStatusMessage.TimedOut),
        GdacsRefreshStatus.InvalidResponse => ProviderRefreshOutcome.Failed(ProviderStatusMessage.InvalidResponse),
        _ => ProviderRefreshOutcome.Failed(ProviderStatusMessage.PersistenceFailure),
    };
}
