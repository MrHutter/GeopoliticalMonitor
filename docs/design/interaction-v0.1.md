# Interaction v0.1 Direction

The app is launched as a Windows desktop executable.

Selecting a persisted Event from the map or event feed synchronizes the same shared selected-event state.

Clicking inside a conflict polygon or a tinted tension country, where no Event marker or FIRMS point is under the pointer, opens one bounded popup listing every entry there (name, class, parties, basis, as-of date, "approximate" for polygons, and the review status). Event markers and FIRMS points keep click priority; the popup never selects or creates an Event. A hidden reference layer opens no popup.

The Event header scope starts at `All Events` each launch and can select one locally persisted Watchlist. Each launch also starts at the `30D` time range and the `High and above` minimum severity with an empty search; none of these is persisted, and the first Event request already carries them. The native Event query applies global-or-Watchlist membership scope, time range, search, and minimum severity with AND semantics and returns at most the newest 2,000 matching Events by last update (plus the total match count); that window is the one shared collection for rows and Map markers, displayed newest first (last update descending, then id ascending, the native window order). The list scrolls inside the bounded Events panel and renders 100 rows at first and 100 more when scrolling near the end; the counter, capped notice and Map always use the whole collection. Changing scope, range, search or severity returns the list to the top and the first 100 rows; reloads with unchanged criteria keep the rendered rows and scroll position. A selection from the Map, an Alert or a kept Event always has its row rendered and is scrolled into view once. When more Events match, the panel shows `Newest N of M shown.`; the full sentence `Newest N of M shown. Narrow range or search for others.` is in its hover text and accessible name. Search typing is debounced (about 250 ms), only the latest response is applied, and the previous result stays visible while a request is in flight. The selected Event is sent as a keep id, so it stays in the window while it still matches the criteria even if it is older than the newest 2,000. If the selected Event no longer matches, selection clears and is not implicitly restored when the scope or filter is later broadened. When nothing matches, the list names a narrowed range and severity and how to widen them, for example `No Events at High and above in the last 30 days. Widen the range or severity to see more.`; `No Events yet. Automatic refresh is running.` appears only when the store is empty at `ALL`, all severities, `All Events` and no search.

Selection should:

- Highlight the relevant map marker.
- Highlight the relevant event-feed row.
- Populate the bottom selected-event details panel.

Ordinary event selection must not aggressively zoom the map. The Sources tab supports explicit manual X status-URL attachment to the selected Event; automatic import remains absent.

The Events header includes a compact provider status strip with one line per provider. Each provider line shows its name, its state, `Updated N min ago` (or `Not updated yet`), the next refresh time, and an inline Refresh button; the states are: refreshing, stale, failed with a fixed short message, or not configured. Relative times update about every 30 seconds. GDACS refreshes on an in-app timer while the app is open, and its row holds the manual `Refresh GDACS` action. Activating it disables the control while one native request is in flight, retains the current list and map, and shows a bounded result or failure; the control is also disabled while a timer-driven refresh runs. A finished refresh, manual or timer, reloads the authoritative local Event list without clearing the selected Event, search, time range, severity filter, Watchlist scope, or open Details. The UCDP Candidate row also shows `monthly release` and the month its data runs through, the next check time, `No new release` after a check that found nothing, and the manual `Refresh UCDP` action, disabled while UCDP is refreshing.

GDACS earthquake rows participate in the same search and time-range filters as every Event. `Natural Disaster` is searchable display text, markers use the Event epicenter, and selecting an imported Event exposes its `Unverified` status plus the linked canonical GDACS DatasetRecord source.

The selected Event's Sources tab also offers `Find ReliefWeb reports`. Opening the compact form does not search; the user may edit the title-derived query, UTC date range, and optional existing ISO3 value before explicitly submitting. At most 20 candidates remain transient to that Event. Attachment is a separate explicit action, refreshes authoritative Sources on success, and clears no existing source rows on failure. Switching Events clears candidates and status without changing persisted associations.

`Manage` opens the compact Watchlist surface for selection, creation, rename, and confirmed deletion. A newly created empty Watchlist becomes active. Renaming preserves its identity and active scope. Deleting an active Watchlist returns to `All Events`; deleting any Watchlist preserves Events and Sources. The selected Event Overview shows explicit membership checkboxes, supports the same Event in multiple Watchlists, and provides a useful create/manage path when no Watchlists exist. Watchlist selection itself is session-only.

The top-bar Alerts control shows the local unread count, capped visually at `99+`. Startup requests only that summary and performs no provider work. Activating Alerts opens a blocking right-side modal, then loads Watchlist monitoring rules and the newest 200 persistent history rows. Its Monitoring section exposes one `Monitor GDACS updates` toggle per Watchlist. Enabling begins with future qualifying refreshes and does not backfill; disabling preserves history.

Alert history distinguishes unread and read entries and provides no acknowledgement, dismiss, delete, snooze, or pagination action. Activating an entry attempts mark-read, closes the modal, returns Event scope to `All Events`, clears search, selects `ALL` time, clears the severity filter, selects the referenced Event (requested as the keep id, so it appears even when it is older than the newest 2,000), opens Overview, and explicitly locates it on the Map. Scrolling the page to the Details panel on activation is implemented behind the `alertScrollsToDetails` flag, which ships turned off, so activation does not scroll today. Navigation still proceeds if mark-read fails; a removed underlying Event produces a bounded stale-history status. The modal traps focus, supports Escape/backdrop/close dismissal, and returns focus to the Alerts trigger.

