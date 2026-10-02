import { createRef, type ComponentProps } from 'react';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fireEvent, render, screen } from '@testing-library/react';
import type { NativeEvent } from '../../types/events';
import { EventPanel, eventListPageSize } from './EventPanel';

// jsdom computes no layout, so these tests cover structure and behavior; the bounded height itself is asserted
// through the stylesheet rules that create it.

const baseTime = Date.parse('2026-09-30T12:00:00Z');

function makeEvents(count: number): NativeEvent[] {
  return Array.from({ length: count }, (_, index) => ({
    id: `evt-${String(index).padStart(5, '0')}`,
    title: `Event number ${index}`,
    summary: 'Summary',
    category: 'Security',
    status: 'Active',
    startedAtUtc: new Date(baseTime - index * 60_000).toISOString(),
    lastUpdatedAtUtc: new Date(baseTime - index * 60_000).toISOString(),
    location: { latitude: 1, longitude: 2, displayName: 'Place', countryCode: null, region: 'Region' },
    createdAtUtc: new Date(baseTime).toISOString(),
    updatedAtUtc: new Date(baseTime).toISOString(),
    sourceCount: 1,
    severity: index % 2 === 0 ? 'Low' : 'Critical',
    severityRule: 'manual',
    severityInputs: 'test',
    provider: 'Manual',
  }));
}

type PanelProps = ComponentProps<typeof EventPanel>;

function panelProps(overrides: Partial<PanelProps> = {}): PanelProps {
  return {
    events: makeEvents(2000),
    selectedEventId: null,
    selectedRange: 'ALL',
    loadError: null,
    watchlists: [],
    activeWatchlistId: null,
    minimumSeverity: 'All',
    eventWindow: { totalCount: 5000, capped: true, settled: true },
    emptyMessage: 'No events match the active filters.',
    manageButtonRef: createRef<HTMLButtonElement>(),
    gdacsRefresh: { kind: 'idle' },
    providerStatuses: [],
    onRefreshGdacs: vi.fn(),
    onSelectEvent: vi.fn(),
    onSelectWatchlist: vi.fn(),
    onMinimumSeverityChange: vi.fn(),
    onManageWatchlists: vi.fn(),
    criteriaKey: '|ALL||All',
    ...overrides,
  };
}

const list = () => screen.getByRole('list', { name: 'event list' });
// Attribute queries keep large lists fast; role queries with accessible names are slow in jsdom.
const rows = () => Array.from(list().querySelectorAll<HTMLElement>('[role="listitem"]'));

function setScrollMetrics(element: HTMLElement, metrics: { scrollTop: number; clientHeight: number; scrollHeight: number }) {
  Object.defineProperty(element, 'clientHeight', { configurable: true, value: metrics.clientHeight });
  Object.defineProperty(element, 'scrollHeight', { configurable: true, value: metrics.scrollHeight });
  element.scrollTop = metrics.scrollTop;
}

