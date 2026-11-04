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
  isProviderRefreshStatusMessage,
  isGdacsRefreshErrorMessage,
  isGdacsRefreshResultMessage,
  isFirmsKeyStatusMessage,
  isFirmsRefreshErrorMessage,
  isFirmsRefreshResultMessage,
  requestFirmsKeyRemove,
  requestFirmsKeySet,
  requestFirmsKeyStatus,
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
  requestProviderRefreshStatus,
  requestEventsList,
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

  it('strictly validates provider refresh status pushes', () => {
    const provider = {
      provider: 'Gdacs',
      displayName: 'GDACS',
      state: 'Failed',
      lastSuccessAtUtc: '2026-09-30T12:01:00+00:00',
      nextRefreshAtUtc: '2026-09-30T12:31:00+00:00',
      message: 'Request timed out. Existing events were preserved.',
      newAlerts: 0,
    };
    const accepts = (providers: unknown, extra: Record<string, unknown> = {}) =>
      isProviderRefreshStatusMessage({ type: 'providers.refresh.status', payload: { providers, ...extra } });

    expect(accepts([provider])).toBe(true);
    expect(accepts([])).toBe(true);
    for (const state of ['Idle', 'Refreshing', 'Failed', 'Stale', 'Unconfigured']) {
      expect(accepts([{ ...provider, state }])).toBe(true);
    }
    expect(accepts([{ ...provider, lastSuccessAtUtc: null, nextRefreshAtUtc: null, message: null }])).toBe(true);

    expect(accepts([{ ...provider, state: 'Paused' }])).toBe(false);
    expect(accepts([{ ...provider, provider: 'Gdelt' }])).toBe(false);
    expect(accepts([{ ...provider, message: 'Exception: stack trace at C:\\secret' }])).toBe(false);
    expect(accepts([{ ...provider, url: 'https://www.gdacs.org' }])).toBe(false);
    expect(accepts([{ ...provider, newAlerts: -1 }])).toBe(false);
    expect(accepts([{ ...provider, newAlerts: 1.5 }])).toBe(false);
    expect(accepts([{ ...provider, lastSuccessAtUtc: 'yesterday' }])).toBe(false);
    expect(accepts([{ ...provider, displayName: '' }])).toBe(false);
    expect(accepts([provider, provider])).toBe(false);
    expect(accepts([provider], { extra: true })).toBe(false);
    expect(accepts('Gdacs')).toBe(false);
    expect(isProviderRefreshStatusMessage({ type: 'providers.refresh.status', payload: { providers: [provider] }, extra: 1 })).toBe(false);
    expect(isProviderRefreshStatusMessage({ type: 'providers.refresh.status.get', payload: { providers: [provider] } })).toBe(false);
  });

  it('requests provider refresh status with an empty payload', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };

    expect(requestProviderRefreshStatus()).toBe(true);
    expect(postMessage).toHaveBeenCalledWith({ type: 'providers.refresh.status.get', payload: {} });
  });

  it('strictly validates windowed event list responses', () => {
    const event = {
      id: 'evt-one', title: 'Title', summary: 'Summary', category: 'Security', status: 'Active',
      startedAtUtc: '2026-09-17T10:00:00Z', lastUpdatedAtUtc: '2026-09-17T10:05:00Z',
      location: { latitude: 1, longitude: 2, displayName: 'Place', countryCode: null, region: null },
      createdAtUtc: '2026-09-17T09:00:00Z', updatedAtUtc: '2026-09-17T10:05:00Z', sourceCount: 0,
      severity: 'Low', severityRule: 'manual', severityInputs: 'test', provider: 'Manual',
    };
    const accepts = (payload: Record<string, unknown>) => isEventsListResultMessage({ type: 'events.list.result', payload });

    expect(accepts({ events: [event], totalCount: 4812, capped: true, requestId: 9 })).toBe(true);
    expect(accepts({ events: [], totalCount: 0, capped: false, requestId: null })).toBe(true);
    expect(accepts({ events: [event] })).toBe(false);
    expect(accepts({ events: [event], totalCount: 1, capped: false })).toBe(false);
    expect(accepts({ events: [event], totalCount: 1, capped: false, requestId: 1, extra: true })).toBe(false);
    expect(accepts({ events: [event], totalCount: 0, capped: false, requestId: 1 })).toBe(false);
    expect(accepts({ events: [event], totalCount: -1, capped: false, requestId: 1 })).toBe(false);
    expect(accepts({ events: [event], totalCount: 1.5, capped: false, requestId: 1 })).toBe(false);
    expect(accepts({ events: [event], totalCount: 1, capped: 'no', requestId: 1 })).toBe(false);
    expect(accepts({ events: [event], totalCount: 1, capped: false, requestId: '1' })).toBe(false);
    expect(accepts({ events: [event], totalCount: 1, capped: false, requestId: -2 })).toBe(false);
  });

  it('posts typed windowed list criteria, omitting absent ids and bounding search', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } };

    expect(requestEventsList()).toBe(true);
    expect(requestEventsList({ requestId: 4, range: '7D', search: 'x'.repeat(260), minimumSeverity: 'High' })).toBe(true);
    expect(requestEventsList({ requestId: 5, range: 'ALL', search: '', minimumSeverity: 'All', watchlistId: 'wl-00000000000000000000000000000001', keepEventId: 'evt-one' })).toBe(true);

    expect(postMessage).toHaveBeenNthCalledWith(1, { type: 'events.list', payload: {} });
    expect(postMessage).toHaveBeenNthCalledWith(2, { type: 'events.list', payload: { requestId: 4, range: '7D', search: 'x'.repeat(200), minimumSeverity: 'High' } });
    expect(postMessage).toHaveBeenNthCalledWith(3, { type: 'events.list', payload: { requestId: 5, range: 'ALL', search: '', minimumSeverity: 'All', watchlistId: 'wl-00000000000000000000000000000001', keepEventId: 'evt-one' } });
  });

  it('strictly validates event severity, provider and field set', () => {
    const valid = {
      id: 'evt-gdacs-eq-1',
      title: 'Earthquake',
      summary: 'Summary',
      category: 'NaturalDisaster',
      status: 'Unverified',
      startedAtUtc: '2026-09-17T10:00:00Z',
      lastUpdatedAtUtc: '2026-09-17T10:05:00Z',
      location: { latitude: 1, longitude: 2, displayName: 'Place', countryCode: null, region: null },
      createdAtUtc: '2026-09-17T09:00:00Z',
      updatedAtUtc: '2026-09-17T10:05:00Z',
      sourceCount: 1,
      severity: 'Catastrophic',
      severityRule: 'gdacs-eq-v1',
      severityInputs: 'alert=Red; magnitude=7.8',
      provider: 'Gdacs',
    };
    const accepts = (event: unknown) => isEventsListResultMessage({ type: 'events.list.result', payload: { events: [event], totalCount: 1, capped: false, requestId: 1 } });
    const missingSeverity: Record<string, unknown> = { ...valid };
    delete missingSeverity.severity;

    expect(accepts(valid)).toBe(true);
    for (const severity of ['Low', 'Medium', 'High', 'Critical', 'Catastrophic']) {
      expect(accepts({ ...valid, severity })).toBe(true);
    }
    for (const provider of ['Gdacs', 'UcdpCandidate', 'Gdelt', 'Demo', 'Manual']) {
      expect(accepts({ ...valid, provider })).toBe(true);
    }
    expect(accepts({ ...valid, severity: 'Severe' })).toBe(false);
    expect(accepts({ ...valid, severity: 'high' })).toBe(false);
    expect(accepts({ ...valid, provider: 'Acled' })).toBe(false);
    expect(accepts({ ...valid, provider: null })).toBe(false);
    expect(accepts({ ...valid, providerKey: '1' })).toBe(false);
    expect(accepts({ ...valid, unexpected: true })).toBe(false);
    expect(accepts(missingSeverity)).toBe(false);
    expect(accepts({ ...valid, severityRule: '' })).toBe(false);
    expect(accepts({ ...valid, severityRule: 'r'.repeat(65) })).toBe(false);
    expect(accepts({ ...valid, severityInputs: 'i'.repeat(257) })).toBe(false);
    expect(accepts({ ...valid, severityInputs: 7 })).toBe(false);
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
      severity: 'High',
      severityRule: 'gdacs-eq-v1',
      severityInputs: 'alert=Orange; magnitude=6.4',
      provider: 'Gdacs',
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

    expect(isEventsListResultMessage({ type: 'events.list.result', payload: { events: [eventPayload], totalCount: 1, capped: false, requestId: 3 } })).toBe(true);
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
    const appInfo = { applicationName: 'Geopolitical Monitor', shell: 'WinUI 3', uiHost: 'WebView2 desktop host', persistence: 'SQLite local application data', productionInboundPortsRequired: false, xPaneSide: 'Right' };
    expect(isAppInfoMessage({ type: 'app.info', payload: { ...appInfo, demoEventsRemoved: true } })).toBe(true);
    expect(isAppInfoMessage({ type: 'app.info', payload: { ...appInfo, demoEventsRemoved: false } })).toBe(true);
    expect(isAppInfoMessage({ type: 'app.info', payload: { ...appInfo, demoEventsRemoved: 'true' } })).toBe(false);
    expect(isAppInfoMessage({ type: 'app.info', payload: { ...appInfo, demoEventsRemoved: 1 } })).toBe(false);
    expect(isAppInfoMessage({ type: 'app.info', payload: { applicationName: 'Geopolitical Monitor', shell: 'WinUI 3', uiHost: 'WebView2', persistence: 'SQLite', productionInboundPortsRequired: false, xPaneSide: 'Center' } })).toBe(false);
    expect(isAppInfoMessage({ type: 'native.execute', payload: {} })).toBe(false);
    expect(isPreferenceValueMessage({ type: 'preferences.value', payload: { key: 'AccentTheme', value: 'Purple' } })).toBe(false);
    expect(isXPaneSideValueMessage({ type: 'xPane.side.value', payload: { value: 'Center' } })).toBe(false);
    expect(isPreferenceUpdatedMessage({ type: 'preferences.updated', payload: { key: 'Unknown', value: 'Blue' } })).toBe(false);
    expect(isPreferenceErrorMessage({ type: 'preferences.error', payload: { key: 'AccentTheme' } })).toBe(false);
    expect(isEventsListResultMessage({ type: 'events.list.result', payload: { events: [{ id: 'evt-demo', category: 'Security' }], totalCount: 1, capped: false, requestId: 1 } })).toBe(false);
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

describe('NASA FIRMS key status bridge', () => {
  const status = { type: 'providers.firms.key.status', payload: { configured: true, source: 'App', formatWarning: false, storeUnavailable: false, outcome: null } };

  it('accepts only exact status payloads with no key field', () => {
    expect(isFirmsKeyStatusMessage(status)).toBe(true);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, outcome: 'Saved' } })).toBe(true);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { configured: false, source: 'None', formatWarning: false, storeUnavailable: true, outcome: 'StoreUnavailable' } })).toBe(true);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, key: 'fake-key-zz-never-real-zz-0000' } })).toBe(false);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, mapKey: null } })).toBe(false);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, source: 'Clipboard' } })).toBe(false);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, outcome: 'Revealed' } })).toBe(false);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, configured: 'true' } })).toBe(false);
    expect(isFirmsKeyStatusMessage({ ...status, payload: { ...status.payload, configured: false } })).toBe(false);
    expect(isFirmsKeyStatusMessage({ ...status, extra: true })).toBe(false);
  });

  it('accepts the categorized FIRMS refresh errors', () => {
    for (const statusName of ['KeyRejected', 'RateLimited', 'TimedOut', 'ProductUnavailable']) {
      expect(isFirmsRefreshErrorMessage({ type: 'providers.firms.refresh.error', payload: { status: statusName, message: 'Bounded message.' } })).toBe(true);
    }
  });

  it('sends key requests with an empty payload only', () => {
    const postMessage = vi.fn();
    window.chrome = { webview: { postMessage, addEventListener: vi.fn(), removeEventListener: vi.fn() } } as unknown as typeof window.chrome;

    expect(requestFirmsKeyStatus()).toBe(true);
    expect(requestFirmsKeySet()).toBe(true);
    expect(requestFirmsKeyRemove()).toBe(true);
    expect(postMessage.mock.calls.map(([message]) => message)).toEqual([
      { type: 'providers.firms.key.status.get', payload: {} },
      { type: 'providers.firms.key.set', payload: {} },
      { type: 'providers.firms.key.remove', payload: {} },
    ]);
    delete window.chrome;
  });
});
