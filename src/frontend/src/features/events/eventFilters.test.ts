import { filterEvents, filterEventsByMinimumSeverity, sortEventsForList, type EventTimeRange, type MinimumSeverity } from './eventFilters';
import { eventSeverities, type NativeEvent } from '../../types/events';

const now = Date.parse('2026-09-17T12:00:00Z');

function event(overrides: Partial<NativeEvent> = {}): NativeEvent {
  return {
    id: 'evt-security',
    title: 'Reported security activity near Baltic border area',
    summary: 'Several observers reported movement near the crossing.',
    category: 'Security',
    status: 'Developing',
    startedAtUtc: '2026-09-17T11:00:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:30:00Z',
    location: {
      latitude: 57.4,
      longitude: 24.8,
      displayName: 'Northern corridor',
      region: 'Baltic demo region',
    },
    createdAtUtc: '2026-09-17T10:00:00Z',
    updatedAtUtc: '2026-09-17T11:30:00Z',
    sourceCount: 3,
    severity: 'Medium',
    severityRule: 'manual',
    severityInputs: 'test',
    provider: 'Manual',
    ...overrides,
  };
}

describe('filterEvents', () => {
  const events = [event()];

  it.each([
    ['', 'empty'],
    ['   ', 'whitespace-only'],
  ])('treats %s search as no search filter', (query) => {
    expect(filterEvents(events, query, 'ALL', now)).toEqual(events);
  });

  it.each([
    ['SECURITY ACTIVITY', 'title'],
    ['observers reported', 'summary'],
    ['security', 'category'],
    ['developing', 'status'],
    ['northern corridor', 'location name'],
    ['baltic demo', 'location region'],
  ])('matches case-insensitive %s search text in the %s', (query) => {
    expect(filterEvents(events, `  ${query}  `, 'ALL', now)).toEqual(events);
  });

  it('matches the human-readable category label', () => {
    const diplomatic = event({ category: 'PoliticalDiplomatic' });

    expect(filterEvents([diplomatic], 'political / diplomatic', 'ALL', now)).toEqual([diplomatic]);

    const naturalDisaster = event({ category: 'NaturalDisaster' });
    expect(filterEvents([naturalDisaster], 'natural disaster', 'ALL', now)).toEqual([naturalDisaster]);
  });

  it('removes events that do not match search', () => {
    expect(filterEvents(events, 'economic', 'ALL', now)).toEqual([]);
  });

  it('does not time-filter ALL', () => {
    const oldEvent = event({ startedAtUtc: '2001-01-01T00:00:00Z' });

    expect(filterEvents([oldEvent], '', 'ALL', now)).toEqual([oldEvent]);
  });

  it.each([
    ['24H', 24 * 60 * 60 * 1000],
    ['7D', 7 * 24 * 60 * 60 * 1000],
    ['30D', 30 * 24 * 60 * 60 * 1000],
    ['90D', 90 * 24 * 60 * 60 * 1000],
  ] satisfies Array<[EventTimeRange, number]>)('uses an inclusive cutoff for %s', (range, duration) => {
    const atCutoff = event({ id: `${range}-cutoff`, startedAtUtc: new Date(now - duration).toISOString() });
    const inside = event({ id: `${range}-inside`, startedAtUtc: new Date(now - duration + 1).toISOString() });
    const outside = event({ id: `${range}-outside`, startedAtUtc: new Date(now - duration - 1).toISOString() });

    expect(filterEvents([atCutoff, inside, outside], '', range, now)).toEqual([atCutoff, inside]);
  });

  it('interprets offset timestamps as instants and excludes invalid bounded timestamps', () => {
    const sameInstant = event({ id: 'offset', startedAtUtc: '2026-09-17T13:00:00+02:00' });
    const invalid = event({ id: 'invalid', startedAtUtc: 'not-a-timestamp' });

    expect(filterEvents([sameInstant, invalid], '', '24H', now)).toEqual([sameInstant]);
  });

  it('matches severity words in search', () => {
    const critical = event({ id: 'critical', severity: 'Critical' });
    const low = event({ id: 'low', severity: 'Low' });

    expect(filterEvents([critical, low], 'CRITICAL', 'ALL', now)).toEqual([critical]);
  });

  it.each([
    ['All', ['low', 'medium', 'high', 'critical', 'catastrophic']],
    ['Medium', ['medium', 'high', 'critical', 'catastrophic']],
    ['High', ['high', 'critical', 'catastrophic']],
    ['Critical', ['critical', 'catastrophic']],
  ] satisfies Array<[MinimumSeverity, string[]]>)('keeps %s and above for the minimum severity filter', (minimum, expectedIds) => {
    const levels = eventSeverities.map((severity) => event({ id: severity.toLowerCase(), severity }));

    expect(filterEventsByMinimumSeverity(levels, minimum).map((item) => item.id)).toEqual(expectedIds);
  });

  it('combines minimum severity with search and time range using AND semantics', () => {
    const recentHigh = event({ id: 'recent-high', severity: 'High' });
    const recentLow = event({ id: 'recent-low', severity: 'Low' });
    const oldHigh = event({ id: 'old-high', severity: 'High', startedAtUtc: '2026-09-01T00:00:00Z' });
    const recentHighOtherTopic = event({ id: 'recent-high-economic', severity: 'High', category: 'Economic', title: 'Market update', summary: 'Prices' });

    const visible = filterEventsByMinimumSeverity(filterEvents([recentHigh, recentLow, oldHigh, recentHighOtherTopic], 'security', '7D', now), 'High');

    expect(visible).toEqual([recentHigh]);
  });

  it('sorts by severity descending, then by last update descending, with invalid timestamps last', () => {
    const lowNewest = event({ id: 'low-newest', severity: 'Low', lastUpdatedAtUtc: '2026-09-17T11:59:00Z' });
    const highOld = event({ id: 'high-old', severity: 'High', lastUpdatedAtUtc: '2026-09-10T00:00:00Z' });
    const highNew = event({ id: 'high-new', severity: 'High', lastUpdatedAtUtc: '2026-09-17T11:00:00Z' });
    const highInvalid = event({ id: 'high-invalid', severity: 'High', lastUpdatedAtUtc: 'not-a-timestamp' });
    const catastrophic = event({ id: 'catastrophic', severity: 'Catastrophic', lastUpdatedAtUtc: '2026-09-01T00:00:00Z' });
    const input = [lowNewest, highInvalid, highOld, catastrophic, highNew];

    expect(sortEventsForList(input).map((item) => item.id)).toEqual(['catastrophic', 'high-new', 'high-old', 'high-invalid', 'low-newest']);
    expect(input.map((item) => item.id)).toEqual(['low-newest', 'high-invalid', 'high-old', 'catastrophic', 'high-new']);
  });

  it('combines search and time-range filters with AND semantics', () => {
    const matchingRecent = event({ id: 'recent-security' });
    const matchingOld = event({ id: 'old-security', startedAtUtc: '2026-09-01T00:00:00Z' });
    const recentNonMatch = event({ id: 'recent-economic', category: 'Economic', title: 'Market update' });

    expect(filterEvents([matchingRecent, matchingOld, recentNonMatch], 'security', '7D', now)).toEqual([matchingRecent]);
  });
});
