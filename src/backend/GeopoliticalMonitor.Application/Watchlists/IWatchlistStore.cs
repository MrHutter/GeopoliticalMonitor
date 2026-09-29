using GeopoliticalMonitor.Domain.Events;
using GeopoliticalMonitor.Domain.Watchlists;

namespace GeopoliticalMonitor.Application.Watchlists;

public interface IWatchlistStore
{
    Task<IReadOnlyList<WatchlistSummaryDto>> GetWatchlistsAsync(CancellationToken cancellationToken = default);
    Task<CreateWatchlistResult> CreateWatchlistAsync(Watchlist watchlist, int maximumWatchlists, CancellationToken cancellationToken = default);
    Task<RenameWatchlistResult> RenameWatchlistAsync(Watchlist watchlist, CancellationToken cancellationToken = default);
    Task<DeleteWatchlistResult> DeleteWatchlistAsync(WatchlistId watchlistId, CancellationToken cancellationToken = default);
    Task<WatchlistEventIdsResult> GetEventIdsAsync(WatchlistId watchlistId, CancellationToken cancellationToken = default);
    Task<EventWatchlistIdsResult> GetWatchlistIdsAsync(EventId eventId, CancellationToken cancellationToken = default);
    Task<SetWatchlistMembershipResult> SetMembershipAsync(
        WatchlistId watchlistId,
        EventId eventId,
        bool isMember,
        int maximumEventsPerWatchlist,
        CancellationToken cancellationToken = default);
}
