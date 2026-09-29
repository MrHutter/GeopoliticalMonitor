import type { RefObject } from 'react';
import { productDisplayNameUpper } from '../../productIdentity';
import { eventTimeRanges, type EventTimeRange } from '../events/eventFilters';

type TopBarProps = {
  searchQuery: string;
  onSearchQueryChange: (query: string) => void;
  selectedRange: EventTimeRange;
  onRangeChange: (range: EventTimeRange) => void;
  unreadAlertCount: number;
  alertsButtonRef: RefObject<HTMLButtonElement | null>;
  onOpenAlerts: () => void;
  settingsButtonRef: RefObject<HTMLButtonElement | null>;
  onOpenSettings: () => void;
};

export function TopBar({ searchQuery, onSearchQueryChange, selectedRange, onRangeChange, unreadAlertCount, alertsButtonRef, onOpenAlerts, settingsButtonRef, onOpenSettings }: TopBarProps) {
  const utcTime = new Intl.DateTimeFormat('en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date());

  return (
    <header className="top-bar">
      <div className="brand-lockup" aria-label="Application identity">
        <span className="brand-mark" aria-hidden="true" />
        <div>
          <strong>{productDisplayNameUpper}</strong>
          <span>LOCAL DESKTOP</span>
        </div>
      </div>

      <label className="search-box">
        <span className="sr-only">Search</span>
        <input
          type="search"
          placeholder="Search events, locations, keywords..."
          value={searchQuery}
          onChange={(event) => onSearchQueryChange(event.target.value)}
        />
      </label>

      <div className="top-actions">
        <div className="range-control" aria-label="Time range">
          {eventTimeRanges.map((range) => (
            <button
              key={range}
              type="button"
              className={range === selectedRange ? 'is-active' : ''}
              onClick={() => onRangeChange(range)}
            >
              {range}
            </button>
          ))}
        </div>
        <span className="utc-clock" aria-label="UTC time">UTC {utcTime}</span>
        <button ref={alertsButtonRef} type="button" className="alerts-entry" aria-label={`Alerts${unreadAlertCount > 0 ? `, ${unreadAlertCount} unread` : ''}`} title="Alerts" onClick={onOpenAlerts}>
          <span aria-hidden="true">Alerts</span>
          {unreadAlertCount > 0 ? <strong className="alerts-badge">{unreadAlertCount > 99 ? '99+' : unreadAlertCount}</strong> : null}
        </button>
        <button ref={settingsButtonRef} type="button" className="settings-entry" aria-label="Settings" title="Settings" onClick={onOpenSettings}>
          <span aria-hidden="true">{'\u2699\uFE0E'}</span>
        </button>
      </div>
    </header>
  );
}
