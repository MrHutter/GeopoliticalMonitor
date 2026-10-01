import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useAccentPreference } from './app/useAccentPreference';
import { AlertsPanel } from './features/alerts/AlertsPanel';
import { DetailTab, EventDetailsPanel, type XPostAttachmentFeedback } from './features/event-details/EventDetailsPanel';
import type { ReliefWebDiscoveryState } from './features/event-details/ReliefWebDiscovery';
import { EventPanel } from './features/events/EventPanel';
import { filterEvents, filterEventsByMinimumSeverity, type EventTimeRange, type MinimumSeverity } from './features/events/eventFilters';
import { MapWorkspace } from './features/map/MapWorkspace';
import { filterFirmsObservations, type FirmsDisplayMode } from './features/map/firmsMap';
import { SettingsPanel } from './features/settings/SettingsPanel';
import { TopBar } from './features/settings/TopBar';
import { WatchlistManager } from './features/watchlists/WatchlistManager';
import { initialMapLayers, mockXPosts } from './mock/dashboardData';
import {
  AppInfoMessage,
  AlertOccurrence,
  AlertRule,
  FirmsObservation,
  FirmsViewport,
  ProviderRefreshStatus,
  WatchlistSummary,
  XPaneSide,
  attachXPostSourceToEvent,
  attachReliefWebReport,
  createWatchlist,
  deleteWatchlist,
  isAlertErrorMessage,
  isAlertMarkReadResultMessage,
  isAlertRuleSetResultMessage,
  isAlertRulesListResultMessage,
  isAlertSummaryResultMessage,
  isAlertsListResultMessage,
  isAppInfoMessage,
  isAttachXPostErrorMessage,
  isAttachXPostResultMessage,
  isEventSourcesResultMessage,
  isEventsErrorMessage,
  isEventsListResultMessage,
  isGdacsRefreshErrorMessage,
  isGdacsRefreshResultMessage,
  isFirmsRefreshErrorMessage,
  isFirmsRefreshResultMessage,
  isEventWatchlistMembershipsResultMessage,
  isReliefWebAttachErrorMessage,
  isReliefWebAttachResultMessage,
  isReliefWebSearchErrorMessage,
  isReliefWebSearchResultMessage,
  isPreferenceErrorMessage,
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
  isProviderRefreshStatusMessage,
  nativeValueToAccent,
  markAlertRead,
  requestAccentPreference,
  requestAlertRules,
  requestAlertSummary,
  requestAlertsList,
  requestEventSources,
  requestEventsList,
  requestEventWatchlistMemberships,
  requestGdacsRefresh,
  requestProviderRefreshStatus,
  requestFirmsRefresh,
  requestWatchlistMembers,
  requestWatchlistsList,
  renameWatchlist,
  searchReliefWebReports,
  sendAppReady,
  setAlertRuleEnabled,
  setWatchlistMembership,
} from './nativeBridge';
import { NativeEvent, NativeSourceItem } from './types/events';
import { MapLayer } from './types/mock';

const EventLoadFailureMessage = 'Unable to load local event data.';
const formatUtcTime = (value: string) => new Intl.DateTimeFormat('en-GB', {
  hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone: 'UTC',
}).format(new Date(value));
type AlertResource = 'summary' | 'rules' | 'list';
type AlertRequestState = Record<AlertResource, { inFlight: boolean; queued: boolean }>;

