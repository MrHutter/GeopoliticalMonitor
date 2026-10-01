# UI v0.1 Direction

The primary application UI is embedded React hosted inside the Windows desktop application through a privileged WebView2. A separate native WebView2 hosts the normal X website.

The application is dark-native. The base palette intentionally aligns with the near-black character of the embedded X website while preserving separate trust boundaries and app-owned styling only. The base should read as neutral black/charcoal rather than navy or blue-charcoal.

Visual direction:

- Near-black and neutral-charcoal base surfaces; avoid a noticeably blue/navy application foundation beside X.
- Avoid white-heavy styling.
- Orange is the default accent.
- Blue, red, green, and yellow are optional alternate accents.
- Semantic geopolitical and event colors remain independent from the selected accent.
- Flat dense panels.
- Restrained borders.
- Minimal shadows.
- No cyberpunk, HUD, radar, scanline, terminal-green, or videogame strategy-map styling.
- The map region is the dominant visual element.

Implemented desktop shell:

```text
native window
  main region = privileged React application WebView2
    top    = identity, search, time range, UTC, Alerts, Settings
    body   = Events + Map, ordered by XPaneSide so the Map remains between X and Events
    bottom = selected-event details tabs spanning Map + Events

  outer left or right region = isolated native X website WebView2
    splitter = native horizontal resize grip, persisted locally as XPaneWidth
    side = persisted locally as XPaneSide
    header = My X Feed / X Website
    controls = Back, Forward, Home, Refresh, Open External, Move Left/Right, Copy Post Link, Collapse, Clear Session
    content = normal https://x.com/home website
```

The former React mock right-side X feed is no longer the production right-side pane. Mock X SourceItems remain available where useful for selected-event Sources/X tab tests.

Task 003 uses MapLibre GL JS as the embedded map renderer. The basemap is bundled local Natural Earth-derived 1:110m country/land geometry from `world-atlas`; no remote style URL, tile server, or external map provider is required at runtime. The neutral sovereign layer follows internationally recognized borders and represents Crimea within Ukraine. Operational control or occupation is a distinct future overlay concern requiring independent sources and timestamps.

The Map Layers surface includes `NASA FIRMS — Fire / Thermal Anomalies` as a real session-only layer, default off. Visibility never triggers networking. `Refresh FIRMS` explicitly requests the bounded visible viewport and reports refreshing, unconfigured, oversized-area, provider/safety failure, or last-success summary without clearing a prior successful snapshot. The display control defaults to `Significant`, offers `Strong only` and `All detections`, shows displayed versus total snapshot counts plus the fixed 24h scope, and filters locally without fetching. FIRMS points render below Event markers and the selected Event ring, use four restrained age buckets independent of Accent Theme and Event semantics, open a bounded observation popup without selecting an Event, and show a separate detection-age legend plus NASA FIRMS / LANCE attribution.

The X website retains its own styling and user-selected X display settings. Do not inject Geopolitical Monitor theme CSS into x.com. All five accent themes share the same neutral dark base and change only accent/focus/glow tokens; semantic event colors remain independent.

The Events panel replaces the former static Watchlist label with a compact `All Events` / named-Watchlist scope selector and nearby `Manage` action, while retaining the active range. A provider status strip below the header shows each provider's last update, next refresh, and state in neutral styling (failed and stale use a restrained attention tone, not severity colors); the manual `Refresh GDACS` command sits in the GDACS row. Scope is applied to the one shared Event-row/Map-marker collection before search and time filters, so Map dominance and row-marker synchronization remain intact. Inline pending, success, and error copy stays inside the panel without replacing existing rows. The `NaturalDisaster` semantic color is a restrained green independent of Accent Theme and is used consistently by row edges, MapLibre markers, and the legend. GDACS DatasetRecord cards display attribution, bounded provider facts, external ID, and the canonical source URL without rendering provider HTML.

Permanent layout rule:

