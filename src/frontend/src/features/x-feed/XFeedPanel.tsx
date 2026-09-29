import { NativeEvent } from '../../types/events';
import { MockXPost } from '../../types/mock';

type XFeedPanelProps = {
  posts: MockXPost[];
  eventsById: Map<string, NativeEvent>;
  savedPostIds: Set<string>;
  attachedPostIds: Set<string>;
  onLocatePost: (post: MockXPost) => void;
  onAttachPost: (post: MockXPost) => void;
  onToggleSavedPost: (postId: string) => void;
};

export function XFeedPanel({ posts, eventsById, savedPostIds, attachedPostIds, onLocatePost, onAttachPost, onToggleSavedPost }: XFeedPanelProps) {
  return (
    <aside className="x-feed panel" aria-labelledby="x-feed-title">
      <div className="panel-header">
        <div>
          <p className="eyebrow">My X Feed</p>
          <h2 id="x-feed-title">Following</h2>
        </div>
        <span className="count-pill">Mock</span>
      </div>

      <div className="x-post-list" aria-label="mock X Following feed">
        {posts.map((post) => {
          const relatedEvent = post.relatedEventId ? eventsById.get(post.relatedEventId) : undefined;
          const saved = savedPostIds.has(post.id);
          const attached = attachedPostIds.has(post.id);

          return (
            <article key={post.id} className={`x-post ${saved ? 'is-saved' : ''}`}>
              <header>
                <div><strong>{post.displayName}</strong><span>{post.handle}</span></div>
                <time>{post.timestamp}</time>
              </header>
              <p>{post.text}</p>
              {relatedEvent ? <span className="related-chip">{relatedEvent.location.region ?? relatedEvent.location.displayName}</span> : <span className="related-chip is-muted">Unlinked source</span>}
              <div className="post-actions">
                <button type="button" onClick={() => onLocatePost(post)} disabled={!relatedEvent}>Locate</button>
                <button type="button" onClick={() => onAttachPost(post)} disabled={!relatedEvent}>{attached ? 'Attached' : 'Attach'}</button>
                <button type="button" className={saved ? 'is-active' : ''} onClick={() => onToggleSavedPost(post.id)}>{saved ? 'Saved' : 'Save'}</button>
              </div>
            </article>
          );
        })}
      </div>
    </aside>
  );
}