export function App() {
  const [appInfo, setAppInfo] = useState<AppInfoMessage['payload'] | null>(null);
  const [xPaneSide, setXPaneSide] = useState<XPaneSide>('Right');
  const { accent, setAccent, applyPersistedAccent, persistenceError, reportPersistenceError } = useAccentPreference();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const settingsButtonRef = useRef<HTMLButtonElement>(null);
  const [watchlistManagerOpen, setWatchlistManagerOpen] = useState(false);
  const watchlistManageButtonRef = useRef<HTMLButtonElement>(null);
  const openSettings = useCallback(() => setSettingsOpen(true), []);
  const closeSettings = useCallback(() => setSettingsOpen(false), []);
  const [alertsOpen, setAlertsOpen] = useState(false);
  const alertsButtonRef = useRef<HTMLButtonElement>(null);
  const alertsOpenRef = useRef(false);
  const alertsLoadedRef = useRef(false);
  const alertRequestStateRef = useRef<AlertRequestState>({
    summary: { inFlight: false, queued: false },
    rules: { inFlight: false, queued: false },
    list: { inFlight: false, queued: false },
  });
  const [alertUnreadCount, setAlertUnreadCount] = useState(0);
  const [alertRules, setAlertRules] = useState<AlertRule[]>([]);
  const [alertHistory, setAlertHistory] = useState<AlertOccurrence[]>([]);
  const [alertHasMore, setAlertHasMore] = useState(false);
  const [pendingAlertRuleWatchlistId, setPendingAlertRuleWatchlistId] = useState<string | null>(null);
  const [alertStatus, setAlertStatus] = useState<{ kind: 'info' | 'error'; message: string } | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedRange, setSelectedRange] = useState<EventTimeRange>('ALL');
  const [minimumSeverity, setMinimumSeverity] = useState<MinimumSeverity>('All');
  const [events, setEvents] = useState<NativeEvent[]>([]);
  const [watchlists, setWatchlists] = useState<WatchlistSummary[]>([]);
  const [activeWatchlistId, setActiveWatchlistId] = useState<string | null>(null);
  const [membersByWatchlist, setMembersByWatchlist] = useState<Map<string, Set<string>>>(() => new Map());
  const [membershipsByEvent, setMembershipsByEvent] = useState<Map<string, Set<string>>>(() => new Map());
  const [pendingActiveWatchlistId, setPendingActiveWatchlistId] = useState<string | null>(null);
  const [pendingMembershipWatchlistId, setPendingMembershipWatchlistId] = useState<string | null>(null);
  const [watchlistStatus, setWatchlistStatus] = useState<string | null>(null);
  const [eventSourcesById, setEventSourcesById] = useState<Map<string, NativeSourceItem[]>>(() => new Map());
  const [eventLoadError, setEventLoadError] = useState<string | null>(null);
  const [gdacsRefresh, setGdacsRefresh] = useState<{ kind: 'idle' | 'pending' | 'success' | 'error'; message?: string }>({ kind: 'idle' });
  const [providerStatuses, setProviderStatuses] = useState<ProviderRefreshStatus[]>([]);
  const providerLastSuccessRef = useRef<Map<string, string | null> | null>(null);
  const eventsLoadedRef = useRef(false);
  const [firmsObservations, setFirmsObservations] = useState<FirmsObservation[]>([]);
  const [firmsRefreshedAtUtc, setFirmsRefreshedAtUtc] = useState<string | null>(null);
  const [firmsViewport, setFirmsViewport] = useState<FirmsViewport | null>(null);
  const [firmsDisplayMode, setFirmsDisplayMode] = useState<FirmsDisplayMode>('significant');
  const [firmsStatus, setFirmsStatus] = useState<{
    kind: 'ready' | 'refreshing' | 'success' | 'error' | 'unconfigured' | 'area-too-large';
    message?: string;
  }>({ kind: 'ready' });
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const selectedEventIdRef = useRef<string | null>(null);
  const [activeTab, setActiveTab] = useState<DetailTab>('Overview');
  const [xPostAttachmentFeedback, setXPostAttachmentFeedback] = useState<XPostAttachmentFeedback | null>(null);
  const [reliefWebDiscovery, setReliefWebDiscovery] = useState<ReliefWebDiscoveryState | null>(null);
  const [mapLayers, setMapLayers] = useState<MapLayer[]>(initialMapLayers);
  const [locateRequest, setLocateRequest] = useState<{ eventId: string; requestId: number } | null>(null);
  const locateRequestIdRef = useRef(0);
  const attachedPostIds = useMemo(() => new Set<string>(), []);
  const visibleFirmsObservations = useMemo(
    () => filterFirmsObservations(firmsObservations, firmsDisplayMode),
    [firmsDisplayMode, firmsObservations],
  );

  const requestAlertResource = useCallback((resource: AlertResource) => {
    const state = alertRequestStateRef.current[resource];
    if (state.inFlight) {
      state.queued = true;
      return;
    }

    state.inFlight = true;
    const sent = resource === 'summary'
      ? requestAlertSummary()
      : resource === 'rules'
        ? requestAlertRules()
        : requestAlertsList();
    if (!sent) {
      state.inFlight = false;
      setAlertStatus({ kind: 'error', message: 'Alerts are unavailable. Existing Events and Watchlists were not changed.' });
    }
  }, []);

  const completeAlertResource = useCallback((resource: AlertResource) => {
    const state = alertRequestStateRef.current[resource];
    state.inFlight = false;
    if (state.queued) {
      state.queued = false;
      queueMicrotask(() => requestAlertResource(resource));
    }
  }, [requestAlertResource]);

  const openAlerts = useCallback(() => {
    alertsOpenRef.current = true;
    setAlertsOpen(true);
    setAlertStatus((current) => current?.kind === 'error' ? current : null);
    requestAlertResource('rules');
    requestAlertResource('list');
  }, [requestAlertResource]);

  const closeAlerts = useCallback(() => {
    alertsOpenRef.current = false;
    setAlertsOpen(false);
  }, []);

  useEffect(() => {
    const webview = window.chrome?.webview;

    const onMessage = (event: { data: unknown }) => {
      if (isAppInfoMessage(event.data)) {
        setAppInfo(event.data.payload);
        setXPaneSide(event.data.payload.xPaneSide);
        requestEventsList();
        requestWatchlistsList();
        requestAlertResource('summary');
        requestProviderRefreshStatus();
        return;
      }

      if (isProviderRefreshStatusMessage(event.data)) {
        const statuses = event.data.payload.providers;
        const previous = providerLastSuccessRef.current;
        providerLastSuccessRef.current = new Map(statuses.map((status) => [status.provider, status.lastSuccessAtUtc]));
        setProviderStatuses(statuses);
        // The first status only establishes a baseline. Later, a newer last-success time means a manual or
        // timer refresh finished: reload Events without resetting selection, filters or open Details.
        const refreshed = previous === null ? [] : statuses.filter((status) => status.lastSuccessAtUtc !== null && status.lastSuccessAtUtc !== previous.get(status.provider));
        if (refreshed.length > 0) {
          const selectedId = selectedEventIdRef.current;
          setEventSourcesById((current) => (selectedId && current.has(selectedId) ? new Map([[selectedId, current.get(selectedId)!]]) : new Map()));
          requestEventsList();
          if (selectedId) requestEventSources(selectedId);
          if (refreshed.some((status) => status.newAlerts > 0)) {
            requestAlertResource('summary');
            if (alertsLoadedRef.current || alertsOpenRef.current) requestAlertResource('list');
          }
        }
        return;
      }

      if (isXPaneSideValueMessage(event.data)) {
        setXPaneSide(event.data.payload.value);
        return;
      }

      if (isPreferenceValueMessage(event.data) || isPreferenceUpdatedMessage(event.data)) {
        applyPersistedAccent(nativeValueToAccent(event.data.payload.value));
        return;
      }

      if (isPreferenceErrorMessage(event.data)) {
        reportPersistenceError(event.data.payload.message);
        return;
      }

      if (isEventsListResultMessage(event.data)) {
        const nativeEvents = event.data.payload.events;
        setEvents(nativeEvents);
        setEventLoadError(null);
        // Only the first non-empty load auto-selects; later reloads (manual or timer) never select a different Event.
        const isFirstLoad = !eventsLoadedRef.current;
        if (nativeEvents.length > 0) eventsLoadedRef.current = true;
        setSelectedEventId((current) => (current && nativeEvents.some((item) => item.id === current) ? current : isFirstLoad ? nativeEvents[0]?.id ?? null : null));
        return;
      }

      if (isWatchlistsListResultMessage(event.data)) {
        const nextWatchlists = event.data.payload.watchlists;
        setWatchlists(nextWatchlists);
        setActiveWatchlistId((current) => current && nextWatchlists.some((item) => item.id === current) ? current : null);
        return;
      }

      if (isWatchlistCreateResultMessage(event.data)) {
        const created = event.data.payload.watchlist;
        setWatchlists((current) => [...current.filter((item) => item.id !== created.id), created].sort((left, right) => left.name.localeCompare(right.name)));
        setMembersByWatchlist((current) => new Map(current).set(created.id, new Set()));
        setActiveWatchlistId(created.id);
        setWatchlistStatus(`Created ${created.name}.`);
        return;
      }

      if (isWatchlistRenameResultMessage(event.data)) {
        const renamed = event.data.payload.watchlist;
        setWatchlists((current) => current.map((item) => item.id === renamed.id ? renamed : item).sort((left, right) => left.name.localeCompare(right.name)));
        setWatchlistStatus(event.data.payload.disposition === 'Updated' ? `Renamed to ${renamed.name}.` : `${renamed.name} was unchanged.`);
        return;
      }

      if (isWatchlistDeleteResultMessage(event.data)) {
        const deletedId = event.data.payload.watchlistId;
        setWatchlists((current) => current.filter((item) => item.id !== deletedId));
        setMembersByWatchlist((current) => {
          const next = new Map(current);
          next.delete(deletedId);
          return next;
        });
        setMembershipsByEvent((current) => new Map([...current].map(([eventId, memberships]) => [eventId, new Set([...memberships].filter((id) => id !== deletedId))])));
        setActiveWatchlistId((current) => current === deletedId ? null : current);
        setPendingActiveWatchlistId((current) => current === deletedId ? null : current);
        setWatchlistStatus('Watchlist deleted. Events were kept.');
        requestAlertResource('summary');
        if (alertsLoadedRef.current) {
          requestAlertResource('rules');
          requestAlertResource('list');
        }
        return;
      }

      if (isWatchlistMembersResultMessage(event.data)) {
        const { watchlistId, eventIds } = event.data.payload;
        setMembersByWatchlist((current) => new Map(current).set(watchlistId, new Set(eventIds)));
        setPendingActiveWatchlistId((pending) => {
          if (pending === watchlistId) setActiveWatchlistId(watchlistId);
          return pending === watchlistId ? null : pending;
        });
        return;
      }

      if (isEventWatchlistMembershipsResultMessage(event.data)) {
        const { eventId, watchlistIds } = event.data.payload;
        setMembershipsByEvent((current) => new Map(current).set(eventId, new Set(watchlistIds)));
        return;
      }

      if (isWatchlistMembershipSetResultMessage(event.data)) {
        const { watchlistId, eventId, isMember, eventCount } = event.data.payload;
        setWatchlists((current) => current.map((item) => item.id === watchlistId ? { ...item, eventCount } : item));
        setMembersByWatchlist((current) => {
          if (!current.has(watchlistId)) return current;
          const next = new Map(current);
          const members = new Set(next.get(watchlistId)!);
          if (isMember) members.add(eventId); else members.delete(eventId);
          next.set(watchlistId, members);
          return next;
        });
        setMembershipsByEvent((current) => {
          const next = new Map(current);
          const memberships = new Set(next.get(eventId) ?? []);
          if (isMember) memberships.add(watchlistId); else memberships.delete(watchlistId);
          next.set(eventId, memberships);
          return next;
        });
        setPendingMembershipWatchlistId(null);
        setWatchlistStatus(isMember ? 'Event added to watchlist.' : 'Event removed from watchlist.');
        return;
      }

      if (isWatchlistErrorMessage(event.data)) {
        setPendingActiveWatchlistId(null);
        setPendingMembershipWatchlistId(null);
        setWatchlistStatus(event.data.payload.message);
        return;
      }

      if (isAlertSummaryResultMessage(event.data)) {
        setAlertUnreadCount(event.data.payload.unreadCount);
        completeAlertResource('summary');
        return;
      }

      if (isAlertRulesListResultMessage(event.data)) {
        setAlertRules(event.data.payload.rules);
        completeAlertResource('rules');
        return;
      }

      if (isAlertRuleSetResultMessage(event.data)) {
        const { disposition, rule } = event.data.payload;
        setPendingAlertRuleWatchlistId(null);
        if (rule) {
          setAlertRules((current) => [...current.filter((item) => item.id !== rule.id), rule]
            .sort((left, right) => left.watchlistId.localeCompare(right.watchlistId)));
        }
        setAlertStatus(disposition === 'WatchlistNotFound'
          ? { kind: 'error', message: 'The Watchlist is no longer available.' }
          : disposition === 'RuleNotFound'
            ? { kind: 'error', message: 'Alert monitoring was not configured for that Watchlist.' }
            : { kind: 'info', message: disposition === 'Disabled' ? 'Monitoring disabled. Alert history was preserved.' : disposition === 'Unchanged' ? 'Monitoring was unchanged.' : 'Monitoring enabled. Existing Event state was not evaluated.' });
        return;
      }

      if (isAlertsListResultMessage(event.data)) {
        setAlertHistory(event.data.payload.alerts);
        setAlertUnreadCount(event.data.payload.unreadCount);
        setAlertHasMore(event.data.payload.hasMore);
        alertsLoadedRef.current = true;
        completeAlertResource('list');
        return;
      }

      if (isAlertMarkReadResultMessage(event.data)) {
        const { alertId, readAtUtc, disposition } = event.data.payload;
        if (disposition === 'NotFound') {
          setAlertStatus({ kind: 'error', message: 'This alert is no longer available.' });
          requestAlertResource('summary');
          requestAlertResource('list');
          return;
        }

        if (disposition === 'Updated') {
          setAlertHistory((current) => current.map((item) => item.id === alertId ? { ...item, readAtUtc } : item));
          setAlertUnreadCount((current) => Math.max(0, current - 1));
        }
        return;
      }

      if (isAlertErrorMessage(event.data)) {
        const operation = event.data.payload.operation;
        setAlertStatus({ kind: 'error', message: event.data.payload.message });
        if (operation === 'alerts.summary') completeAlertResource('summary');
        if (operation === 'alerts.rules.list') completeAlertResource('rules');
        if (operation === 'alerts.list') completeAlertResource('list');
        if (operation === 'alerts.rules.set') setPendingAlertRuleWatchlistId(null);
        return;
      }

      if (isGdacsRefreshResultMessage(event.data)) {
        const result = event.data.payload;
        if (result.status === 'AlreadyInProgress') {
          setGdacsRefresh({ kind: 'pending', message: 'A GDACS refresh is already in progress.' });
          return;
        }

        setGdacsRefresh({
          kind: 'success',
          message: `GDACS refresh complete: ${result.newEvents} new, ${result.updatedEvents} updated, ${result.unchangedRecords} unchanged, ${result.skippedMalformedRecords} skipped, ${result.conflicts} conflicts.`,
        });
        setEventSourcesById(new Map());
        requestEventsList();
        if (result.newAlerts > 0) {
          requestAlertResource('summary');
          if (alertsLoadedRef.current || alertsOpenRef.current) requestAlertResource('list');
        }
        return;
      }

      if (isGdacsRefreshErrorMessage(event.data)) {
        setGdacsRefresh({ kind: 'error', message: event.data.payload.message });
        return;
      }

      if (isFirmsRefreshResultMessage(event.data)) {
        const result = event.data.payload;
        setFirmsObservations(result.observations);
        setFirmsRefreshedAtUtc(result.refreshedAtUtc);
        const newest = result.newestAcquiredAtUtc ? ` · newest ${formatUtcTime(result.newestAcquiredAtUtc)} UTC` : '';
        setFirmsStatus({
          kind: 'success',
          message: `Snapshot refreshed ${formatUtcTime(result.refreshedAtUtc)} UTC${newest}.`,
        });
        return;
      }

      if (isFirmsRefreshErrorMessage(event.data)) {
        const status = event.data.payload.status;
        setFirmsStatus({
          kind: status === 'ConfigurationUnavailable'
            ? 'unconfigured'
            : status === 'AreaTooLarge' || status === 'InvalidViewport'
              ? 'area-too-large'
              : 'error',
          message: event.data.payload.message,
        });
        return;
      }

      if (isEventSourcesResultMessage(event.data)) {
        const eventId = event.data.payload.eventId;
        const sources = event.data.payload.sources;
        setEventSourcesById((current) => {
          const next = new Map(current);
          next.set(eventId, sources);
          return next;
        });
        setEvents((current) => current.map((item) => (
          item.id === eventId ? { ...item, sourceCount: sources.length } : item
        )));
        return;
      }

      if (isAttachXPostResultMessage(event.data)) {
        const { eventId, disposition } = event.data.payload;
        const feedback = disposition === 'AlreadyAttached'
          ? { kind: 'info' as const, message: 'This X post is already attached.' }
          : disposition === 'ExistingSourceAttached'
            ? { kind: 'success' as const, message: 'Existing X post attached.' }
            : { kind: 'success' as const, message: 'X post attached.' };
        setXPostAttachmentFeedback({ eventId, ...feedback });
        requestEventSources(eventId);
        return;
      }

      if (isAttachXPostErrorMessage(event.data)) {
        setXPostAttachmentFeedback({
          eventId: event.data.payload.eventId,
          kind: 'error',
          message: event.data.payload.message,
        });
        return;
      }

      if (isReliefWebSearchResultMessage(event.data)) {
        const { eventId, candidates, skippedMalformedReports } = event.data.payload;
        if (selectedEventIdRef.current !== eventId) return;
        setReliefWebDiscovery((current) => ({
          eventId,
          kind: 'results',
          candidates,
          message: candidates.length === 0
            ? 'No matching ReliefWeb reports found.'
            : skippedMalformedReports > 0
              ? `${candidates.length} reports found; ${skippedMalformedReports} malformed result${skippedMalformedReports === 1 ? '' : 's'} skipped.`
              : `${candidates.length} report${candidates.length === 1 ? '' : 's'} found.`,
          attachedReportIds: current?.eventId === eventId ? current.attachedReportIds : new Set<string>(),
        }));
        return;
      }

      if (isReliefWebSearchErrorMessage(event.data)) {
        const { eventId, message } = event.data.payload;
        if (selectedEventIdRef.current !== eventId) return;
        setReliefWebDiscovery((current) => ({
          eventId,
          kind: 'error',
          candidates: current?.eventId === eventId ? current.candidates : [],
          message,
          attachedReportIds: current?.eventId === eventId ? current.attachedReportIds : new Set<string>(),
        }));
        return;
      }

      if (isReliefWebAttachResultMessage(event.data)) {
        const { eventId, reportId, disposition } = event.data.payload;
        if (selectedEventIdRef.current !== eventId) return;
        setReliefWebDiscovery((current) => {
          const attachedReportIds = new Set(current?.attachedReportIds ?? []);
          attachedReportIds.add(reportId);
          return {
            eventId,
            kind: 'results',
            candidates: current?.eventId === eventId ? current.candidates : [],
            message: disposition === 'AlreadyAttached'
              ? 'This ReliefWeb report is already attached.'
              : disposition === 'ExistingSourceAttached'
                ? 'Existing ReliefWeb source attached.'
                : 'ReliefWeb report attached.',
            attachedReportIds,
          };
        });
        requestEventSources(eventId);
        return;
      }

      if (isReliefWebAttachErrorMessage(event.data)) {
        const { eventId, message } = event.data.payload;
        if (selectedEventIdRef.current !== eventId) return;
        setReliefWebDiscovery((current) => ({
          eventId,
          kind: 'error',
          candidates: current?.eventId === eventId ? current.candidates : [],
          message,
          attachedReportIds: current?.eventId === eventId ? current.attachedReportIds : new Set<string>(),
        }));
        return;
      }

      if (isEventsErrorMessage(event.data)) {
        setEventLoadError(EventLoadFailureMessage);
      }
    };

    webview?.addEventListener('message', onMessage);
    sendAppReady();
    requestAccentPreference();

    return () => {
      webview?.removeEventListener('message', onMessage);
    };
  }, [applyPersistedAccent, completeAlertResource, reportPersistenceError, requestAlertResource]);

  const activeWatchlist = watchlists.find((watchlist) => watchlist.id === activeWatchlistId) ?? null;
  const activeMemberships = activeWatchlistId ? membersByWatchlist.get(activeWatchlistId) ?? null : null;
  const scopedEvents = useMemo(
    () => activeWatchlistId ? events.filter((event) => activeMemberships?.has(event.id)) : events,
    [activeMemberships, activeWatchlistId, events],
  );
  const filteredEvents = useMemo(
    () => filterEventsByMinimumSeverity(filterEvents(scopedEvents, searchQuery, selectedRange), minimumSeverity),
    [minimumSeverity, scopedEvents, searchQuery, selectedRange],
  );
  const eventsById = useMemo(() => new Map(filteredEvents.map((event) => [event.id, event])), [filteredEvents]);
  const selectedEvent = selectedEventId ? eventsById.get(selectedEventId) ?? null : null;
  const selectedSources = selectedEventId ? eventSourcesById.get(selectedEventId) ?? [] : [];
  const selectedWatchlistMemberships = selectedEvent ? membershipsByEvent.get(selectedEvent.id) ?? null : null;
  const relatedPosts = selectedEvent ? mockXPosts.filter((post) => post.relatedEventId === selectedEvent.id) : [];
  const attachedReliefWebReportIds = useMemo(() => new Set(
    selectedSources
      .filter((source) => source.externalId?.startsWith('ReliefWeb:Report:'))
      .map((source) => source.externalId!.slice('ReliefWeb:Report:'.length)),
  ), [selectedSources]);
  const visibleReliefWebDiscovery = selectedEvent ? {
    ...(reliefWebDiscovery?.eventId === selectedEvent.id ? reliefWebDiscovery : {
      eventId: selectedEvent.id,
      kind: 'idle' as const,
      candidates: [],
      attachedReportIds: new Set<string>(),
    }),
    attachedReportIds: new Set([
      ...(reliefWebDiscovery?.eventId === selectedEvent.id ? reliefWebDiscovery.attachedReportIds : []),
      ...attachedReliefWebReportIds,
    ]),
  } : null;

  useEffect(() => {
    selectedEventIdRef.current = selectedEventId;
    setReliefWebDiscovery(null);
  }, [selectedEventId]);

  useEffect(() => {
    if (selectedEventId && !selectedEvent) {
      setSelectedEventId(null);
    }
  }, [selectedEvent, selectedEventId]);

  useEffect(() => {
    if (selectedEvent && !eventSourcesById.has(selectedEvent.id)) {
      requestEventSources(selectedEvent.id);
    }
  }, [eventSourcesById, selectedEvent]);

  useEffect(() => {
    if (selectedEvent && watchlists.length > 0 && !membershipsByEvent.has(selectedEvent.id)) {
      requestEventWatchlistMemberships(selectedEvent.id);
    }
  }, [membershipsByEvent, selectedEvent, watchlists.length]);

  const selectWatchlist = (watchlistId: string | null) => {
    setWatchlistStatus(null);
    if (watchlistId === null) {
      setPendingActiveWatchlistId(null);
      setActiveWatchlistId(null);
      return;
    }

    if (membersByWatchlist.has(watchlistId)) {
      setPendingActiveWatchlistId(null);
      setActiveWatchlistId(watchlistId);
      return;
    }

    setPendingActiveWatchlistId(watchlistId);
    if (!requestWatchlistMembers(watchlistId)) {
      setPendingActiveWatchlistId(null);
      setWatchlistStatus('Desktop watchlist service is unavailable.');
    }
  };

  const addWatchlist = (name: string) => {
    setWatchlistStatus('Creating watchlist...');
    if (!createWatchlist(name)) setWatchlistStatus('Desktop watchlist service is unavailable.');
  };

  const updateWatchlistName = (watchlistId: string, name: string) => {
    setWatchlistStatus('Renaming watchlist...');
    if (!renameWatchlist(watchlistId, name)) setWatchlistStatus('Desktop watchlist service is unavailable.');
  };

  const removeWatchlist = (watchlistId: string) => {
    setWatchlistStatus('Deleting watchlist...');
    if (!deleteWatchlist(watchlistId)) setWatchlistStatus('Desktop watchlist service is unavailable.');
  };

  const toggleWatchlistMembership = (watchlistId: string, isMember: boolean) => {
    if (!selectedEvent) return;
    setPendingMembershipWatchlistId(watchlistId);
    if (!setWatchlistMembership(watchlistId, selectedEvent.id, isMember)) {
      setPendingMembershipWatchlistId(null);
      setWatchlistStatus('Desktop watchlist service is unavailable.');
    }
  };

  const toggleAlertRule = (watchlistId: string, enabled: boolean) => {
    if (pendingAlertRuleWatchlistId) return;
    setPendingAlertRuleWatchlistId(watchlistId);
    setAlertStatus({ kind: 'info', message: enabled ? 'Enabling monitoring...' : 'Disabling monitoring...' });
    if (!setAlertRuleEnabled(watchlistId, enabled)) {
      setPendingAlertRuleWatchlistId(null);
      setAlertStatus({ kind: 'error', message: 'Alerts are unavailable. Existing Events and Watchlists were not changed.' });
    }
  };

  const activateAlert = (alert: AlertOccurrence) => {
    if (!markAlertRead(alert.id)) {
      setAlertStatus({ kind: 'error', message: 'Alert read state could not be saved.' });
    }
    closeAlerts();
    setPendingActiveWatchlistId(null);
    setActiveWatchlistId(null);
    setSearchQuery('');
    setSelectedRange('ALL');
    setMinimumSeverity('All');
    setSelectedEventId(alert.eventId);
    setActiveTab('Overview');
    locateRequestIdRef.current += 1;
    setLocateRequest({ eventId: alert.eventId, requestId: locateRequestIdRef.current });
    if (!events.some((item) => item.id === alert.eventId)) {
      setAlertStatus({ kind: 'error', message: 'This alert is no longer available.' });
      requestAlertResource('summary');
      requestAlertResource('list');
    }
  };

  const toggleMapLayer = (layerId: string) => {
    setMapLayers((layers) => layers.map((layer) => (layer.id === layerId ? { ...layer, enabled: !layer.enabled } : layer)));
  };

  const attachXPost = (eventId: string, url: string) => {
    setXPostAttachmentFeedback({ eventId, kind: 'pending', message: 'Attaching X post...' });
    if (!attachXPostSourceToEvent(eventId, url)) {
      setXPostAttachmentFeedback({ eventId, kind: 'error', message: 'Desktop attachment service is unavailable.' });
    }
  };

  const searchReliefWeb = (eventId: string, query: string, fromDateUtc: string, toDateUtc: string, countryIso3?: string) => {
    setReliefWebDiscovery({ eventId, kind: 'searching', candidates: [], message: 'Searching ReliefWeb...', attachedReportIds: new Set<string>() });
    if (!searchReliefWebReports(eventId, query, fromDateUtc, toDateUtc, countryIso3)) {
      setReliefWebDiscovery({ eventId, kind: 'error', candidates: [], message: 'Desktop ReliefWeb search is unavailable.', attachedReportIds: new Set<string>() });
    }
  };

  const attachReliefWeb = (eventId: string, reportId: string) => {
    setReliefWebDiscovery((current) => ({
      eventId,
      kind: current?.kind === 'error' ? 'results' : current?.kind ?? 'results',
      candidates: current?.eventId === eventId ? current.candidates : [],
      message: 'Retrieving authoritative ReliefWeb metadata...',
      attachingReportId: reportId,
      attachedReportIds: current?.eventId === eventId ? current.attachedReportIds : new Set<string>(),
    }));
    if (!attachReliefWebReport(eventId, reportId)) {
      setReliefWebDiscovery((current) => ({
        eventId,
        kind: 'error',
        candidates: current?.eventId === eventId ? current.candidates : [],
        message: 'Desktop ReliefWeb attachment is unavailable.',
        attachedReportIds: current?.eventId === eventId ? current.attachedReportIds : new Set<string>(),
      }));
    }
  };

  const refreshGdacs = () => {
    if (gdacsRefresh.kind === 'pending') return;
    setGdacsRefresh({ kind: 'pending', message: 'Requesting the preceding four days of GDACS earthquakes...' });
    if (!requestGdacsRefresh()) {
      setGdacsRefresh({ kind: 'error', message: 'Desktop GDACS refresh is unavailable.' });
    }
  };

  const refreshFirms = () => {
    if (firmsStatus.kind === 'refreshing') return;
    if (!firmsViewport) {
      setFirmsStatus({ kind: 'error', message: 'The current map viewport is unavailable.' });
      return;
    }
    setFirmsStatus({ kind: 'refreshing', message: 'Requesting NASA FIRMS detections for the visible map area...' });
    if (!requestFirmsRefresh(firmsViewport)) {
      setFirmsStatus({ kind: 'error', message: 'Desktop NASA FIRMS refresh is unavailable. Existing layer data was preserved.' });
    }
  };

  return (
    <main className="app-shell" data-x-pane-side={xPaneSide.toLowerCase()}>
      <TopBar
        searchQuery={searchQuery}
        onSearchQueryChange={setSearchQuery}
        selectedRange={selectedRange}
        onRangeChange={setSelectedRange}
        unreadAlertCount={alertUnreadCount}
        alertsButtonRef={alertsButtonRef}
        onOpenAlerts={openAlerts}
        settingsButtonRef={settingsButtonRef}
        onOpenSettings={openSettings}
      />

      <div className="desktop-status" aria-label="desktop bridge status">
        <span>{appInfo ? `${appInfo.shell} / ${appInfo.uiHost}` : 'Waiting for desktop host'}</span>
        <span>{appInfo?.persistence ?? 'SQLite local application data'}</span>
        <span>{appInfo?.productionInboundPortsRequired === false ? 'No inbound ports' : 'Pending host confirmation'}</span>
      </div>

      <div className="workspace-grid" data-x-pane-side={xPaneSide.toLowerCase()} aria-label="map and events workspace">
        <EventPanel
          events={filteredEvents}
          selectedEventId={selectedEventId}
          selectedRange={selectedRange}
          loadError={eventLoadError}
          watchlists={watchlists}
          activeWatchlistId={activeWatchlistId}
          minimumSeverity={minimumSeverity}
          emptyMessage={activeWatchlist ? (activeWatchlist.eventCount === 0 ? 'No events in this watchlist.' : 'No watchlist events match the active filters.') : 'No events match the active filters.'}
          manageButtonRef={watchlistManageButtonRef}
          gdacsRefresh={gdacsRefresh}
          providerStatuses={providerStatuses}
          onRefreshGdacs={refreshGdacs}
          onSelectEvent={setSelectedEventId}
          onSelectWatchlist={selectWatchlist}
          onMinimumSeverityChange={setMinimumSeverity}
          onManageWatchlists={() => setWatchlistManagerOpen(true)}
        />
        <MapWorkspace
          accent={accent}
          events={filteredEvents}
          layers={mapLayers}
          selectedEventId={selectedEvent?.id ?? ''}
          locateRequest={locateRequest}
          firmsObservations={visibleFirmsObservations}
          firmsRefreshedAtUtc={firmsRefreshedAtUtc}
          firmsStatus={firmsStatus}
          firmsDisplayMode={firmsDisplayMode}
          firmsTotalCount={firmsObservations.length}
          onSelectEvent={setSelectedEventId}
          onToggleLayer={toggleMapLayer}
          onRefreshFirms={refreshFirms}
          onViewportChange={setFirmsViewport}
          onFirmsDisplayModeChange={setFirmsDisplayMode}
        />
      </div>

      <EventDetailsPanel
        event={selectedEvent}
        sources={selectedSources}
        activeTab={activeTab}
        relatedPosts={relatedPosts}
        attachedPostIds={attachedPostIds}
        watchlists={watchlists}
        watchlistMemberships={selectedWatchlistMemberships}
        pendingMembershipWatchlistId={pendingMembershipWatchlistId}
        watchlistStatus={watchlistStatus}
        xPostAttachmentFeedback={xPostAttachmentFeedback}
        reliefWebDiscovery={visibleReliefWebDiscovery}
        onTabChange={setActiveTab}
        onAttachXPost={attachXPost}
        onSearchReliefWeb={searchReliefWeb}
        onAttachReliefWeb={attachReliefWeb}
        onToggleWatchlistMembership={toggleWatchlistMembership}
        onManageWatchlists={() => setWatchlistManagerOpen(true)}
      />

      {watchlistManagerOpen ? (
        <WatchlistManager
          watchlists={watchlists}
          activeWatchlistId={activeWatchlistId}
          status={watchlistStatus}
          triggerRef={watchlistManageButtonRef}
          onSelect={selectWatchlist}
          onCreate={addWatchlist}
          onRename={updateWatchlistName}
          onDelete={removeWatchlist}
          onClose={() => setWatchlistManagerOpen(false)}
        />
      ) : null}

      {alertsOpen ? (
        <AlertsPanel
          rules={alertRules}
          alerts={alertHistory}
          unreadCount={alertUnreadCount}
          hasMore={alertHasMore}
          watchlists={watchlists}
          pendingWatchlistId={pendingAlertRuleWatchlistId}
          status={alertStatus}
          triggerRef={alertsButtonRef}
          onSetRule={toggleAlertRule}
          onActivateAlert={activateAlert}
          onClose={closeAlerts}
        />
      ) : null}

      {settingsOpen ? (
        <SettingsPanel
          accent={accent}
          appInfo={appInfo}
          persistenceError={persistenceError}
          triggerRef={settingsButtonRef}
          onAccentChange={setAccent}
          onClose={closeSettings}
        />
      ) : null}
    </main>
  );
}
