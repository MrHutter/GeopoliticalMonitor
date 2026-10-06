import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { alertScrollsToDetails, App, type AppProps } from './App';
import { eventLayerId, eventSourceId, getCategoryColor, selectedEventLayerId } from './features/map/mapEvents';
import { firmsLayerId, firmsSourceId } from './features/map/firmsMap';
import { productDisplayName, productDisplayNameUpper } from './productIdentity';
import type { NativeEvent, NativeSourceItem } from './types/events';
import { simulateEventWindow, type SimulatedEventListRequest } from './test/eventWindowSimulator';

type LayerHandler = (event: { features?: Array<{ properties?: Record<string, unknown> }>; lngLat?: unknown }) => void;

type MockSource = { data: unknown; setData: ReturnType<typeof vi.fn> };

const maplibreMock = vi.hoisted(() => {
  class MockPopup {
    static instances: MockPopup[] = [];
    setLngLat = vi.fn(() => this);
    setHTML = vi.fn((_html: string) => this);
    addTo = vi.fn(() => this);
    remove = vi.fn(() => this);
    options?: { className?: string };
    constructor(options?: { className?: string }) { this.options = options; MockPopup.instances.push(this); }
    static byClass(className: string) { return MockPopup.instances.filter((popup) => popup.options?.className === className).at(-1)!; }
  }

  class MockMap {
    static instances: MockMap[] = [];
    options: unknown;
    sources = new Map<string, MockSource>();
    layers = new Map<string, unknown>();
    loadHandlers: Array<() => void> = [];
    layerHandlers = new Map<string, LayerHandler[]>();
    eventHandlers = new Map<string, Array<(event?: unknown) => void>>();
    queryRenderedFeatures = vi.fn((_point?: unknown, _options?: { layers?: string[] }): Array<{ properties: Record<string, unknown> }> => []);
    setFilter = vi.fn();
    setPaintProperty = vi.fn();
    setLayoutProperty = vi.fn();
    easeTo = vi.fn();
    zoomIn = vi.fn();
    zoomOut = vi.fn();
    resize = vi.fn();
    remove = vi.fn();
    isStyleLoaded = vi.fn(() => true);
    canvas = { style: { cursor: '' } };
    bounds = { west: 10, south: 20, east: 30, north: 40 };

    constructor(options: unknown) {
      this.options = options;
      MockMap.instances.push(this);
      queueMicrotask(() => this.loadHandlers.forEach((handler) => handler()));
    }

    on(type: string, layerOrHandler: string | (() => void), maybeHandler?: LayerHandler) {
      if (type === 'load' && typeof layerOrHandler === 'function') {
        this.loadHandlers.push(layerOrHandler);
        return this;
      }
      if (typeof layerOrHandler === 'function') {
        const handlers = this.eventHandlers.get(type) ?? [];
        handlers.push(layerOrHandler);
        this.eventHandlers.set(type, handlers);
        return this;
      }
      if (typeof layerOrHandler === 'string' && maybeHandler) {
        const key = `${type}:${layerOrHandler}`;
        const handlers = this.layerHandlers.get(key) ?? [];
        handlers.push(maybeHandler);
        this.layerHandlers.set(key, handlers);
      }
      return this;
    }

    off() { return this; }

    triggerLayer(type: string, layerId: string, event: Parameters<LayerHandler>[0]) {
      const handlers = this.layerHandlers.get(`${type}:${layerId}`) ?? [];
      handlers.forEach((handler) => handler(event));
    }

    trigger(type: string, event?: unknown) { (this.eventHandlers.get(type) ?? []).forEach((handler) => handler(event)); }

    addSource(id: string, source: { data?: unknown }) {
      this.sources.set(id, { data: source.data, setData: vi.fn((data: unknown) => { const stored = this.sources.get(id); if (stored) stored.data = data; }) });
    }

    getSource(id: string) { return this.sources.get(id); }
    addLayer(layer: { id: string }) { this.layers.set(layer.id, layer); }
    getLayer(id: string) { return this.layers.get(id); }
    getCanvas() { return this.canvas; }
    getBounds() {
      return {
        getWest: () => this.bounds.west,
        getSouth: () => this.bounds.south,
        getEast: () => this.bounds.east,
        getNorth: () => this.bounds.north,
      };
    }
  }

  return { MockMap, MockPopup, setWorkerUrl: vi.fn(), setWorkerCount: vi.fn() };
});

vi.mock('maplibre-gl', () => ({ Map: maplibreMock.MockMap, Popup: maplibreMock.MockPopup, setWorkerUrl: maplibreMock.setWorkerUrl, setWorkerCount: maplibreMock.setWorkerCount }));
vi.mock('maplibre-gl/dist/maplibre-gl.css', () => ({}));
vi.mock('maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url', () => ({ default: '/assets/mock-maplibre-worker.js' }));

const nativeEvents: NativeEvent[] = [
  {
    id: 'evt-demo-baltic-security',
    title: 'Reported security activity near Baltic demo border area',
    summary: 'Demo event showing several source items supporting one normalized security occurrence.',
    category: 'Security',
    status: 'Developing',
    startedAtUtc: '2026-09-17T11:48:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:54:00Z',
    location: { latitude: 57.4, longitude: 24.8, displayName: 'Northern corridor', countryCode: 'DE', region: 'Baltic demo region' },
    createdAtUtc: '2026-09-17T06:00:00Z',
    updatedAtUtc: '2026-09-17T11:54:00Z',
    sourceCount: 3,
    severity: 'Medium',
    severityRule: 'manual',
    severityInputs: 'demo data',
    provider: 'Demo',
  },
  {
    id: 'evt-demo-red-sea-shipping',
    title: 'Commercial shipping disruption reported in Red Sea demo lane',
    summary: 'Demo maritime-adjacent logistics event using local source material only.',
    category: 'Infrastructure',
    status: 'Active',
    startedAtUtc: '2026-09-17T11:32:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:38:00Z',
    location: { latitude: 15.8, longitude: 41.9, displayName: 'Southern transit lane', region: 'Red Sea demo corridor' },
    createdAtUtc: '2026-09-17T06:00:00Z',
    updatedAtUtc: '2026-09-17T11:38:00Z',
    sourceCount: 3,
    severity: 'High',
    severityRule: 'manual',
    severityInputs: 'demo data',
    provider: 'Demo',
  },
];

const olderEvent: NativeEvent = {
  ...nativeEvents[0],
  id: 'evt-demo-older-economic',
  title: 'Older economic briefing outside the seven-day range',
  summary: 'Historical demonstration event for time-range filtering.',
  category: 'Economic',
  status: 'Resolved',
  startedAtUtc: '2026-09-01T12:00:00Z',
  lastUpdatedAtUtc: '2026-09-01T13:00:00Z',
  location: { latitude: 40.7, longitude: -74.0, displayName: 'Financial district', region: 'North America demo region' },
  updatedAtUtc: '2026-09-01T13:00:00Z',
  sourceCount: 1,
};

const gdacsEvent: NativeEvent = {
  ...nativeEvents[0],
  id: 'evt-gdacs-eq-1500811',
  title: 'Earthquake in Russia',
  summary: 'Earthquake in Russia',
  category: 'NaturalDisaster',
  status: 'Unverified',
  startedAtUtc: '2026-09-17T10:00:00Z',
  lastUpdatedAtUtc: '2026-09-17T10:05:00Z',
  location: { latitude: 53.1928, longitude: 160.5129, displayName: 'Russia', countryCode: 'RUS' },
  sourceCount: 1,
  severity: 'Critical',
  severityRule: 'gdacs-eq-v1',
  severityInputs: 'alert=Red; magnitude=7.1',
  provider: 'Gdacs',
};

const firmsObservation = {
  id: 'firms-0123456789abcdef0123456789abcdef',
  latitude: 60.1234,
  longitude: 24.5678,
  acquiredAtUtc: '2026-09-28T14:37:00Z',
  source: 'VIIRS_NOAA20_NRT' as const,
  satellite: '1',
  confidence: 'Nominal' as const,
  frp: 18.6,
  brightnessTemperatureI4: 331.2,
  brightnessTemperatureI5: 290.4,
  scan: 0.42,
  track: 0.51,
  processingVersion: '2.0NRT',
  dayNight: 'Day' as const,
};

const firmsStrongObservation = {
  ...firmsObservation,
  id: 'firms-11111111111111111111111111111111',
  latitude: 61.1,
  longitude: 25.1,
  confidence: 'High' as const,
  frp: 50,
};

const firmsLowObservation = {
  ...firmsObservation,
  id: 'firms-22222222222222222222222222222222',
  latitude: 62.1,
  longitude: 26.1,
  confidence: 'Low' as const,
  frp: 500,
};

const firmsBelowSignificantObservation = {
  ...firmsObservation,
  id: 'firms-33333333333333333333333333333333',
  latitude: 63.1,
  longitude: 27.1,
  confidence: 'Nominal' as const,
  frp: 9.99,
};

const firmsResult = {
  type: 'providers.firms.refresh.result',
  payload: {
    observations: [firmsObservation],
    observationCount: 1,
    refreshedAtUtc: '2026-09-28T15:06:00Z',
    oldestAcquiredAtUtc: firmsObservation.acquiredAtUtc,
    newestAcquiredAtUtc: firmsObservation.acquiredAtUtc,
    coverage: { west: 10, south: 20, east: 30, north: 40 },
    skippedMalformedRecords: 0,
  },
};

const firmsFilterResult = {
  ...firmsResult,
  payload: {
    ...firmsResult.payload,
    observations: [
      firmsObservation,
      firmsStrongObservation,
      firmsLowObservation,
      firmsBelowSignificantObservation,
    ],
    observationCount: 4,
  },
};

const gdacsSource: NativeSourceItem = {
  id: 'src-gdacs-eq-1500811-1661062',
  sourceType: 'DatasetRecord',
  externalId: 'GDACS:EQ:1500811:1661062',
  title: 'Earthquake in Russia - GDACS episode 1661062',
  text: 'Provider: Global Disaster Awareness and Coordination System, GDACS\nGDACS alert level: Orange\nMagnitude: 7.8 M',
  url: 'https://www.gdacs.org/report.aspx?eventid=1500811&episodeid=1661062&eventtype=EQ',
  authorDisplayName: 'Global Disaster Awareness and Coordination System, GDACS',
  publishedAtUtc: '2026-09-17T10:05:00Z',
  retrievedAtUtc: '2026-09-17T12:00:00Z',
  location: gdacsEvent.location,
};

const balticSources: NativeSourceItem[] = [
  { id: 'src-demo-baltic-x-1', sourceType: 'XPost', externalId: 'src-demo-baltic-x-1', title: 'Demo field watcher', text: 'Demo-only post referencing movement near a northern border corridor.', authorDisplayName: 'Demo Field Watcher', authorHandle: '@demo_field_watch', publishedAtUtc: '2026-09-17T11:48:00Z', retrievedAtUtc: '2026-09-17T11:51:00Z', location: null },
  { id: 'src-demo-baltic-news-1', sourceType: 'NewsArticle', externalId: 'src-demo-baltic-news-1', title: 'Regional demo brief', text: 'Fictional regional brief describing similar security activity.', authorDisplayName: 'Regional Demo Desk', authorHandle: null, publishedAtUtc: '2026-09-17T11:52:00Z', retrievedAtUtc: '2026-09-17T11:55:00Z', location: null },
];

const attachedXSource: NativeSourceItem = {
  id: 'src-x-manual',
  sourceType: 'XPost',
  externalId: '1759288472993702104',
  title: null,
  text: 'X post 1759288472993702104',
  url: 'https://x.com/example/status/1759288472993702104',
  authorDisplayName: null,
  authorHandle: null,
  publishedAtUtc: '2024-02-18T18:47:12.617Z',
  retrievedAtUtc: '2026-09-19T12:00:00Z',
  location: null,
};

const attachedReliefWebSource: NativeSourceItem = {
  id: 'src-reliefweb-report-5187658',
  sourceType: 'Other',
  externalId: 'ReliefWeb:Report:5187658',
  title: 'Flood response situation report',
  text: 'ReliefWeb report metadata. Format: Situation Report. Countries: Example Country.',
  url: 'https://reliefweb.int/report/example-country/flood-response',
  authorDisplayName: 'EHO',
  authorHandle: null,
  publishedAtUtc: '2026-09-20T08:30:00Z',
  retrievedAtUtc: '2026-09-24T12:00:00Z',
  location: null,
};

const alertWatchlistId = 'wl-0123456789abcdef0123456789abcdef';
const alertRule = {
  id: 'ar-0123456789abcdef0123456789abcdef',
  watchlistId: alertWatchlistId,
  triggerType: 'GdacsEventMateriallyUpdated' as const,
  isEnabled: true,
  createdAtUtc: '2026-09-17T10:00:00Z',
  lastEnabledAtUtc: '2026-09-17T10:00:00Z',
};
const unreadAlert = {
  id: 'al-fedcba9876543210fedcba9876543210',
  alertRuleId: alertRule.id,
  eventId: nativeEvents[0].id,
  watchlistId: alertWatchlistId,
  eventTitle: nativeEvents[0].title,
  watchlistName: 'Baltic security',
  triggerType: 'GdacsEventMateriallyUpdated' as const,
  providerRevisionAtUtc: '2026-09-17T11:56:00Z',
  triggeredAtUtc: '2026-09-17T11:57:00Z',
  readAtUtc: null,
};

