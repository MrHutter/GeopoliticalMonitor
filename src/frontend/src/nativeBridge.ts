import type { NativeEvent, NativeSourceItem, NativeEventCategory, NativeEventProvider, NativeEventSeverity, NativeEventStatus, NativeSourceType, ReliefWebReportCandidate } from './types/events';
import type { Accent } from './types/mock';

export const nativeAccentValues = ['Orange', 'Blue', 'Red', 'Green', 'Yellow'] as const;
export type NativeAccentValue = (typeof nativeAccentValues)[number];
export const xPaneSideValues = ['Left', 'Right'] as const;
export type XPaneSide = (typeof xPaneSideValues)[number];

export type AppInfoMessage = {
  type: 'app.info';
  payload: {
    applicationName: string;
    shell: string;
    uiHost: string;
    persistence: string;
    productionInboundPortsRequired: boolean;
    xPaneSide: XPaneSide;
  };
};

export type XPaneSideValueMessage = {
  type: 'xPane.side.value';
  payload: { value: XPaneSide };
};

export type PreferenceValueMessage = {
  type: 'preferences.value';
  payload: { key: 'AccentTheme'; value: NativeAccentValue };
};

export type PreferenceUpdatedMessage = {
  type: 'preferences.updated';
  payload: { key: 'AccentTheme'; value: NativeAccentValue };
};

export type PreferenceErrorMessage = {
  type: 'preferences.error';
  payload: { key: 'AccentTheme'; message: string };
};

export type EventsListResultMessage = {
  type: 'events.list.result';
  payload: { events: NativeEvent[] };
};

export type EventSourcesResultMessage = {
  type: 'events.sources.result';
  payload: { eventId: string; sources: NativeSourceItem[] };
};

export const attachXPostDispositions = ['CreatedAndAttached', 'ExistingSourceAttached', 'AlreadyAttached'] as const;
export type AttachXPostDisposition = (typeof attachXPostDispositions)[number];

export type AttachXPostResultMessage = {
  type: 'events.sources.attachXPost.result';
  payload: { eventId: string; disposition: AttachXPostDisposition; source: NativeSourceItem };
};

export type AttachXPostErrorMessage = {
  type: 'events.sources.attachXPost.error';
  payload: { eventId: string; message: string };
};

export type EventsErrorMessage = {
  type: 'events.error';
  payload: { message: string };
};

export const gdacsRefreshStatuses = ['Succeeded', 'AlreadyInProgress'] as const;
export type GdacsRefreshResultMessage = {
  type: 'providers.gdacs.refresh.result';
  payload: {
    status: (typeof gdacsRefreshStatuses)[number];
    newEvents: number;
    updatedEvents: number;
    newSources: number;
    updatedSources: number;
    unchangedRecords: number;
    skippedMalformedRecords: number;
    conflicts: number;
    newAlerts: number;
  };
};

export type GdacsRefreshErrorMessage = {
  type: 'providers.gdacs.refresh.error';
  payload: { status: string; message: string };
};

export const scheduledProviderIds = ['Gdacs'] as const;
export type ScheduledProviderId = (typeof scheduledProviderIds)[number];
export const providerRefreshStates = ['Idle', 'Refreshing', 'Failed', 'Stale', 'Unconfigured'] as const;
export type ProviderRefreshState = (typeof providerRefreshStates)[number];
export const providerStatusMessages = [
  'Up to date.',
  'Provider unavailable. Existing events were preserved.',
  'Request timed out. Existing events were preserved.',
  'Unsupported provider response. No provider data was changed.',
  'Provider data could not be saved. Existing events were preserved.',
  'Not configured.',
] as const;
export type ProviderStatusMessage = (typeof providerStatusMessages)[number];

export type ProviderRefreshStatus = {
  provider: ScheduledProviderId;
  displayName: string;
  state: ProviderRefreshState;
  lastSuccessAtUtc: string | null;
  nextRefreshAtUtc: string | null;
  message: ProviderStatusMessage | null;
  newAlerts: number;
};

export type ProviderRefreshStatusMessage = {
  type: 'providers.refresh.status';
  payload: { providers: ProviderRefreshStatus[] };
};

export type FirmsViewport = { west: number; south: number; east: number; north: number };

export type FirmsObservation = {
  id: string;
  latitude: number;
  longitude: number;
  acquiredAtUtc: string;
  source: 'VIIRS_NOAA20_NRT' | 'VIIRS_NOAA21_NRT';
  satellite: string;
  confidence: 'Low' | 'Nominal' | 'High';
  frp: number;
  brightnessTemperatureI4: number;
  brightnessTemperatureI5: number | null;
  scan: number;
  track: number;
  processingVersion: string;
  dayNight: 'Day' | 'Night';
};

export type FirmsRefreshResultMessage = {
  type: 'providers.firms.refresh.result';
  payload: {
    observations: FirmsObservation[];
    observationCount: number;
    refreshedAtUtc: string;
    oldestAcquiredAtUtc: string | null;
    newestAcquiredAtUtc: string | null;
    coverage: FirmsViewport;
    skippedMalformedRecords: number;
  };
};

