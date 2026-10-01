namespace GeopoliticalMonitor.Application.Refresh;

/// <summary>
/// In-process refresh scheduler that runs registered providers on a timer only while the app window is open.
/// It owns no threads beyond one tick loop, persists nothing, and stops with the window.
/// Manual and timer refreshes share each provider's own service instance and in-flight gate.
/// </summary>
public sealed class ProviderRefreshScheduler
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan FirstRunDelay = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan BackoffBase = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan BackoffCap = TimeSpan.FromHours(6);
    public const double JitterFraction = 0.1;
    public const int StaleIntervalMultiplier = 3;

    private readonly object gate = new();
    private readonly List<Entry> entries;
    private readonly TimeProvider timeProvider;
    private readonly Func<double> random;
    private readonly Dictionary<ScheduledProviderId, ProviderRefreshState> lastPublishedStates = [];
    private CancellationTokenSource? loopCancellation;
    private Task? loopTask;
    private DateTimeOffset? startedAtUtc;
    private int startRequested;
    private volatile bool stopped;

    public ProviderRefreshScheduler(
        IEnumerable<IScheduledProvider> providers,
        TimeProvider? timeProvider = null,
        Func<double>? random = null)
    {
        entries = providers.Select(provider => new Entry(provider)).ToList();
        if (entries.Select(entry => entry.Provider.Id).Distinct().Count() != entries.Count)
        {
            throw new ArgumentException("Each scheduled provider may be registered once.", nameof(providers));
        }

        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.random = random ?? Random.Shared.NextDouble;
    }

    /// <summary>Raised after every state transition. Handlers must not block; they may be invoked on any thread.</summary>
    public event Action? StatusChanged;

    public bool IsStarted => Volatile.Read(ref startRequested) == 1;

    public Task? LoopTask => loopTask;

    /// <summary>Starts the tick loop once. Repeated calls (for example a page reload sending app.ready again) do nothing.</summary>
    public void Start(CancellationToken lifetimeToken)
    {
        if (stopped || Interlocked.Exchange(ref startRequested, 1) == 1)
        {
            return;
        }

        var now = Now();
        lock (gate)
        {
            startedAtUtc = now;
            foreach (var entry in entries.Where(item => !item.Unconfigured))
            {
                entry.NextDueAtUtc ??= now + FirstRunDelay;
            }
        }

        loopCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetimeToken);
        var token = loopCancellation.Token;
        loopTask = Task.Run(() => RunLoopAsync(token), CancellationToken.None);
        RaiseStatusChanged();
    }

    /// <summary>Cancels the loop and waits a bounded time for it to finish. No status is raised afterwards.</summary>
    public void Stop(TimeSpan wait)
    {
        stopped = true;
        var cancellation = loopCancellation;
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            loopTask?.Wait(wait);
        }
        catch (AggregateException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested || stopped)
            {
                return;
            }

            bool due;
            lock (gate)
            {
                due = !entry.Unconfigured
                    && !entry.InFlight
                    && entry.NextDueAtUtc is { } nextDue
                    && Now() >= nextDue;
            }

            if (due)
            {
                await RunTimerRefreshAsync(entry, cancellationToken).ConfigureAwait(false);
            }
        }

        if (StatesChangedSinceLastPublish())
        {
            RaiseStatusChanged();
        }
    }

    /// <summary>
    /// Runs a user-requested refresh through the same tracking as the timer. The refresh delegate must call the
    /// provider's shared service instance; its own gate decides whether the refresh actually runs.
    /// </summary>
    public async Task<TResult> RefreshManuallyAsync<TResult>(
        ScheduledProviderId providerId,
        Func<CancellationToken, Task<TResult>> refresh,
        Func<TResult, ProviderRefreshOutcome> classify,
        CancellationToken cancellationToken)
    {
        var entry = entries.Single(item => item.Provider.Id == providerId);
        TryBegin(entry, isTimer: false, out var markedInFlight);
        if (markedInFlight)
        {
            RaiseStatusChanged();
        }

        TResult result;
        try
        {
            result = await refresh(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            Complete(entry, markedInFlight, ProviderRefreshOutcome.Cancelled);
            throw;
        }

        Complete(entry, markedInFlight, classify(result));
        return result;
    }

    public IReadOnlyList<ProviderRefreshStatus> GetStatuses()
    {
        lock (gate)
        {
            var now = Now();
            return entries.Select(entry => ToStatus(entry, now)).ToArray();
        }
    }

    public TimeSpan CalculateBackoff(TimeSpan interval, int consecutiveFailures)
    {
        var exponent = Math.Clamp(consecutiveFailures - 1, 0, 20);
        var exponential = BackoffBase * Math.Pow(2, exponent);
        var baseDelay = exponential > interval ? exponential : interval;
        if (baseDelay > BackoffCap)
        {
            baseDelay = BackoffCap;
        }

        var jitter = 1 + ((Math.Clamp(random(), 0, 1) * 2) - 1) * JitterFraction;
        var jittered = baseDelay * jitter;
        return jittered > BackoffCap ? BackoffCap : jittered;
    }

    private async Task RunTimerRefreshAsync(Entry entry, CancellationToken cancellationToken)
    {
        if (!TryBegin(entry, isTimer: true, out var markedInFlight))
        {
            return;
        }

        RaiseStatusChanged();
        ProviderRefreshOutcome outcome;
        try
        {
            outcome = await entry.Provider.RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Timer refreshes never throw out of the loop. Cancellation is not a failure.
            outcome = exception is OperationCanceledException or ObjectDisposedException
                ? ProviderRefreshOutcome.Cancelled
                : ProviderRefreshOutcome.Failed(ProviderStatusMessage.ProviderUnavailable);
        }

        Complete(entry, markedInFlight, outcome);
    }

    /// <summary>Marks the provider in flight. A timer refresh is skipped (not queued) when one is already running.</summary>
    private bool TryBegin(Entry entry, bool isTimer, out bool markedInFlight)
    {
        lock (gate)
        {
            if (entry.InFlight)
            {
                markedInFlight = false;
                return !isTimer;
            }

            entry.InFlight = true;
            markedInFlight = true;
            return true;
        }
    }

    private void Complete(Entry entry, bool markedInFlight, ProviderRefreshOutcome outcome)
    {
        lock (gate)
        {
            if (markedInFlight)
            {
                entry.InFlight = false;
            }

            Apply(entry, outcome, Now());
        }

        RaiseStatusChanged();
    }

    private void Apply(Entry entry, ProviderRefreshOutcome outcome, DateTimeOffset now)
    {
        switch (outcome.Kind)
        {
            case ProviderRefreshOutcomeKind.Succeeded:
                entry.ConsecutiveFailures = 0;
                entry.LastSuccessAtUtc = now;
                entry.NextDueAtUtc = now + entry.Provider.Interval;
                entry.LastOutcome = ProviderRefreshOutcomeKind.Succeeded;
                entry.Message = ProviderStatusMessage.UpToDate;
                entry.NewAlerts = Math.Max(0, outcome.NewAlerts);
                break;
            case ProviderRefreshOutcomeKind.Failed:
                entry.ConsecutiveFailures++;
                entry.NextDueAtUtc = now + CalculateBackoff(entry.Provider.Interval, entry.ConsecutiveFailures);
                entry.LastOutcome = ProviderRefreshOutcomeKind.Failed;
                entry.Message = outcome.Message == ProviderStatusMessage.None ? ProviderStatusMessage.ProviderUnavailable : outcome.Message;
                entry.NewAlerts = 0;
                break;
            case ProviderRefreshOutcomeKind.Unconfigured:
                entry.Unconfigured = true;
                entry.NextDueAtUtc = null;
                entry.LastOutcome = ProviderRefreshOutcomeKind.Unconfigured;
                entry.Message = ProviderStatusMessage.NotConfigured;
                entry.NewAlerts = 0;
                break;
            case ProviderRefreshOutcomeKind.Cancelled:
            case ProviderRefreshOutcomeKind.AlreadyInProgress:
            default:
                break;
        }
    }

    private ProviderRefreshStatus ToStatus(Entry entry, DateTimeOffset now)
    {
        var state = entry.Unconfigured
            ? ProviderRefreshState.Unconfigured
            : entry.InFlight
                ? ProviderRefreshState.Refreshing
                : entry.LastOutcome == ProviderRefreshOutcomeKind.Failed
                    ? ProviderRefreshState.Failed
                    : IsStale(entry, now) ? ProviderRefreshState.Stale : ProviderRefreshState.Idle;

        return new ProviderRefreshStatus(
            entry.Provider.Id,
            entry.Provider.DisplayName,
            state,
            entry.LastSuccessAtUtc,
            entry.Unconfigured ? null : entry.NextDueAtUtc,
            entry.Message,
            entry.NewAlerts);
    }

    private bool IsStale(Entry entry, DateTimeOffset now)
    {
        var threshold = entry.Provider.Interval * StaleIntervalMultiplier;
        var reference = entry.LastSuccessAtUtc ?? startedAtUtc;
        return reference is { } since && now - since > threshold;
    }

    private bool StatesChangedSinceLastPublish()
    {
        lock (gate)
        {
            var now = Now();
            return entries.Any(entry =>
                !lastPublishedStates.TryGetValue(entry.Provider.Id, out var published)
                || published != ToStatus(entry, now).State);
        }
    }

    private void RaiseStatusChanged()
    {
        if (stopped)
        {
            return;
        }

        lock (gate)
        {
            var now = Now();
            foreach (var entry in entries)
            {
                lastPublishedStates[entry.Provider.Id] = ToStatus(entry, now).State;
            }
        }

        try
        {
            StatusChanged?.Invoke();
        }
        catch (Exception)
        {
            // A status observer must never break the refresh loop.
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TickInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await TickAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private DateTimeOffset Now() => timeProvider.GetUtcNow().ToUniversalTime();

    private sealed class Entry(IScheduledProvider provider)
    {
        public IScheduledProvider Provider { get; } = provider;
        public DateTimeOffset? NextDueAtUtc { get; set; }
        public DateTimeOffset? LastSuccessAtUtc { get; set; }
        public int ConsecutiveFailures { get; set; }
        public ProviderRefreshOutcomeKind? LastOutcome { get; set; }
        public ProviderStatusMessage Message { get; set; }
        public int NewAlerts { get; set; }
        public bool InFlight { get; set; }
        public bool Unconfigured { get; set; }
    }
}
