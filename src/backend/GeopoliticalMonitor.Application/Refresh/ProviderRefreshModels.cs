namespace GeopoliticalMonitor.Application.Refresh;

/// <summary>Fixed identities of providers that may be refreshed on the in-app timer.</summary>
public enum ScheduledProviderId
{
    Gdacs,
    UcdpCandidate,
}

public enum ProviderRefreshState
{
    Idle,
    Refreshing,
    Failed,
    Stale,
    Unconfigured,
}

public enum ProviderRefreshOutcomeKind
{
    Succeeded,
    AlreadyInProgress,
    Failed,
    Cancelled,
    Unconfigured,
}

/// <summary>The fixed set of short status messages that may cross the bridge.</summary>
public enum ProviderStatusMessage
{
    None,
    UpToDate,
    ProviderUnavailable,
    TimedOut,
    InvalidResponse,
    PersistenceFailure,
    NotConfigured,
    NoNewRelease,
}

public sealed record ProviderRefreshOutcome(
    ProviderRefreshOutcomeKind Kind,
    ProviderStatusMessage Message = ProviderStatusMessage.None,
    int NewAlerts = 0)
{
    public static ProviderRefreshOutcome Succeeded(int newAlerts) => new(ProviderRefreshOutcomeKind.Succeeded, ProviderStatusMessage.UpToDate, newAlerts);
    public static ProviderRefreshOutcome SucceededWith(ProviderStatusMessage message) => new(ProviderRefreshOutcomeKind.Succeeded, message);
    public static ProviderRefreshOutcome Failed(ProviderStatusMessage message) => new(ProviderRefreshOutcomeKind.Failed, message);
    public static ProviderRefreshOutcome AlreadyInProgress { get; } = new(ProviderRefreshOutcomeKind.AlreadyInProgress);
    public static ProviderRefreshOutcome Cancelled { get; } = new(ProviderRefreshOutcomeKind.Cancelled);
    public static ProviderRefreshOutcome Unconfigured { get; } = new(ProviderRefreshOutcomeKind.Unconfigured, ProviderStatusMessage.NotConfigured);
}

public sealed record ProviderRefreshStatus(
    ScheduledProviderId Provider,
    string DisplayName,
    ProviderRefreshState State,
    DateTimeOffset? LastSuccessAtUtc,
    DateTimeOffset? NextRefreshAtUtc,
    ProviderStatusMessage Message,
    int NewAlerts,
    string? CadenceLabel = null,
    string? DataThrough = null);

/// <summary>Optional provider-specific status: a fixed cadence label, the month the newest data runs through (yyyy-MM), and a data-age stale flag.</summary>
public sealed record ProviderStatusDetail(string? CadenceLabel, string? DataThrough, bool DataStale)
{
    public static ProviderStatusDetail None { get; } = new(null, null, false);
}

/// <summary>A provider refresh the scheduler may run. Implementations wrap one existing provider service.</summary>
public interface IScheduledProvider
{
    ScheduledProviderId Id { get; }
    string DisplayName { get; }
    TimeSpan Interval { get; }

    /// <summary>Delay from the first app.ready to the first timer refresh.</summary>
    TimeSpan FirstRunDelay => ProviderRefreshScheduler.FirstRunDelay;

    Task<ProviderRefreshOutcome> RefreshAsync(CancellationToken cancellationToken);

    /// <summary>Called under the scheduler lock; must be fast and must not block.</summary>
    ProviderStatusDetail GetStatusDetail(DateTimeOffset now) => ProviderStatusDetail.None;
}

public static class ProviderStatusMessages
{
    public static string? ToText(ProviderStatusMessage message) => message switch
    {
        ProviderStatusMessage.UpToDate => "Up to date.",
        ProviderStatusMessage.ProviderUnavailable => "Provider unavailable. Existing events were preserved.",
        ProviderStatusMessage.TimedOut => "Request timed out. Existing events were preserved.",
        ProviderStatusMessage.InvalidResponse => "Unsupported provider response. No provider data was changed.",
        ProviderStatusMessage.PersistenceFailure => "Provider data could not be saved. Existing events were preserved.",
        ProviderStatusMessage.NotConfigured => "Not configured.",
        ProviderStatusMessage.NoNewRelease => "No new release",
        _ => null,
    };
}