```text
Events | Map | X   when XPaneSide is Right
X | Map | Events   when XPaneSide is Left
```

The map remains visually between X and Events. The selected-event details panel stays below the Map + Events region and spans that main-app width. The top bar belongs only to the main application region and must remain responsive so search, time range, and Settings stay reachable when X is wide. All five accent choices remain reachable inside Settings.

Responsive desktop rule: the main app has priority over X width. The native host clamps X before the main application falls below 940 px. Inside the main app, Events uses a bounded side-panel column of roughly 250-320 px while Map is `minmax(0, 1fr)`. No desktop breakpoint may switch the Events + Map workspace to one column.

Compact top bar: at reduced main-app width, the header reflows deliberately rather than hiding required controls. Brand and action groups stay on the first row, Search moves to a full-width second row, and the compact Settings button remains available. The base palette is neutral black/charcoal, not navy.

Alerts surface: the top bar places a compact Alerts control beside Settings. Its unread badge uses the active accent and displays `99+` above 99 without changing the semantic Event colors. Alerts opens a blocking right-side modal using the Settings interaction language: initial focus on Close, explicit forward/reverse focus containment, Escape and backdrop dismissal, and focus return to the trigger. It is mounted only while open; normal startup requests only the unread summary.

The modal has a collapsible Monitoring section with dense Watchlist rows and direct `Monitor GDACS updates` toggles, followed by newest-first persistent history. History rows show Event, Watchlist, fixed GDACS material-update reason, provider revision time, observed time, and read state. Empty/status copy distinguishes no monitored Watchlists, monitoring with no triggered history, no unread Alerts, removed data, and unavailable persistence. There are no toast surfaces, Event-row badges, Details history tab, bulk actions, or pagination in this slice.

Settings surface: the Settings button opens a right-side modal panel over a blocking backdrop. Focus moves into the panel; forward and reverse Tab traversal are explicitly contained; Escape and the close button dismiss it; and focus returns to the trigger. The panel is mounted only while open. Appearance presents Orange, Blue, Red, Green, and Yellow as direct radio choices using the existing persisted `AccentTheme` contract. A read-only Application section shows existing bridge-provided identity, shell, UI host, persistence, and inbound-port information. X-pane controls remain in the native X surface rather than moving into Settings.

Watchlist management reuses the compact right-side modal language for selecting, creating, renaming, and confirming deletion without adding a permanent navigation column. The selected Event Overview presents dense checkbox-style membership controls and a Manage action. Empty states distinguish no Watchlists, an active Watchlist with no members, and members excluded by current filters. Watchlists add no cards, bright SaaS treatment, or provider controls, and active scope is not persisted.

The X website scrollbar is page-owned. App-owned chrome must not overlap the WebView content edge, and X page styling must not be changed through injection.

X toolbar design: row one remains the feed identity plus Move Left/Right and Collapse. Row two keeps primary navigation visible: Back, Forward, Home, Refresh. Secondary actions live in the overflow menu so narrow panes do not show clipped command strips: Open External, Copy Post Link, Clear Session, and Ad Blocker. The X WebView uses the default WebView2 scrollbar; FluentOverlay remains deferred and page CSS injection remains prohibited.

Ad Blocker is a production X control dialog reporting extension-host availability, installed, managed, enabled, and payload-trusted state, version, runtime ID, and whether an application-managed source exists. `Install AdBlock...` accepts only the compiled reviewed Chrome Web Store `6.47.0` payload, creates a bounded staged LocalAppData copy, and revalidates final `Current` before the verified WebView2 install transaction. An old binding requires one explicit re-enrollment and restart; after restart, `Reconcile` removes any same-ID unbound instance and performs a fresh verified add rather than granting authority from ID equality. An unknown newer version remains blocked until a reviewed trust record is added. Enable, Disable, and Remove are available only for the exact revalidated trusted instance. `Test Extension Support` remains a secondary diagnostic action and cannot establish production authenticity.
