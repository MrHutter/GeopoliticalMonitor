namespace GeopoliticalMonitor.Application.Tests;

/// <summary>
/// Deterministic TimeProvider for scheduler tests. Time moves only through <see cref="Advance"/>. Timers created by
/// PeriodicTimer fire during Advance only when <see cref="FireTimers"/> is true, so tests can drive TickAsync directly
/// without a concurrent loop.
/// </summary>
internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly object sync = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now = start;

    public bool FireTimers { get; set; }

    public int TimerCount
    {
        get
        {
            lock (sync)
            {
                return timers.Count;
            }
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (sync)
        {
            return now;
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (sync)
        {
            timers.Add(timer);
        }

        timer.Change(dueTime, period);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        DateTimeOffset target;
        lock (sync)
        {
            target = now + by;
        }

        while (true)
        {
            ManualTimer? next;
            lock (sync)
            {
                next = FireTimers
                    ? timers.Where(item => item.DueAt is { } due && due <= target).OrderBy(item => item.DueAt).FirstOrDefault()
                    : null;
                if (next is null)
                {
                    now = target;
                    return;
                }

                now = next.DueAt!.Value;
                next.MoveToNextPeriod();
            }

            next.Fire();
        }
    }

    public void SetNow(DateTimeOffset value)
    {
        lock (sync)
        {
            now = value;
        }
    }

    private void Remove(ManualTimer timer)
    {
        lock (sync)
        {
            timers.Remove(timer);
        }
    }

    private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        private TimeSpan period = Timeout.InfiniteTimeSpan;

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            this.period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
            return true;
        }

        public void MoveToNextPeriod()
        {
            DueAt = period == Timeout.InfiniteTimeSpan || period <= TimeSpan.Zero ? null : DueAt + period;
        }

        public void Fire() => callback(state);

        public void Dispose() => owner.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
