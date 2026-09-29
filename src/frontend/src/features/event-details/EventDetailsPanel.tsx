import { useEffect, useState, type FormEvent } from 'react';
import { formatCategory, formatCoordinates, formatEventTime, formatSourceType, NativeEvent, NativeSourceItem } from '../../types/events';
import { MockXPost } from '../../types/mock';
import type { WatchlistSummary } from '../../nativeBridge';
import { ReliefWebDiscovery, type ReliefWebDiscoveryState } from './ReliefWebDiscovery';
import { EventWatchlistControl } from '../watchlists/EventWatchlistControl';

export type DetailTab = 'Overview' | 'Timeline' | 'Sources' | 'X' | 'Related' | 'Location';
export type XPostAttachmentFeedback = {
  eventId: string;
  kind: 'pending' | 'success' | 'info' | 'error';
  message: string;
};

const detailTabs: DetailTab[] = ['Overview', 'Timeline', 'Sources', 'X', 'Related', 'Location'];

type EventDetailsPanelProps = {
  event: NativeEvent | null;
  sources: NativeSourceItem[];
  activeTab: DetailTab;
  relatedPosts: MockXPost[];
  attachedPostIds: Set<string>;
  watchlists: WatchlistSummary[];
  watchlistMemberships: Set<string> | null;
  pendingMembershipWatchlistId: string | null;
  watchlistStatus: string | null;
  xPostAttachmentFeedback: XPostAttachmentFeedback | null;
  onTabChange: (tab: DetailTab) => void;
  onAttachXPost: (eventId: string, url: string) => void;
  reliefWebDiscovery: ReliefWebDiscoveryState | null;
  onSearchReliefWeb: (eventId: string, query: string, fromDateUtc: string, toDateUtc: string, countryIso3?: string) => void;
  onAttachReliefWeb: (eventId: string, reportId: string) => void;
  onToggleWatchlistMembership: (watchlistId: string, isMember: boolean) => void;
  onManageWatchlists: () => void;
};