export const firmsRefreshErrorStatuses = [
  'AlreadyInProgress', 'ConfigurationUnavailable', 'InvalidViewport', 'AreaTooLarge',
  'RequestFailed', 'ResponseTooLarge', 'InvalidProviderResponse', 'TooManyObservations', 'Cancelled',
] as const;

export type FirmsRefreshErrorMessage = {
  type: 'providers.firms.refresh.error';
  payload: { status: (typeof firmsRefreshErrorStatuses)[number]; message: string };
};

export type ReliefWebSearchResultMessage = {
  type: 'events.sources.reliefweb.search.result';
  payload: { eventId: string; candidates: ReliefWebReportCandidate[]; skippedMalformedReports: number };
};

export type ReliefWebSearchErrorMessage = {
  type: 'events.sources.reliefweb.search.error';
  payload: { eventId: string; status: string; message: string };
};

export const reliefWebAttachDispositions = ['CreatedAndAttached', 'ExistingSourceAttached', 'AlreadyAttached'] as const;
export type ReliefWebAttachResultMessage = {
  type: 'events.sources.reliefweb.attach.result';
  payload: {
    eventId: string;
    reportId: string;
    disposition: (typeof reliefWebAttachDispositions)[number];
    source: NativeSourceItem;
  };
};

export type ReliefWebAttachErrorMessage = {
  type: 'events.sources.reliefweb.attach.error';
  payload: { eventId: string; reportId: string; status: string; message: string };
};

export const watchlistRenameDispositions = ['Updated', 'Unchanged'] as const;
export const watchlistDeleteDispositions = ['Deleted'] as const;
export const watchlistMembershipDispositions = ['Added', 'AlreadyMember', 'Removed', 'AlreadyAbsent'] as const;
export const watchlistErrorCodes = ['NameConflict', 'WatchlistNotFound', 'EventNotFound', 'WatchlistLimitReached', 'MembershipLimitReached', 'PersistenceUnavailable'] as const;

export type WatchlistSummary = {
  id: string;
  name: string;
  eventCount: number;
};

export type WatchlistsListResultMessage = {
  type: 'watchlists.list.result';
  payload: { watchlists: WatchlistSummary[] };
};

export type WatchlistCreateResultMessage = {
  type: 'watchlists.create.result';
  payload: { watchlist: WatchlistSummary };
};

export type WatchlistRenameResultMessage = {
  type: 'watchlists.rename.result';
  payload: { disposition: (typeof watchlistRenameDispositions)[number]; watchlist: WatchlistSummary };
};

export type WatchlistDeleteResultMessage = {
  type: 'watchlists.delete.result';
  payload: { watchlistId: string; disposition: (typeof watchlistDeleteDispositions)[number] };
};

export type WatchlistMembersResultMessage = {
  type: 'watchlists.members.get.result';
  payload: { watchlistId: string; eventIds: string[] };
};

export type EventWatchlistMembershipsResultMessage = {
  type: 'watchlists.memberships.getForEvent.result';
  payload: { eventId: string; watchlistIds: string[] };
};

export type WatchlistMembershipSetResultMessage = {
  type: 'watchlists.members.set.result';
  payload: {
    watchlistId: string;
    eventId: string;
    isMember: boolean;
    disposition: (typeof watchlistMembershipDispositions)[number];
    eventCount: number;
  };
};

export type WatchlistErrorMessage = {
  type: 'watchlists.error';
  payload: {
    operation: string;
    code: (typeof watchlistErrorCodes)[number];
    message: string;
    watchlistId: string | null;
    eventId: string | null;
  };
};

export const alertTriggerTypes = ['GdacsEventMateriallyUpdated'] as const;
export const alertRuleSetDispositions = ['CreatedEnabled', 'Enabled', 'Disabled', 'Unchanged', 'RuleNotFound', 'WatchlistNotFound'] as const;
export const alertMarkReadDispositions = ['Updated', 'AlreadyRead', 'NotFound'] as const;
export const alertErrorCodes = ['PersistenceUnavailable'] as const;

export type AlertRule = {
  id: string;
  watchlistId: string;
  triggerType: (typeof alertTriggerTypes)[number];
  isEnabled: boolean;
  createdAtUtc: string;
  lastEnabledAtUtc: string;
};

export type AlertOccurrence = {
  id: string;
  alertRuleId: string;
  eventId: string;
  watchlistId: string;
  eventTitle: string;
  watchlistName: string;
  triggerType: (typeof alertTriggerTypes)[number];
  providerRevisionAtUtc: string;
  triggeredAtUtc: string;
  readAtUtc: string | null;
};

export type AlertSummaryResultMessage = {
  type: 'alerts.summary.result';
  payload: { unreadCount: number };
};

export type AlertRulesListResultMessage = {
  type: 'alerts.rules.list.result';
  payload: { rules: AlertRule[] };
};

export type AlertRuleSetResultMessage = {
  type: 'alerts.rules.set.result';
  payload: {
    disposition: (typeof alertRuleSetDispositions)[number];
    rule: AlertRule | null;
  };
};

export type AlertsListResultMessage = {
  type: 'alerts.list.result';
  payload: { alerts: AlertOccurrence[]; unreadCount: number; hasMore: boolean };
};

export type AlertMarkReadResultMessage = {
  type: 'alerts.markRead.result';
  payload: {
    alertId: string;
    readAtUtc: string | null;
    disposition: (typeof alertMarkReadDispositions)[number];
  };
};

