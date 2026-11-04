import { useCallback, useEffect, useRef, type RefObject } from 'react';
import { ucdpAcknowledgement, ucdpCitation, ucdpDownloadsPage, ucdpLicense } from '../sources/ucdpAttribution';
import type { AppInfoMessage, FirmsKeyStatusMessage } from '../../nativeBridge';
import { accentOptions, type Accent } from '../../types/mock';

type SettingsPanelProps = {
  accent: Accent;
  appInfo: AppInfoMessage['payload'] | null;
  persistenceError: string | null;
  triggerRef: RefObject<HTMLButtonElement | null>;
  onAccentChange: (accent: Accent) => void;
  onClose: () => void;
  firmsKeyStatus?: FirmsKeyStatus | null;
  firmsKeyPending?: boolean;
  onSetFirmsKey?: () => void;
  onRemoveFirmsKey?: () => void;
};

type FirmsKeyStatus = FirmsKeyStatusMessage['payload'];

export function firmsKeyStatusLabel(status: FirmsKeyStatus | null | undefined) {
  if (!status) return 'Checking...';
  if (!status.configured) return 'Not configured';
  return status.source === 'App' ? 'Configured (set in app)' : 'Configured (environment variable)';
}

const firmsKeyOutcomeMessages: Partial<Record<NonNullable<FirmsKeyStatus['outcome']>, string>> = {
  Saved: 'Key saved. It is used from the next refresh.',
  Removed: 'Key removed from the app.',
  EmptyKey: 'No key was entered, so nothing was saved.',
  KeyTooLong: 'That key is too long, so nothing was saved.',
  NotStored: 'No key was stored in the app.',
  StoreUnavailable: 'Windows Credential Manager could not be used. Nothing was changed.',
  Busy: 'Another dialog is open. Close it and try again.',
};

const focusableSelector = 'button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

function getFocusableElements(panel: HTMLElement) {
  return Array.from(panel.querySelectorAll<HTMLElement>(focusableSelector))
    .filter((element) => !element.hidden && !element.closest('[hidden], [aria-hidden="true"]'))
    .filter((element) => !(element instanceof HTMLInputElement && element.type === 'radio' && !element.checked));
}

export function SettingsPanel({
  accent,
  appInfo,
  persistenceError,
  triggerRef,
  onAccentChange,
  onClose,
  firmsKeyStatus = null,
  firmsKeyPending = false,
  onSetFirmsKey,
  onRemoveFirmsKey,
}: SettingsPanelProps) {
  const firmsKeyOutcome = firmsKeyStatus?.outcome ? firmsKeyOutcomeMessages[firmsKeyStatus.outcome] : undefined;
  const panelRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  const closeSettings = useCallback(() => {
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
        closeSettings();
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
  }, [closeSettings]);

  return (
    <div className="settings-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) closeSettings(); }}>
      <section
        ref={panelRef}
        className="settings-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="settings-title"
      >
        <header className="settings-header">
          <div>
            <p className="eyebrow">Application</p>
            <h2 id="settings-title">Settings</h2>
          </div>
          <button ref={closeButtonRef} type="button" className="settings-close" aria-label="Close settings" title="Close settings" onClick={closeSettings}>X</button>
        </header>

        <div className="settings-content">
          <section className="settings-section" aria-labelledby="appearance-title">
            <div className="settings-section-heading">
              <p className="eyebrow">Appearance</p>
              <h3 id="appearance-title">Accent theme</h3>
            </div>
            <fieldset className="settings-accent-options">
              <legend className="sr-only">Accent theme</legend>
              {accentOptions.map((option) => (
                <label key={option.value} className="settings-accent-option" data-accent-option={option.value}>
                  <input
                    type="radio"
                    name="settings-accent"
                    value={option.value}
                    checked={accent === option.value}
                    onChange={() => onAccentChange(option.value)}
                  />
                  <i aria-hidden="true" />
                  <span>{option.label}</span>
                </label>
              ))}
            </fieldset>
            {persistenceError ? <p className="settings-error" role="alert">{persistenceError}</p> : null}
          </section>

          {appInfo ? (
            <section className="settings-section" aria-labelledby="application-info-title">
              <div className="settings-section-heading">
                <p className="eyebrow">Runtime</p>
                <h3 id="application-info-title">Application</h3>
              </div>
              <dl className="settings-info">
                <div><dt>Name</dt><dd>{appInfo.applicationName}</dd></div>
                <div><dt>Shell</dt><dd>{appInfo.shell}</dd></div>
                <div><dt>UI host</dt><dd>{appInfo.uiHost}</dd></div>
                <div><dt>Persistence</dt><dd>{appInfo.persistence}</dd></div>
                <div><dt>Inbound ports</dt><dd>{appInfo.productionInboundPortsRequired ? 'Required' : 'None required'}</dd></div>
              </dl>
            </section>
          ) : null}

          <section className="settings-section" aria-labelledby="data-sources-title">
            <div className="settings-section-heading">
              <p className="eyebrow">Keys and attribution</p>
              <h3 id="data-sources-title">Data sources</h3>
            </div>
            <div className="settings-firms-key" aria-labelledby="firms-key-title">
              <dl className="settings-info settings-sources">
                <div>
                  <dt id="firms-key-title">NASA FIRMS key</dt>
                  <dd role="status">{firmsKeyStatusLabel(firmsKeyStatus)}</dd>
                </div>
              </dl>
              {firmsKeyStatus?.formatWarning ? (
                <p className="settings-warning">The key in use is not 32 hexadecimal characters. NASA FIRMS keys normally are, so it may be rejected.</p>
              ) : null}
              {firmsKeyStatus?.storeUnavailable ? (
                <p className="settings-error" role="alert">Windows Credential Manager could not be read. A key set in the app may not be in use.</p>
              ) : null}
              {firmsKeyOutcome ? <p className="settings-note" role="status">{firmsKeyOutcome}</p> : null}
              <div className="settings-actions">
                <button type="button" disabled={firmsKeyPending || !onSetFirmsKey || !firmsKeyStatus} onClick={onSetFirmsKey}>Set key...</button>
                <button
                  type="button"
                  disabled={firmsKeyPending || !onRemoveFirmsKey || firmsKeyStatus?.source !== 'App'}
                  onClick={onRemoveFirmsKey}
                >
                  Remove key
                </button>
              </div>
              <p className="settings-note">
                The key is entered in a Windows dialog and stored in Windows Credential Manager for this Windows account.
                This window never sees it. A key set here is used instead of the GEOPOLITICALMONITOR_FIRMS_MAP_KEY environment variable.
              </p>
            </div>
            <dl className="settings-info settings-sources">
              <div><dt>UCDP Candidate</dt><dd>Uppsala Conflict Data Program, monthly release ({ucdpDownloadsPage})</dd></div>
              <div><dt>Citation</dt><dd>{ucdpCitation}</dd></div>
              <div><dt>Funding</dt><dd>{ucdpAcknowledgement}</dd></div>
              <div><dt>License</dt><dd>{ucdpLicense}</dd></div>
            </dl>
          </section>
        </div>
      </section>
    </div>
  );
}
