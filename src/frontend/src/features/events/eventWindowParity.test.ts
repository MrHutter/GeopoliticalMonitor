import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { NativeEvent, NativeEventCategory, NativeEventSeverity, NativeEventStatus } from '../../types/events';
import { filterEvents, filterEventsByMinimumSeverity, type EventTimeRange, type MinimumSeverity } from './eventFilters';

type ParityEvent = {
  id: string;
  title: string;
  summary: string;
  category: NativeEventCategory;
  status: NativeEventStatus;
  severity: NativeEventSeverity;
  startedAtUtc: string;
  lastUpdatedAtUtc: string;
  locationDisplayName: string;
  region: string | null;
};

type ParityCase = { name: string; range: EventTimeRange; search: string; minimumSeverity: MinimumSeverity; expectedIds: string[] };

// The same fixture is executed by the native windowed query in EventWindowQueryTests, so both implementations of the
// Event list filter semantics must agree case by case.
const fixture = JSON.parse(readFileSync(join(process.cwd(), '..', '..', 'tests', 'fixtures', 'event-window-parity.json'), 'utf8')) as {
  nowUtc: string;
  events: ParityEvent[];
  cases: ParityCase[];
};

const now = Date.parse(fixture.nowUtc);
const events: NativeEvent[] = fixture.events.map((item) => ({
  id: item.id,
  title: item.title,
  summary: item.summary,
  category: item.category,
  status: item.status,
  startedAtUtc: item.startedAtUtc,
  lastUpdatedAtUtc: item.lastUpdatedAtUtc,
  location: { latitude: 1, longitude: 2, displayName: item.locationDisplayName, region: item.region },
  createdAtUtc: item.startedAtUtc,
  updatedAtUtc: item.lastUpdatedAtUtc,
  sourceCount: 0,
  severity: item.severity,
  severityRule: 'manual',
  severityInputs: 'parity',
  provider: 'Manual',
}));

describe('Event window parity fixture', () => {
  it('has cases to check', () => {
    expect(fixture.cases.length).toBeGreaterThan(10);
  });

  it.each(fixture.cases.map((testCase) => [testCase.name, testCase] as const))('frontend reference filter matches: %s', (_name, testCase) => {
    const ids = filterEventsByMinimumSeverity(filterEvents(events, testCase.search, testCase.range, now), testCase.minimumSeverity).map((item) => item.id);

    expect([...ids].sort()).toEqual([...testCase.expectedIds].sort());
  });
});