export type AlertErrorMessage = {
  type: 'alerts.error';
  payload: {
    operation: string;
    code: (typeof alertErrorCodes)[number];
    message: string;
    watchlistId: string | null;
    alertId: string | null;
  };
};

const accentToNative: Record<Accent, NativeAccentValue> = {
  orange: 'Orange',
  blue: 'Blue',
  red: 'Red',
  green: 'Green',
  yellow: 'Yellow',
};

const nativeToAccent: Record<NativeAccentValue, Accent> = {
  Orange: 'orange',
  Blue: 'blue',
  Red: 'red',
  Green: 'green',
  Yellow: 'yellow',
};

const eventCategories: NativeEventCategory[] = ['MilitaryConflict', 'Security', 'PoliticalDiplomatic', 'CivilUnrest', 'Infrastructure', 'Humanitarian', 'NaturalDisaster', 'Economic', 'Other'];
const eventStatuses: NativeEventStatus[] = ['Developing', 'Active', 'Resolved', 'Unverified'];
const eventSeverityValues: NativeEventSeverity[] = ['Low', 'Medium', 'High', 'Critical', 'Catastrophic'];
const eventProviders: NativeEventProvider[] = ['Gdacs', 'UcdpCandidate', 'Gdelt', 'Demo', 'Manual'];
const nativeEventKeys = [
  'id', 'title', 'summary', 'category', 'status', 'startedAtUtc', 'lastUpdatedAtUtc', 'location', 'createdAtUtc', 'updatedAtUtc', 'sourceCount',
  'severity', 'severityRule', 'severityInputs', 'provider',
] as const;
const sourceTypes: NativeSourceType[] = ['XPost', 'NewsArticle', 'OfficialStatement', 'DatasetRecord', 'Other'];

export function accentToNativeValue(accent: Accent): NativeAccentValue {
  return accentToNative[accent];
}

export function nativeValueToAccent(value: NativeAccentValue): Accent {
  return nativeToAccent[value];
}

export function sendAppReady() {
  window.chrome?.webview?.postMessage({ type: 'app.ready', payload: {} });
}

export function requestAccentPreference() {
  window.chrome?.webview?.postMessage({ type: 'preferences.get', payload: { key: 'AccentTheme' } });
}

export function sendAccentPreference(accent: Accent) {
  window.chrome?.webview?.postMessage({
    type: 'preferences.set',
    payload: { key: 'AccentTheme', value: accentToNativeValue(accent) },
  });
}

export function requestEventsList() {
  window.chrome?.webview?.postMessage({ type: 'events.list', payload: {} });
}

export function requestWatchlistsList() {
  window.chrome?.webview?.postMessage({ type: 'watchlists.list', payload: {} });
}

export function requestAlertSummary() {
  return postNativeMessage({ type: 'alerts.summary', payload: {} });
}

export function requestAlertRules() {
  return postNativeMessage({ type: 'alerts.rules.list', payload: {} });
}

export function setAlertRuleEnabled(watchlistId: string, enabled: boolean) {
  return postNativeMessage({ type: 'alerts.rules.set', payload: { watchlistId, enabled } });
}

export function requestAlertsList() {
  return postNativeMessage({ type: 'alerts.list', payload: {} });
}

export function markAlertRead(alertId: string) {
  return postNativeMessage({ type: 'alerts.markRead', payload: { alertId } });
}

export function createWatchlist(name: string) {
  return postWatchlistMessage({ type: 'watchlists.create', payload: { name } });
}

export function renameWatchlist(watchlistId: string, name: string) {
  return postWatchlistMessage({ type: 'watchlists.rename', payload: { watchlistId, name } });
}

export function deleteWatchlist(watchlistId: string) {
  return postWatchlistMessage({ type: 'watchlists.delete', payload: { watchlistId } });
}

export function requestWatchlistMembers(watchlistId: string) {
  return postWatchlistMessage({ type: 'watchlists.members.get', payload: { watchlistId } });
}

export function requestEventWatchlistMemberships(eventId: string) {
  return postWatchlistMessage({ type: 'watchlists.memberships.getForEvent', payload: { eventId } });
}

export function setWatchlistMembership(watchlistId: string, eventId: string, isMember: boolean) {
  return postWatchlistMessage({ type: 'watchlists.members.set', payload: { watchlistId, eventId, isMember } });
}

export function requestProviderRefreshStatus() {
  return postNativeMessage({ type: 'providers.refresh.status.get', payload: {} });
}

export function isProviderRefreshStatusMessage(value: unknown): value is ProviderRefreshStatusMessage {
  if (!isMessage(value, 'providers.refresh.status', ['providers'])) return false;
  const providers = value.payload.providers;
  if (!Array.isArray(providers) || providers.length > scheduledProviderIds.length) return false;
  const seen = new Set<unknown>();
  return providers.every((item) => {
    if (!isProviderRefreshStatus(item) || seen.has(item.provider)) return false;
    seen.add(item.provider);
    return true;
  });
}

