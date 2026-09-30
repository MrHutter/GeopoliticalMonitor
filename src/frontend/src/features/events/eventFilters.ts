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

export function sortEventsForList(events: NativeEvent[]) {
  return [...events].sort((left, right) =>
    severityRank(right.severity) - severityRank(left.severity)
    || compareNewestFirst(left.lastUpdatedAtUtc, right.lastUpdatedAtUtc));
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
