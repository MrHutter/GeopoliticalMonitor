import { useEffect, useLayoutEffect, useMemo, useRef, useState, type RefObject, type UIEvent } from 'react';
import type { ProviderRefreshStatus, WatchlistSummary } from '../../nativeBridge';
import { formatCategory, formatEventTime, formatProviderLabel, NativeEvent, NativeEventStatus } from '../../types/events';
import { getCategoryColor } from '../map/mapEvents';
import { minimumSeverityLabels, minimumSeverityOptions, sortEventsForList, type MinimumSeverity } from './eventFilters';
import { ProviderStatusStrip } from './ProviderStatusStrip';

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
  eventWindow: { totalCount: number; capped: boolean; settled: boolean };
  emptyMessage: string;
  manageButtonRef: RefObject<HTMLButtonElement | null>;
  gdacsRefresh: GdacsRefreshFeedback;
  providerStatuses: ProviderRefreshStatus[];
  onRefreshGdacs: () => void;
  ucdpRefresh?: GdacsRefreshFeedback;
  onRefreshUcdp?: () => void;
  onSelectEvent: (eventId: string) => void;
  onSelectWatchlist: (watchlistId: string | null) => void;
  onMinimumSeverityChange: (minimumSeverity: MinimumSeverity) => void;
  onManageWatchlists: () => void;
  /** Changes whenever the list criteria change (scope, range, search, severity); resets the rendered window. */
  criteriaKey?: string;
};

/** Rows rendered at first and added per step; the list grows incrementally instead of rendering every Event. */
export const eventListPageSize = 100;
/** Distance from the end of the list, in pixels, at which the next page is rendered. */
export const eventListLoadMoreThreshold = 400;

function roundUpToPage(count: number) {
  return Math.ceil(count / eventListPageSize) * eventListPageSize;
}

const countFormatter = new Intl.NumberFormat('en-US');

/** Short capped-results text that fits one line in the narrowest Events panel. */
export function formatCappedNoticeShort(shown: number, total: number) {
  return `Newest ${countFormatter.format(shown)} of ${countFormatter.format(total)} shown.`;
}

export const cappedNoticeAdvice = 'Narrow range or search for others.';

/** Complete capped-results sentence, used for the title and accessible name. */
export function formatCappedNotice(shown: number, total: number) {
  return `${formatCappedNoticeShort(shown, total)} ${cappedNoticeAdvice}`;
}

const statusDotClass: Record<NativeEventStatus, string> = { Active: 'active', Developing: 'developing', Unverified: 'unverified', Resolved: 'resolved' };

