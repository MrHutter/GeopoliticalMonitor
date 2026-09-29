import { formatCategory, type NativeEvent } from '../../types/events';

export const eventTimeRanges = ['24H', '7D', '30D', '90D', 'ALL'] as const;

export type EventTimeRange = (typeof eventTimeRanges)[number];

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