function isProviderRefreshStatus(value: unknown): value is ProviderRefreshStatus {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, ['provider', 'displayName', 'state', 'lastSuccessAtUtc', 'nextRefreshAtUtc', 'message', 'newAlerts'])) return false;
  const item = value as Record<string, unknown>;
  return isOneOf(item.provider, scheduledProviderIds)
    && isBoundedText(item.displayName, 40)
    && isOneOf(item.state, providerRefreshStates)
    && isNullableTimestamp(item.lastSuccessAtUtc)
    && isNullableTimestamp(item.nextRefreshAtUtc)
    && (item.message === null || isOneOf(item.message, providerStatusMessages))
    && typeof item.newAlerts === 'number' && Number.isInteger(item.newAlerts) && item.newAlerts >= 0;
}

function isNullableTimestamp(value: unknown) {
  return value === null || (typeof value === 'string' && value.length <= 40 && Number.isFinite(Date.parse(value)));
}

export function requestGdacsRefresh() {
  const webview = window.chrome?.webview;
  if (!webview) return false;
  webview.postMessage({ type: 'providers.gdacs.refresh', payload: {} });
  return true;
}

export function requestFirmsRefresh(viewport: FirmsViewport) {
  return postNativeMessage({ type: 'providers.firms.refresh', payload: viewport });
}

export function requestEventSources(eventId: string) {
  window.chrome?.webview?.postMessage({ type: 'events.sources.get', payload: { id: eventId } });
}

export function attachXPostSourceToEvent(eventId: string, url: string) {
  const webview = window.chrome?.webview;
  if (!webview) return false;
  webview.postMessage({ type: 'events.sources.attachXPost', payload: { id: eventId, url } });
  return true;
}

export function searchReliefWebReports(
  eventId: string,
  queryText: string,
  fromDateUtc: string,
  toDateUtc: string,
  countryIso3?: string,
) {
  const webview = window.chrome?.webview;
  if (!webview) return false;
  webview.postMessage({
    type: 'events.sources.reliefweb.search',
    payload: {
      eventId,
      queryText,
      fromDateUtc,
      toDateUtc,
      ...(countryIso3 ? { countryIso3 } : {}),
    },
  });
  return true;
}

export function attachReliefWebReport(eventId: string, reportId: string) {
  const webview = window.chrome?.webview;
  if (!webview) return false;
  webview.postMessage({ type: 'events.sources.reliefweb.attach', payload: { eventId, reportId } });
  return true;
}

export function isAppInfoMessage(value: unknown): value is AppInfoMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<AppInfoMessage>;
  return message.type === 'app.info'
    && !!message.payload
    && typeof message.payload.applicationName === 'string'
    && typeof message.payload.shell === 'string'
    && typeof message.payload.uiHost === 'string'
    && typeof message.payload.persistence === 'string'
    && typeof message.payload.productionInboundPortsRequired === 'boolean'
    && isOneOf(message.payload.xPaneSide, xPaneSideValues);
}

export function isXPaneSideValueMessage(value: unknown): value is XPaneSideValueMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<XPaneSideValueMessage>;
  return message.type === 'xPane.side.value' && !!message.payload && isOneOf(message.payload.value, xPaneSideValues);
}

export function isPreferenceValueMessage(value: unknown): value is PreferenceValueMessage {
  return isPreferenceMessage(value, 'preferences.value');
}

export function isPreferenceUpdatedMessage(value: unknown): value is PreferenceUpdatedMessage {
  return isPreferenceMessage(value, 'preferences.updated');
}

export function isPreferenceErrorMessage(value: unknown): value is PreferenceErrorMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<PreferenceErrorMessage>;
  return message.type === 'preferences.error' && !!message.payload && message.payload.key === 'AccentTheme' && typeof message.payload.message === 'string';
}

export function isEventsListResultMessage(value: unknown): value is EventsListResultMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<EventsListResultMessage>;
  return message.type === 'events.list.result' && !!message.payload && Array.isArray(message.payload.events) && message.payload.events.every(isNativeEvent);
}

export function isEventSourcesResultMessage(value: unknown): value is EventSourcesResultMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<EventSourcesResultMessage>;
  return message.type === 'events.sources.result' && !!message.payload && typeof message.payload.eventId === 'string' && Array.isArray(message.payload.sources) && message.payload.sources.every(isNativeSourceItem);
}

export function isAttachXPostResultMessage(value: unknown): value is AttachXPostResultMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<AttachXPostResultMessage>;
  return message.type === 'events.sources.attachXPost.result'
    && !!message.payload
    && typeof message.payload.eventId === 'string'
    && isOneOf(message.payload.disposition, attachXPostDispositions)
    && isNativeSourceItem(message.payload.source);
}

export function isAttachXPostErrorMessage(value: unknown): value is AttachXPostErrorMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<AttachXPostErrorMessage>;
  return message.type === 'events.sources.attachXPost.error'
    && !!message.payload
    && typeof message.payload.eventId === 'string'
    && typeof message.payload.message === 'string';
}

export function isEventsErrorMessage(value: unknown): value is EventsErrorMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<EventsErrorMessage>;
  return message.type === 'events.error' && !!message.payload && typeof message.payload.message === 'string';
}

