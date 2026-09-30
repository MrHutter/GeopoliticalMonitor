import type { RefObject } from 'react';
import type { WatchlistSummary } from '../../nativeBridge';
import { formatCategory, formatEventTime, formatProviderLabel, NativeEvent, NativeEventStatus } from '../../types/events';
import { getCategoryColor } from '../map/mapEvents';
import { minimumSeverityLabels, minimumSeverityOptions, sortEventsForList, type MinimumSeverity } from './eventFilters';

export type GdacsRefreshFeedback = {
  kind: 'idle' | 'pending' | 'success' | 'error';
  message?: string;
};

type EventPanelProps = {
  events: NativeEvent[];
  selectedEventId: string | null;
  selectedRange: string;
  loadError: string | null;
  watchlists: WatchlistSummary[];
  activeWatchlistId: string | null;
  minimumSeverity: MinimumSeverity;
  emptyMessage: string;
  manageButtonRef: RefObject<HTMLButtonElement | null>;
  gdacsRefresh: GdacsRefreshFeedback;
  onRefreshGdacs: () => void;
  onSelectEvent: (eventId: string) => void;
  onSelectWatchlist: (watchlistId: string | null) => void;
  onMinimumSeverityChange: (minimumSeverity: MinimumSeverity) => void;
  onManageWatchlists: () => void;
};

const statusDotClass: Record<NativeEventStatus, string> = { Active: 'active', Developing: 'developing', Unverified: 'unverified', Resolved: 'resolved' };

export function EventPanel({ events, selectedEventId, selectedRange, loadError, watchlists, activeWatchlistId, minimumSeverity, emptyMessage, manageButtonRef, gdacsRefresh, onRefreshGdacs, onSelectEvent, onSelectWatchlist, onMinimumSeverityChange, onManageWatchlists }: EventPanelProps) {
  const orderedEvents = sortEventsForList(events);

  return (
    <aside className="event-panel panel" aria-labelledby="event-panel-title">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Events</p>
          <h2 id="event-panel-title" className="sr-only">Event scope</h2>
          <label className="event-scope-control">
            <span className="sr-only">Event scope</span>
            <select value={activeWatchlistId ?? ''} onChange={(event) => onSelectWatchlist(event.target.value || null)}>
              <option value="">All Events</option>
              {watchlists.map((watchlist) => <option key={watchlist.id} value={watchlist.id}>{watchlist.name}</option>)}
            </select>
          </label>
          <label className="event-scope-control event-severity-control">
            <span className="sr-only">Minimum severity</span>
            <select value={minimumSeverity} onChange={(event) => onMinimumSeverityChange(event.target.value as MinimumSeverity)}>
              {minimumSeverityOptions.map((option) => <option key={option} value={option}>{minimumSeverityLabels[option]}</option>)}
            </select>
          </label>
        </div>
        <div className="event-panel-actions">
          <button ref={manageButtonRef} type="button" className="watchlist-manage-button" onClick={onManageWatchlists}>Manage</button>
          <span className="count-pill">{selectedRange}</span>
          <button type="button" className="gdacs-refresh-button" onClick={onRefreshGdacs} disabled={gdacsRefresh.kind === 'pending'}>
            {gdacsRefresh.kind === 'pending' ? 'Refreshing...' : 'Refresh GDACS'}
          </button>
        </div>
      </div>

      <div className="summary-strip" aria-label="event summary">
        <span>{events.length} events</span>
        <span>{events.reduce((total, event) => total + event.sourceCount, 0)} sources</span>
      </div>

      {loadError ? <p className="demo-note" role="alert">{loadError}</p> : null}
      {gdacsRefresh.message ? <p className={`gdacs-refresh-status is-${gdacsRefresh.kind}`} role={gdacsRefresh.kind === 'error' ? 'alert' : 'status'}>{gdacsRefresh.message}</p> : null}

      <div className="event-list" role="list" aria-label="event list">
        {!loadError && orderedEvents.length === 0 ? (
          <p className="event-empty-state" role="status">{emptyMessage}</p>
        ) : null}
        {orderedEvents.map((event) => (
          <div key={event.id} role="listitem">
            <button type="button" className={`event-row ${event.id === selectedEventId ? 'is-selected' : ''}`} style={{ borderLeftColor: getCategoryColor(event.category) }} onClick={() => onSelectEvent(event.id)} aria-pressed={event.id === selectedEventId}>
              <span className={`status-dot status-${statusDotClass[event.status]}`} aria-hidden="true" />
              <span className="event-row-main">
                <span className="event-row-meta">
                  <span>{formatEventTime(event.lastUpdatedAtUtc)}</span>
                  <span>{event.location.region ?? event.location.displayName}</span>
                </span>
                <strong>{event.title}</strong>
                <span className="event-row-badges">
                  <span className={`event-badge severity-badge severity-${event.severity.toLowerCase()}`}>{event.severity}</span>
                  {event.status === 'Unverified' ? <span className="event-badge unverified-badge">Unverified</span> : null}
                  {formatProviderLabel(event.provider) ? <span className="event-provider-label">{formatProviderLabel(event.provider)}</span> : null}
                </span>
                <span className="event-row-footer">
                  <span>{event.sourceCount} sources</span>
                  <span>{formatCategory(event.category)}</span>
                </span>
              </span>
            </button>
          </div>
        ))}
      </div>
    </aside>
  );
}
