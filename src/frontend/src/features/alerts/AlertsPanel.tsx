import { useCallback, useEffect, useRef, type RefObject } from 'react';
import type { AlertOccurrence, AlertRule, WatchlistSummary } from '../../nativeBridge';

type AlertsPanelProps = {
  rules: AlertRule[];
  alerts: AlertOccurrence[];
  unreadCount: number;
  hasMore: boolean;
  watchlists: WatchlistSummary[];
  pendingWatchlistId: string | null;
  status: { kind: 'info' | 'error'; message: string } | null;
  triggerRef: RefObject<HTMLButtonElement | null>;
  onSetRule: (watchlistId: string, enabled: boolean) => void;
  onActivateAlert: (alert: AlertOccurrence) => void;
  onClose: () => void;
};

const focusableSelector = 'button:not([disabled]), input:not([disabled]), summary, [tabindex]:not([tabindex="-1"])';

function getFocusableElements(panel: HTMLElement) {
  return Array.from(panel.querySelectorAll<HTMLElement>(focusableSelector))
    .filter((element) => !element.hidden && !element.closest('[hidden], [aria-hidden="true"]'));
}

function formatUtc(value: string) {
  return new Intl.DateTimeFormat('en-GB', {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: 'UTC',
  }).format(new Date(value)) + ' UTC';
}

export function AlertsPanel({
  rules,
  alerts,
  unreadCount,
  hasMore,
  watchlists,
  pendingWatchlistId,
  status,
  triggerRef,
  onSetRule,
  onActivateAlert,
  onClose,
}: AlertsPanelProps) {
  const panelRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const enabledRuleCount = rules.filter((rule) => rule.isEnabled).length;

  const closePanel = useCallback(() => {
    onClose();
    queueMicrotask(() => triggerRef.current?.focus());
  }, [onClose, triggerRef]);

  useEffect(() => {
    const panel = panelRef.current;
    if (!panel) return;

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        closePanel();
        return;
      }

      if (event.key !== 'Tab') return;
      const focusable = getFocusableElements(panel);
      if (focusable.length === 0) return;
      event.preventDefault();
      event.stopPropagation();
      const currentIndex = focusable.indexOf(document.activeElement as HTMLElement);
      const nextIndex = event.shiftKey
        ? (currentIndex <= 0 ? focusable.length - 1 : currentIndex - 1)
        : (currentIndex < 0 || currentIndex === focusable.length - 1 ? 0 : currentIndex + 1);
      focusable[nextIndex].focus();
    };

    const handleFocusIn = (event: FocusEvent) => {
      if (event.target instanceof Node && !panel.contains(event.target)) {
        getFocusableElements(panel)[0]?.focus();
      }
    };

    document.addEventListener('keydown', handleKeyDown, true);
    document.addEventListener('focusin', handleFocusIn, true);
    closeButtonRef.current?.focus();
    return () => {
      document.removeEventListener('keydown', handleKeyDown, true);
      document.removeEventListener('focusin', handleFocusIn, true);
    };
  }, [closePanel]);

  return (
    <div className="settings-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) closePanel(); }}>
      <section ref={panelRef} className="settings-panel alerts-panel" role="dialog" aria-modal="true" aria-labelledby="alerts-panel-title">
        <header className="settings-header">
          <div>
            <p className="eyebrow">Local monitoring</p>
            <h2 id="alerts-panel-title">Alerts</h2>
          </div>
          <button ref={closeButtonRef} type="button" className="settings-close" aria-label="Close Alerts" onClick={closePanel}>X</button>
        </header>

        <div className="settings-content alerts-content">
          <details className="alerts-monitoring" open>
            <summary>Monitoring</summary>
            <div className="alerts-monitoring-list">
              {watchlists.length === 0 ? <p className="demo-note">Create a Watchlist before enabling monitoring.</p> : null}
              {watchlists.map((watchlist) => {
                const rule = rules.find((item) => item.watchlistId === watchlist.id);
                const pending = pendingWatchlistId === watchlist.id;
                return (
                  <label key={watchlist.id} className="alert-rule-row">
                    <span><strong>{watchlist.name}</strong><small>Monitor GDACS updates</small></span>
                    <input
                      type="checkbox"
                      checked={rule?.isEnabled ?? false}
                      disabled={pending}
                      onChange={(event) => onSetRule(watchlist.id, event.target.checked)}
                    />
                  </label>
                );
              })}
              {enabledRuleCount === 0 ? <p className="alerts-empty">No Watchlists are monitored.</p> : null}
            </div>
          </details>

          <section className="alerts-history" aria-labelledby="alerts-history-title">
            <header>
              <h3 id="alerts-history-title">History</h3>
              <span>{unreadCount > 0 ? `${unreadCount} unread` : 'Read'}</span>
            </header>

            {status ? <p className={`alerts-status is-${status.kind}`} role={status.kind === 'error' ? 'alert' : 'status'}>{status.message}</p> : null}
            {alerts.length === 0 && enabledRuleCount > 0 ? <p className="alerts-empty">Monitoring is on. No updates have triggered an alert yet.</p> : null}
            {alerts.length > 0 && unreadCount === 0 ? <p className="alerts-empty">No unread alerts.</p> : null}

            <div className="alert-list">
              {alerts.map((alert) => (
                <button
                  key={alert.id}
                  type="button"
                  className={`alert-list-item ${alert.readAtUtc ? 'is-read' : 'is-unread'}`}
                  onClick={() => onActivateAlert(alert)}
                >
                  <span className="alert-list-heading"><strong>{alert.eventTitle}</strong><em>{alert.readAtUtc ? 'Read' : 'Unread'}</em></span>
                  <span>{alert.watchlistName}</span>
                  <span>GDACS reported a material Event update</span>
                  <small>Provider revision {formatUtc(alert.providerRevisionAtUtc)}</small>
                  <small>Observed {formatUtc(alert.triggeredAtUtc)}</small>
                </button>
              ))}
            </div>
            {hasMore ? <p className="alerts-empty">Showing the newest 200 alerts.</p> : null}
          </section>
        </div>
      </section>
    </div>
  );
}