const resetWebView = () => { delete window.chrome; };

const latestMap = async () => {
  await waitFor(() => expect(maplibreMock.MockMap.instances.length).toBeGreaterThan(0));
  const map = maplibreMock.MockMap.instances.at(-1);
  if (!map) throw new Error('Expected mock map instance');
  await waitFor(() => expect(map.getLayer(eventLayerId)).toBeTruthy());
  return map;
};

/**
 * Renders App with a simulated native host. Emitting a legacy-shaped `events.list.result` (no requestId) sets the
 * simulated native Event store and answers the latest windowed request; later `events.list` requests are answered
 * from that store with the shared reference filter, Watchlist membership, the cap and the keep-Event rule.
 *
 * Most tests exercise behavior that does not depend on the startup filter defaults, so by default the helper chooses
 * ALL and All severities through the real controls right after render. No `events.list` request is sent before
 * app.info, so request 1 still carries those criteria. Pass `filters: 'startup'` to keep the startup defaults.
 */
function renderWithWebView(options: { cap?: number; autoRespond?: boolean; appProps?: AppProps; filters?: 'all' | 'startup' } = {}) {
  let messageHandler: ((event: { data: unknown }) => void) | undefined;
  let dataset: NativeEvent[] | null = null;
  let lastListRequest: SimulatedEventListRequest | null = null;
  const members = new Map<string, Set<string>>();
  const answer = (request: SimulatedEventListRequest) => messageHandler?.({ data: simulateEventWindow(dataset ?? [], request, { cap: options.cap ?? 2000, members }) });
  const postMessage = vi.fn((message: unknown) => {
    const typed = message as { type?: string; payload?: SimulatedEventListRequest };
    if (typed?.type !== 'events.list') return;
    lastListRequest = typed.payload ?? {};
    // Like the real host, an empty store still answers; tests that need data emit a fixture list.
    if (options.autoRespond !== false) answer(lastListRequest);
  });
  window.chrome = { webview: { postMessage, addEventListener: vi.fn((_type, listener) => { messageHandler = listener; }), removeEventListener: vi.fn() } };
  const rendered = render(<App {...options.appProps} />);
  if (options.filters !== 'startup') {
    fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
    fireEvent.change(screen.getByRole('combobox', { name: 'Minimum severity' }), { target: { value: 'All' } });
  }
  const emit =(data: unknown) => {
    const message = data as { type?: string; payload?: Record<string, unknown> };
    if (message?.type === 'watchlists.members.get.result' && message.payload) {
      members.set(message.payload.watchlistId as string, new Set(message.payload.eventIds as string[]));
    }
    if (message?.type === 'watchlists.members.set.result' && message.payload) {
      const watchlistId = message.payload.watchlistId as string;
      const current = new Set(members.get(watchlistId) ?? []);
      if (message.payload.isMember) current.add(message.payload.eventId as string);
      else current.delete(message.payload.eventId as string);
      members.set(watchlistId, current);
    }
    if (message?.type === 'events.list.result' && message.payload && !('requestId' in message.payload)) {
      dataset = message.payload.events as NativeEvent[];
      const request = lastListRequest;
      if (request) act(() => answer(request));
      return;
    }
    act(() => messageHandler?.({ data }));
  };
  const respondToLatest = () => {
    const request = lastListRequest;
    if (request) act(() => answer(request));
  };
  const listRequests = () => postMessage.mock.calls
    .map(([message]) => message as { type?: string; payload?: SimulatedEventListRequest })
    .filter((message) => message.type === 'events.list')
    .map((message) => message.payload ?? {});
  const respond = (request: SimulatedEventListRequest) => act(() => answer(request));
  return { postMessage, emit, unmount: rendered.unmount, respondToLatest, respond, listRequests };
}

function providerStatus(overrides: Partial<{
  state: 'Idle' | 'Refreshing' | 'Failed' | 'Stale' | 'Unconfigured';
  lastSuccessAtUtc: string | null;
  nextRefreshAtUtc: string | null;
  message: string | null;
  newAlerts: number;
}>) {
  return {
    type: 'providers.refresh.status',
    payload: {
      providers: [{
        provider: 'Gdacs',
        displayName: 'GDACS',
        state: 'Idle',
        lastSuccessAtUtc: null,
        nextRefreshAtUtc: null,
        message: null,
        newAlerts: 0,
        ...overrides,
      }],
    },
  };
}

function emitAppInfo(emit: (data: unknown) => void, xPaneSide: 'Left' | 'Right' = 'Right') {
  emit({ type: 'app.info', payload: { applicationName: productDisplayName, shell: 'WinUI 3', uiHost: 'WebView2 desktop host', persistence: 'SQLite local application data', productionInboundPortsRequired: false, xPaneSide } });
}