export function isGdacsRefreshResultMessage(value: unknown): value is GdacsRefreshResultMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<GdacsRefreshResultMessage>;
  const payload = message.payload;
  return message.type === 'providers.gdacs.refresh.result'
    && !!payload
    && isOneOf(payload.status, gdacsRefreshStatuses)
    && [payload.newEvents, payload.updatedEvents, payload.newSources, payload.updatedSources, payload.unchangedRecords, payload.skippedMalformedRecords, payload.conflicts, payload.newAlerts]
      .every((item) => typeof item === 'number' && Number.isInteger(item) && item >= 0);
}

export function isGdacsRefreshErrorMessage(value: unknown): value is GdacsRefreshErrorMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<GdacsRefreshErrorMessage>;
  return message.type === 'providers.gdacs.refresh.error'
    && !!message.payload
    && typeof message.payload.status === 'string'
    && typeof message.payload.message === 'string';
}

export function isFirmsRefreshResultMessage(value: unknown): value is FirmsRefreshResultMessage {
  if (!isMessage(value, 'providers.firms.refresh.result', [
    'observations', 'observationCount', 'refreshedAtUtc', 'oldestAcquiredAtUtc',
    'newestAcquiredAtUtc', 'coverage', 'skippedMalformedRecords',
  ])) return false;
  const payload = value.payload;
  if (!Array.isArray(payload.observations)
    || payload.observations.length > 20_000
    || !payload.observations.every(isFirmsObservation)
    || !isNonNegativeSafeInteger(payload.observationCount)
    || payload.observationCount !== payload.observations.length
    || !isUtcTimestamp(payload.refreshedAtUtc)
    || !(payload.oldestAcquiredAtUtc === null || isUtcTimestamp(payload.oldestAcquiredAtUtc))
    || !(payload.newestAcquiredAtUtc === null || isUtcTimestamp(payload.newestAcquiredAtUtc))
    || !isFirmsViewport(payload.coverage)
    || !isNonNegativeSafeInteger(payload.skippedMalformedRecords)) return false;
  if (payload.observations.length === 0) {
    return payload.oldestAcquiredAtUtc === null && payload.newestAcquiredAtUtc === null;
  }
  return payload.oldestAcquiredAtUtc !== null && payload.newestAcquiredAtUtc !== null;
}

export function isFirmsRefreshErrorMessage(value: unknown): value is FirmsRefreshErrorMessage {
  return isMessage(value, 'providers.firms.refresh.error', ['status', 'message'])
    && isOneOf(value.payload.status, firmsRefreshErrorStatuses)
    && isBoundedString(value.payload.message, 256, true);
}

export function isReliefWebSearchResultMessage(value: unknown): value is ReliefWebSearchResultMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<ReliefWebSearchResultMessage>;
  return message.type === 'events.sources.reliefweb.search.result'
    && !!message.payload
    && typeof message.payload.eventId === 'string'
    && Array.isArray(message.payload.candidates)
    && message.payload.candidates.length <= 20
    && message.payload.candidates.every(isReliefWebCandidate)
    && Number.isInteger(message.payload.skippedMalformedReports)
    && message.payload.skippedMalformedReports >= 0;
}

export function isReliefWebSearchErrorMessage(value: unknown): value is ReliefWebSearchErrorMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<ReliefWebSearchErrorMessage>;
  return message.type === 'events.sources.reliefweb.search.error'
    && !!message.payload
    && typeof message.payload.eventId === 'string'
    && typeof message.payload.status === 'string'
    && typeof message.payload.message === 'string';
}

export function isReliefWebAttachResultMessage(value: unknown): value is ReliefWebAttachResultMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<ReliefWebAttachResultMessage>;
  return message.type === 'events.sources.reliefweb.attach.result'
    && !!message.payload
    && typeof message.payload.eventId === 'string'
    && typeof message.payload.reportId === 'string'
    && isOneOf(message.payload.disposition, reliefWebAttachDispositions)
    && isNativeSourceItem(message.payload.source);
}

export function isReliefWebAttachErrorMessage(value: unknown): value is ReliefWebAttachErrorMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<ReliefWebAttachErrorMessage>;
  return message.type === 'events.sources.reliefweb.attach.error'
    && !!message.payload
    && typeof message.payload.eventId === 'string'
    && typeof message.payload.reportId === 'string'
    && typeof message.payload.status === 'string'
    && typeof message.payload.message === 'string';
}

export function isWatchlistsListResultMessage(value: unknown): value is WatchlistsListResultMessage {
  if (!isMessage(value, 'watchlists.list.result', ['watchlists'])) return false;
  const watchlists = value.payload.watchlists;
  return Array.isArray(watchlists) && watchlists.length <= 100 && watchlists.every(isWatchlistSummary);
}

export function isWatchlistCreateResultMessage(value: unknown): value is WatchlistCreateResultMessage {
  return isMessage(value, 'watchlists.create.result', ['watchlist']) && isWatchlistSummary(value.payload.watchlist);
}

export function isWatchlistRenameResultMessage(value: unknown): value is WatchlistRenameResultMessage {
  return isMessage(value, 'watchlists.rename.result', ['disposition', 'watchlist'])
    && isOneOf(value.payload.disposition, watchlistRenameDispositions)
    && isWatchlistSummary(value.payload.watchlist);
}

