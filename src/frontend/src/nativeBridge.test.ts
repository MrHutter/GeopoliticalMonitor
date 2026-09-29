import {
  accentToNativeValue,
  attachXPostSourceToEvent,
  attachReliefWebReport,
  createWatchlist,
  deleteWatchlist,
  isAppInfoMessage,
  isAlertErrorMessage,
  isAlertMarkReadResultMessage,
  isAlertRuleSetResultMessage,
  isAlertRulesListResultMessage,
  isAlertsListResultMessage,
  isAlertSummaryResultMessage,
  isAttachXPostErrorMessage,
  isAttachXPostResultMessage,
  isPreferenceErrorMessage,
  isEventSourcesResultMessage,
  isEventsErrorMessage,
  isEventsListResultMessage,
  isEventWatchlistMembershipsResultMessage,
  isGdacsRefreshErrorMessage,
  isGdacsRefreshResultMessage,
  isFirmsRefreshErrorMessage,
  isFirmsRefreshResultMessage,
  isReliefWebAttachResultMessage,
  isReliefWebSearchErrorMessage,
  isReliefWebSearchResultMessage,
  isPreferenceUpdatedMessage,
  isPreferenceValueMessage,
  isWatchlistCreateResultMessage,
  isWatchlistDeleteResultMessage,
  isWatchlistErrorMessage,
  isWatchlistMembershipSetResultMessage,
  isWatchlistMembersResultMessage,
  isWatchlistRenameResultMessage,
  isWatchlistsListResultMessage,
  isXPaneSideValueMessage,
  nativeValueToAccent,
  markAlertRead,
  requestAlertRules,
  requestAlertsList,
  requestAlertSummary,
  requestGdacsRefresh,
  requestFirmsRefresh,
  requestEventWatchlistMemberships,
  requestWatchlistMembers,
  requestWatchlistsList,
  renameWatchlist,
  searchReliefWebReports,
  setWatchlistMembership,
  setAlertRuleEnabled,
} from './nativeBridge';

