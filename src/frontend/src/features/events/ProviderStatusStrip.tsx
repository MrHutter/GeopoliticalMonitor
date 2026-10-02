import { useEffect, useState } from 'react';
import type { ProviderRefreshStatus } from '../../nativeBridge';
import { formatMonthKey } from '../../types/events';

const relativeTimeRefreshMilliseconds = 30_000;

const defaultGdacsStatus: ProviderRefreshStatus = {
  provider: 'Gdacs',
  displayName: 'GDACS',
  state: 'Idle',
  lastSuccessAtUtc: null,
  nextRefreshAtUtc: null,
  message: null,
  newAlerts: 0,
};

type ProviderStatusStripProps = {
  statuses: ProviderRefreshStatus[];
  gdacsRefreshPending: boolean;
  onRefreshGdacs: () => void;
  ucdpRefreshPending?: boolean;
  onRefreshUcdp?: () => void;
};

export function formatUpdated(lastSuccessAtUtc: string | null, nowMilliseconds: number) {
  if (!lastSuccessAtUtc) return 'Not updated yet';
  const timestamp = Date.parse(lastSuccessAtUtc);
  if (!Number.isFinite(timestamp)) return 'Not updated yet';
  const minutes = Math.max(0, Math.floor((nowMilliseconds - timestamp) / 60_000));
  if (minutes < 1) return 'Updated just now';
  if (minutes < 60) return `Updated ${minutes} min ago`;
  return `Updated ${Math.floor(minutes / 60)} h ago`;
}

export function formatNextRefresh(nextRefreshAtUtc: string | null, label = 'Next') {
  if (!nextRefreshAtUtc) return 'No refresh scheduled';
  const date = new Date(nextRefreshAtUtc);
  if (Number.isNaN(date.getTime())) return 'No refresh scheduled';
  return `${label} ${date.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}`;
}

export function formatDataThrough(dataThrough: string | undefined, short = false) {
  if (!dataThrough) return 'no data yet';
  const month = formatMonthKey(dataThrough);
  // The short form ("Aug 2026") keeps the UCDP detail on one line in the narrowest Events panel.
  return `data through ${short ? month.replace(/^(\w{3})\w*/, '$1') : month}`;
}

/** Short state for the first line; Failed shows its fixed message on the second line instead. */
export function formatProviderShortState(status: ProviderRefreshStatus) {
  return status.state === 'Failed' ? 'Failed' : formatProviderState(status);
}

export function formatProviderState(status: ProviderRefreshStatus) {
  switch (status.state) {
    case 'Refreshing':
      return 'Refreshing…';
    case 'Failed':
      return status.message ?? 'Refresh failed.';
    case 'Stale':
      return 'Stale';
    case 'Unconfigured':
      return 'Not configured';
    default:
      return status.message === 'No new release' ? status.message : null;
  }
}

export function ProviderStatusStrip({ statuses, gdacsRefreshPending, onRefreshGdacs, ucdpRefreshPending = false, onRefreshUcdp }: ProviderStatusStripProps) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), relativeTimeRefreshMilliseconds);
    return () => window.clearInterval(id);
  }, []);

  const rows = statuses.some((status) => status.provider === 'Gdacs') ? statuses : [defaultGdacsStatus, ...statuses];

  return (
    <div className="provider-status-strip" role="group" aria-label="Provider refresh status">
      {rows.map((status) => {
        const stateLabel = formatProviderState(status);
        const isRefreshing = status.state === 'Refreshing';
        const isUcdp = status.provider === 'UcdpCandidate';
        const refreshLabel = isUcdp ? 'Refresh UCDP' : 'Refresh GDACS';
        const refreshPending = isUcdp ? ucdpRefreshPending : gdacsRefreshPending;
        const showRefresh = status.provider === 'Gdacs' || (isUcdp && !!onRefreshUcdp);
        const shortState = formatProviderShortState(status);
        const updated = formatUpdated(status.lastSuccessAtUtc, now);
        const isFailed = status.state === 'Failed';
        const detail = isUcdp
          ? `${status.cadenceLabel ?? 'monthly release'}, ${formatDataThrough(status.dataThrough, true)} · ${formatNextRefresh(status.nextRefreshAtUtc, 'Next check')}`
          : `${updated} · ${formatNextRefresh(status.nextRefreshAtUtc)}`;
        const fullText = [
          status.displayName,
          stateLabel,
          isUcdp ? updated : null,
          isUcdp
            ? `${status.cadenceLabel ?? 'monthly release'}, ${formatDataThrough(status.dataThrough)} · ${formatNextRefresh(status.nextRefreshAtUtc, 'Next check')}`
            : detail,
        ].filter(Boolean).join(' · ');
        return (
          <div key={status.provider} className={`provider-status-row is-${status.state.toLowerCase()}`} aria-label={`${status.displayName} refresh status`} title={fullText}>
            <div className="provider-status-line">
              <span className="provider-status-text">
                <span className="provider-status-name">{status.displayName}</span>
                {shortState ? <span className="provider-status-state" role={isFailed ? undefined : 'status'}>{shortState}</span> : null}
                {isUcdp ? <span className="provider-status-updated">{updated}</span> : null}
              </span>
              {showRefresh ? (
                <button
                  type="button"
                  className="gdacs-refresh-button"
                  aria-label={refreshPending ? 'Refreshing...' : refreshLabel}
                  onClick={isUcdp ? onRefreshUcdp : onRefreshGdacs}
                  disabled={refreshPending || isRefreshing}
                >
                  {refreshPending ? 'Refreshing...' : 'Refresh'}
                </button>
              ) : null}
            </div>
            {isFailed ? (
              <div className="provider-status-detail provider-status-message" role="alert">{stateLabel}</div>
            ) : (
              <div className="provider-status-detail">{detail}</div>
            )}
          </div>
        );
      })}
    </div>
  );
}