export function isWatchlistDeleteResultMessage(value: unknown): value is WatchlistDeleteResultMessage {
  return isMessage(value, 'watchlists.delete.result', ['watchlistId', 'disposition'])
    && isWatchlistId(value.payload.watchlistId)
    && isOneOf(value.payload.disposition, watchlistDeleteDispositions);
}

export function isWatchlistMembersResultMessage(value: unknown): value is WatchlistMembersResultMessage {
  if (!isMessage(value, 'watchlists.members.get.result', ['watchlistId', 'eventIds'])) return false;
  return isWatchlistId(value.payload.watchlistId)
    && isUniqueStringArray(value.payload.eventIds, 2000, isEventId);
}

export function isEventWatchlistMembershipsResultMessage(value: unknown): value is EventWatchlistMembershipsResultMessage {
  if (!isMessage(value, 'watchlists.memberships.getForEvent.result', ['eventId', 'watchlistIds'])) return false;
  return isEventId(value.payload.eventId)
    && isUniqueStringArray(value.payload.watchlistIds, 100, isWatchlistId);
}

export function isWatchlistMembershipSetResultMessage(value: unknown): value is WatchlistMembershipSetResultMessage {
  if (!isMessage(value, 'watchlists.members.set.result', ['watchlistId', 'eventId', 'isMember', 'disposition', 'eventCount'])) return false;
  return isWatchlistId(value.payload.watchlistId)
    && isEventId(value.payload.eventId)
    && typeof value.payload.isMember === 'boolean'
    && isOneOf(value.payload.disposition, watchlistMembershipDispositions)
    && isBoundedCount(value.payload.eventCount, 2000)
    && membershipDispositionMatchesState(value.payload.disposition, value.payload.isMember);
}

export function isWatchlistErrorMessage(value: unknown): value is WatchlistErrorMessage {
  if (!isMessage(value, 'watchlists.error', ['operation', 'code', 'message', 'watchlistId', 'eventId'])) return false;
  return isBoundedString(value.payload.operation, 80, true)
    && isOneOf(value.payload.code, watchlistErrorCodes)
    && isBoundedString(value.payload.message, 256, true)
    && (value.payload.watchlistId === null || isWatchlistId(value.payload.watchlistId))
    && (value.payload.eventId === null || isEventId(value.payload.eventId));
}

export function isAlertSummaryResultMessage(value: unknown): value is AlertSummaryResultMessage {
  return isMessage(value, 'alerts.summary.result', ['unreadCount'])
    && isNonNegativeSafeInteger(value.payload.unreadCount);
}

export function isAlertRulesListResultMessage(value: unknown): value is AlertRulesListResultMessage {
  if (!isMessage(value, 'alerts.rules.list.result', ['rules'])) return false;
  const rules = value.payload.rules;
  return Array.isArray(rules)
    && rules.length <= 100
    && rules.every(isAlertRule)
    && new Set(rules.map((rule) => rule.id)).size === rules.length;
}

export function isAlertRuleSetResultMessage(value: unknown): value is AlertRuleSetResultMessage {
  if (!isMessage(value, 'alerts.rules.set.result', ['disposition', 'rule'])) return false;
  if (!isOneOf(value.payload.disposition, alertRuleSetDispositions)) return false;
  const requiresRule = ['CreatedEnabled', 'Enabled', 'Disabled', 'Unchanged'].includes(value.payload.disposition);
  return requiresRule ? isAlertRule(value.payload.rule) : value.payload.rule === null;
}

export function isAlertsListResultMessage(value: unknown): value is AlertsListResultMessage {
  if (!isMessage(value, 'alerts.list.result', ['alerts', 'unreadCount', 'hasMore'])) return false;
  const alerts = value.payload.alerts;
  return Array.isArray(alerts)
    && alerts.length <= 200
    && alerts.every(isAlertOccurrence)
    && new Set(alerts.map((alert) => alert.id)).size === alerts.length
    && isNonNegativeSafeInteger(value.payload.unreadCount)
    && typeof value.payload.hasMore === 'boolean';
}

export function isAlertMarkReadResultMessage(value: unknown): value is AlertMarkReadResultMessage {
  if (!isMessage(value, 'alerts.markRead.result', ['alertId', 'readAtUtc', 'disposition'])) return false;
  if (!isAlertId(value.payload.alertId) || !isOneOf(value.payload.disposition, alertMarkReadDispositions)) return false;
  return value.payload.disposition === 'NotFound'
    ? value.payload.readAtUtc === null
    : isUtcTimestamp(value.payload.readAtUtc);
}

export function isAlertErrorMessage(value: unknown): value is AlertErrorMessage {
  if (!isMessage(value, 'alerts.error', ['operation', 'code', 'message', 'watchlistId', 'alertId'])) return false;
  return isBoundedString(value.payload.operation, 80, true)
    && isOneOf(value.payload.code, alertErrorCodes)
    && isBoundedString(value.payload.message, 256, true)
    && (value.payload.watchlistId === null || isWatchlistId(value.payload.watchlistId))
    && (value.payload.alertId === null || isAlertId(value.payload.alertId));
}

function isPreferenceMessage(value: unknown, type: 'preferences.value' | 'preferences.updated'): value is PreferenceValueMessage | PreferenceUpdatedMessage {
  if (!value || typeof value !== 'object') return false;
  const message = value as Partial<PreferenceValueMessage>;
  return message.type === type && !!message.payload && message.payload.key === 'AccentTheme' && isOneOf(message.payload.value, nativeAccentValues);
}

