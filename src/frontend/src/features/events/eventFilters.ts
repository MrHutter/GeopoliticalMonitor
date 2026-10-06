import { formatCategory, severityRank, type NativeEvent, type NativeEventSeverity } from '../../types/events';

export const eventTimeRanges = ['24H', '7D', '30D', '90D', 'ALL'] as const;

export type EventTimeRange = (typeof eventTimeRanges)[number];

export const minimumSeverityOptions = ['All', 'Medium', 'High', 'Critical'] as const;

export type MinimumSeverity = (typeof minimumSeverityOptions)[number];

export const minimumSeverityLabels: Record<MinimumSeverity, string> = {
  All: 'All severities',
  Medium: 'Medium and above',
  High: 'High and above',
  Critical: 'Critical and above',
};

/** Startup Events filters, applied on every launch and never persisted. Alert activation still resets to ALL and All. */
export const defaultTimeRange: EventTimeRange = '30D';
export const defaultMinimumSeverity: MinimumSeverity = 'High';

const rangePhrases: Record<Exclude<EventTimeRange, 'ALL'>, string> = {
  '24H': 'the last 24 hours',
  '7D': 'the last 7 days',
  '30D': 'the last 30 days',
  '90D': 'the last 90 days',
};

/**
 * Empty-list message for criteria narrower than ALL / All severities, naming the active range and severity and how to
 * widen them. With neither narrowed (search only) the generic filter message stays.
 */
export function filterEmptyMessage(range: EventTimeRange, minimumSeverity: MinimumSeverity, search: string, scope: 'all' | 'watchlist' = 'all') {
  const severityPart = minimumSeverity === 'All' ? '' : ` at ${minimumSeverityLabels[minimumSeverity]}`;
  const rangePart = range === 'ALL' ? '' : ` in ${rangePhrases[range]}`;
  if (!severityPart && !rangePart) {
    return scope === 'watchlist' ? 'No watchlist events match the active filters.' : 'No events match the active filters.';
  }

  const subject = scope === 'watchlist' ? 'No watchlist Events' : 'No Events';
  const widen = severityPart && rangePart ? 'range or severity' : rangePart ? 'range' : 'severity';
  return search.trim().length > 0
    ? `${subject}${severityPart}${rangePart} match the search. Clear the search or widen the ${widen} to see more.`
    : `${subject}${severityPart}${rangePart}. Widen the ${widen} to see more.`;
}

const rangeDurationMilliseconds: Record<EventTimeRange, number> = {
  '24H': 24 * 60 * 60 * 1000,
  '7D': 7 * 24 * 60 * 60 * 1000,
  '30D': 30 * 24 * 60 * 60 * 1000,
  '90D': 90 * 24 * 60 * 60 * 1000,
  ALL: 0,
};

export function filterEvents(
  events: NativeEvent[],
  searchQuery: string,
  timeRange: EventTimeRange,
  nowMilliseconds = Date.now(),
) {
  const normalizedQuery = searchQuery.trim().toLowerCase();
  const cutoff = timeRange === 'ALL' ? null : nowMilliseconds - rangeDurationMilliseconds[timeRange];

  return events.filter((event) => {
    const matchesSearch = normalizedQuery.length === 0 || [
      event.title,
      event.summary,
      event.category,
      formatCategory(event.category),
      event.status,
      event.severity,
      event.location.displayName,
      event.location.region,
    ].some((value) => value?.toLowerCase().includes(normalizedQuery));

    if (!matchesSearch || cutoff === null) {
      return matchesSearch;
    }

    const eventTimestamp = Date.parse(event.startedAtUtc);
    return Number.isFinite(eventTimestamp) && eventTimestamp >= cutoff;
  });
}

export function filterEventsByMinimumSeverity(events: NativeEvent[], minimumSeverity: MinimumSeverity) {
  if (minimumSeverity === 'All') {
    return events;
  }

  const minimumRank = severityRank(minimumSeverity satisfies NativeEventSeverity);
  return events.filter((event) => severityRank(event.severity) >= minimumRank);
}

/** Newest first: last update descending, then id ascending (ordinal), the same order the native window query uses. */
export function sortEventsForList(events: NativeEvent[]) {
  return [...events].sort((left, right) =>
    compareNewestFirst(left.lastUpdatedAtUtc, right.lastUpdatedAtUtc)
    || compareOrdinal(left.id, right.id));
}

function compareOrdinal(left: string, right: string) {
  if (left === right) return 0;
  return left < right ? -1 : 1;
}

function compareNewestFirst(left: string, right: string) {
  const leftTimestamp = timestampOrMinimum(left);
  const rightTimestamp = timestampOrMinimum(right);
  if (leftTimestamp === rightTimestamp) return 0;
  return rightTimestamp > leftTimestamp ? 1 : -1;
}

function timestampOrMinimum(value: string) {
  const timestamp = Date.parse(value);
  return Number.isFinite(timestamp) ? timestamp : Number.NEGATIVE_INFINITY;
}
