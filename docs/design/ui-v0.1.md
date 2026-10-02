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

The Events panel replaces the former static Watchlist label with a compact `All Events` / named-Watchlist scope selector and nearby `Manage` action, while retaining the active range. A provider status strip below the header shows each provider's last update, next refresh, and state in neutral styling (failed and stale use a restrained attention tone, not severity colors); the manual `Refresh GDACS` command sits in the GDACS row, and the UCDP Candidate row adds its `monthly release` label, data-through month and `Refresh UCDP` command. Scope, search, time range, and severity are applied by the native windowed query, whose result (at most the newest 2,000 matches) is the one shared Event-row/Map-marker collection, so Map dominance and row-marker synchronization remain intact. When the result is capped, a dense muted notice under the summary states how many of how many matching Events are shown. Inline pending, success, and error copy stays inside the panel without replacing existing rows. The `NaturalDisaster` semantic color is a restrained green independent of Accent Theme and is used consistently by row edges, MapLibre markers, and the legend. GDACS DatasetRecord cards display attribution, bounded provider facts, external ID, and the canonical source URL without rendering provider HTML.

Permanent layout rule:

```text
Events | Map | X   when XPaneSide is Right
X | Map | Events   when XPaneSide is Left
```

The map remains visually between X and Events. The selected-event details panel stays below the Map + Events region and spans that main-app width. The top bar belongs only to the main application region and must remain responsive so search, time range, and Settings stay reachable when X is wide. All five accent choices remain reachable inside Settings.

Responsive desktop rule: the main app has priority over X width. The native host clamps X before the main application falls below 940 px. Inside the main app, Events uses a bounded side-panel column of 300-320 px (`clamp(300px, 28%, 320px)`, 310 px maximum up to 1,350 px and up to 340 px at 1,900 px and wider) while Map is `minmax(0, 1fr)`; at the 940 px minimum app view the Map keeps about 610 px. No desktop breakpoint may switch the Events + Map workspace to one column.

Bounded workspace: the top bar and Map + Events row form a first-screen block whose height is definite and derived only from the viewport (`max(calc(100vh - 10px), 590px)`), so Map and Events reach the bottom edge of the window and the row (at least 520 px) never takes its height from the Event list. Details follow below that block at their natural height (at least 230 px) and are reached by scrolling the page. The Events panel is a flex column whose header, provider status strip and notices keep their natural height while only the list scrolls (`min-height: 0`, `overflow-y: auto`). The Map column flexes with the row and MapLibre resizes through a ResizeObserver on its canvas.

Density: the Events panel is a size container (`container: events-panel / inline-size`); its default layout is built for a 300 px panel and container queries only relax it at wider widths (and stack the selects below 260 px). The header puts the label, the events counter, Manage and the range on one row (the sources counter is shown only from a 350 px container and is otherwise visually hidden but available to assistive technology; nothing wraps onto a clipped line) and both selects on a second row, wide enough for `All Events` and `All severities` in full (longer labels ellipsize with the full text in the title). Each provider status is at most two lines: line 1 has the name, a short state and an inline `Refresh` button that never wraps (its accessible name stays `Refresh GDACS` or `Refresh UCDP`); line 2 is a muted detail (`Updated … · Next …` for GDACS, `monthly release, data through Aug 2026 · Next check …` for UCDP, or the fixed failure message). The capped notice is exactly one line, `Newest N of M shown.` (the advice part is shown only from a 370 px container), with the complete sentence `… Narrow range or search for others.` as its title and accessible name. Counter, notice and provider lines are single nowrap lines with fixed line heights that ellipsize as a last resort, so the list always begins below them. The map toolbar starts with the small `Map` label and title; the layer toggles grow evenly (up to 360 px each) to fill every row they share, so wrapped toggle rows span the full width; the FIRMS display filter, `Refresh FIRMS` and the 24h label keep their natural widths together on a row of their own, with the FIRMS summary or status right-aligned on that row when it fits and on its own line below otherwise. With FIRMS off the toolbar is two rows from a map about 1,000 px wide and three rows at about 612 px. The placeholder Aircraft, Naval and Internet outages toggles are hidden behind `showPlaceholderMapLayers` (false); they never affected the map. The legend lists one swatch per Event category in the marker colors and list labels, plus a `Size = severity` key drawn at the marker sizes. Event markers are about 28% smaller than before (3-5.4 px at world zoom).