function postWatchlistMessage(message: unknown) {
  return postNativeMessage(message);
}

function postNativeMessage(message: unknown) {
  const webview = window.chrome?.webview;
  if (!webview) return false;
  webview.postMessage(message);
  return true;
}

function isAlertRule(value: unknown): value is AlertRule {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, ['id', 'watchlistId', 'triggerType', 'isEnabled', 'createdAtUtc', 'lastEnabledAtUtc'])) return false;
  const item = value as Partial<AlertRule>;
  return isAlertRuleId(item.id)
    && isWatchlistId(item.watchlistId)
    && isOneOf(item.triggerType, alertTriggerTypes)
    && typeof item.isEnabled === 'boolean'
    && isUtcTimestamp(item.createdAtUtc)
    && isUtcTimestamp(item.lastEnabledAtUtc);
}

function isAlertOccurrence(value: unknown): value is AlertOccurrence {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, [
    'id', 'alertRuleId', 'eventId', 'watchlistId', 'eventTitle', 'watchlistName',
    'triggerType', 'providerRevisionAtUtc', 'triggeredAtUtc', 'readAtUtc',
  ])) return false;
  const item = value as Partial<AlertOccurrence>;
  return isAlertId(item.id)
    && isAlertRuleId(item.alertRuleId)
    && isEventId(item.eventId)
    && isWatchlistId(item.watchlistId)
    && isBoundedString(item.eventTitle, 256, true)
    && isBoundedString(item.watchlistName, 80, true)
    && isOneOf(item.triggerType, alertTriggerTypes)
    && isUtcTimestamp(item.providerRevisionAtUtc)
    && isUtcTimestamp(item.triggeredAtUtc)
    && (item.readAtUtc === null || isUtcTimestamp(item.readAtUtc));
}

function isFirmsObservation(value: unknown): value is FirmsObservation {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, [
    'id', 'latitude', 'longitude', 'acquiredAtUtc', 'source', 'satellite', 'confidence', 'frp',
    'brightnessTemperatureI4', 'brightnessTemperatureI5', 'scan', 'track', 'processingVersion', 'dayNight',
  ])) return false;
  const item = value as Partial<FirmsObservation>;
  return typeof item.id === 'string'
    && /^firms-[0-9a-f]{32}$/.test(item.id)
    && isFiniteNumberInRange(item.latitude, -90, 90)
    && isFiniteNumberInRange(item.longitude, -180, 180)
    && isUtcTimestamp(item.acquiredAtUtc)
    && isOneOf(item.source, ['VIIRS_NOAA20_NRT', 'VIIRS_NOAA21_NRT'] as const)
    && isBoundedString(item.satellite, 32, true)
    && isOneOf(item.confidence, ['Low', 'Nominal', 'High'] as const)
    && isFiniteNumberInRange(item.frp, 0, Number.MAX_VALUE)
    && isFiniteNumberInRange(item.brightnessTemperatureI4, 0, Number.MAX_VALUE)
    && (item.brightnessTemperatureI5 === null || isFiniteNumberInRange(item.brightnessTemperatureI5, 0, Number.MAX_VALUE))
    && isFiniteNumberInRange(item.scan, Number.MIN_VALUE, Number.MAX_VALUE)
    && isFiniteNumberInRange(item.track, Number.MIN_VALUE, Number.MAX_VALUE)
    && isBoundedString(item.processingVersion, 32, true)
    && isOneOf(item.dayNight, ['Day', 'Night'] as const);
}

function isFirmsViewport(value: unknown): value is FirmsViewport {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, ['west', 'south', 'east', 'north'])) return false;
  const bounds = value as Partial<FirmsViewport>;
  return isFiniteNumberInRange(bounds.west, -180, 180)
    && isFiniteNumberInRange(bounds.east, -180, 180)
    && isFiniteNumberInRange(bounds.south, -90, 90)
    && isFiniteNumberInRange(bounds.north, -90, 90)
    && bounds.south! < bounds.north!;
}

function isFiniteNumberInRange(value: unknown, minimum: number, maximum: number): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value >= minimum && value <= maximum;
}

function isAlertRuleId(value: unknown): value is string {
  return typeof value === 'string' && /^ar-[0-9a-f]{32}$/.test(value);
}

function isAlertId(value: unknown): value is string {
  return typeof value === 'string' && /^al-[0-9a-f]{32}$/.test(value);
}

function isUtcTimestamp(value: unknown): value is string {
  return isBoundedString(value, 64, true)
    && (value.endsWith('Z') || value.endsWith('+00:00'))
    && Number.isFinite(Date.parse(value));
}

function isNonNegativeSafeInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;
}

function isWatchlistSummary(value: unknown): value is WatchlistSummary {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, ['id', 'name', 'eventCount'])) return false;
  const item = value as Partial<WatchlistSummary>;
  return isWatchlistId(item.id)
    && isBoundedString(item.name, 80, true)
    && isBoundedCount(item.eventCount, 2000);
}

function isWatchlistId(value: unknown): value is string {
  return typeof value === 'string' && /^wl-[0-9a-f]{32}$/.test(value);
}

