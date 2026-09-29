import { useCallback, useEffect, useRef, useState, type FormEvent, type RefObject } from 'react';
import type { WatchlistSummary } from '../../nativeBridge';

type WatchlistManagerProps = {
  watchlists: WatchlistSummary[];
  activeWatchlistId: string | null;
  status: string | null;
  triggerRef: RefObject<HTMLButtonElement | null>;
  onSelect: (watchlistId: string | null) => void;
  onCreate: (name: string) => void;
  onRename: (watchlistId: string, name: string) => void;
  onDelete: (watchlistId: string) => void;
  onClose: () => void;
};

const focusableSelector = 'button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

function getFocusableElements(panel: HTMLElement) {
  return Array.from(panel.querySelectorAll<HTMLElement>(focusableSelector))
    .filter((element) => !element.hidden && !element.closest('[hidden], [aria-hidden="true"]'));
}

export function WatchlistManager({ watchlists, activeWatchlistId, status, triggerRef, onSelect, onCreate, onRename, onDelete, onClose }: WatchlistManagerProps) {
  const panelRef = useRef<HTMLElement>(null);
  const closeButtonRef = useRef<HTMLButtonElement>(null);
  const [newName, setNewName] = useState('');
  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState('');
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const closeManager = useCallback(() => {
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
        closeManager();
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
  }, [closeManager]);

  const submitCreate = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!newName.trim()) return;
    onCreate(newName);
    setNewName('');
  };

  const submitRename = (event: FormEvent<HTMLFormElement>, watchlistId: string) => {
    event.preventDefault();
    if (!renameValue.trim()) return;
    onRename(watchlistId, renameValue);
    setRenamingId(null);
  };

  return (
    <div className="settings-backdrop" onMouseDown={(event) => { if (event.target === event.currentTarget) closeManager(); }}>
      <section ref={panelRef} className="settings-panel watchlist-manager" role="dialog" aria-modal="true" aria-labelledby="watchlist-manager-title">
        <header className="settings-header">
          <div>
            <p className="eyebrow">Local collections</p>
            <h2 id="watchlist-manager-title">Manage watchlists</h2>
          </div>
          <button ref={closeButtonRef} type="button" className="settings-close" aria-label="Close watchlist manager" onClick={closeManager}>X</button>
        </header>

        <div className="settings-content">
          <section className="settings-section">
            <button type="button" className={`watchlist-manager-row ${activeWatchlistId === null ? 'is-active' : ''}`} onClick={() => onSelect(null)}>
              <span>All Events</span><small>Unscoped</small>
            </button>
            {watchlists.length === 0 ? <p className="demo-note">No watchlists. Create one to group monitored events.</p> : null}
            {watchlists.map((watchlist) => (
              <div key={watchlist.id} className="watchlist-manager-item">
                {renamingId === watchlist.id ? (
                  <form className="watchlist-inline-form" onSubmit={(event) => submitRename(event, watchlist.id)}>
                    <label className="sr-only" htmlFor={`rename-${watchlist.id}`}>Watchlist name</label>
                    <input id={`rename-${watchlist.id}`} maxLength={80} value={renameValue} onChange={(event) => setRenameValue(event.target.value)} autoFocus />
                    <button type="submit">Save</button>
                    <button type="button" onClick={() => setRenamingId(null)}>Cancel</button>
                  </form>
                ) : deletingId === watchlist.id ? (
                  <div className="watchlist-delete-confirmation" role="alert">
                    <p>Delete “{watchlist.name}”? Events and sources stay; its Alert monitoring and history will be deleted.</p>
                    <div>
                      <button type="button" className="is-destructive" onClick={() => { onDelete(watchlist.id); setDeletingId(null); }}>Delete</button>
                      <button type="button" onClick={() => setDeletingId(null)}>Cancel</button>
                    </div>
                  </div>
                ) : (
                  <>
                    <button type="button" className={`watchlist-manager-row ${activeWatchlistId === watchlist.id ? 'is-active' : ''}`} onClick={() => onSelect(watchlist.id)}>
                      <span>{watchlist.name}</span><small>{watchlist.eventCount} event{watchlist.eventCount === 1 ? '' : 's'}</small>
                    </button>
                    <div className="watchlist-item-actions">
                      <button type="button" onClick={() => { setRenamingId(watchlist.id); setRenameValue(watchlist.name); setDeletingId(null); }}>Rename</button>
                      <button type="button" onClick={() => { setDeletingId(watchlist.id); setRenamingId(null); }}>Delete</button>
                    </div>
                  </>
                )}
              </div>
            ))}
          </section>

          <section className="settings-section">
            <form className="watchlist-create-form" onSubmit={submitCreate}>
              <label htmlFor="new-watchlist-name">New watchlist</label>
              <div>
                <input id="new-watchlist-name" maxLength={80} value={newName} onChange={(event) => setNewName(event.target.value)} placeholder="Watchlist name" />
                <button type="submit" disabled={!newName.trim()}>Create</button>
              </div>
            </form>
            {status ? <p className="watchlist-status" role="status">{status}</p> : null}
          </section>
        </div>
      </section>
    </div>
  );
}
