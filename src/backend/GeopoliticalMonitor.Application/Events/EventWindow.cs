using System.Globalization;
using GeopoliticalMonitor.Domain.Events;

namespace GeopoliticalMonitor.Application.Events;

public enum EventTimeRange
{
    All,
    Last24Hours,
    Last7Days,
    Last30Days,
    Last90Days,
}

public enum EventMinimumSeverity
{
    All,
    Medium,
    High,
    Critical,
}

/// <summary>Typed criteria for the windowed Event list. Defaults reproduce the old unfiltered list request.</summary>
public sealed record EventWindowRequest(
    EventTimeRange Range = EventTimeRange.All,
    string Search = "",
    EventMinimumSeverity MinimumSeverity = EventMinimumSeverity.All,
    string? WatchlistId = null,
    string? KeepEventId = null);

/// <summary>Store-level criteria after the service resolved the clock and normalized the search text.</summary>
public sealed record EventWindowCriteria(
    DateTimeOffset? StartedAtOrAfterUtc,
    string NormalizedSearch,
    int MinimumSeverityRank,
    string? WatchlistId,
    string? KeepEventId);

public sealed record EventWindowPage(
    IReadOnlyList<Event> Events,
    IReadOnlyDictionary<EventId, int> SourceCounts,
    int TotalCount);

public sealed record EventWindowResult(IReadOnlyList<EventDto> Events, int TotalCount, bool Capped);

public interface IEventWindowStore
{
    Task<EventWindowPage> QueryEventWindowAsync(EventWindowCriteria criteria, int cap, CancellationToken cancellationToken = default);
}

/// <summary>
/// The single definition of Event list filter semantics on the native side. It mirrors the frontend rules in
/// <c>eventFilters.ts</c>: inclusive time cutoffs on StartedAtUtc, a trimmed case-insensitive substring search across
/// title, summary, category (raw and display label), status, severity, location display name and region, a minimum
/// severity rank, and AND combination. Parity is checked by a shared fixture run through both implementations.
/// </summary>
public static class EventWindowRules
{
    public const int DefaultCap = 2000;
    public const int MaximumSearchLength = 200;

    private static readonly string[] SeverityOrder = ["Low", "Medium", "High", "Critical", "Catastrophic"];

    public static bool TryParseRange(string? value, out EventTimeRange range)
    {
        range = value switch
        {
            "ALL" => EventTimeRange.All,
            "24H" => EventTimeRange.Last24Hours,
            "7D" => EventTimeRange.Last7Days,
            "30D" => EventTimeRange.Last30Days,
            "90D" => EventTimeRange.Last90Days,
            _ => (EventTimeRange)(-1),
        };
        return Enum.IsDefined(range);
    }

    public static bool TryParseMinimumSeverity(string? value, out EventMinimumSeverity minimumSeverity)
    {
        minimumSeverity = value switch
        {
            "All" => EventMinimumSeverity.All,
            "Medium" => EventMinimumSeverity.Medium,
            "High" => EventMinimumSeverity.High,
            "Critical" => EventMinimumSeverity.Critical,
            _ => (EventMinimumSeverity)(-1),
        };
        return Enum.IsDefined(minimumSeverity);
    }

    public static TimeSpan? Duration(EventTimeRange range) => range switch
    {
        EventTimeRange.Last24Hours => TimeSpan.FromHours(24),
        EventTimeRange.Last7Days => TimeSpan.FromDays(7),
        EventTimeRange.Last30Days => TimeSpan.FromDays(30),
        EventTimeRange.Last90Days => TimeSpan.FromDays(90),
        _ => null,
    };

    public static int MinimumRank(EventMinimumSeverity minimumSeverity) => minimumSeverity switch
    {
        EventMinimumSeverity.Medium => SeverityRank(nameof(EventSeverity.Medium)),
        EventMinimumSeverity.High => SeverityRank(nameof(EventSeverity.High)),
        EventMinimumSeverity.Critical => SeverityRank(nameof(EventSeverity.Critical)),
        _ => 0,
    };