Only a GDACS refresh, manual or timer-driven, can create Alerts. A qualifying material Event update refreshes the unread summary and already-loaded history through coalesced bridge requests. Watchlist deletion warns that its monitoring and history will be removed, while Events and Sources stay; successful deletion reconciles Alert summary, rules, and loaded history. There are no Windows notifications and no app-closed monitoring; the GDACS timer runs only while the app is open.

## X Website Pane

The native X pane is a user-operated X website browser surface that can sit on the left or right outer edge. It loads `https://x.com/home` by default. If the user is signed out, X shows its normal sign-in experience. If X allows WebView2 sign-in, the user may sign in directly with X; Geopolitical Monitor does not observe credentials.

The user manually chooses For You or Following inside the X website. The app must not automatically click Following, scroll, collect posts, inspect DOM content, inject scripts, or import posts.

Pane controls:

- Back and Forward use normal WebView history.
- Home navigates to `https://x.com/home`.
- Refresh performs normal browser reload.
- Open External opens the current permitted X page, or X Home fallback, in the system browser.
- Move Left/Move Right switches the X pane side without navigating or recreating the X page; XPaneSide persists locally.
- Copy Post Link is enabled only for current public X status URLs, normalizes the URL, and writes only that URL to the clipboard.
- The vertical splitter between the app and X pane resizes the X pane horizontally. Width changes apply immediately during drag and persist to the local `XPaneWidth` preference when the drag completes.
- Collapse hides the native X pane and splitter, does not save zero width, and lets the React application region expand.
- Clear Session asks for confirmation, clears the isolated X WebView2 browsing data where supported, and returns to X Home.

External non-X HTTPS links from the X website open in the user's system browser. Non-HTTPS schemes are blocked.

Orange, Blue, Red, Green, and Yellow accent preferences are selected in the application Settings panel and persist through the native desktop bridge into the local SQLite `AccentTheme` preference. Browser `localStorage` is not the preference authority. A change applies immediately, and reopening Settings shows the persisted active choice. The selected-marker ring follows the active accent, while semantic event marker colors remain independent from accent choice. The native X pane header and splitter hover/drag state follow the selected accent where practical; x.com itself is not restyled, inspected, or injected.

Do not add cloud sync, external map tiles, automatic provider refresh beyond the approved in-app GDACS and UCDP Candidate timer, automatic event correlation, X API access, OAuth developer app requirements, or additional external synchronization without explicit task authority.

Layout contract:

```text
Events | Map | X   when X is Right
X | Map | Events   when X is Left
```

The map remains between X and Events. Details remain below the Map + Events region; the Map and Events extend to the bottom of the window and Details are reached by scrolling the page. Selecting an Event updates Details but does not scroll the page. The React top bar adapts at narrower widths while retaining a compact Settings button. Settings is a modal side panel: while open it blocks pointer interaction, explicitly cycles forward and reverse keyboard focus within its live controls, supports Escape dismissal and focus return, and leaves Event selection, search, and time-range state intact. The Settings component is mounted only while open and adds no Settings-specific native bridge request to normal dashboard startup.

Copy Post Link alone does not create SourceItem, Event, or EventSourceAssociation records and does not extract post text, media, replies, counts, cookies, tokens, or page source. To attach a source, the user opens the selected Event's Sources tab, chooses Attach X post, manually pastes the copied public status URL, and submits it. The application derives only the creation timestamp from the numeric status ID, persists a URL-only XPost SourceItem, and reuses that SourceItem when the same canonical URL supports another Event.

During X resizing, the same clamp policy applies on both sides: effective maximum X width is the smaller of the configured percentage cap and `window width - 940 px main app minimum - splitter width`. Dragging updates the rendered width live and persists only on drag completion. A temporarily oversized stored preference is clamped for the current window without being rewritten solely because the window is smaller.

The right-click post-link affordance uses only WebView2 context-menu target metadata. If right-clicking a post body does not expose a valid status URL, the app must not walk DOM ancestry, inspect accessibility data, inject scripts, or intercept network traffic to infer it; the top-level Copy Post Link remains available after opening an individual post.

Ad Blocker opens the production X status dialog. Opening it explicitly enumerates the live X profile and revalidates the production binding; startup does neither. `Install AdBlock...` lets the owner select an official unpacked eyeo Chrome MV3 root, validates bounded identity metadata, copies it to stable application-owned LocalAppData, verifies the copy, and installs that managed path. `Reconcile` can explicitly restore authority only when that exact managed-path-derived instance is already installed. Enable, Disable, Remove, and refresh operate through the owning X WebView dispatcher and verify post-operation state. Remove clears the binding only after verified WebView2 removal, then separately attempts managed-payload cleanup without touching the owner's source. Microsoft component and unrelated extensions remain untouched. `Test Extension Support` opens the separate diagnostic flow and is never automatic.
