import { filterEvents, filterEventsByMinimumSeverity, type EventTimeRange, type MinimumSeverity } from '../features/events/eventFilters';
import type { NativeEvent } from '../types/events';

export type SimulatedEventListRequest = {
  requestId?: number;
  range?: EventTimeRange;
  search?: string;
  minimumSeverity?: MinimumSeverity;
  watchlistId?: string;
  keepEventId?: string;
};

/**
 * Test-only stand-in for the native windowed Event query. It applies the frontend reference filter (the semantics the
 * native query reproduces and the parity fixture checks), Watchlist membership, newest-by-update ordering with id
 * tie-break, the cap, and the keep-Event rule, then answers with the bridge response shape.
 */
export function simulateEventWindow(
  dataset: NativeEvent[],
  request: SimulatedEventListRequest,
  options: { cap: number; members: Map<string, Set<string>>; now?: number },
) {
  const scoped = request.watchlistId
    ? dataset.filter((event) => options.members.get(request.watchlistId!)?.has(event.id) ?? false)
    : dataset;
  const matching = filterEventsByMinimumSeverity(
    filterEvents(scoped, request.search ?? '', request.range ?? 'ALL', options.now ?? Date.now()),
    request.minimumSeverity ?? 'All',
  );
  const ordered = [...matching].sort((left, right) =>
    Date.parse(right.lastUpdatedAtUtc) - Date.parse(left.lastUpdatedAtUtc) || (left.id < right.id ? -1 : left.id > right.id ? 1 : 0));
  const window = ordered.slice(0, options.cap);
  const keep = request.keepEventId ? matching.find((event) => event.id === request.keepEventId) : undefined;
  if (keep && !window.some((event) => event.id === keep.id)) {
    if (window.length >= options.cap) window.pop();
    window.push(keep);
  }

  return {
    type: 'events.list.result',
    payload: { events: window, totalCount: matching.length, capped: matching.length > options.cap, requestId: request.requestId ?? null },
  };
}