    public static int SeverityRank(string? severity) => Array.IndexOf(SeverityOrder, severity);

    /// <summary>Same as the frontend: trim (including a byte-order mark) and lower-case.</summary>
    public static string NormalizeSearch(string? search)
        => (search ?? string.Empty).Trim().Trim('﻿').Trim().ToLowerInvariant();

    /// <summary>Display label used by the frontend's <c>formatCategory</c>; search matches both forms.</summary>
    public static string CategoryLabel(string category) => category switch
    {
        nameof(EventCategory.MilitaryConflict) => "Military conflict",
        nameof(EventCategory.PoliticalDiplomatic) => "Political / diplomatic",
        nameof(EventCategory.CivilUnrest) => "Civil unrest",
        nameof(EventCategory.NaturalDisaster) => "Natural Disaster",
        _ => category,
    };

    public static bool MatchesSearch(
        string normalizedSearch,
        string? title,
        string? summary,
        string? category,
        string? status,
        string? severity,
        string? locationDisplayName,
        string? region)
    {
        if (normalizedSearch.Length == 0)
        {
            return true;
        }

        return Contains(title, normalizedSearch)
            || Contains(summary, normalizedSearch)
            || Contains(category, normalizedSearch)
            || (category is not null && Contains(CategoryLabel(category), normalizedSearch))
            || Contains(status, normalizedSearch)
            || Contains(severity, normalizedSearch)
            || Contains(locationDisplayName, normalizedSearch)
            || Contains(region, normalizedSearch);
    }

    public static bool Matches(
        EventWindowCriteria criteria,
        DateTimeOffset? startedAtUtc,
        string? title,
        string? summary,
        string? category,
        string? status,
        string? severity,
        string? locationDisplayName,
        string? region)
    {
        if (criteria.StartedAtOrAfterUtc is { } cutoff && (startedAtUtc is null || startedAtUtc.Value < cutoff))
        {
            return false;
        }

        if (criteria.MinimumSeverityRank > 0 && SeverityRank(severity) < criteria.MinimumSeverityRank)
        {
            return false;
        }

        return MatchesSearch(criteria.NormalizedSearch, title, summary, category, status, severity, locationDisplayName, region);
    }

    public static bool TryParseStoredTimestamp(string? value, out DateTimeOffset timestamp)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp);

    private static bool Contains(string? value, string normalizedSearch)
        => value is not null && value.ToLowerInvariant().Contains(normalizedSearch, StringComparison.Ordinal);
}

public sealed class EventWindowService
{
    private readonly IEventWindowStore store;
    private readonly TimeProvider timeProvider;

    public EventWindowService(IEventWindowStore store, TimeProvider? timeProvider = null, int cap = EventWindowRules.DefaultCap)
    {
        if (cap < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(cap), "The Event window cap must be positive.");
        }

        this.store = store;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        Cap = cap;
    }

    public int Cap { get; }

    public async Task<EventWindowResult> QueryAsync(EventWindowRequest request, CancellationToken cancellationToken = default)
    {
        var search = request.Search ?? string.Empty;
        if (search.Length > EventWindowRules.MaximumSearchLength)
        {
            throw new ArgumentException("Event search text is too long.", nameof(request));
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var criteria = new EventWindowCriteria(
            EventWindowRules.Duration(request.Range) is { } duration ? now - duration : null,
            EventWindowRules.NormalizeSearch(search),
            EventWindowRules.MinimumRank(request.MinimumSeverity),
            request.WatchlistId,
            request.KeepEventId);

        var page = await store.QueryEventWindowAsync(criteria, Cap, cancellationToken).ConfigureAwait(false);
        var events = page.Events
            .Select(item => EventDtoMapper.ToDto(item, page.SourceCounts.GetValueOrDefault(item.Id)))
            .ToArray();
        return new EventWindowResult(events, page.TotalCount, page.TotalCount > Cap);
    }
}