export function EventDetailsPanel({ event, sources, activeTab, relatedPosts, attachedPostIds, watchlists, watchlistMemberships, pendingMembershipWatchlistId, watchlistStatus, xPostAttachmentFeedback, reliefWebDiscovery, onTabChange, onAttachXPost, onSearchReliefWeb, onAttachReliefWeb, onToggleWatchlistMembership, onManageWatchlists }: EventDetailsPanelProps) {
  const xSources = sources.filter((source) => source.sourceType === 'XPost');
  const [isAttachmentFormOpen, setIsAttachmentFormOpen] = useState(false);
  const [xPostUrl, setXPostUrl] = useState('');
  const [validationMessage, setValidationMessage] = useState<string | null>(null);
  const visibleFeedback = event && xPostAttachmentFeedback?.eventId === event.id ? xPostAttachmentFeedback : null;

  useEffect(() => {
    setIsAttachmentFormOpen(false);
    setXPostUrl('');
    setValidationMessage(null);
  }, [event?.id]);

  useEffect(() => {
    if (visibleFeedback?.kind === 'success' || visibleFeedback?.kind === 'info') {
      setIsAttachmentFormOpen(false);
      setXPostUrl('');
      setValidationMessage(null);
    }
  }, [visibleFeedback]);

  const submitXPost = (formEvent: FormEvent<HTMLFormElement>) => {
    formEvent.preventDefault();
    if (!event) return;
    const normalizedInput = xPostUrl.trim();
    if (!normalizedInput) {
      setValidationMessage('Enter an X post URL.');
      return;
    }

    setValidationMessage(null);
    onAttachXPost(event.id, normalizedInput);
  };

  return (
    <section className="details-panel panel" aria-labelledby="details-title">
      <div className="details-heading">
        <div>
          <p className="eyebrow">Selected event</p>
          <h2 id="details-title">{event?.title ?? 'No event selected'}</h2>
        </div>
        <div className="details-meta" aria-label="selected event metadata">
          <span>{event?.location.region ?? event?.location.displayName ?? 'No location'}</span>
          <span>{event?.status ?? 'Pending'}</span>
          <span>{event?.sourceCount ?? 0} sources</span>
        </div>
      </div>

      <div className="tab-list" role="tablist" aria-label="event detail tabs">
        {detailTabs.map((tab) => (
          <button key={tab} type="button" role="tab" aria-selected={activeTab === tab} className={activeTab === tab ? 'is-active' : ''} onClick={() => onTabChange(tab)}>
            {tab}
          </button>
        ))}
      </div>

      <div className="detail-content" role="tabpanel">
        {!event && <p className="demo-note">No event selected.</p>}

        {event && activeTab === 'Overview' && (
          <>
            <EventWatchlistControl
              watchlists={watchlists}
              memberships={watchlistMemberships}
              pendingWatchlistId={pendingMembershipWatchlistId}
              status={watchlistStatus}
              onToggle={onToggleWatchlistMembership}
              onManage={onManageWatchlists}
            />
            <div className="overview-grid">
              <p>{event.summary}</p>
              <dl>
                <div><dt>Category</dt><dd>{formatCategory(event.category)}</dd></div>
                <div><dt>Status</dt><dd>{event.status}</dd></div>
                <div><dt>Location</dt><dd>{event.location.displayName}</dd></div>
                <div><dt>Started</dt><dd>{formatEventTime(event.startedAtUtc)}</dd></div>
                <div><dt>Updated</dt><dd>{formatEventTime(event.lastUpdatedAtUtc)}</dd></div>
                <div><dt>Coordinates</dt><dd>{formatCoordinates(event.location)}</dd></div>
              </dl>
              <div className="tag-row" aria-label="event tags">
                <span>{formatCategory(event.category)}</span>
                <span>{event.status}</span>
              </div>
            </div>
          </>
        )}

        {event && activeTab === 'Timeline' && (
          <ol className="timeline-list">
            <li><time>{formatEventTime(event.createdAtUtc)}</time><span>Event created in local SQLite.</span></li>
            <li><time>{formatEventTime(event.startedAtUtc)}</time><span>Occurrence start time.</span></li>
            <li><time>{formatEventTime(event.lastUpdatedAtUtc)}</time><span>Latest persisted event update.</span></li>
          </ol>
        )}

        {event && activeTab === 'Sources' && (
          <div className="sources-workspace">
            <div className="source-toolbar">
              <span>{sources.length} linked source{sources.length === 1 ? '' : 's'}</span>
              <button type="button" onClick={() => { setIsAttachmentFormOpen(true); setValidationMessage(null); }}>Attach X post</button>
            </div>

            {isAttachmentFormOpen ? (
              <form className="x-post-attachment-form" onSubmit={submitXPost}>
                <label htmlFor="x-post-url">X post URL</label>
                <input
                  id="x-post-url"
                  type="text"
                  inputMode="url"
                  value={xPostUrl}
                  onChange={(inputEvent) => setXPostUrl(inputEvent.target.value)}
                  placeholder="https://x.com/account/status/123..."
                  aria-describedby="x-post-attachment-feedback"
                  disabled={visibleFeedback?.kind === 'pending'}
                />
                <div className="x-post-attachment-actions">
                  <button type="submit" disabled={visibleFeedback?.kind === 'pending'}>Attach</button>
                  <button type="button" onClick={() => { setIsAttachmentFormOpen(false); setXPostUrl(''); setValidationMessage(null); }} disabled={visibleFeedback?.kind === 'pending'}>Cancel</button>
                </div>
              </form>
            ) : null}

            {validationMessage ? <p id="x-post-attachment-feedback" className="attachment-feedback is-error" role="alert">{validationMessage}</p> : null}
            {!validationMessage && visibleFeedback ? (
              <p id="x-post-attachment-feedback" className={`attachment-feedback is-${visibleFeedback.kind}`} role={visibleFeedback.kind === 'error' ? 'alert' : 'status'}>{visibleFeedback.message}</p>
            ) : null}

            <div className="source-grid">
              {sources.map((source) => (
                <article key={source.id}>
                  <span>{formatSourceType(source.sourceType)}</span>
                  <strong>{source.title ?? source.text.slice(0, 54)}</strong>
                  {source.sourceType === 'XPost' && source.url ? <p className="source-url">{source.url}</p> : <p>{source.text}</p>}
                  {source.sourceType === 'DatasetRecord' && source.url ? <p className="source-url">Canonical source: {source.url}</p> : null}
                  {source.sourceType === 'Other' && source.externalId?.startsWith('ReliefWeb:Report:') && source.url ? <p className="source-url">Canonical ReliefWeb report: {source.url}</p> : null}
                  {source.sourceType === 'Other' && source.externalId?.startsWith('ReliefWeb:Report:') ? <small>Metadata via ReliefWeb</small> : null}
                  {source.externalId ? <small>{source.externalId}</small> : null}
                  <time>{source.authorDisplayName ?? (source.url ? 'URL-only source' : 'Local source')} / {formatEventTime(source.publishedAtUtc)}</time>
                </article>
              ))}
            </div>

            {reliefWebDiscovery ? (
              <ReliefWebDiscovery
                event={event}
                state={reliefWebDiscovery}
                onSearch={onSearchReliefWeb}
                onAttach={onAttachReliefWeb}
              />
            ) : null}
          </div>
        )}

        {event && activeTab === 'X' && (
          <div className="related-x-list">
            {xSources.length ? (
              xSources.map((source) => (
                <article key={source.id}>
                  <strong>{source.authorDisplayName ?? source.title ?? (source.externalId ? `X post ${source.externalId}` : 'X source')}</strong>
                  <span>{source.authorHandle ?? (source.url ? 'URL-only source' : '@demo')} / {formatEventTime(source.publishedAtUtc)}</span>
                  <p className={source.url ? 'source-url' : undefined}>{source.url ?? source.text}</p>
                  {attachedPostIds.has(source.id) ? <em>Attached to selected event</em> : null}
                </article>
              ))
            ) : (
              <p>No persisted X posts linked to this event.</p>
            )}
          </div>
        )}

        {event && activeTab === 'Related' && (
          <div className="related-panel-copy">
            <p>Related mock signals remain reserved for future graph and saved-item workflows. Persisted source associations are shown in Sources and X.</p>
            <span>{relatedPosts.length} mock right-feed item{relatedPosts.length === 1 ? '' : 's'} linked by demo id</span>
          </div>
        )}

        {event && activeTab === 'Location' && (
          <dl className="location-grid">
            <div><dt>Location</dt><dd>{event.location.displayName}</dd></div>
            <div><dt>Region</dt><dd>{event.location.region ?? 'Not specified'}</dd></div>
            <div><dt>Country</dt><dd>{event.location.countryCode ?? 'Not specified'}</dd></div>
            <div><dt>Map point</dt><dd>{formatCoordinates(event.location)}</dd></div>
          </dl>
        )}
      </div>
    </section>
  );
}
