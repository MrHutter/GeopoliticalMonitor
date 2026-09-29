import type { WatchlistSummary } from '../../nativeBridge';

type EventWatchlistControlProps = {
  watchlists: WatchlistSummary[];
  memberships: Set<string> | null;
  pendingWatchlistId: string | null;
  status: string | null;
  onToggle: (watchlistId: string, isMember: boolean) => void;
  onManage: () => void;
};

export function EventWatchlistControl({ watchlists, memberships, pendingWatchlistId, status, onToggle, onManage }: EventWatchlistControlProps) {
  return (
    <section className="event-watchlists" aria-labelledby="event-watchlists-title">
      <div className="event-watchlists-heading">
        <h3 id="event-watchlists-title">Watchlists</h3>
        <button type="button" onClick={onManage}>Manage</button>
      </div>
      {watchlists.length === 0 ? <p>No watchlists. Create one to group monitored events.</p> : null}
      {watchlists.length > 0 && memberships === null ? <p>Loading memberships...</p> : null}
      {status ? <p role="status">{status}</p> : null}
      {memberships !== null ? (
        <div className="event-watchlist-options">
          {watchlists.map((watchlist) => (
            <label key={watchlist.id}>
              <input
                type="checkbox"
                checked={memberships.has(watchlist.id)}
                disabled={pendingWatchlistId === watchlist.id}
                onChange={(event) => onToggle(watchlist.id, event.target.checked)}
              />
              <span>{watchlist.name}</span>
              <small>{watchlist.eventCount}</small>
            </label>
          ))}
        </div>
      ) : null}
    </section>
  );
}
