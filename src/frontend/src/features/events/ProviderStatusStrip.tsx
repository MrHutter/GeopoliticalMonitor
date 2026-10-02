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

export function formatDataThrough(dataThrough: string | undefined) {
  return dataThrough ? `data through ${formatMonthKey(dataThrough)}` : 'no data yet';
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
        return (
          <div key={status.provider} className={`provider-status-row is-${status.state.toLowerCase()}`} aria-label={`${status.displayName} refresh status`}>
            <span className="provider-status-name">{status.displayName}</span>
            {isUcdp ? (
              <span className="provider-status-cadence">
                {status.cadenceLabel ?? 'monthly release'}, {formatDataThrough(status.dataThrough)}
              </span>
            ) : null}
            <span className="provider-status-updated">{formatUpdated(status.lastSuccessAtUtc, now)}</span>
            <span className="provider-status-next">{formatNextRefresh(status.nextRefreshAtUtc, isUcdp ? 'Next check' : 'Next')}</span>
            {stateLabel ? <span className="provider-status-state" role={status.state === 'Failed' ? 'alert' : 'status'}>{stateLabel}</span> : null}
            {status.provider === 'Gdacs' ? (
              <button type="button" className="gdacs-refresh-button" onClick={onRefreshGdacs} disabled={gdacsRefreshPending || isRefreshing}>
                {gdacsRefreshPending ? 'Refreshing...' : 'Refresh GDACS'}
              </button>
            ) : null}
            {isUcdp && onRefreshUcdp ? (
              <button type="button" className="gdacs-refresh-button" onClick={onRefreshUcdp} disabled={ucdpRefreshPending || isRefreshing}>
                {ucdpRefreshPending ? 'Refreshing...' : 'Refresh UCDP'}
              </button>
            ) : null}
          </div>
        );
      })}
    </div>
  );
}