describe('EventPanel bounded, incremental list', () => {
  const scrollIntoView = vi.fn();

  beforeEach(() => {
    scrollIntoView.mockReset();
    Element.prototype.scrollIntoView = scrollIntoView;
  });

  it('renders only the first page of 2,000 Events while the counter and notice report the whole collection', () => {
    render(<EventPanel {...panelProps()} />);

    expect(rows()).toHaveLength(eventListPageSize);
    expect(eventListPageSize).toBe(100);
    expect(screen.getByLabelText('event summary')).toHaveTextContent('2000 events');
    expect(screen.getByText('Newest 2,000 of 5,000 shown. Narrow range or search.')).toBeInTheDocument();
    expect(list().getAttribute('style')).toBeNull();
  });

  it('lists the newest Events first by last update, then id, regardless of severity', () => {
    const [older, newer, tieB, tieA] = [
      { ...makeEvents(1)[0], id: 'evt-older', severity: 'Catastrophic' as const, lastUpdatedAtUtc: '2026-09-01T00:00:00Z' },
      { ...makeEvents(1)[0], id: 'evt-newer', severity: 'Low' as const, lastUpdatedAtUtc: '2026-09-29T00:00:00Z' },
      { ...makeEvents(1)[0], id: 'evt-tie-b', lastUpdatedAtUtc: '2026-09-15T00:00:00Z' },
      { ...makeEvents(1)[0], id: 'evt-tie-a', lastUpdatedAtUtc: '2026-09-15T00:00:00Z' },
    ];
    render(<EventPanel {...panelProps({ events: [older, tieB, newer, tieA], eventWindow: { totalCount: 4, capped: false, settled: true } })} />);

    expect(rows().map((row) => row.getAttribute('data-event-id'))).toEqual(['evt-newer', 'evt-tie-a', 'evt-tie-b', 'evt-older']);
  });

  it('renders the next page when scrolling near the end, and not before', () => {
    render(<EventPanel {...panelProps()} />);

    setScrollMetrics(list(), { scrollTop: 1000, clientHeight: 600, scrollHeight: 9200 });
    fireEvent.scroll(list());
    expect(rows()).toHaveLength(100);

    setScrollMetrics(list(), { scrollTop: 8300, clientHeight: 600, scrollHeight: 9200 });
    fireEvent.scroll(list());
    expect(rows()).toHaveLength(200);
  });

  it('stops at the end of the collection', () => {
    render(<EventPanel {...panelProps({ events: makeEvents(150), eventWindow: { totalCount: 150, capped: false, settled: true } })} />);

    setScrollMetrics(list(), { scrollTop: 8300, clientHeight: 600, scrollHeight: 9200 });
    fireEvent.scroll(list());
    fireEvent.scroll(list());
    expect(rows()).toHaveLength(150);
  });

  it('extends the window to an off-window selection and scrolls that row into view once', () => {
    const props = panelProps();
    const { rerender } = render(<EventPanel {...props} />);
    const target = props.events[1500];

    rerender(<EventPanel {...props} selectedEventId={target.id} />);

    expect(rows()).toHaveLength(1600);
    const item = list().querySelector<HTMLElement>(`[data-event-id="${target.id}"]`)!;
    expect(item.querySelector('button')).toHaveAttribute('aria-pressed', 'true');
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
    expect(scrollIntoView.mock.contexts[0]).toBe(item);

    rerender(<EventPanel {...props} events={[...props.events]} selectedEventId={target.id} />);
    expect(scrollIntoView).toHaveBeenCalledTimes(1);
  });

  it('resets to the first page and the top when the criteria change', () => {
    const props = panelProps();
    const { rerender } = render(<EventPanel {...props} />);
    setScrollMetrics(list(), { scrollTop: 8300, clientHeight: 600, scrollHeight: 9200 });
    fireEvent.scroll(list());
    expect(rows()).toHaveLength(200);

    rerender(<EventPanel {...props} criteriaKey="|30D||All" />);

    expect(rows()).toHaveLength(100);
    expect(list().scrollTop).toBe(0);
  });

  it('keeps the rendered amount and scroll position across a reload with unchanged criteria', () => {
    const props = panelProps();
    const { rerender } = render(<EventPanel {...props} />);
    setScrollMetrics(list(), { scrollTop: 8300, clientHeight: 600, scrollHeight: 9200 });
    fireEvent.scroll(list());
    expect(rows()).toHaveLength(200);

    rerender(<EventPanel {...props} events={props.events.map((event) => ({ ...event }))} />);

    expect(rows()).toHaveLength(200);
    expect(list().scrollTop).toBe(8300);
    expect(scrollIntoView).not.toHaveBeenCalled();
  });

  it('bounds the panel in the stylesheet so the list scrolls instead of growing the layout', () => {
    const css = readFileSync(join(process.cwd(), 'src', 'styles.css'), 'utf8');
    const rule = (selector: string) => {
      const start = css.indexOf(`${selector} {`);
      expect(start).toBeGreaterThanOrEqual(0);
      return css.slice(start, css.indexOf('}', start));
    };

    expect(rule('.event-panel > .event-list')).toMatch(/min-height:\s*0/);
    expect(rule('.event-panel > .event-list')).toMatch(/overflow-y:\s*auto/);
    expect(rule('.event-panel > .event-list')).toMatch(/flex:\s*1 1 0/);
    expect(rule('.event-panel > :not(.event-list)')).toMatch(/flex:\s*0 0 auto/);
    expect(rule('.workspace-grid')).toMatch(/grid-template-rows:\s*minmax\(0, 1fr\)/);
    expect(rule('.workspace-grid')).toMatch(/min-height:\s*0/);
    expect(rule('.first-screen')).toMatch(/\n\s*height:\s*max\(calc\(100vh - 10px\), 700px\)/);
    expect(rule('.first-screen')).toMatch(/grid-template-rows:\s*auto auto minmax\(520px, 1fr\)/);
    expect(rule('.app-shell')).not.toMatch(/\n\s*(min-)?height:/);
    expect(rule('.panel')).toMatch(/min-height:\s*0/);
  });
});