export function EventPanel({ events, selectedEventId, selectedRange, loadError, watchlists, activeWatchlistId, minimumSeverity, eventWindow, emptyMessage, manageButtonRef, gdacsRefresh, providerStatuses, onRefreshGdacs, ucdpRefresh = { kind: 'idle' }, onRefreshUcdp, onSelectEvent, onSelectWatchlist, onMinimumSeverityChange, onManageWatchlists, criteriaKey = '' }: EventPanelProps) {
  const orderedEvents = useMemo(() => sortEventsForList(events), [events]);
  const activeWatchlist = activeWatchlistId ? watchlists.find((watchlist) => watchlist.id === activeWatchlistId) : undefined;
  const listRef = useRef<HTMLDivElement>(null);
  const [renderedCount, setRenderedCount] = useState(eventListPageSize);
  const appliedCriteriaKeyRef = useRef(criteriaKey);
  const revealedSelectionRef = useRef<string | null>(null);
  const selectedIndex = selectedEventId ? orderedEvents.findIndex((event) => event.id === selectedEventId) : -1;
  // The selected Event's row is always rendered, even when it lies beyond the incrementally rendered window.
  const visibleCount = Math.max(renderedCount, selectedIndex >= 0 ? roundUpToPage(selectedIndex + 1) : 0);
  const renderedEvents = orderedEvents.slice(0, visibleCount);

  // New criteria start again at the top with the first page; reloads with unchanged criteria keep both.
  useLayoutEffect(() => {
    if (appliedCriteriaKeyRef.current === criteriaKey) return;
    appliedCriteriaKeyRef.current = criteriaKey;
    setRenderedCount(eventListPageSize);
    if (listRef.current) listRef.current.scrollTop = 0;
  }, [criteriaKey]);

  // Keep an extended window once the selection required it, so later selections do not shrink the list.
  useEffect(() => {
    if (visibleCount > renderedCount) setRenderedCount(visibleCount);
  }, [renderedCount, visibleCount]);

  // Reveal a newly selected Event once (map click, Alert or kept selection); timer reloads do not scroll again.
  useEffect(() => {
    if (!selectedEventId) {
      revealedSelectionRef.current = null;
      return;
    }

    if (revealedSelectionRef.current === selectedEventId || selectedIndex < 0) return;
    const row = Array.from(listRef.current?.querySelectorAll<HTMLElement>('[data-event-id]') ?? [])
      .find((element) => element.dataset.eventId === selectedEventId);
    if (!row) return;
    revealedSelectionRef.current = selectedEventId;
    row.scrollIntoView?.({ block: 'nearest' });
  }, [selectedEventId, selectedIndex, renderedEvents.length]);

  const handleListScroll = (scrollEvent: UIEvent<HTMLDivElement>) => {
    const list = scrollEvent.currentTarget;
    if (visibleCount >= orderedEvents.length) return;
    if (list.scrollTop + list.clientHeight >= list.scrollHeight - eventListLoadMoreThreshold) {
      setRenderedCount(Math.min(roundUpToPage(visibleCount + 1), roundUpToPage(orderedEvents.length)));
    }
  };

  return (
    <aside className="event-panel panel" aria-labelledby="event-panel-title">
      <div className="panel-header event-panel-header">
        <div className="event-panel-title-row">
          <p className="eyebrow">Events</p>
          <h2 id="event-panel-title" className="sr-only">Event scope</h2>
          <div className="summary-strip" aria-label="event summary">
            <span>{events.length} events</span>
            {/* Sources are visually hidden in a narrow panel (never cut off) and stay available to assistive technology. */}
            <span className="summary-sources">{events.reduce((total, event) => total + event.sourceCount, 0)} sources</span>
          </div>
          <div className="event-panel-actions">
            <button ref={manageButtonRef} type="button" className="watchlist-manage-button" onClick={onManageWatchlists}>Manage</button>
            <span className="count-pill">{selectedRange}</span>
          </div>
        </div>
        <div className="event-filter-row">
          <label className="event-scope-control">
            <span className="sr-only">Event scope</span>
            <select value={activeWatchlistId ?? ''} title={activeWatchlist?.name ?? 'All Events'} onChange={(event) => onSelectWatchlist(event.target.value || null)}>
              <option value="">All Events</option>
              {watchlists.map((watchlist) => <option key={watchlist.id} value={watchlist.id}>{watchlist.name}</option>)}
            </select>
          </label>
          <label className="event-scope-control event-severity-control">
            <span className="sr-only">Minimum severity</span>
            <select value={minimumSeverity} title={minimumSeverityLabels[minimumSeverity]} onChange={(event) => onMinimumSeverityChange(event.target.value as MinimumSeverity)}>
              {minimumSeverityOptions.map((option) => <option key={option} value={option}>{minimumSeverityLabels[option]}</option>)}
            </select>
          </label>
        </div>
      </div>

      <ProviderStatusStrip
        statuses={providerStatuses}
        gdacsRefreshPending={gdacsRefresh.kind === 'pending'}
        onRefreshGdacs={onRefreshGdacs}
        ucdpRefreshPending={ucdpRefresh.kind === 'pending'}
        onRefreshUcdp={onRefreshUcdp}
      />

      {eventWindow.capped ? (
        <p
          className="event-window-notice"
          role="status"
          title={formatCappedNotice(events.length, eventWindow.totalCount)}
          aria-label={formatCappedNotice(events.length, eventWindow.totalCount)}
        >
          <span>{formatCappedNoticeShort(events.length, eventWindow.totalCount)}</span>
          <span className="event-window-notice-advice"> {cappedNoticeAdvice}</span>
        </p>
      ) : null}

      {loadError ? <p className="demo-note" role="alert">{loadError}</p> : null}
      {gdacsRefresh.message ? <p className={`gdacs-refresh-status is-${gdacsRefresh.kind}`} role={gdacsRefresh.kind === 'error' ? 'alert' : 'status'}>{gdacsRefresh.message}</p> : null}
      {ucdpRefresh.message ? <p className={`gdacs-refresh-status is-${ucdpRefresh.kind}`} role={ucdpRefresh.kind === 'error' ? 'alert' : 'status'}>{ucdpRefresh.message}</p> : null}

      <div ref={listRef} className="event-list" role="list" aria-label="event list" onScroll={handleListScroll}>
        {!loadError && eventWindow.settled && orderedEvents.length === 0 ? (
          <p className="event-empty-state" role="status">{emptyMessage}</p>
        ) : null}
        {renderedEvents.map((event) => (
          <div key={event.id} role="listitem" data-event-id={event.id}>
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