describe('nativeBridge', () => {
  it('accepts app.info payloads', () => {
    expect(
      isAppInfoMessage({
        type: 'app.info',
        payload: {
          applicationName: 'Geopolitical Monitor',
          shell: 'WinUI 3',
          uiHost: 'WebView2 desktop host',
          persistence: 'SQLite local application data',
          productionInboundPortsRequired: false,
          xPaneSide: 'Right',
        },
      }),
    ).toBe(true);
  });

  it('accepts typed preference and X pane side payloads', () => {
    const accents = [
      ['Orange', 'orange'],
      ['Blue', 'blue'],
      ['Red', 'red'],
      ['Green', 'green'],
      ['Yellow', 'yellow'],
    ] as const;

    accents.forEach(([nativeValue, accent]) => {
      expect(isPreferenceValueMessage({ type: 'preferences.value', payload: { key: 'AccentTheme', value: nativeValue } })).toBe(true);
      expect(isPreferenceUpdatedMessage({ type: 'preferences.updated', payload: { key: 'AccentTheme', value: nativeValue } })).toBe(true);
      expect(nativeValueToAccent(nativeValue)).toBe(accent);
      expect(accentToNativeValue(accent)).toBe(nativeValue);
    });

    expect(isPreferenceErrorMessage({ type: 'preferences.error', payload: { key: 'AccentTheme', message: 'Nope' } })).toBe(true);
    expect(isXPaneSideValueMessage({ type: 'xPane.side.value', payload: { value: 'Left' } })).toBe(true);
    expect(isXPaneSideValueMessage({ type: 'xPane.side.value', payload: { value: 'Right' } })).toBe(true);
  });

  it('accepts typed event and source payloads', () => {
    const eventPayload = {
      id: 'evt-demo',
      title: 'Demo event',
      summary: 'Demo summary',
      category: 'Security',
      status: 'Active',
      startedAtUtc: '2026-09-17T10:00:00Z',
      lastUpdatedAtUtc: '2026-09-17T10:05:00Z',
      location: { latitude: 57.4, longitude: 24.8, displayName: 'Northern corridor' },
      createdAtUtc: '2026-09-17T09:00:00Z',
      updatedAtUtc: '2026-09-17T10:05:00Z',
      sourceCount: 2,
    };
    const sourcePayload = {
      id: 'src-demo',
      sourceType: 'XPost',
      title: 'Demo source',
      text: 'Demo text',
      publishedAtUtc: '2026-09-17T10:00:00Z',
      retrievedAtUtc: '2026-09-17T10:02:00Z',
      location: null,
    };

    expect(isEventsListResultMessage({ type: 'events.list.result', payload: { events: [eventPayload] } })).toBe(true);
    expect(isEventSourcesResultMessage({ type: 'events.sources.result', payload: { eventId: 'evt-demo', sources: [sourcePayload] } })).toBe(true);
    expect(isAttachXPostResultMessage({ type: 'events.sources.attachXPost.result', payload: { eventId: 'evt-demo', disposition: 'CreatedAndAttached', source: sourcePayload } })).toBe(true);
    expect(isAttachXPostErrorMessage({ type: 'events.sources.attachXPost.error', payload: { eventId: 'evt-demo', message: 'Unable to attach.' } })).toBe(true);
    expect(isEventsErrorMessage({ type: 'events.error', payload: { message: 'Unable to load local event data.' } })).toBe(true);
  });

  it('sends only the selected Event id and pasted URL for X post attachment', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };

    expect(attachXPostSourceToEvent('evt-demo', 'https://x.com/example/status/1759288472993702104')).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({
      type: 'events.sources.attachXPost',
      payload: { id: 'evt-demo', url: 'https://x.com/example/status/1759288472993702104' },
    });

    delete window.chrome;
  });

  it('sends the fixed empty GDACS refresh request and validates typed responses', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };

    expect(requestGdacsRefresh()).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({ type: 'providers.gdacs.refresh', payload: {} });
    expect(isGdacsRefreshResultMessage({
      type: 'providers.gdacs.refresh.result',
      payload: { status: 'Succeeded', newEvents: 1, updatedEvents: 0, newSources: 1, updatedSources: 0, unchangedRecords: 0, skippedMalformedRecords: 1, conflicts: 0, newAlerts: 0 },
    })).toBe(true);
    expect(isGdacsRefreshErrorMessage({ type: 'providers.gdacs.refresh.error', payload: { status: 'TimedOut', message: 'Timed out.' } })).toBe(true);
    expect(isGdacsRefreshResultMessage({ type: 'providers.gdacs.refresh.result', payload: { status: 'Succeeded', newEvents: -1 } })).toBe(false);

    delete window.chrome;
  });

  it('sends narrow alert requests and validates bounded typed responses', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };
    const watchlistId = 'wl-0123456789abcdef0123456789abcdef';
    const ruleId = 'ar-0123456789abcdef0123456789abcdef';
    const alertId = 'al-fedcba9876543210fedcba9876543210';
    const rule = { id: ruleId, watchlistId, triggerType: 'GdacsEventMateriallyUpdated', isEnabled: true, createdAtUtc: '2026-09-27T10:00:00Z', lastEnabledAtUtc: '2026-09-27T10:00:00Z' };
    const alert = { id: alertId, alertRuleId: ruleId, eventId: 'evt-demo', watchlistId, eventTitle: 'Updated event', watchlistName: 'Priority', triggerType: 'GdacsEventMateriallyUpdated', providerRevisionAtUtc: '2026-09-27T11:00:00Z', triggeredAtUtc: '2026-09-27T11:01:00Z', readAtUtc: null };

    expect(requestAlertSummary()).toBe(true);
    expect(requestAlertRules()).toBe(true);
    expect(setAlertRuleEnabled(watchlistId, true)).toBe(true);
    expect(requestAlertsList()).toBe(true);
    expect(markAlertRead(alertId)).toBe(true);
    expect(postMessage.mock.calls.map(([message]) => message)).toEqual([
      { type: 'alerts.summary', payload: {} },
      { type: 'alerts.rules.list', payload: {} },
      { type: 'alerts.rules.set', payload: { watchlistId, enabled: true } },
      { type: 'alerts.list', payload: {} },
      { type: 'alerts.markRead', payload: { alertId } },
    ]);

    expect(isAlertSummaryResultMessage({ type: 'alerts.summary.result', payload: { unreadCount: 3 } })).toBe(true);
    expect(isAlertRulesListResultMessage({ type: 'alerts.rules.list.result', payload: { rules: [rule] } })).toBe(true);
    expect(isAlertRuleSetResultMessage({ type: 'alerts.rules.set.result', payload: { disposition: 'CreatedEnabled', rule } })).toBe(true);
    expect(isAlertsListResultMessage({ type: 'alerts.list.result', payload: { alerts: [alert], unreadCount: 1, hasMore: false } })).toBe(true);
    expect(isAlertMarkReadResultMessage({ type: 'alerts.markRead.result', payload: { alertId, readAtUtc: '2026-09-27T11:02:00Z', disposition: 'Updated' } })).toBe(true);
    expect(isAlertErrorMessage({ type: 'alerts.error', payload: { operation: 'alerts.list', code: 'PersistenceUnavailable', message: 'Alerts are unavailable.', watchlistId: null, alertId: null } })).toBe(true);

    expect(isAlertSummaryResultMessage({ type: 'alerts.summary.result', payload: { unreadCount: -1 } })).toBe(false);
    expect(isAlertRulesListResultMessage({ type: 'alerts.rules.list.result', payload: { rules: Array(101).fill(rule) } })).toBe(false);
    expect(isAlertRuleSetResultMessage({ type: 'alerts.rules.set.result', payload: { disposition: 'WatchlistNotFound', rule } })).toBe(false);
    expect(isAlertsListResultMessage({ type: 'alerts.list.result', payload: { alerts: [{ ...alert, unexpected: true }], unreadCount: 1, hasMore: false } })).toBe(false);
    expect(isAlertMarkReadResultMessage({ type: 'alerts.markRead.result', payload: { alertId, readAtUtc: null, disposition: 'Updated' } })).toBe(false);

    delete window.chrome;
  });

  it('sends narrow ReliefWeb requests and validates bounded typed responses', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };

    expect(searchReliefWebReports('evt-demo', 'flood', '2026-09-01T00:00:00Z', '2026-09-30T00:00:00Z', 'EXA')).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({
      type: 'events.sources.reliefweb.search',
      payload: { eventId: 'evt-demo', queryText: 'flood', fromDateUtc: '2026-09-01T00:00:00Z', toDateUtc: '2026-09-30T00:00:00Z', countryIso3: 'EXA' },
    });
    expect(attachReliefWebReport('evt-demo', '5187658')).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({ type: 'events.sources.reliefweb.attach', payload: { eventId: 'evt-demo', reportId: '5187658' } });

    const candidate = { reportId: '5187658', title: 'Flood report', canonicalUrl: 'https://reliefweb.int/report/example/flood', publishedAtUtc: '2026-09-20T00:00:00Z', organizations: ['EHO'], formats: ['Situation Report'], countries: ['Example'], disasters: [{ id: '1', name: 'Flood', glide: null, types: ['Flood'] }], languages: ['English'] };
    expect(isReliefWebSearchResultMessage({ type: 'events.sources.reliefweb.search.result', payload: { eventId: 'evt-demo', candidates: [candidate], skippedMalformedReports: 0 } })).toBe(true);
    expect(isReliefWebSearchErrorMessage({ type: 'events.sources.reliefweb.search.error', payload: { eventId: 'evt-demo', status: 'ConfigurationUnavailable', message: 'ReliefWeb integration is not configured.' } })).toBe(true);
    expect(isReliefWebAttachResultMessage({ type: 'events.sources.reliefweb.attach.result', payload: { eventId: 'evt-demo', reportId: '5187658', disposition: 'CreatedAndAttached', source: { id: 'src-reliefweb-report-5187658', sourceType: 'Other', text: 'ReliefWeb report metadata.', publishedAtUtc: '2026-09-20T00:00:00Z', retrievedAtUtc: '2026-09-24T00:00:00Z', location: null } } })).toBe(true);
    expect(isReliefWebSearchResultMessage({ type: 'events.sources.reliefweb.search.result', payload: { eventId: 'evt-demo', candidates: Array(21).fill(candidate), skippedMalformedReports: 0 } })).toBe(false);

    delete window.chrome;
  });

  it('sends narrow watchlist requests and validates bounded typed responses', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };
    const watchlistId = 'wl-0123456789abcdef0123456789abcdef';
    const summary = { id: watchlistId, name: 'Baltic security', eventCount: 1 };

    requestWatchlistsList();
    expect(createWatchlist('Baltic security')).toBe(true);
    expect(renameWatchlist(watchlistId, 'Northern security')).toBe(true);
    expect(deleteWatchlist(watchlistId)).toBe(true);
    expect(requestWatchlistMembers(watchlistId)).toBe(true);
    expect(requestEventWatchlistMemberships('evt-demo')).toBe(true);
    expect(setWatchlistMembership(watchlistId, 'evt-demo', true)).toBe(true);

    expect(postMessage.mock.calls.map(([message]) => message)).toEqual([
      { type: 'watchlists.list', payload: {} },
      { type: 'watchlists.create', payload: { name: 'Baltic security' } },
      { type: 'watchlists.rename', payload: { watchlistId, name: 'Northern security' } },
      { type: 'watchlists.delete', payload: { watchlistId } },
      { type: 'watchlists.members.get', payload: { watchlistId } },
      { type: 'watchlists.memberships.getForEvent', payload: { eventId: 'evt-demo' } },
      { type: 'watchlists.members.set', payload: { watchlistId, eventId: 'evt-demo', isMember: true } },
    ]);
    expect(isWatchlistsListResultMessage({ type: 'watchlists.list.result', payload: { watchlists: [summary] } })).toBe(true);
    expect(isWatchlistCreateResultMessage({ type: 'watchlists.create.result', payload: { watchlist: summary } })).toBe(true);
    expect(isWatchlistRenameResultMessage({ type: 'watchlists.rename.result', payload: { disposition: 'Updated', watchlist: summary } })).toBe(true);
    expect(isWatchlistDeleteResultMessage({ type: 'watchlists.delete.result', payload: { watchlistId, disposition: 'Deleted' } })).toBe(true);
    expect(isWatchlistMembersResultMessage({ type: 'watchlists.members.get.result', payload: { watchlistId, eventIds: ['evt-demo'] } })).toBe(true);
    expect(isEventWatchlistMembershipsResultMessage({ type: 'watchlists.memberships.getForEvent.result', payload: { eventId: 'evt-demo', watchlistIds: [watchlistId] } })).toBe(true);
    expect(isWatchlistMembershipSetResultMessage({ type: 'watchlists.members.set.result', payload: { watchlistId, eventId: 'evt-demo', isMember: true, disposition: 'Added', eventCount: 1 } })).toBe(true);
    expect(isWatchlistErrorMessage({ type: 'watchlists.error', payload: { operation: 'watchlists.create', code: 'NameConflict', message: 'A watchlist with that name already exists.', watchlistId: null, eventId: null } })).toBe(true);

    expect(isWatchlistsListResultMessage({ type: 'watchlists.list.result', payload: { watchlists: Array(101).fill(summary) } })).toBe(false);
    expect(isWatchlistMembersResultMessage({ type: 'watchlists.members.get.result', payload: { watchlistId, eventIds: Array(2001).fill('evt-demo') } })).toBe(false);
    expect(isWatchlistMembershipSetResultMessage({ type: 'watchlists.members.set.result', payload: { watchlistId, eventId: 'evt-demo', isMember: false, disposition: 'Added', eventCount: 1 } })).toBe(false);
    expect(isWatchlistCreateResultMessage({ type: 'watchlists.create.result', payload: { watchlist: { ...summary, unexpected: true } } })).toBe(false);

    delete window.chrome;
  });

  it('rejects malformed messages', () => {
    expect(isAppInfoMessage({ type: 'app.info', payload: { shell: 'WinUI 3' } })).toBe(false);
    expect(isAppInfoMessage({ type: 'app.info', payload: { applicationName: 'Geopolitical Monitor', shell: 'WinUI 3', uiHost: 'WebView2', persistence: 'SQLite', productionInboundPortsRequired: false, xPaneSide: 'Center' } })).toBe(false);
    expect(isAppInfoMessage({ type: 'native.execute', payload: {} })).toBe(false);
    expect(isPreferenceValueMessage({ type: 'preferences.value', payload: { key: 'AccentTheme', value: 'Purple' } })).toBe(false);
    expect(isXPaneSideValueMessage({ type: 'xPane.side.value', payload: { value: 'Center' } })).toBe(false);
    expect(isPreferenceUpdatedMessage({ type: 'preferences.updated', payload: { key: 'Unknown', value: 'Blue' } })).toBe(false);
    expect(isPreferenceErrorMessage({ type: 'preferences.error', payload: { key: 'AccentTheme' } })).toBe(false);
    expect(isEventsListResultMessage({ type: 'events.list.result', payload: { events: [{ id: 'evt-demo', category: 'Security' }] } })).toBe(false);
    expect(isEventSourcesResultMessage({ type: 'events.sources.result', payload: { eventId: 'evt-demo', sources: [{ id: 'src-demo', sourceType: 'Unknown' }] } })).toBe(false);
    expect(isAttachXPostResultMessage({ type: 'events.sources.attachXPost.result', payload: { eventId: 'evt-demo', disposition: 'Unknown', source: {} } })).toBe(false);
    expect(isAttachXPostErrorMessage({ type: 'events.sources.attachXPost.error', payload: { eventId: 'evt-demo' } })).toBe(false);
  });

  it('sends only FIRMS viewport bounds and accepts a strict result', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };
    expect(requestFirmsRefresh({ west: 10, south: 20, east: 30, north: 40 })).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({
      type: 'providers.firms.refresh',
      payload: { west: 10, south: 20, east: 30, north: 40 },
    });

    const result = {
      type: 'providers.firms.refresh.result',
      payload: {
        observations: [{
          id: 'firms-0123456789abcdef0123456789abcdef',
          latitude: 60.1,
          longitude: 24.5,
          acquiredAtUtc: '2026-09-28T14:37:00Z',
          source: 'VIIRS_NOAA20_NRT',
          satellite: '1',
          confidence: 'Nominal',
          frp: 18.6,
          brightnessTemperatureI4: 331.2,
          brightnessTemperatureI5: 290.4,
          scan: 0.42,
          track: 0.51,
          processingVersion: '2.0NRT',
          dayNight: 'Day',
        }],
        observationCount: 1,
        refreshedAtUtc: '2026-09-28T15:06:00Z',
        oldestAcquiredAtUtc: '2026-09-28T14:37:00Z',
        newestAcquiredAtUtc: '2026-09-28T14:37:00Z',
        coverage: { west: 10, south: 20, east: 30, north: 40 },
        skippedMalformedRecords: 0,
      },
    };

    expect(isFirmsRefreshResultMessage(result)).toBe(true);
    expect(isFirmsRefreshResultMessage({ ...result, payload: { ...result.payload, unexpected: true } })).toBe(false);
    expect(isFirmsRefreshResultMessage({ ...result, payload: { ...result.payload, coverage: { west: Number.NaN, south: 20, east: 30, north: 40 } } })).toBe(false);
    expect(isFirmsRefreshResultMessage({ ...result, payload: { ...result.payload, refreshedAtUtc: 'not-a-time' } })).toBe(false);
    expect(isFirmsRefreshResultMessage({ ...result, payload: { ...result.payload, observations: [{ ...result.payload.observations[0], frp: Number.NaN }] } })).toBe(false);
    expect(isFirmsRefreshResultMessage({ ...result, payload: { ...result.payload, observations: [{ ...result.payload.observations[0], extra: true }] } })).toBe(false);

    expect(isFirmsRefreshErrorMessage({
      type: 'providers.firms.refresh.error',
      payload: { status: 'ConfigurationUnavailable', message: 'NASA FIRMS is not configured.' },
    })).toBe(true);
    expect(isFirmsRefreshErrorMessage({
      type: 'providers.firms.refresh.error',
      payload: { status: 'Unknown', message: 'bad' },
    })).toBe(false);
    delete window.chrome;
  });
});