function isEventId(value: unknown): value is string {
  return typeof value === 'string' && value.length > 0 && value.length <= 128 && /^[\p{L}\p{N}_.-]+$/u.test(value);
}

function isBoundedCount(value: unknown, maximum: number): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value <= maximum;
}

function isUniqueStringArray(
  value: unknown,
  maximumItems: number,
  predicate: (item: unknown) => item is string,
): value is string[] {
  return Array.isArray(value)
    && value.length <= maximumItems
    && value.every(predicate)
    && new Set(value).size === value.length;
}

function membershipDispositionMatchesState(
  disposition: (typeof watchlistMembershipDispositions)[number],
  isMember: boolean,
) {
  return isMember
    ? disposition === 'Added' || disposition === 'AlreadyMember'
    : disposition === 'Removed' || disposition === 'AlreadyAbsent';
}

function isMessage<T extends string>(
  value: unknown,
  type: T,
  payloadKeys: readonly string[],
): value is { type: T; payload: Record<string, unknown> } {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, ['type', 'payload'])) return false;
  const message = value as { type?: unknown; payload?: unknown };
  return message.type === type
    && !!message.payload
    && typeof message.payload === 'object'
    && hasExactKeys(message.payload, payloadKeys);
}

function hasExactKeys(value: object, expected: readonly string[]) {
  const keys = Object.keys(value).sort();
  return keys.length === expected.length && keys.every((key, index) => key === [...expected].sort()[index]);
}

function isNativeEvent(value: unknown): value is NativeEvent {
  if (!value || typeof value !== 'object' || !hasExactKeys(value, nativeEventKeys)) return false;
  const item = value as Partial<NativeEvent>;
  return typeof item.id === 'string' && typeof item.title === 'string' && typeof item.summary === 'string' && isOneOf(item.category, eventCategories) && isOneOf(item.status, eventStatuses) && typeof item.startedAtUtc === 'string' && typeof item.lastUpdatedAtUtc === 'string' && isNativeLocation(item.location) && typeof item.createdAtUtc === 'string' && typeof item.updatedAtUtc === 'string' && typeof item.sourceCount === 'number'
    && isOneOf(item.severity, eventSeverityValues) && isBoundedText(item.severityRule, 64) && isBoundedText(item.severityInputs, 256) && isOneOf(item.provider, eventProviders);
}

function isBoundedText(value: unknown, maximumLength: number): value is string {
  return typeof value === 'string' && value.trim().length > 0 && value.length <= maximumLength;
}

function isNativeSourceItem(value: unknown): value is NativeSourceItem {
  if (!value || typeof value !== 'object') return false;
  const item = value as Partial<NativeSourceItem>;
  return typeof item.id === 'string' && isOneOf(item.sourceType, sourceTypes) && typeof item.text === 'string' && typeof item.publishedAtUtc === 'string' && typeof item.retrievedAtUtc === 'string' && (item.location == null || isNativeLocation(item.location));
}

function isReliefWebCandidate(value: unknown): value is ReliefWebReportCandidate {
  if (!value || typeof value !== 'object') return false;
  const item = value as Partial<ReliefWebReportCandidate>;
  return isBoundedString(item.reportId, 20, true)
    && /^\d+$/.test(item.reportId)
    && isBoundedString(item.title, 256, true)
    && isReliefWebUrl(item.canonicalUrl)
    && (item.publishedAtUtc == null || isBoundedString(item.publishedAtUtc, 64, true))
    && isStringArray(item.organizations, 8, 256)
    && isStringArray(item.formats, 16, 128)
    && isStringArray(item.countries, 16, 128)
    && isStringArray(item.languages, 16, 128)
    && Array.isArray(item.disasters)
    && item.disasters.length <= 16
    && item.disasters.every((disaster) => !!disaster
      && isBoundedString(disaster.id, 20, true)
      && /^\d+$/.test(disaster.id)
      && isBoundedString(disaster.name, 256, true)
      && (disaster.glide == null || isBoundedString(disaster.glide, 64, true))
      && isStringArray(disaster.types, 8, 128));
}

function isReliefWebUrl(value: unknown) {
  if (!isBoundedString(value, 1024, true)) return false;
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && (url.hostname === 'reliefweb.int' || url.hostname === 'www.reliefweb.int');
  } catch {
    return false;
  }
}

function isStringArray(value: unknown, maximumItems: number, maximumLength: number): value is string[] {
  return Array.isArray(value)
    && value.length <= maximumItems
    && value.every((item) => isBoundedString(item, maximumLength, true));
}

function isBoundedString(value: unknown, maximumLength: number, required: boolean): value is string {
  return typeof value === 'string'
    && value.length <= maximumLength
    && (!required || value.length > 0);
}

function isNativeLocation(value: unknown): value is NativeEvent['location'] {
  if (!value || typeof value !== 'object') return false;
  const location = value as Partial<NativeEvent['location']>;
  return typeof location.latitude === 'number' && typeof location.longitude === 'number' && typeof location.displayName === 'string';
}

function isOneOf<T extends string>(value: unknown, allowed: readonly T[]): value is T {
  return typeof value === 'string' && allowed.includes(value as T);
}