describe('App', () => {
  beforeEach(() => {
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-09-17T12:00:00Z'));
  });

  afterEach(() => {
    resetWebView();
    window.localStorage.clear();
    document.documentElement.removeAttribute('data-accent');
    maplibreMock.MockMap.instances.length = 0;
    maplibreMock.MockPopup.instances.length = 0;
    vi.restoreAllMocks();
    vi.clearAllMocks();
  });

  it('renders the desktop shell and requests native events after app.info', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();

    expect(screen.getByText(productDisplayNameUpper)).toBeInTheDocument();
    expect(screen.getByText('LOCAL DESKTOP')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toBeInTheDocument();
    ['24H', '7D', '30D', '90D', 'ALL'].forEach((range) => expect(screen.getByRole('button', { name: range })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'ALL' })).toHaveClass('is-active');
    expect(screen.getByLabelText('UTC time')).toHaveTextContent(/^UTC /);
    expect(screen.getByRole('button', { name: 'Settings' })).toBeInTheDocument();
    ['Orange', 'Blue', 'Red', 'Green', 'Yellow'].forEach((label) => expect(screen.queryByLabelText(label)).not.toBeInTheDocument());
    expect(postMessage).toHaveBeenCalledWith({ type: 'app.ready', payload: {} });
    expect(postMessage).toHaveBeenCalledWith({ type: 'preferences.get', payload: { key: 'AccentTheme' } });
    expect(postMessage.mock.calls.some(([message]) => (message as { type?: string }).type?.includes('reliefweb'))).toBe(false);
    expect(screen.queryByLabelText('mock X Following feed')).not.toBeInTheDocument();
    expect(screen.queryByRole('dialog', { name: 'Settings' })).not.toBeInTheDocument();

    emitAppInfo(emit);

    await waitFor(() => expect(postMessage).toHaveBeenCalledWith({ type: 'events.list', payload: expect.objectContaining({ range: 'ALL' }) }));
    expect(screen.queryByLabelText('desktop bridge status')).not.toBeInTheDocument();
    expect(screen.queryByText('No inbound ports')).not.toBeInTheDocument();
    expect(screen.queryByText('Waiting for desktop host')).not.toBeInTheDocument();
    expect(postMessage).toHaveBeenCalledWith({ type: 'events.list', payload: expect.objectContaining({ range: 'ALL', search: '', minimumSeverity: 'All' }) });
  });

  it('does not mount Settings or add Settings-only bridge work during dashboard startup', async () => {
    const { postMessage } = renderWithWebView();
    await latestMap();

    expect(screen.queryByRole('dialog', { name: 'Settings' })).not.toBeInTheDocument();
    expect(postMessage).toHaveBeenCalledWith({ type: 'app.ready', payload: {} });
    expect(postMessage).toHaveBeenCalledWith({ type: 'preferences.get', payload: { key: 'AccentTheme' } });
    const startupRequestCount = postMessage.mock.calls.length;

    fireEvent.click(screen.getByRole('button', { name: 'Settings' }));
    expect(screen.getByRole('dialog', { name: 'Settings' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Application' })).not.toBeInTheDocument();
    expect(postMessage).toHaveBeenCalledTimes(startupRequestCount);

    fireEvent.click(screen.getByRole('button', { name: 'Close settings' }));
    fireEvent.click(screen.getByRole('button', { name: 'Settings' }));
    expect(postMessage).toHaveBeenCalledTimes(startupRequestCount);
  });

  it('loads only the alert summary at startup and presents bounded monitoring and history on demand', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: alertWatchlistId, name: 'Baltic security', eventCount: 1 }] } });

    expect(postMessage).toHaveBeenCalledWith({ type: 'alerts.summary', payload: {} });
    expect(postMessage.mock.calls.some(([message]) => (message as { type?: string }).type === 'alerts.rules.list')).toBe(false);
    expect(postMessage.mock.calls.some(([message]) => (message as { type?: string }).type === 'alerts.list')).toBe(false);

    emit({ type: 'alerts.summary.result', payload: { unreadCount: 125 } });
    const alertsButton = await screen.findByRole('button', { name: 'Alerts, 125 unread' });
    expect(alertsButton).toHaveTextContent('99+');
    fireEvent.click(alertsButton);

    const dialog = screen.getByRole('dialog', { name: 'Alerts' });
    expect(within(dialog).getByRole('button', { name: 'Close Alerts' })).toHaveFocus();
    expect(postMessage).toHaveBeenCalledWith({ type: 'alerts.rules.list', payload: {} });
    expect(postMessage).toHaveBeenCalledWith({ type: 'alerts.list', payload: {} });

    emit({ type: 'alerts.rules.list.result', payload: { rules: [alertRule] } });
    emit({ type: 'alerts.list.result', payload: { alerts: [], unreadCount: 0, hasMore: false } });
    expect(await within(dialog).findByText('Monitoring is on. No updates have triggered an alert yet.')).toBeInTheDocument();
    emit({ type: 'alerts.list.result', payload: { alerts: [unreadAlert], unreadCount: 1, hasMore: true } });
    const monitoringToggle = await within(dialog).findByRole('checkbox', { name: /Baltic security/ });
    expect(monitoringToggle).toBeChecked();
    expect(within(dialog).getByText(nativeEvents[0].title)).toBeInTheDocument();
    expect(within(dialog).getByText('Showing the newest 200 alerts.')).toBeInTheDocument();

    fireEvent.click(monitoringToggle);
    expect(postMessage).toHaveBeenCalledWith({ type: 'alerts.rules.set', payload: { watchlistId: alertWatchlistId, enabled: false } });
    emit({ type: 'alerts.rules.set.result', payload: { disposition: 'Disabled', rule: { ...alertRule, isEnabled: false } } });
    expect(await within(dialog).findByText('Monitoring disabled. Alert history was preserved.')).toBeInTheDocument();
    expect(within(dialog).getByText('No Watchlists are monitored.')).toBeInTheDocument();
    emit({ type: 'alerts.list.result', payload: { alerts: [{ ...unreadAlert, readAtUtc: '2026-09-17T11:58:00Z' }], unreadCount: 0, hasMore: false } });
    expect(await within(dialog).findByText('No unread alerts.')).toBeInTheDocument();

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('dialog', { name: 'Alerts' })).not.toBeInTheDocument();
    await waitFor(() => expect(alertsButton).toHaveFocus());
  });

  it('activates an alert by marking it read, restoring All Events, opening Overview, and locating the Event', async () => {
    const { postMessage, emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 1 } });
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: alertWatchlistId, name: 'Baltic security', eventCount: 1 }] } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: alertWatchlistId } });
    emit({ type: 'watchlists.members.get.result', payload: { watchlistId: alertWatchlistId, eventIds: [nativeEvents[1].id] } });
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'shipping' } });
    fireEvent.click(screen.getByRole('button', { name: '24H' }));

    fireEvent.click(screen.getByRole('button', { name: 'Alerts, 1 unread' }));
    emit({ type: 'alerts.rules.list.result', payload: { rules: [alertRule] } });
    emit({ type: 'alerts.list.result', payload: { alerts: [unreadAlert], unreadCount: 1, hasMore: false } });
    fireEvent.click(await screen.findByRole('button', { name: new RegExp(nativeEvents[0].title) }));

    expect(postMessage).toHaveBeenCalledWith({ type: 'alerts.markRead', payload: { alertId: unreadAlert.id } });
    expect(screen.queryByRole('dialog', { name: 'Alerts' })).not.toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue('');
    expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'ALL' })).toHaveClass('is-active');
    expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Overview' })).toHaveAttribute('aria-selected', 'true');
    await waitFor(() => expect(map.easeTo).toHaveBeenCalledWith(expect.objectContaining({ center: [24.8, 57.4], zoom: 3.15 })));

    emit({ type: 'alerts.markRead.result', payload: { alertId: unreadAlert.id, readAtUtc: null, disposition: 'NotFound' } });
    expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Alerts, 1 unread' }));
    expect(await screen.findByText('This alert is no longer available.')).toBeInTheDocument();
  });

  it('coalesces repeated alert refreshes while the startup summary request is in flight', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    const result = { type: 'providers.gdacs.refresh.result', payload: { status: 'Succeeded', newEvents: 0, updatedEvents: 1, newSources: 0, updatedSources: 1, unchangedRecords: 0, skippedMalformedRecords: 0, conflicts: 0, newAlerts: 2 } };
    emit(result);
    emit(result);

    const summaryRequests = () => postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'alerts.summary');
    expect(summaryRequests()).toHaveLength(1);
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 2 } });
    await waitFor(() => expect(summaryRequests()).toHaveLength(2));
    expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'alerts.list')).toHaveLength(0);
  });

  it('opens, closes, and reopens Settings without clearing the selected Event or top-bar controls', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    const secondEvent = await screen.findByRole('button', { name: /Commercial shipping disruption/i });
    fireEvent.click(secondEvent);
    expect(screen.getByRole('heading', { name: nativeEvents[1].title })).toBeInTheDocument();

    const settingsButton = screen.getByRole('button', { name: 'Settings' });
    fireEvent.click(settingsButton);
    const dialog = screen.getByRole('dialog', { name: 'Settings' });
    const closeButton = within(dialog).getByRole('button', { name: 'Close settings' });
    const activeTheme = within(dialog).getByRole('radio', { name: 'Orange' });
    const search = screen.getByPlaceholderText('Search events, locations, keywords...');
    expect(closeButton).toHaveFocus();
    expect(activeTheme).toBeChecked();
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(activeTheme).toHaveFocus();
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(closeButton).toHaveFocus();
    for (let index = 0; index < 8; index += 1) {
      fireEvent.keyDown(document, { key: 'Tab' });
      expect(dialog.contains(document.activeElement)).toBe(true);
    }
    fireEvent.keyDown(document, { key: 'Tab', shiftKey: true });
    expect(activeTheme).toHaveFocus();
    fireEvent.keyDown(document, { key: 'Tab', shiftKey: true });
    expect(closeButton).toHaveFocus();
    search.focus();
    expect(closeButton).toHaveFocus();
    ['Orange', 'Blue', 'Red', 'Green', 'Yellow'].forEach((label) => expect(within(dialog).getByRole('radio', { name: label })).toBeInTheDocument());
    expect(within(dialog).getByText(productDisplayName)).toBeInTheDocument();
    expect(within(dialog).getByText('WinUI 3')).toBeInTheDocument();
    expect(within(dialog).getByText('WebView2 desktop host')).toBeInTheDocument();
    expect(within(dialog).getByText('SQLite local application data')).toBeInTheDocument();
    expect(within(dialog).getByText('None required')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'ALL' })).toHaveClass('is-active');

    fireEvent.click(closeButton);
    expect(screen.queryByRole('dialog', { name: 'Settings' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: nativeEvents[1].title })).toBeInTheDocument();
    await waitFor(() => expect(settingsButton).toHaveFocus());
    search.focus();
    expect(search).toHaveFocus();

    fireEvent.click(settingsButton);
    const reopenedDialog = screen.getByRole('dialog', { name: 'Settings' });
    expect(within(reopenedDialog).getByRole('button', { name: 'Close settings' })).toHaveFocus();
    fireEvent.keyDown(document, { key: 'Tab' });
    expect(reopenedDialog.contains(document.activeElement)).toBe(true);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('dialog', { name: 'Settings' })).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: nativeEvents[1].title })).toBeInTheDocument();
    await waitFor(() => expect(settingsButton).toHaveFocus());
  });

  it('tracks X pane side and exposes reversible workspace layout state', async () => {
    const { emit } = renderWithWebView();
    await latestMap();

    const workspace = screen.getByLabelText('map and events workspace');
    const detailsPanel = document.querySelector('.details-panel');
    expect(workspace).toHaveAttribute('data-x-pane-side', 'right');
    expect(detailsPanel && (workspace.compareDocumentPosition(detailsPanel) & Node.DOCUMENT_POSITION_FOLLOWING)).toBeTruthy();

    emitAppInfo(emit, 'Left');
    await waitFor(() => expect(workspace).toHaveAttribute('data-x-pane-side', 'left'));

    emit({ type: 'xPane.side.value', payload: { value: 'Right' } });
    await waitFor(() => expect(workspace).toHaveAttribute('data-x-pane-side', 'right'));
  });
  it('renders native event rows and map coordinates from events.list', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events');

    const source = map.getSource(eventSourceId);
    await waitFor(() => expect(source?.setData).toHaveBeenCalled());
    expect(source?.data).toMatchObject({ features: expect.arrayContaining([expect.objectContaining({ geometry: { coordinates: [24.8, 57.4], type: 'Point' } })]) });
  });

  it('disables MapLibre world copies for a single-world presentation', async () => {
    renderWithWebView();
    const map = await latestMap();

    expect(map.options).toMatchObject({ renderWorldCopies: false });
  });

  it('manually refreshes GDACS, gates duplicate clicks, reloads events, and presents DatasetRecord provenance', async () => {
    const { postMessage, emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    const refresh = screen.getByRole('button', { name: 'Refresh GDACS' });
    fireEvent.click(refresh);
    expect(postMessage).toHaveBeenCalledWith({ type: 'providers.gdacs.refresh', payload: {} });
    expect(refresh).toBeDisabled();
    expect(screen.getByRole('button', { name: /Reported security activity/i })).toBeInTheDocument();
    fireEvent.click(refresh);
    expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'providers.gdacs.refresh')).toHaveLength(1);

    emit({
      type: 'providers.gdacs.refresh.result',
      payload: { status: 'Succeeded', newEvents: 1, updatedEvents: 0, newSources: 1, updatedSources: 0, unchangedRecords: 0, skippedMalformedRecords: 0, conflicts: 0, newAlerts: 0 },
    });
    expect(await screen.findByText(/GDACS refresh complete: 1 new/)).toBeInTheDocument();
    expect(postMessage).toHaveBeenCalledWith({ type: 'events.list', payload: expect.objectContaining({ range: 'ALL', search: '', minimumSeverity: 'All' }) });

    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, gdacsEvent] } });
    expect(await screen.findByRole('button', { name: /Earthquake in Russia/i })).toHaveTextContent('Natural Disaster');
    await waitFor(() => expect(map.getSource(eventSourceId)?.data).toMatchObject({ features: expect.arrayContaining([
      expect.objectContaining({ properties: expect.objectContaining({ id: gdacsEvent.id, category: 'NaturalDisaster' }), geometry: { type: 'Point', coordinates: [160.5129, 53.1928] } }),
    ]) }));

    fireEvent.click(screen.getByRole('button', { name: /Earthquake in Russia/i }));
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    emit({ type: 'events.sources.result', payload: { eventId: gdacsEvent.id, sources: [gdacsSource] } });
    expect(await screen.findByText('GDACS:EQ:1500811:1661062')).toBeInTheDocument();
    expect(screen.getByText(/Canonical source: https:\/\/www.gdacs.org/)).toBeInTheDocument();
    expect(screen.getAllByText(/Global Disaster Awareness and Coordination System, GDACS/).length).toBeGreaterThan(0);
  });

  it('keeps loaded events visible when GDACS refresh fails', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    fireEvent.click(screen.getByRole('button', { name: 'Refresh GDACS' }));
    emit({ type: 'providers.gdacs.refresh.error', payload: { status: 'TimedOut', message: 'The GDACS request timed out. Existing events were preserved.' } });

    expect(await screen.findByRole('alert')).toHaveTextContent('Existing events were preserved');
    expect(screen.getByRole('button', { name: /Reported security activity/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Refresh GDACS' })).toBeEnabled();
  });

  it('requests provider status after app.info and shows every provider state in the status strip', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    expect(postMessage).toHaveBeenCalledWith({ type: 'providers.refresh.status.get', payload: {} });
    const row = () => screen.getByLabelText('GDACS refresh status');

    expect(row()).toHaveTextContent('Not updated yet');
    expect(row()).toHaveTextContent('No refresh scheduled');

    const lastSuccess = new Date(Date.now() - 5 * 60_000).toISOString();
    emit(providerStatus({ state: 'Idle', lastSuccessAtUtc: lastSuccess, nextRefreshAtUtc: new Date(Date.now() + 25 * 60_000).toISOString(), message: 'Up to date.' }));
    await waitFor(() => expect(row()).toHaveTextContent('Updated 5 min ago'));
    expect(row()).toHaveTextContent(/Next \d/);
    expect(row()).toHaveClass('is-idle');

    emit(providerStatus({ state: 'Refreshing', lastSuccessAtUtc: lastSuccess }));
    await waitFor(() => expect(row()).toHaveTextContent('Refreshing…'));

    emit(providerStatus({ state: 'Failed', lastSuccessAtUtc: lastSuccess, message: 'Request timed out. Existing events were preserved.' }));
    await waitFor(() => expect(within(row()).getByRole('alert')).toHaveTextContent('Request timed out. Existing events were preserved.'));
    expect(row()).toHaveClass('is-failed');

    emit(providerStatus({ state: 'Stale', lastSuccessAtUtc: new Date(Date.now() - 3 * 60 * 60_000).toISOString() }));
    await waitFor(() => expect(row()).toHaveTextContent('Stale'));
    expect(row()).toHaveTextContent('Updated 3 h ago');

    emit(providerStatus({ state: 'Unconfigured', message: 'Not configured.' }));
    await waitFor(() => expect(row()).toHaveTextContent('Not configured'));
    expect(row()).toHaveClass('is-unconfigured');
    expect(screen.getByRole('group', { name: 'Provider refresh status' }).textContent?.toLowerCase()).not.toContain('hybrid');
  });

  it('ignores provider status pushes with unexpected fields or values', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);

    emit({ type: 'providers.refresh.status', payload: { providers: [{ ...providerStatus({ state: 'Refreshing' }).payload.providers[0], url: 'https://example.invalid' }] } });
    emit({ type: 'providers.refresh.status', payload: { providers: [{ ...providerStatus({}).payload.providers[0], state: 'Exploded' }] } });

    expect(screen.getByLabelText('GDACS refresh status')).not.toHaveTextContent('Refreshing…');
    expect(screen.getByRole('button', { name: 'Refresh GDACS' })).toBeEnabled();
  });

  it('disables the manual GDACS action while a timer-driven refresh runs', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    emit(providerStatus({ state: 'Refreshing' }));
    const refresh = await screen.findByRole('button', { name: 'Refresh GDACS' });
    await waitFor(() => expect(refresh).toBeDisabled());
    fireEvent.click(refresh);
    expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'providers.gdacs.refresh')).toHaveLength(0);

    emit(providerStatus({ state: 'Idle', lastSuccessAtUtc: new Date().toISOString() }));
    await waitFor(() => expect(screen.getByRole('button', { name: 'Refresh GDACS' })).toBeEnabled());
  });

  it('reloads Events after a timer-driven refresh without resetting selection, filters or open Details', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 0 } });
    emit(providerStatus({ state: 'Idle' }));
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, gdacsEvent] } });

    fireEvent.click(await screen.findByRole('button', { name: /Commercial shipping disruption/i }));
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'demo' } });
    fireEvent.change(screen.getByLabelText('Minimum severity'), { target: { value: 'Medium' } });
    fireEvent.click(screen.getByRole('button', { name: '30D' }));
    const eventListRequests = () => postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'events.list').length;
    const summaryRequests = () => postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'alerts.summary').length;
    const listRequestsBefore = eventListRequests();
    const summaryRequestsBefore = summaryRequests();

    emit(providerStatus({ state: 'Refreshing' }));
    expect(eventListRequests()).toBe(listRequestsBefore);
    emit(providerStatus({ state: 'Idle', lastSuccessAtUtc: new Date().toISOString(), newAlerts: 1 }));

    await waitFor(() => expect(eventListRequests()).toBe(listRequestsBefore + 1));
    expect(summaryRequests()).toBe(summaryRequestsBefore + 1);
    expect(postMessage).toHaveBeenCalledWith({ type: 'events.sources.get', payload: { id: nativeEvents[1].id } });
    const updatedShipping = { ...nativeEvents[1], summary: 'Updated summary from the refreshed list.' };
    emit({ type: 'events.list.result', payload: { events: [nativeEvents[0], updatedShipping, gdacsEvent] } });

    expect(await screen.findByRole('heading', { name: nativeEvents[1].title })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Sources' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toHaveValue('demo');
    expect(screen.getByLabelText('Minimum severity')).toHaveValue('Medium');
    expect(screen.getByRole('button', { name: '30D' })).toHaveClass('is-active');
    expect(screen.getByRole('button', { name: /Commercial shipping disruption/i })).toHaveAttribute('aria-pressed', 'true');
  });

  it('shows the UCDP row, runs the manual UCDP check and reloads after an import without resetting the view', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 0 } });
    const ucdpEvent: NativeEvent = {
      ...nativeEvents[0],
      id: 'evt-ucdp-cand-900001',
      title: 'State-based violence: Northtown',
      category: 'MilitaryConflict',
      status: 'Unverified',
      severity: 'High',
      severityRule: 'ucdp-candidate-v1',
      severityInputs: 'best=12; low=10; high=15',
      provider: 'UcdpCandidate',
      providerLastSeenRelease: 202608,
    };
    const statuses = (ucdp: Record<string, unknown>) => ({
      type: 'providers.refresh.status',
      payload: {
        providers: [
          providerStatus({ state: 'Idle' }).payload.providers[0],
          { provider: 'UcdpCandidate', displayName: 'UCDP Candidate', state: 'Idle', lastSuccessAtUtc: null, nextRefreshAtUtc: null, message: null, newAlerts: 0, cadenceLabel: 'monthly release', ...ucdp },
        ],
      },
    });
    emit(statuses({}));
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, ucdpEvent] } });
    const ucdpRow = () => screen.getByLabelText('UCDP Candidate refresh status');
    await waitFor(() => expect(ucdpRow()).toHaveTextContent('monthly release, no data yet'));

    fireEvent.click(await screen.findByRole('button', { name: /Commercial shipping disruption/i }));
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'demo' } });
    fireEvent.change(screen.getByLabelText('Minimum severity'), { target: { value: 'Medium' } });
    fireEvent.click(screen.getByRole('button', { name: '30D' }));
    const eventListRequests = () => postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'events.list').length;

    fireEvent.click(within(ucdpRow()).getByRole('button', { name: 'Refresh UCDP' }));
    expect(postMessage).toHaveBeenCalledWith({ type: 'providers.ucdp.refresh', payload: {} });
    expect(within(ucdpRow()).getByRole('button', { name: 'Refreshing...' })).toBeDisabled();
    emit(statuses({ state: 'Refreshing' }));
    await waitFor(() => expect(ucdpRow()).toHaveTextContent('Refreshing…'));
    const listRequestsBefore = eventListRequests();

    emit(statuses({ state: 'Idle', lastSuccessAtUtc: new Date().toISOString(), nextRefreshAtUtc: new Date(Date.now() + 6 * 3_600_000).toISOString(), message: 'Up to date.', dataThrough: '2026-08' }));
    emit({
      type: 'providers.ucdp.refresh.result',
      payload: {
        status: 'Succeeded', newestRelease: '2026-08', releasesImported: 2, newEvents: 3, updatedEvents: 1, unchangedRecords: 0, conflicts: 0,
        skippedMalformedRecords: 0, skippedInvalidCoordinates: 0, skippedImpreciseLocation: 4, skippedZeroFatalities: 5, skippedOutsideWindow: 0, droppedByCap: 0,
      },
    });

    await waitFor(() => expect(eventListRequests()).toBe(listRequestsBefore + 1));
    expect(await screen.findByText('UCDP import complete: 3 new, 1 updated, 0 unchanged, 0 conflicts. Data through August 2026.')).toBeInTheDocument();
    expect(ucdpRow()).toHaveTextContent('monthly release, data through Aug 2026');
    expect(ucdpRow()).toHaveAttribute('title', expect.stringContaining('data through August 2026'));
    expect(ucdpRow()).toHaveTextContent(/Next check \d/);
    expect(within(ucdpRow()).getByRole('button', { name: 'Refresh UCDP' })).toBeEnabled();
    expect(screen.getByRole('heading', { name: nativeEvents[1].title })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Sources' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toHaveValue('demo');
    expect(screen.getByLabelText('Minimum severity')).toHaveValue('Medium');
    expect(screen.getByRole('button', { name: '30D' })).toHaveClass('is-active');
    expect(screen.getByRole('button', { name: /Commercial shipping disruption/i })).toHaveAttribute('aria-pressed', 'true');

    emit({ type: 'providers.ucdp.refresh.error', payload: { status: 'TimedOut', message: 'The UCDP request timed out. Existing events were preserved.' } });
    expect(await screen.findByRole('alert')).toHaveTextContent('The UCDP request timed out. Existing events were preserved.');
    expect(screen.getByRole('group', { name: 'Provider refresh status' }).textContent?.toLowerCase()).not.toContain('hybrid');
  });

  it('does not select an Event on a timer-driven reload when the selection was cleared', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit(providerStatus({ state: 'Idle' }));
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());

    const search = screen.getByPlaceholderText('Search events, locations, keywords...');
    fireEvent.change(search, { target: { value: 'shipping' } });
    await waitFor(() => expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument());
    fireEvent.change(search, { target: { value: '' } });
    expect(await screen.findByRole('button', { name: /Reported security activity/i })).toBeInTheDocument();

    emit(providerStatus({ state: 'Idle', lastSuccessAtUtc: new Date().toISOString() }));
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Reported security activity/i })).toHaveAttribute('aria-pressed', 'false');
  });

  it('sends typed window criteria with the selected Event as the keep id and debounces search typing', async () => {
    const { emit, listRequests } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    expect(listRequests()[0]).toEqual({ requestId: 1, range: 'ALL', search: '', minimumSeverity: 'All' });

    fireEvent.click(screen.getByRole('button', { name: '7D' }));
    expect(listRequests().at(-1)).toEqual({ requestId: 2, range: '7D', search: '', minimumSeverity: 'All', keepEventId: nativeEvents[0].id });

    const before = listRequests().length;
    const search = screen.getByPlaceholderText('Search events, locations, keywords...');
    fireEvent.change(search, { target: { value: 'b' } });
    fireEvent.change(search, { target: { value: 'ba' } });
    fireEvent.change(search, { target: { value: 'bal' } });
    expect(listRequests()).toHaveLength(before);

    await waitFor(() => expect(listRequests()).toHaveLength(before + 1));
    expect(listRequests().at(-1)).toMatchObject({ search: 'bal', range: '7D' });
    await new Promise((resolve) => setTimeout(resolve, 300));
    expect(listRequests()).toHaveLength(before + 1);
  });

  it('discards stale responses and keeps the previous result visible while a request is in flight', async () => {
    const { emit, listRequests, respond, respondToLatest } = renderWithWebView({ autoRespond: false });
    await latestMap();
    emitAppInfo(emit);
    expect(screen.queryByText('No events match the active filters.')).not.toBeInTheDocument();
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events'));

    fireEvent.click(screen.getByRole('button', { name: '24H' }));
    const staleRequest = listRequests().at(-1)!;
    fireEvent.click(screen.getByRole('button', { name: '30D' }));

    expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events');
    expect(screen.getByRole('button', { name: /Older economic briefing/i })).toBeInTheDocument();
    expect(screen.queryByText('No events match the active filters.')).not.toBeInTheDocument();

    respond(staleRequest);
    expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events');

    respondToLatest();
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events'));
    expect(screen.getByRole('button', { name: '30D' })).toHaveClass('is-active');

    fireEvent.click(screen.getByRole('button', { name: '24H' }));
    respondToLatest();
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
  });

  it('shows the capped notice with real numbers and gives the map and list the same capped collection', async () => {
    const { emit } = renderWithWebView({ cap: 2 });
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });

    const notice = await screen.findByRole('status', { name: 'Newest 2 of 3 shown. Narrow range or search for others.' });
    expect(notice).toHaveAttribute('title', 'Newest 2 of 3 shown. Narrow range or search for others.');
    expect(notice.firstElementChild).toHaveTextContent('Newest 2 of 3 shown.');
    expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events');
    const rowTitles = within(screen.getByRole('list', { name: 'event list' })).getAllByRole('button').map((row) => row.textContent);
    expect(rowTitles.some((text) => text?.includes(olderEvent.title))).toBe(false);
    await waitFor(() => expect((map.getSource(eventSourceId)?.data as { features: Array<{ properties: { id: string } }> }).features.map((feature) => feature.properties.id).sort())
      .toEqual([nativeEvents[0].id, nativeEvents[1].id].sort()));

    fireEvent.click(screen.getByRole('button', { name: '24H' }));
    await waitFor(() => expect(screen.queryByText(/^Newest \d+ of \d+ shown/)).not.toBeInTheDocument());
  });

  it('keeps a selected Event that still matches even outside the cap and clears it when the criteria exclude it', async () => {
    const { emit } = renderWithWebView({ cap: 2 });
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });
    const search = screen.getByPlaceholderText('Search events, locations, keywords...');

    fireEvent.change(search, { target: { value: 'economic' } });
    fireEvent.click(await screen.findByRole('button', { name: /Older economic briefing/i }));
    expect(screen.getByRole('heading', { name: olderEvent.title })).toBeInTheDocument();

    fireEvent.change(search, { target: { value: '' } });
    await waitFor(() => expect(screen.getByRole('button', { name: /Reported security activity/i })).toBeInTheDocument());
    expect(screen.getByRole('heading', { name: olderEvent.title })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Older economic briefing/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events');

    fireEvent.click(screen.getByRole('button', { name: '7D' }));
    await waitFor(() => expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
    await waitFor(() => expect(screen.getByRole('button', { name: /Reported security activity/i })).toBeInTheDocument());
    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
  });

  it('activates an Alert whose Event is older than the newest capped window', async () => {
    const { postMessage, emit, listRequests } = renderWithWebView({ cap: 2 });
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 1 } });
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
    expect(screen.queryByRole('button', { name: /Older economic briefing/i })).not.toBeInTheDocument();
    const oldAlert = { ...unreadAlert, eventId: olderEvent.id, eventTitle: olderEvent.title };

    fireEvent.click(screen.getByRole('button', { name: 'Alerts, 1 unread' }));
    emit({ type: 'alerts.rules.list.result', payload: { rules: [alertRule] } });
    emit({ type: 'alerts.list.result', payload: { alerts: [oldAlert], unreadCount: 1, hasMore: false } });
    fireEvent.click(await screen.findByRole('button', { name: new RegExp(olderEvent.title) }));

    expect(postMessage).toHaveBeenCalledWith({ type: 'alerts.markRead', payload: { alertId: oldAlert.id } });
    expect(listRequests().at(-1)).toMatchObject({ range: 'ALL', search: '', minimumSeverity: 'All', keepEventId: olderEvent.id });
    expect(await screen.findByRole('heading', { name: olderEvent.title })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Older economic briefing/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events');
    await waitFor(() => expect((map.getSource(eventSourceId)?.data as { features: Array<{ properties: { id: string } }> }).features.map((feature) => feature.properties.id))
      .toContain(olderEvent.id));
  });

  it('reports an activated Alert as unavailable only when the native window does not contain its Event', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 1 } });
    const missingAlert = { ...unreadAlert, eventId: 'evt-deleted-event', eventTitle: 'Deleted event' };

    fireEvent.click(await screen.findByRole('button', { name: 'Alerts, 1 unread' }));
    emit({ type: 'alerts.rules.list.result', payload: { rules: [alertRule] } });
    emit({ type: 'alerts.list.result', payload: { alerts: [missingAlert], unreadCount: 1, hasMore: false } });
    fireEvent.click(await screen.findByRole('button', { name: /Deleted event/ }));
    await waitFor(() => expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: /^Alerts/ }));
    expect(await screen.findByText('This alert is no longer available.')).toBeInTheDocument();
  });

  describe('conflict and tension reference layers', () => {
    it('adds tension, conflict fill and outline below FIRMS and Event markers, and no demo polygons', async () => {
      renderWithWebView();
      const map = await latestMap();

      expect([...map.layers.keys()]).toEqual([
        'tension-countries-fill', 'conflict-zones-fill', 'conflict-zones-outline', firmsLayerId, eventLayerId, selectedEventLayerId,
      ]);
      expect(map.getLayer('mock-conflict-zones-fill')).toBeUndefined();
      expect(map.getSource('mock-conflict-zones')).toBeUndefined();
      const conflictData = map.getSource('conflict-zones')?.data as { features: Array<{ properties: { entryId: string } }> };
      expect(conflictData.features.map((feature) => feature.properties.entryId)).toContain('russia-ukraine');
      const tensionData = map.getSource('tension-countries')?.data as { features: Array<{ properties: { countryCode: string } }> };
      expect(tensionData.features.map((feature) => feature.properties.countryCode)).not.toContain('ISR');
    });

    it('hides each reference layer with its own toggle and drops its legend swatch', async () => {
      renderWithWebView();
      const map = await latestMap();
      expect(screen.getByText('Armed conflict (approx.)')).toBeInTheDocument();
      expect(screen.getByText('Elevated tension')).toBeInTheDocument();

      fireEvent.click(screen.getByLabelText(/^Tension/));
      expect(map.setLayoutProperty).toHaveBeenCalledWith('tension-countries-fill', 'visibility', 'none');
      expect(screen.queryByText('Elevated tension')).not.toBeInTheDocument();
      fireEvent.click(screen.getByLabelText(/Conflict zones/));
      expect(map.setLayoutProperty).toHaveBeenCalledWith('conflict-zones-fill', 'visibility', 'none');
      expect(map.setLayoutProperty).toHaveBeenCalledWith('conflict-zones-outline', 'visibility', 'none');
      expect(screen.queryByText('Armed conflict (approx.)')).not.toBeInTheDocument();
    });

    it('opens one popup listing each entry under the pointer, without selecting an Event', async () => {
      const { emit } = renderWithWebView();
      const map = await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: nativeEvents } });
      await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
      map.queryRenderedFeatures.mockImplementation((_point, options) => (options?.layers?.includes('conflict-zones-fill')
        ? [{ properties: { entryId: 'russia-ukraine' } }, { properties: { countryCode: 'PAK', entryIds: 'india-pakistan,pakistan-afghanistan' } }]
        : []));
      const referencePopup = maplibreMock.MockPopup.byClass('conflict-tension-popup');

      map.trigger('click', { point: { x: 1, y: 2 }, lngLat: { lng: 37, lat: 48 } });

      const html = referencePopup.setHTML.mock.calls.at(-1)?.[0] as string;
      expect(html.match(/<section>/g)).toHaveLength(3);
      expect(html).toContain('Russia-Ukraine');
      expect(html).toContain('India-Pakistan');
      expect(html).toContain('Draft, not yet reviewed by the owner');
      expect(referencePopup.addTo).toHaveBeenCalled();
      expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();
    });

    it('gives Event markers and FIRMS points click priority over the reference popup', async () => {
      renderWithWebView();
      const map = await latestMap();
      map.queryRenderedFeatures.mockImplementation((_point, options) => (options?.layers?.includes(eventLayerId)
        ? [{ properties: { id: 'evt-demo-baltic-security' } }]
        : [{ properties: { entryId: 'russia-ukraine' } }]));
      const referencePopup = maplibreMock.MockPopup.byClass('conflict-tension-popup');

      map.trigger('click', { point: { x: 1, y: 2 }, lngLat: { lng: 37, lat: 48 } });

      expect(referencePopup.setHTML).not.toHaveBeenCalled();
      expect(map.queryRenderedFeatures.mock.calls[0][1]?.layers).toEqual([eventLayerId, selectedEventLayerId, firmsLayerId]);
    });
  });

  describe('demo data removal notice and empty state', () => {
    const appInfoPayload = { applicationName: productDisplayName, shell: 'WinUI 3', uiHost: 'WebView2 desktop host', persistence: 'SQLite local application data', productionInboundPortsRequired: false, xPaneSide: 'Right' };

    it('shows the one-time removal notice only when the host reports removed demo Events, and dismisses it', async () => {
      const { emit } = renderWithWebView();
      await latestMap();
      emit({ type: 'app.info', payload: { ...appInfoPayload, demoEventsRemoved: true } });

      const notice = await screen.findByText('Demo Events were removed.');
      expect(notice.closest('[role="status"]')).not.toBeNull();
      fireEvent.click(screen.getByRole('button', { name: 'Dismiss notice' }));
      expect(screen.queryByText('Demo Events were removed.')).not.toBeInTheDocument();

      // A repeated app.info in the same session does not bring a dismissed notice back.
      emit({ type: 'app.info', payload: { ...appInfoPayload, demoEventsRemoved: true } });
      expect(screen.queryByText('Demo Events were removed.')).not.toBeInTheDocument();
    });

    it('shows no notice when the field is false or absent, and rejects a non-boolean value', async () => {
      const { emit, postMessage } = renderWithWebView();
      await latestMap();
      emit({ type: 'app.info', payload: { ...appInfoPayload, demoEventsRemoved: false } });
      emitAppInfo(emit);
      expect(screen.queryByText('Demo Events were removed.')).not.toBeInTheDocument();

      const listRequestsBefore = postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'events.list').length;
      emit({ type: 'app.info', payload: { ...appInfoPayload, demoEventsRemoved: 'yes' } });
      expect(screen.queryByText('Demo Events were removed.')).not.toBeInTheDocument();
      expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'events.list')).toHaveLength(listRequestsBefore);
    });

    it('shows the store-empty state at the all-inclusive criteria with the provider status strip visible', async () => {
      const { emit } = renderWithWebView();
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: [] } });

      expect(await screen.findByText('No Events yet. Automatic refresh is running.')).toBeInTheDocument();
      expect(screen.getByRole('group', { name: 'Provider refresh status' })).toBeInTheDocument();
      expect(screen.queryByText('No events match the active filters.')).not.toBeInTheDocument();
    });

    it('names the narrowed range or severity in the filter-empty message for criteria other than the all-inclusive ones', async () => {
      const { emit } = renderWithWebView();
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: [] } });
      await screen.findByText('No Events yet. Automatic refresh is running.');

      fireEvent.click(screen.getByRole('button', { name: '7D' }));
      expect(await screen.findByText('No Events in the last 7 days. Widen the range to see more.')).toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
      expect(await screen.findByText('No Events yet. Automatic refresh is running.')).toBeInTheDocument();
      fireEvent.change(screen.getByLabelText('Minimum severity'), { target: { value: 'High' } });
      expect(await screen.findByText('No Events at High and above. Widen the severity to see more.')).toBeInTheDocument();
    });

    it('keeps the filter-empty message when matching Events exist but are filtered out', async () => {
      const { emit } = renderWithWebView();
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: nativeEvents } });
      await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent(`${nativeEvents.length} events`));

      fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'no-such-event' } });
      expect(await screen.findByText('No events match the active filters.')).toBeInTheDocument();
      expect(screen.queryByText('No Events yet. Automatic refresh is running.')).not.toBeInTheDocument();
    });
  });

  describe('Events filter startup defaults', () => {
    const allFixtures = () => [...nativeEvents, gdacsEvent, olderEvent];

    it('starts at 30D and High and above and sends them in the first and only startup request', async () => {
      const { emit, listRequests } = renderWithWebView({ filters: 'startup' });
      await latestMap();

      expect(screen.getByRole('button', { name: '30D' })).toHaveClass('is-active');
      expect(screen.getByRole('button', { name: 'ALL' })).not.toHaveClass('is-active');
      expect(screen.getByRole('combobox', { name: 'Minimum severity' })).toHaveDisplayValue('High and above');
      expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue('');
      expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toHaveValue('');
      expect(listRequests()).toEqual([]);

      emitAppInfo(emit);
      expect(listRequests()).toEqual([{ requestId: 1, range: '30D', search: '', minimumSeverity: 'High' }]);
      emit({ type: 'events.list.result', payload: { events: allFixtures() } });
      await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
      expect(screen.getByRole('button', { name: new RegExp(nativeEvents[1].title) })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Earthquake in Russia/ })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: new RegExp(nativeEvents[0].title) })).not.toBeInTheDocument();
    });

    it('restores the full list when ALL and All severities are chosen', async () => {
      const { emit, listRequests } = renderWithWebView({ filters: 'startup' });
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: allFixtures() } });
      await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));

      fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
      fireEvent.change(screen.getByRole('combobox', { name: 'Minimum severity' }), { target: { value: 'All' } });

      await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('4 events'));
      expect(listRequests().at(-1)).toMatchObject({ range: 'ALL', search: '', minimumSeverity: 'All' });
      expect(screen.getByRole('button', { name: 'ALL' })).toHaveClass('is-active');
      expect(screen.getByRole('combobox', { name: 'Minimum severity' })).toHaveDisplayValue('All severities');
    });

    it('keeps the current range and severity across a refresh reload and Watchlist changes', async () => {
      const { emit, listRequests } = renderWithWebView({ filters: 'startup' });
      await latestMap();
      const watchlistId = 'wl-0123456789abcdef0123456789abcdef';
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: allFixtures() } });
      emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: watchlistId, name: 'Baltic security', eventCount: 2 }] } });
      emit(providerStatus({ lastSuccessAtUtc: '2026-09-17T11:00:00Z' }));

      const requestsBeforeReload = listRequests().length;
      emit(providerStatus({ lastSuccessAtUtc: '2026-09-17T11:30:00Z' }));
      expect(listRequests().length).toBe(requestsBeforeReload + 1);
      expect(listRequests().at(-1)).toMatchObject({ range: '30D', minimumSeverity: 'High' });

      fireEvent.click(screen.getByRole('button', { name: '7D' }));
      fireEvent.change(screen.getByRole('combobox', { name: 'Minimum severity' }), { target: { value: 'Medium' } });
      emit(providerStatus({ lastSuccessAtUtc: '2026-09-17T11:45:00Z' }));
      expect(listRequests().at(-1)).toMatchObject({ range: '7D', minimumSeverity: 'Medium' });

      fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: watchlistId } });
      emit({ type: 'watchlists.members.get.result', payload: { watchlistId, eventIds: [nativeEvents[0].id, olderEvent.id] } });
      await waitFor(() => expect(listRequests().at(-1)).toMatchObject({ range: '7D', minimumSeverity: 'Medium', watchlistId }));
      fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: '' } });
      await waitFor(() => expect(listRequests().at(-1)).not.toHaveProperty('watchlistId'));
      expect(listRequests().at(-1)).toMatchObject({ range: '7D', minimumSeverity: 'Medium' });
      expect(screen.getByRole('button', { name: '7D' })).toHaveClass('is-active');
      expect(screen.getByRole('combobox', { name: 'Minimum severity' })).toHaveDisplayValue('Medium and above');
    });

    it('resets to ALL, All severities, All Events and no search on Alert activation, not to the startup defaults', async () => {
      const { emit, listRequests } = renderWithWebView({ filters: 'startup' });
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'alerts.summary.result', payload: { unreadCount: 1 } });
      emit({ type: 'events.list.result', payload: { events: allFixtures() } });
      await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
      fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'shipping' } });

      fireEvent.click(screen.getByRole('button', { name: 'Alerts, 1 unread' }));
      emit({ type: 'alerts.rules.list.result', payload: { rules: [alertRule] } });
      emit({ type: 'alerts.list.result', payload: { alerts: [unreadAlert], unreadCount: 1, hasMore: false } });
      fireEvent.click(await screen.findByRole('button', { name: new RegExp(nativeEvents[0].title) }));

      expect(listRequests().at(-1)).toEqual({ requestId: expect.any(Number), range: 'ALL', search: '', minimumSeverity: 'All', keepEventId: nativeEvents[0].id });
      expect(screen.getByRole('button', { name: 'ALL' })).toHaveClass('is-active');
      expect(screen.getByRole('combobox', { name: 'Minimum severity' })).toHaveDisplayValue('All severities');
      expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue('');
      expect(screen.getByPlaceholderText('Search events, locations, keywords...')).toHaveValue('');
      expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();
    });

    it('names the startup criteria instead of the store-empty state when the store is empty', async () => {
      const { emit } = renderWithWebView({ filters: 'startup' });
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: [] } });

      expect(await screen.findByText('No Events at High and above in the last 30 days. Widen the range or severity to see more.')).toBeInTheDocument();
      expect(screen.queryByText('No Events yet. Automatic refresh is running.')).not.toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
      expect(await screen.findByText('No Events at High and above. Widen the severity to see more.')).toBeInTheDocument();
      fireEvent.change(screen.getByRole('combobox', { name: 'Minimum severity' }), { target: { value: 'All' } });
      expect(await screen.findByText('No Events yet. Automatic refresh is running.')).toBeInTheDocument();
    });
  });

  describe('Alert activation scroll to Details', () => {
    const scrollIntoView = vi.fn();
    // EventPanel also calls scrollIntoView to reveal a selected row; only calls on the Details region count here.
    const detailsScrollCalls = () => scrollIntoView.mock.calls.filter((_, index) => (scrollIntoView.mock.contexts[index] as Element | undefined)?.classList?.contains('details-panel'));

    beforeEach(() => {
      scrollIntoView.mockReset();
      Element.prototype.scrollIntoView = scrollIntoView;
    });

    afterEach(() => {
      Reflect.deleteProperty(window, 'matchMedia');
    });

    async function activateAlertFrom(emit: (data: unknown) => void) {
      emit({ type: 'alerts.summary.result', payload: { unreadCount: 1 } });
      fireEvent.click(await screen.findByRole('button', { name: 'Alerts, 1 unread' }));
      emit({ type: 'alerts.rules.list.result', payload: { rules: [alertRule] } });
      emit({ type: 'alerts.list.result', payload: { alerts: [unreadAlert], unreadCount: 1, hasMore: false } });
      fireEvent.click(await within(screen.getByRole('dialog', { name: 'Alerts' })).findByRole('button', { name: new RegExp(nativeEvents[0].title) }));
      await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Alerts' })).not.toBeInTheDocument());
    }

    it('ships turned off: Alert activation makes no scroll call to Details', async () => {
      expect(alertScrollsToDetails).toBe(false);
      const { emit } = renderWithWebView();
      const map = await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: nativeEvents } });

      await activateAlertFrom(emit);

      expect(screen.getByRole('tab', { name: 'Overview' })).toHaveAttribute('aria-selected', 'true');
      await waitFor(() => expect(map.easeTo).toHaveBeenCalledWith(expect.objectContaining({ center: [24.8, 57.4], zoom: 3.15 })));
      expect(detailsScrollCalls()).toHaveLength(0);
    });

    it('when enabled, scrolls the Details region into view once on Alert activation and keeps the map locate', async () => {
      const { emit } = renderWithWebView({ appProps: { scrollAlertToDetails: true } });
      const map = await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: nativeEvents } });
      await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());

      fireEvent.click(within(screen.getByRole('list', { name: 'event list' })).getByRole('button', { name: /Commercial shipping disruption/i }));
      map.triggerLayer('click', eventLayerId, { features: [{ properties: { id: 'evt-demo-baltic-security' } }], lngLat: { lng: 24.8, lat: 57.4 } });
      await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: '30D' }));
      fireEvent.change(screen.getByLabelText('Minimum severity'), { target: { value: 'Medium' } });
      emit(providerStatus({ state: 'Refreshing' }));
      emit(providerStatus({ state: 'Idle', lastSuccessAtUtc: new Date().toISOString() }));
      expect(detailsScrollCalls()).toHaveLength(0);

      await activateAlertFrom(emit);

      await waitFor(() => expect(detailsScrollCalls()).toHaveLength(1));
      expect(detailsScrollCalls()[0]).toEqual([{ block: 'start', behavior: 'smooth' }]);
      await waitFor(() => expect(map.easeTo).toHaveBeenCalledWith(expect.objectContaining({ center: [24.8, 57.4], zoom: 3.15 })));

      emit({ type: 'events.list.result', payload: { events: nativeEvents } });
      emit(providerStatus({ state: 'Idle', lastSuccessAtUtc: new Date(Date.now() + 60_000).toISOString() }));
      expect(detailsScrollCalls()).toHaveLength(1);
    });

    it('when enabled, scrolls without animation if reduced motion is preferred', async () => {
      Object.defineProperty(window, 'matchMedia', {
        configurable: true,
        value: vi.fn((query: string) => ({ matches: query === '(prefers-reduced-motion: reduce)', media: query, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
      });
      const { emit } = renderWithWebView({ appProps: { scrollAlertToDetails: true } });
      await latestMap();
      emitAppInfo(emit);
      emit({ type: 'events.list.result', payload: { events: nativeEvents } });

      await activateAlertFrom(emit);

      await waitFor(() => expect(detailsScrollCalls()).toEqual([[{ block: 'start', behavior: 'auto' }]]));
    });
  });

  it('clicking event row and marker select the same persisted Event', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(within(screen.getByRole('list', { name: 'event list' })).getByRole('button', { name: /Commercial shipping disruption/i }));
    expect(screen.getByRole('heading', { name: nativeEvents[1].title })).toBeInTheDocument();
    expect(map.setFilter).toHaveBeenCalledWith(selectedEventLayerId, ['==', ['get', 'id'], 'evt-demo-red-sea-shipping']);

    map.triggerLayer('click', eventLayerId, { features: [{ properties: { id: 'evt-demo-baltic-security' } }], lngLat: { lng: 24.8, lat: 57.4 } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
  });

  it('uses the same search-filtered Events for the list and Map markers', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('button', { name: /Commercial shipping disruption/i })).toBeInTheDocument());

    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'SHIPPING' } });

    await waitFor(() => expect(screen.queryByRole('button', { name: /Reported security activity/i })).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: /Commercial shipping disruption/i })).toBeInTheDocument();
    const source = map.getSource(eventSourceId);
    await waitFor(() => expect(source?.data).toMatchObject({
      features: [expect.objectContaining({ properties: expect.objectContaining({ id: 'evt-demo-red-sea-shipping' }) })],
    }));
  });

  it('starts with ALL and applies bounded time ranges to both visible rows and markers', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events'));
    expect(screen.getByRole('button', { name: /Older economic briefing/i })).toBeInTheDocument();
    await waitFor(() => expect(map.getSource(eventSourceId)?.data).toMatchObject({ features: expect.arrayContaining([
      expect.objectContaining({ properties: expect.objectContaining({ id: olderEvent.id }) }),
    ]) }));

    fireEvent.click(screen.getByRole('button', { name: '24H' }));

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
    expect(screen.queryByRole('button', { name: /Older economic briefing/i })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: '30D' }));

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events'));
    expect(screen.getByRole('button', { name: /Older economic briefing/i })).toBeInTheDocument();
    await waitFor(() => expect(map.getSource(eventSourceId)?.data).toMatchObject({ features: expect.arrayContaining([
      expect.objectContaining({ properties: expect.objectContaining({ id: olderEvent.id }) }),
    ]) }));

    fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
    expect(screen.getByLabelText('event summary')).toHaveTextContent('3 events');
  });

  it('shows severity, unverified and provider text in rows and lists the newest Events first', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, gdacsEvent, olderEvent] } });

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('4 events'));
    const rows = within(screen.getByRole('list', { name: 'event list' })).getAllByRole('button');
    expect(rows.map((row) => row.textContent)).toEqual([
      expect.stringContaining('Reported security activity'),
      expect.stringContaining('Commercial shipping disruption'),
      expect.stringContaining('Earthquake in Russia'),
      expect.stringContaining('Older economic briefing'),
    ]);
    expect(within(rows[2]).getByText('Critical')).toHaveClass('severity-badge', 'severity-critical');
    expect(within(rows[2]).getByText('Unverified')).toHaveClass('unverified-badge');
    expect(within(rows[2]).getByText('GDACS')).toBeInTheDocument();
    expect(within(rows[1]).getByText('High')).toBeInTheDocument();
    expect(within(rows[1]).queryByText('Unverified')).not.toBeInTheDocument();
    expect(within(rows[1]).getByText('Demo')).toBeInTheDocument();
    expect(rows.every((row) => !row.textContent?.toLowerCase().includes('hybrid'))).toBe(true);
  });

  it('feeds the map the whole collection while the list renders incrementally and reveals a map-selected row', async () => {
    const scrollIntoView = vi.fn();
    Element.prototype.scrollIntoView = scrollIntoView;
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    const many: NativeEvent[] = Array.from({ length: 250 }, (_, index) => ({
      ...nativeEvents[0],
      id: `evt-bulk-${String(index).padStart(4, '0')}`,
      title: `Bulk event ${index}`,
      lastUpdatedAtUtc: new Date(Date.parse('2026-09-17T12:00:00Z') - index * 60_000).toISOString(),
    }));
    emit({ type: 'events.list.result', payload: { events: many } });

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('250 events'));
    const listItems = () => screen.getByRole('list', { name: 'event list' }).querySelectorAll('[role="listitem"]');
    expect(listItems()).toHaveLength(100);
    await waitFor(() => expect((map.getSource(eventSourceId)?.data as { features: unknown[] }).features).toHaveLength(250));

    map.triggerLayer('click', eventLayerId, { features: [{ properties: { id: 'evt-bulk-0220' } }], lngLat: { lng: 24.8, lat: 57.4 } });

    await waitFor(() => expect(listItems()).toHaveLength(250));
    const revealed = screen.getByRole('list', { name: 'event list' }).querySelector('[data-event-id="evt-bulk-0220"]');
    expect(revealed?.querySelector('button')).toHaveAttribute('aria-pressed', 'true');
    expect(scrollIntoView.mock.contexts).toContain(revealed);
    expect(screen.getByLabelText('event summary')).toHaveTextContent('250 events');
  });

  it('applies the minimum severity filter with search, time range, rows and markers together', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, gdacsEvent, olderEvent] } });
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('4 events'));
    const severityFilter = screen.getByLabelText('Minimum severity');

    fireEvent.change(severityFilter, { target: { value: 'High' } });

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
    expect(screen.queryByRole('button', { name: /Reported security activity/i })).not.toBeInTheDocument();
    await waitFor(() => expect(map.getSource(eventSourceId)?.data).toMatchObject({ features: [
      expect.objectContaining({ properties: expect.objectContaining({ severity: 'High' }) }),
      expect.objectContaining({ properties: expect.objectContaining({ severity: 'Critical' }) }),
    ] }));

    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'shipping' } });
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('1 events'));

    fireEvent.change(severityFilter, { target: { value: 'Critical' } });
    expect(await screen.findByText('No Events at Critical and above match the search. Clear the search or widen the severity to see more.')).toBeInTheDocument();
    await waitFor(() => expect(map.getSource(eventSourceId)?.data).toMatchObject({ features: [] }));

    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'critical' } });
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('1 events'));
    expect(screen.getByRole('button', { name: /Earthquake in Russia/i })).toBeInTheDocument();

    fireEvent.change(severityFilter, { target: { value: 'All' } });
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: '' } });
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('4 events'));
  });

  it('explains provider-derived and manual severity in Details', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, gdacsEvent] } });

    fireEvent.click(await screen.findByRole('button', { name: /Earthquake in Russia/i }));
    const severityBlock = await screen.findByRole('region', { name: 'Severity' });
    expect(severityBlock).toHaveTextContent('GDACS Red alert, magnitude 7.1, Critical');
    expect(severityBlock).toHaveTextContent('Inputs: alert=Red; magnitude=7.1');
    expect(severityBlock).toHaveTextContent('Severity is derived automatically from provider data and is not an independent assessment.');

    fireEvent.click(screen.getByRole('button', { name: /Commercial shipping disruption/i }));
    const manualBlock = await screen.findByRole('region', { name: 'Severity' });
    await waitFor(() => expect(manualBlock).toHaveTextContent('Set manually, not derived from provider data (demo data).'));
    expect(manualBlock).not.toHaveTextContent('derived automatically');
  });

  it('preserves visible selection, clears filtered selection, and does not restore it with the filter', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    const search = screen.getByPlaceholderText('Search events, locations, keywords...');

    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.change(search, { target: { value: 'northern corridor' } });
    expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();

    fireEvent.change(search, { target: { value: 'shipping' } });
    await waitFor(() => expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument());
    expect(screen.getByText('No event selected.')).toBeInTheDocument();

    fireEvent.change(search, { target: { value: '' } });
    expect(await screen.findByRole('button', { name: /Reported security activity/i })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
  });

  it('combines search and time range and shows an empty state without stale details or markers', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: '7D' }));
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'economic' } });

    expect(await screen.findByText('No Events in the last 7 days match the search. Clear the search or widen the range to see more.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
    await waitFor(() => expect(map.getSource(eventSourceId)?.data).toMatchObject({ features: [] }));

    fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
    await waitFor(() => expect(screen.getByRole('button', { name: /Older economic briefing/i })).toBeInTheDocument());
    expect(screen.queryByText(/^No Events in the last 7 days/)).not.toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
  });

  it('details, Sources tab, and X tab render persisted associated SourceItems', async () => {
    const { emit, postMessage } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });

    await waitFor(() => expect(postMessage).toHaveBeenCalledWith({ type: 'events.sources.get', payload: { id: 'evt-demo-baltic-security' } }));
    emit({ type: 'events.sources.result', payload: { eventId: 'evt-demo-baltic-security', sources: balticSources } });

    expect(screen.getByText('Northern corridor')).toBeInTheDocument();
    expect(screen.getAllByText('Security').length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    expect(screen.getByText('Regional demo brief')).toBeInTheDocument();
    expect(screen.getByText('Demo field watcher')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('tab', { name: 'X' }));
    expect(screen.getByText('Demo Field Watcher')).toBeInTheDocument();
    expect(screen.queryByText('Regional demo brief')).not.toBeInTheDocument();
  });

  it('searches ReliefWeb only on explicit action, attaches a candidate, and clears transient results on Event switch', async () => {
    const { emit, postMessage } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: balticSources } });

    const requestsBeforeOpen = postMessage.mock.calls.length;
    fireEvent.click(screen.getByRole('button', { name: 'Find ReliefWeb reports' }));
    expect(postMessage).toHaveBeenCalledTimes(requestsBeforeOpen);
    expect(screen.getByLabelText('Query')).toHaveValue(nativeEvents[0].title);
    expect(screen.getByLabelText('From')).toHaveValue('2026-09-14');
    expect(screen.getByLabelText('To')).toHaveValue('2026-10-01');
    expect(screen.getByLabelText('Country ISO3')).toHaveValue('');

    fireEvent.change(screen.getByLabelText('Query'), { target: { value: '  flood response  ' } });
    fireEvent.change(screen.getByLabelText('Country ISO3'), { target: { value: 'exa' } });
    fireEvent.click(screen.getByRole('button', { name: 'Search reports' }));
    expect(postMessage).toHaveBeenCalledWith({
      type: 'events.sources.reliefweb.search',
      payload: {
        eventId: nativeEvents[0].id,
        queryText: 'flood response',
        fromDateUtc: '2026-09-14T00:00:00.000Z',
        toDateUtc: '2026-10-01T23:59:59.999Z',
        countryIso3: 'EXA',
      },
    });
    expect(screen.getByRole('button', { name: 'Searching...' })).toBeDisabled();

    const candidate = { reportId: '5187658', title: attachedReliefWebSource.title, canonicalUrl: attachedReliefWebSource.url, publishedAtUtc: attachedReliefWebSource.publishedAtUtc, organizations: ['EHO'], formats: ['Situation Report'], countries: ['Example Country'], disasters: [{ id: '30', name: 'Example Flood', glide: 'FL-2026-EXA', types: ['Flood'] }], languages: ['English'] };
    emit({ type: 'events.sources.reliefweb.search.result', payload: { eventId: nativeEvents[0].id, candidates: [candidate], skippedMalformedReports: 0 } });
    expect(await screen.findByText(attachedReliefWebSource.title!)).toBeInTheDocument();
    expect(screen.getByText('EHO')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'View canonical report' })).toHaveAttribute('href', attachedReliefWebSource.url);

    fireEvent.click(screen.getByRole('button', { name: 'Attach' }));
    expect(postMessage).toHaveBeenCalledWith({ type: 'events.sources.reliefweb.attach', payload: { eventId: nativeEvents[0].id, reportId: '5187658' } });
    const sourceRequestCount = postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'events.sources.get').length;
    emit({ type: 'events.sources.reliefweb.attach.result', payload: { eventId: nativeEvents[0].id, reportId: '5187658', disposition: 'CreatedAndAttached', source: attachedReliefWebSource } });
    await waitFor(() => expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'events.sources.get')).toHaveLength(sourceRequestCount + 1));
    expect(screen.getByRole('button', { name: 'Attached' })).toBeDisabled();

    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: [...balticSources, attachedReliefWebSource] } });
    expect(await screen.findByText(/Canonical ReliefWeb report:/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: new RegExp(nativeEvents[1].title) }));
    expect(screen.queryByText(attachedReliefWebSource.title!)).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Query')).not.toBeInTheDocument();
    emit({ type: 'events.sources.reliefweb.search.result', payload: { eventId: nativeEvents[0].id, candidates: [candidate], skippedMalformedReports: 0 } });
    expect(screen.queryByText(attachedReliefWebSource.title!)).not.toBeInTheDocument();
  });

  it('shows unconfigured ReliefWeb state without removing persisted Sources or X attachment', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: balticSources } });
    fireEvent.click(screen.getByRole('button', { name: 'Find ReliefWeb reports' }));
    fireEvent.click(screen.getByRole('button', { name: 'Search reports' }));
    emit({ type: 'events.sources.reliefweb.search.error', payload: { eventId: nativeEvents[0].id, status: 'ConfigurationUnavailable', message: 'ReliefWeb integration is not configured.' } });

    expect(await screen.findByText('ReliefWeb integration is not configured.')).toBeInTheDocument();
    expect(screen.getByText('Regional demo brief')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Attach X post' })).toBeEnabled();
  });

  it('opens and cancels the selected Event X attachment form and rejects an empty URL', async () => {
    const { emit, postMessage } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));

    fireEvent.click(screen.getByRole('button', { name: 'Attach X post' }));
    expect(screen.getByLabelText('X post URL')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Cancel' }));
    expect(screen.queryByLabelText('X post URL')).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Attach X post' }));
    fireEvent.click(screen.getByRole('button', { name: 'Attach' }));
    expect(screen.getByText('Enter an X post URL.')).toBeInTheDocument();
    expect(postMessage).not.toHaveBeenCalledWith(expect.objectContaining({ type: 'events.sources.attachXPost' }));
  });

  it('submits only Event id and URL, refreshes Sources, and resets after a successful attachment', async () => {
    const { emit, postMessage } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: balticSources } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach X post' }));
    fireEvent.change(screen.getByLabelText('X post URL'), { target: { value: '  https://x.com/example/status/1759288472993702104  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach' }));

    expect(postMessage).toHaveBeenCalledWith({
      type: 'events.sources.attachXPost',
      payload: { id: nativeEvents[0].id, url: 'https://x.com/example/status/1759288472993702104' },
    });
    const sourceRequestCount = postMessage.mock.calls.filter(([message]) => (
      (message as { type?: string }).type === 'events.sources.get'
    )).length;
    emit({ type: 'events.sources.attachXPost.result', payload: { eventId: nativeEvents[0].id, disposition: 'CreatedAndAttached', source: attachedXSource } });
    await waitFor(() => expect(postMessage.mock.calls.filter(([message]) => (
      (message as { type?: string }).type === 'events.sources.get'
    ))).toHaveLength(sourceRequestCount + 1));
    expect(screen.queryByLabelText('X post URL')).not.toBeInTheDocument();
    expect(screen.getByText('X post attached.')).toBeInTheDocument();

    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: [...balticSources, attachedXSource] } });
    expect(await screen.findByText(attachedXSource.url!)).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();
    expect(screen.getByText('3 linked sources')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('tab', { name: 'X' }));
    expect(screen.getByText(/URL-only source/)).toBeInTheDocument();
    expect(screen.getByText(attachedXSource.url!)).toBeInTheDocument();
  });

  it.each([
    ['ExistingSourceAttached', 'Existing X post attached.'],
    ['AlreadyAttached', 'This X post is already attached.'],
  ] as const)('handles %s without duplicating source rows', async (disposition, expectedMessage) => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: [...balticSources, attachedXSource] } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach X post' }));
    fireEvent.change(screen.getByLabelText('X post URL'), { target: { value: attachedXSource.url } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach' }));

    emit({ type: 'events.sources.attachXPost.result', payload: { eventId: nativeEvents[0].id, disposition, source: attachedXSource } });
    emit({ type: 'events.sources.result', payload: { eventId: nativeEvents[0].id, sources: [...balticSources, attachedXSource] } });

    expect(await screen.findByText(expectedMessage)).toBeInTheDocument();
    expect(screen.getAllByText(attachedXSource.url!)).toHaveLength(1);
  });

  it('shows bounded attachment errors and keeps the form available for correction', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    await waitFor(() => expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument());
    fireEvent.click(screen.getByRole('tab', { name: 'Sources' }));
    fireEvent.click(screen.getByRole('button', { name: 'Attach X post' }));
    fireEvent.change(screen.getByLabelText('X post URL'), { target: { value: 'https://x.com/home' } });
    fireEvent.click(screen.getByRole('button', { name: 'Attach' }));
    emit({ type: 'events.sources.attachXPost.error', payload: { eventId: nativeEvents[0].id, message: 'Enter a public X status URL.' } });

    expect(await screen.findByText('Enter a public X status URL.')).toBeInTheDocument();
    expect(screen.getByLabelText('X post URL')).toHaveValue('https://x.com/home');
    expect(screen.queryByText(/stack trace/i)).not.toBeInTheDocument();
  });

  it('event-load failure shows a bounded error without frontend event fallback', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.error', payload: { message: 'SQL stack trace should not appear' } });

    await waitFor(() => expect(screen.getAllByText('Unable to load local event data.').length).toBeGreaterThan(0));
    expect(screen.queryByText('Reported security activity near Baltic border area')).not.toBeInTheDocument();
    expect(screen.queryByText('SQL stack trace should not appear')).not.toBeInTheDocument();
  });

  it.each([
    ['Orange', 'orange'],
    ['Blue', 'blue'],
    ['Red', 'red'],
    ['Green', 'green'],
    ['Yellow', 'yellow'],
  ] as const)('applies %s accent returned from native preference storage', async (nativeValue, accent) => {
    const { emit } = renderWithWebView();
    await latestMap();
    emit({ type: 'preferences.value', payload: { key: 'AccentTheme', value: nativeValue } });

    await waitFor(() => expect(document.documentElement.dataset.accent).toBe(accent));
    fireEvent.click(screen.getByRole('button', { name: 'Settings' }));
    expect(screen.getByLabelText(nativeValue)).toBeChecked();
  });

  it('switches every accent theme through native persistence without changing semantic markers', async () => {
    const { postMessage } = renderWithWebView();
    const map = await latestMap();
    const accents = [
      ['Blue', 'blue', '#22d3ff'],
      ['Red', 'red', '#ff4d5e'],
      ['Green', 'green', '#3dff87'],
      ['Yellow', 'yellow', '#ffe45c'],
      ['Orange', 'orange', '#ff8a1f'],
    ] as const;

    fireEvent.click(screen.getByRole('button', { name: 'Settings' }));
    for (const [nativeValue, accent, color] of accents) {
      fireEvent.click(screen.getByLabelText(nativeValue));

      await waitFor(() => expect(document.documentElement.dataset.accent).toBe(accent));
      expect(postMessage).toHaveBeenCalledWith({ type: 'preferences.set', payload: { key: 'AccentTheme', value: nativeValue } });
      expect(map.setPaintProperty).toHaveBeenCalledWith(selectedEventLayerId, 'circle-stroke-color', color);
      expect(map.setPaintProperty).toHaveBeenCalledWith(selectedEventLayerId, 'circle-color', color);
    }

    expect(window.localStorage.getItem('geopolitical-monitor.accent')).toBeNull();
    expect(getCategoryColor('MilitaryConflict')).toBe('#d95d62');
    expect(getCategoryColor('Security')).toBe('#6da3d9');
  });

  it('applies the active watchlist before existing filters to both the event list and map', async () => {
    const { postMessage, emit } = renderWithWebView();
    const map = await latestMap();
    const watchlistId = 'wl-0123456789abcdef0123456789abcdef';
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: [...nativeEvents, olderEvent] } });
    emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: watchlistId, name: 'Baltic security', eventCount: 2 }] } });

    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: watchlistId } });
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.members.get', payload: { watchlistId } });
    emit({ type: 'watchlists.members.get.result', payload: { watchlistId, eventIds: [nativeEvents[0].id, olderEvent.id] } });

    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('2 events'));
    expect(screen.getByRole('button', { name: new RegExp(nativeEvents[0].title) })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: new RegExp(olderEvent.title) })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: new RegExp(nativeEvents[1].title) })).not.toBeInTheDocument();
    await waitFor(() => {
      const data = map.getSource(eventSourceId)?.data as { features?: Array<{ properties?: { id?: string } }> };
      expect(data.features?.map((feature) => feature.properties?.id)).toEqual([nativeEvents[0].id, olderEvent.id]);
    });

    fireEvent.click(screen.getByRole('button', { name: '7D' }));
    await waitFor(() => expect(screen.getByLabelText('event summary')).toHaveTextContent('1 events'));
    expect(screen.queryByRole('button', { name: new RegExp(olderEvent.title) })).not.toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'older economic' } });
    expect(await screen.findByText('No watchlist Events in the last 7 days match the search. Clear the search or widen the range to see more.')).toBeInTheDocument();
    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'ALL' }));
    expect(await screen.findByRole('button', { name: new RegExp(olderEvent.title) })).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText('Search events, locations, keywords...'), { target: { value: 'shipping' } });
    expect(await screen.findByText('No watchlist events match the active filters.')).toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: '' } });
    expect(await screen.findByRole('button', { name: new RegExp(nativeEvents[1].title) })).toBeInTheDocument();
  });

  it('creates, renames, and confirms deletion through the watchlist manager', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    const watchlistId = 'wl-fedcba9876543210fedcba9876543210';
    emitAppInfo(emit);
    emit({ type: 'watchlists.list.result', payload: { watchlists: [] } });
    emit({ type: 'alerts.summary.result', payload: { unreadCount: 0 } });
    fireEvent.click(screen.getByRole('button', { name: 'Alerts' }));
    emit({ type: 'alerts.rules.list.result', payload: { rules: [] } });
    emit({ type: 'alerts.list.result', payload: { alerts: [], unreadCount: 0, hasMore: false } });
    fireEvent.click(screen.getByRole('button', { name: 'Close Alerts' }));

    fireEvent.click(screen.getByRole('button', { name: 'Manage' }));
    const dialog = screen.getByRole('dialog', { name: 'Manage watchlists' });
    expect(within(dialog).getByText('No watchlists. Create one to group monitored events.')).toBeInTheDocument();
    fireEvent.change(within(dialog).getByLabelText('New watchlist'), { target: { value: 'Baltic security' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Create' }));
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.create', payload: { name: 'Baltic security' } });
    emit({ type: 'watchlists.create.result', payload: { watchlist: { id: watchlistId, name: 'Baltic security', eventCount: 0 } } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue(watchlistId);
    expect(screen.getByText('No events in this watchlist.')).toBeInTheDocument();

    fireEvent.click(within(dialog).getByRole('button', { name: 'Rename' }));
    fireEvent.change(within(dialog).getByLabelText('Watchlist name'), { target: { value: 'Northern security' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }));
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.rename', payload: { watchlistId, name: 'Northern security' } });
    emit({ type: 'watchlists.rename.result', payload: { disposition: 'Updated', watchlist: { id: watchlistId, name: 'Northern security', eventCount: 0 } } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveDisplayValue('Northern security');

    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    expect(within(dialog).getByText('Delete “Northern security”? Events and sources stay; its Alert monitoring and history will be deleted.')).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole('button', { name: 'Delete' }));
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.delete', payload: { watchlistId } });
    const alertsBeforeDelete = postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type?.startsWith('alerts.')).length;
    emit({ type: 'watchlists.delete.result', payload: { watchlistId, disposition: 'Deleted' } });
    expect(within(dialog).getByText('Watchlist deleted. Events were kept.')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue('');
    expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type?.startsWith('alerts.'))).toHaveLength(alertsBeforeDelete + 3);
  });

  it('loads and changes the selected event membership with an explicit bridge request', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    const watchlistId = 'wl-0123456789abcdef0123456789abcdef';
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: watchlistId, name: 'Baltic security', eventCount: 0 }] } });

    await waitFor(() => expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.memberships.getForEvent', payload: { eventId: nativeEvents[0].id } }));
    emit({ type: 'watchlists.memberships.getForEvent.result', payload: { eventId: nativeEvents[0].id, watchlistIds: [] } });
    const membership = await screen.findByRole('checkbox', { name: /Baltic security/ });
    fireEvent.click(membership);
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.members.set', payload: { watchlistId, eventId: nativeEvents[0].id, isMember: true } });
    emit({ type: 'watchlists.members.set.result', payload: { watchlistId, eventId: nativeEvents[0].id, isMember: true, disposition: 'Added', eventCount: 1 } });
    expect(membership).toBeChecked();
    expect(screen.getByText('Event added to watchlist.')).toBeInTheDocument();
  });

  it('supports multiple memberships and clears selection only when removal leaves the active scope', async () => {
    const { postMessage, emit } = renderWithWebView();
    await latestMap();
    const activeId = 'wl-0123456789abcdef0123456789abcdef';
    const otherId = 'wl-fedcba9876543210fedcba9876543210';
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    emit({ type: 'watchlists.list.result', payload: { watchlists: [
      { id: activeId, name: 'Baltic security', eventCount: 1 },
      { id: otherId, name: 'Priority', eventCount: 1 },
    ] } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: activeId } });
    emit({ type: 'watchlists.members.get.result', payload: { watchlistId: activeId, eventIds: [nativeEvents[0].id] } });
    emit({ type: 'watchlists.memberships.getForEvent.result', payload: { eventId: nativeEvents[0].id, watchlistIds: [activeId, otherId] } });

    const activeMembership = await screen.findByRole('checkbox', { name: /Baltic security/ });
    const otherMembership = screen.getByRole('checkbox', { name: /Priority/ });
    expect(activeMembership).toBeChecked();
    expect(otherMembership).toBeChecked();

    fireEvent.click(otherMembership);
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.members.set', payload: { watchlistId: otherId, eventId: nativeEvents[0].id, isMember: false } });
    emit({ type: 'watchlists.members.set.result', payload: { watchlistId: otherId, eventId: nativeEvents[0].id, isMember: false, disposition: 'Removed', eventCount: 0 } });
    expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();

    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: otherId } });
    expect(postMessage).toHaveBeenCalledWith({ type: 'watchlists.members.get', payload: { watchlistId: otherId } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: activeId } });
    emit({ type: 'watchlists.members.get.result', payload: { watchlistId: otherId, eventIds: [nativeEvents[1].id] } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue(activeId);

    fireEvent.click(activeMembership);
    emit({ type: 'watchlists.members.set.result', payload: { watchlistId: activeId, eventId: nativeEvents[0].id, isMember: false, disposition: 'Removed', eventCount: 0 } });
    expect(await screen.findByText('No events in this watchlist.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: '' } });
    expect(screen.getByRole('heading', { name: 'No event selected' })).toBeInTheDocument();
  });

  it('deleting an inactive watchlist preserves active scope while deleting the active one returns to All Events', async () => {
    const { emit } = renderWithWebView();
    await latestMap();
    const activeId = 'wl-0123456789abcdef0123456789abcdef';
    const otherId = 'wl-fedcba9876543210fedcba9876543210';
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    emit({ type: 'watchlists.list.result', payload: { watchlists: [
      { id: activeId, name: 'Baltic security', eventCount: 1 },
      { id: otherId, name: 'Priority', eventCount: 0 },
    ] } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: activeId } });
    emit({ type: 'watchlists.members.get.result', payload: { watchlistId: activeId, eventIds: [nativeEvents[0].id] } });

    fireEvent.click(screen.getAllByRole('button', { name: 'Manage' })[0]);
    let dialog = screen.getByRole('dialog', { name: 'Manage watchlists' });
    const priorityRow = within(dialog).getByRole('button', { name: /Priority/ }).closest('.watchlist-manager-item')!;
    fireEvent.click(within(priorityRow as HTMLElement).getByRole('button', { name: 'Delete' }));
    fireEvent.click(within(priorityRow as HTMLElement).getByRole('button', { name: 'Delete' }));
    emit({ type: 'watchlists.delete.result', payload: { watchlistId: otherId, disposition: 'Deleted' } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue(activeId);

    dialog = screen.getByRole('dialog', { name: 'Manage watchlists' });
    const activeRow = within(dialog).getByRole('button', { name: /Baltic security/ }).closest('.watchlist-manager-item')!;
    fireEvent.click(within(activeRow as HTMLElement).getByRole('button', { name: 'Delete' }));
    fireEvent.click(within(activeRow as HTMLElement).getByRole('button', { name: 'Delete' }));
    emit({ type: 'watchlists.delete.result', payload: { watchlistId: activeId, disposition: 'Deleted' } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue('');
    expect(screen.getByRole('heading', { name: nativeEvents[0].title })).toBeInTheDocument();
  });

  it('reloads persisted watchlists after frontend reinitialization but starts again at All Events', async () => {
    const watchlistId = 'wl-0123456789abcdef0123456789abcdef';
    const first = renderWithWebView();
    await latestMap();
    emitAppInfo(first.emit);
    first.emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    first.emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: watchlistId, name: 'Baltic security', eventCount: 1 }] } });
    fireEvent.change(screen.getByRole('combobox', { name: 'Event scope' }), { target: { value: watchlistId } });
    first.emit({ type: 'watchlists.members.get.result', payload: { watchlistId, eventIds: [nativeEvents[0].id] } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue(watchlistId);
    first.unmount();

    const second = renderWithWebView();
    await latestMap();
    emitAppInfo(second.emit);
    second.emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    second.emit({ type: 'watchlists.list.result', payload: { watchlists: [{ id: watchlistId, name: 'Baltic security', eventCount: 1 }] } });
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveValue('');
    expect(screen.getByRole('combobox', { name: 'Event scope' })).toHaveDisplayValue('All Events');
    expect(second.postMessage).toHaveBeenCalledWith({ type: 'watchlists.list', payload: {} });
  });


  it('uses the Events layer toggle to hide event markers', async () => {
    render(<App />);
    const map = await latestMap();
    fireEvent.click(screen.getByLabelText(/Events/));

    await waitFor(() => {
      expect(map.setLayoutProperty).toHaveBeenCalledWith(eventLayerId, 'visibility', 'none');
      expect(map.setLayoutProperty).toHaveBeenCalledWith(selectedEventLayerId, 'visibility', 'none');
    });
  });

  it('starts FIRMS hidden and never refreshes from startup or visibility changes', async () => {
    const { postMessage } = renderWithWebView();
    const map = await latestMap();
    const firmsToggle = screen.getByLabelText(/NASA FIRMS — Fire \/ Thermal Anomalies/);

    expect(firmsToggle).not.toBeChecked();
    expect(map.getSource(firmsSourceId)).toBeTruthy();
    expect(map.getLayer(firmsLayerId)).toBeTruthy();
    expect(postMessage.mock.calls.some(([message]) => (message as { type?: string }).type === 'providers.firms.refresh')).toBe(false);

    fireEvent.click(firmsToggle);
    fireEvent.click(firmsToggle);
    map.trigger('moveend');
    expect(postMessage.mock.calls.some(([message]) => (message as { type?: string }).type === 'providers.firms.refresh')).toBe(false);
    expect(map.setLayoutProperty).toHaveBeenCalledWith(firmsLayerId, 'visibility', 'visible');
    expect(map.setLayoutProperty).toHaveBeenCalledWith(firmsLayerId, 'visibility', 'none');
    expect(map.setLayoutProperty).not.toHaveBeenCalledWith(eventLayerId, 'visibility', 'none');
  });

  it('sends current viewport only on explicit FIRMS refresh and prevents duplicate refresh', async () => {
    const { postMessage } = renderWithWebView();
    await latestMap();
    const refresh = screen.getByRole('button', { name: 'Refresh FIRMS' });

    fireEvent.click(refresh);
    expect(postMessage).toHaveBeenCalledWith({
      type: 'providers.firms.refresh',
      payload: { west: 10, south: 20, east: 30, north: 40 },
    });
    expect(screen.getByRole('button', { name: 'Refreshing FIRMS...' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Refreshing FIRMS...' }));
    expect(postMessage.mock.calls.filter(([message]) => (message as { type?: string }).type === 'providers.firms.refresh')).toHaveLength(1);
  });

  it('defaults to Significant and switches display subsets locally without changing the snapshot or Events', async () => {
    const { postMessage, emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    emit(firmsFilterResult);

    const filter = screen.getByRole('combobox', { name: 'FIRMS display filter' });
    const firmsSource = map.getSource(firmsSourceId)!;
    const eventSource = map.getSource(eventSourceId)!;
    expect(filter).toHaveValue('significant');
    await waitFor(() => expect((firmsSource.data as { features: unknown[] }).features).toHaveLength(2));
    expect(screen.getByLabelText('FIRMS snapshot summary')).toHaveTextContent('2 shown · 4 detections · 24h');
    const originalEventData = eventSource.data;
    const refreshCount = postMessage.mock.calls.filter(([message]) => (
      message as { type?: string }
    ).type === 'providers.firms.refresh').length;

    firmsSource.setData.mockClear();
    fireEvent.change(filter, { target: { value: 'strong' } });
    await waitFor(() => expect((firmsSource.data as { features: unknown[] }).features).toHaveLength(1));
    expect(firmsSource.setData).toHaveBeenCalled();
    expect(screen.getByLabelText('FIRMS snapshot summary')).toHaveTextContent('1 shown · 4 detections · 24h');

    fireEvent.change(filter, { target: { value: 'all' } });
    await waitFor(() => expect((firmsSource.data as { features: unknown[] }).features).toHaveLength(4));
    expect(screen.getByLabelText('FIRMS snapshot summary')).toHaveTextContent('4 shown · 4 detections · 24h');
    expect(eventSource.data).toBe(originalEventData);
    expect(postMessage.mock.calls.filter(([message]) => (
      message as { type?: string }
    ).type === 'providers.firms.refresh')).toHaveLength(refreshCount);
  });

  it('retains the current display filter across successful and failed refreshes', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    const filter = screen.getByRole('combobox', { name: 'FIRMS display filter' });
    fireEvent.change(filter, { target: { value: 'strong' } });
    emit(firmsFilterResult);

    const source = map.getSource(firmsSourceId)!;
    await waitFor(() => expect((source.data as { features: unknown[] }).features).toHaveLength(1));
    emit({ type: 'providers.firms.refresh.error', payload: { status: 'RequestFailed', message: 'NASA FIRMS refresh failed. Existing layer data was preserved.' } });
    expect((source.data as { features: unknown[] }).features).toHaveLength(1);
    expect(filter).toHaveValue('strong');

    emit(firmsResult);
    await waitFor(() => expect((source.data as { features: unknown[] }).features).toHaveLength(0));
    expect(filter).toHaveValue('strong');
    expect(screen.getByLabelText('FIRMS snapshot summary')).toHaveTextContent('0 shown · 1 detection · 24h');
  });

  it('replaces the transient FIRMS source atomically, preserves it on failure, and keeps Event priority', async () => {
    const { emit } = renderWithWebView();
    const map = await latestMap();
    emitAppInfo(emit);
    emit({ type: 'events.list.result', payload: { events: nativeEvents } });
    fireEvent.click(screen.getByLabelText(/NASA FIRMS — Fire \/ Thermal Anomalies/));
    emit(firmsResult);

    const source = map.getSource(firmsSourceId);
    await waitFor(() => expect(source?.data).toMatchObject({ features: [expect.objectContaining({ id: firmsObservation.id })] }));
    expect(screen.getByLabelText('FIRMS snapshot summary')).toHaveTextContent('1 shown · 1 detection · 24h');
    expect(screen.getByText(/Snapshot refreshed 15:06 UTC.*newest 14:37 UTC/)).toBeInTheDocument();
    expect(screen.getByLabelText('FIRMS detection age legend')).toBeInTheDocument();

    const order = [...map.layers.keys()];
    expect(order.indexOf(firmsLayerId)).toBeLessThan(order.indexOf(eventLayerId));
    expect(order.indexOf(eventLayerId)).toBeLessThan(order.indexOf(selectedEventLayerId));

    const selectedHeading = await screen.findByRole('heading', { name: nativeEvents[0].title });
    map.triggerLayer('click', firmsLayerId, {
      features: [{ properties: firmsObservation }],
      lngLat: { lng: firmsObservation.longitude, lat: firmsObservation.latitude },
    });
    expect(selectedHeading).toBeInTheDocument();
    const firmsPopup = maplibreMock.MockPopup.byClass('firms-map-popup');
    expect(firmsPopup.setHTML).toHaveBeenCalledWith(expect.stringContaining('NASA FIRMS — Fire / Thermal Anomaly'));
    expect(firmsPopup.setHTML).toHaveBeenCalledWith(expect.stringContaining('NASA FIRMS / LANCE'));

    emit({ type: 'providers.firms.refresh.error', payload: { status: 'RequestFailed', message: 'NASA FIRMS refresh failed. Existing layer data was preserved.' } });
    expect(await screen.findByText(/Existing layer data was preserved/)).toBeInTheDocument();
    expect(source?.data).toMatchObject({ features: [expect.objectContaining({ id: firmsObservation.id })] });

    emit({
      type: 'providers.firms.refresh.result',
      payload: {
        ...firmsResult.payload,
        observations: [],
        observationCount: 0,
        oldestAcquiredAtUtc: null,
        newestAcquiredAtUtc: null,
        refreshedAtUtc: '2026-09-28T15:10:00Z',
      },
    });
    await waitFor(() => expect(source?.data).toMatchObject({ features: [] }));
  });

  it.each([
    ['ConfigurationUnavailable', 'NASA FIRMS is not configured.'],
    ['AreaTooLarge', 'Zoom in to a smaller area before refreshing NASA FIRMS.'],
    ['ResponseTooLarge', 'NASA FIRMS returned too much data for this area. Zoom in and refresh again.'],
    ['TooManyObservations', 'Too many FIRMS detections for this area. Zoom in and refresh again.'],
  ])('shows bounded FIRMS %s state', async (status, message) => {
    const { emit } = renderWithWebView();
    await latestMap();
    emit({ type: 'providers.firms.refresh.error', payload: { status, message } });
    expect(await screen.findByText(message)).toBeInTheDocument();
  });
});