Compact top bar: from the 940 px minimum app view up the top bar is one row (compact brand, flexible search of at least 180 px, range buttons, UTC clock, Alerts with its visible label, Settings). The former info bar (shell, persistence, inbound ports) is no longer on the main screen; Settings shows those facts. The map toolbar's layer toggles fill each row they share and the FIRMS controls take their own row (see Density). The base palette is neutral black/charcoal, not navy.

Alerts surface: the top bar places a compact Alerts control beside Settings. Its unread badge uses the active accent and displays `99+` above 99 without changing the semantic Event colors. Alerts opens a blocking right-side modal using the Settings interaction language: initial focus on Close, explicit forward/reverse focus containment, Escape and backdrop dismissal, and focus return to the trigger. It is mounted only while open; normal startup requests only the unread summary.

The modal has a collapsible Monitoring section with dense Watchlist rows and direct `Monitor GDACS updates` toggles, followed by newest-first persistent history. History rows show Event, Watchlist, fixed GDACS material-update reason, provider revision time, observed time, and read state. Empty/status copy distinguishes no monitored Watchlists, monitoring with no triggered history, no unread Alerts, removed data, and unavailable persistence. There are no toast surfaces, Event-row badges, Details history tab, bulk actions, or pagination in this slice.

Settings surface: the Settings button opens a right-side modal panel over a blocking backdrop. Focus moves into the panel; forward and reverse Tab traversal are explicitly contained; Escape and the close button dismiss it; and focus returns to the trigger. The panel is mounted only while open. Appearance presents Orange, Blue, Red, Green, and Yellow as direct radio choices using the existing persisted `AccentTheme` contract. A read-only Application section shows existing bridge-provided identity, shell, UI host, persistence, and inbound-port information. X-pane controls remain in the native X surface rather than moving into Settings.

Watchlist management reuses the compact right-side modal language for selecting, creating, renaming, and confirming deletion without adding a permanent navigation column. The selected Event Overview presents dense checkbox-style membership controls and a Manage action. Empty states distinguish no Watchlists, an active Watchlist with no members, and members excluded by current filters. Watchlists add no cards, bright SaaS treatment, or provider controls, and active scope is not persisted.

The X website scrollbar is page-owned. App-owned chrome must not overlap the WebView content edge, and X page styling must not be changed through injection.

X toolbar design: row one remains the feed identity plus Move Left/Right and Collapse. Row two keeps primary navigation visible: Back, Forward, Home, Refresh. Secondary actions live in the overflow menu so narrow panes do not show clipped command strips: Open External, Copy Post Link, Clear Session, and Ad Blocker. The X WebView uses the default WebView2 scrollbar; FluentOverlay remains deferred and page CSS injection remains prohibited.

Ad Blocker is a production X control dialog reporting extension-host availability, installed, managed, enabled, and payload-trusted state, version, runtime ID, and whether an application-managed source exists. `Install AdBlock...` accepts only the compiled reviewed Chrome Web Store `6.47.0` payload, creates a bounded staged LocalAppData copy, and revalidates final `Current` before the verified WebView2 install transaction. An old binding requires one explicit re-enrollment and restart; after restart, `Reconcile` removes any same-ID unbound instance and performs a fresh verified add rather than granting authority from ID equality. An unknown newer version remains blocked until a reviewed trust record is added. Enable, Disable, and Remove are available only for the exact revalidated trusted instance. `Test Extension Support` remains a secondary diagnostic action and cannot establish production authenticity.
