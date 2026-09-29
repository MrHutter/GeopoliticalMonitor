using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Watchlists;

public sealed class WatchlistService(IWatchlistStore store)
{
    public const int MaximumWatchlists = 100;
    public const int MaximumEventsPerWatchlist = 2000;

    public async Task<IReadOnlyList<WatchlistSummaryDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default)
    {
        var watchlists = await store.GetWatchlistsAsync(cancellationToken).ConfigureAwait(false);
        return watchlists
            .OrderBy(item => Watchlist.CreateComparisonKey(item.Name), StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public Task<CreateWatchlistResult> CreateWatchlistAsync(string name, CancellationToken cancellationToken = default)
    {
        var watchlist = Watchlist.Create(WatchlistId.CreateNew(), name);
        return store.CreateWatchlistAsync(watchlist, MaximumWatchlists, cancellationToken);
    }

    public Task<RenameWatchlistResult> RenameWatchlistAsync(string watchlistId, string name, CancellationToken cancellationToken = default)
    {
        if (!WatchlistId.TryParse(watchlistId, out var parsedId))
        {
            return Task.FromResult(new RenameWatchlistResult(RenameWatchlistDisposition.NotFound));
        }

        var watchlist = Watchlist.Create(parsedId, name);
        return store.RenameWatchlistAsync(watchlist, cancellationToken);
    }

    public Task<DeleteWatchlistResult> DeleteWatchlistAsync(string watchlistId, CancellationToken cancellationToken = default)
    {
        if (!WatchlistId.TryParse(watchlistId, out var parsedId))
        {
            return Task.FromResult(new DeleteWatchlistResult(DeleteWatchlistDisposition.NotFound, watchlistId));
        }

        return store.DeleteWatchlistAsync(parsedId, cancellationToken);
    }

    public Task<WatchlistEventIdsResult> GetEventIdsAsync(string watchlistId, CancellationToken cancellationToken = default)
    {
        if (!WatchlistId.TryParse(watchlistId, out var parsedId))
        {
            return Task.FromResult(new WatchlistEventIdsResult(WatchlistLookupDisposition.WatchlistNotFound, watchlistId, []));
        }

        return store.GetEventIdsAsync(parsedId, cancellationToken);
    }

    public Task<EventWatchlistIdsResult> GetWatchlistIdsForEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (!EventCatalogService.TryParseEventId(eventId, out var parsedId))
        {
            return Task.FromResult(new EventWatchlistIdsResult(WatchlistLookupDisposition.EventNotFound, eventId, []));
        }

        return store.GetWatchlistIdsAsync(parsedId, cancellationToken);
    }

    public Task<SetWatchlistMembershipResult> SetMembershipAsync(
        string watchlistId,
        string eventId,
        bool isMember,
        CancellationToken cancellationToken = default)
    {
        if (!WatchlistId.TryParse(watchlistId, out var parsedWatchlistId))
        {
            return Task.FromResult(new SetWatchlistMembershipResult(
                SetWatchlistMembershipDisposition.WatchlistNotFound,
                watchlistId,
                eventId,
                false,
                0));
        }

        if (!EventCatalogService.TryParseEventId(eventId, out var parsedEventId))
        {
            return Task.FromResult(new SetWatchlistMembershipResult(
                SetWatchlistMembershipDisposition.EventNotFound,
                watchlistId,
                eventId,
                false,
                0));
        }

        return store.SetMembershipAsync(
            parsedWatchlistId,
            parsedEventId,
            isMember,
            MaximumEventsPerWatchlist,
            cancellationToken);
    }
}
