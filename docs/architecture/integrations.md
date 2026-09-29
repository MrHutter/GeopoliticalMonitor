# Integrations

## GDACS Earthquakes

The first live data provider is the official Global Disaster Awareness and Coordination System, GDACS. An explicit `Refresh GDACS` action calls `https://www.gdacs.org/gdacsapi/api/events/geteventlist/SEARCH` with `eventlist=EQ`, preceding-four-day `fromdate`/`todate`, `pagesize=100`, and `pagenumber=1`. The native adapter allows no user URL, credential, paging, retry loop, startup request, or periodic polling. Requests time out after 15 seconds and responses are size- and record-bounded.

Strict GeoJSON parsing accepts point earthquake features only. Individual malformed features are skipped and counted; a malformed response or a nonempty response with no valid records fails without writes. Provider event and episode IDs produce deterministic Event and `DatasetRecord` SourceItem identities. Newer provider timestamps may update only provider-owned fields, and attached canonical GDACS provenance is required before an existing deterministic Event can be changed.

An enabled Watchlist Alert rule is evaluated only during this manual refresh. A strictly newer GDACS revision triggers only when an existing watched Event materially changes in title, summary, start time, or stored location fields. New Events, source-only/new-episode changes, revision-only updates, stale or repeated data, equal-timestamp conflicts, membership changes, X attachment, and ReliefWeb attachment do not trigger. The Event update and deduplicated Alert occurrence commit atomically in the same SQLite transaction.

Imported information is attributed to the Global Disaster Awareness and Coordination System, GDACS. GDACS automated alerts and impact information may be uncertain and should be confirmed with competent authorities before critical decisions. Other GDACS hazards, polygons, tracks, paging, scheduled refresh, GDELT, and a generic provider registry remain deferred.

## NASA FIRMS Fire / Thermal Anomalies

NASA FIRMS is a transient live map boundary, not Event ingestion. An explicit `Refresh FIRMS` command sends only the current viewport's west, south, east, and north values through the privileged bridge. Native code validates a maximum normalized span of 90 degrees longitude and 60 degrees latitude, splits valid antimeridian coverage, and constructs fixed-host HTTPS requests to `firms.modaps.eosdis.nasa.gov`. React cannot select the host, URL, method, product, day range, response limits, or MAP_KEY.

Every logical refresh retrieves exactly `VIIRS_NOAA20_NRT` and `VIIRS_NOAA21_NRT` using `DAY_RANGE=2`. Each HTTP operation has a 15-second timeout, caller/lifetime cancellation, redirects disabled, no retry, and a 4 MiB response limit. Strict CSV parsing validates the known VIIRS fields. Native orchestration retains observations from the rolling latest 24 hours, rejects clearly future data, collapses only exact deterministic identities, and rejects rather than truncates snapshots above 20,000 observations. All normal or antimeridian requests must succeed before the frontend replaces its prior snapshot.

The provider's two-day request is a calendar-day acquisition mechanism, not the user-facing time range; the only first-slice view is the native-filtered rolling latest 24 hours. React retains the complete normalized snapshot and derives the visible subset locally. `Significant` is the default and requires nominal/high confidence plus FRP >= 10 MW; `Strong only` requires nominal/high confidence plus FRP >= 50 MW; `All detections` applies no additional display filter. These are Geopolitical Monitor operational-display presets rather than NASA severity definitions, and changing them issues no bridge or provider request.

The MAP_KEY is read from the process environment variable `GEOPOLITICALMONITOR_FIRMS_MAP_KEY`, trimmed, and held only in native client options. Missing configuration fails locally before HTTP dispatch. FIRMS never runs at startup, on pan/zoom, or from layer visibility changes, and it has no polling, timer, persistence, Event/Source conversion, Watchlist integration, or Alert behavior. Visible provider attribution is NASA FIRMS / LANCE.

## ReliefWeb Reports

ReliefWeb is a report-discovery boundary for an already selected Event. An explicit native POST to `https://api.reliefweb.int/v2/reports` searches title, disaster name, and country name with a `date.original` range, an optional exact ISO3 filter, explicit metadata fields, `limit = 20`, and `offset = 0`. Candidates are transient and never create Events, SourceItems, associations, or map points by themselves.

Attachment accepts only Event ID and ReliefWeb report ID, then retrieves `GET /v2/reports/{id}` and revalidates the authoritative metadata. It persists one immutable `Other` SourceItem identified by `src-reliefweb-report-{reportId}` / `ReliefWeb:Report:{reportId}` and reuses it across Events. Text is a bounded metadata summary; canonical URL, original publication date, and contributing source organizations are retained. Bodies, HTML, PDFs, files, images, and arbitrary provider JSON are excluded.

Both operations require a pre-approved native `appname`, use fixed-host HTTPS with redirects disabled, a 15-second timeout, caller cancellation, and a 1 MiB response cap. Production is currently unconfigured, so the client fails locally before sending a request. Automatic matching, paging, polling, startup requests, provider metadata updates, report-created Events, and a generic provider registry remain deferred.

The current v0.1 X integration is a no-cost, user-operated website pane. It does not use X Developer access, paid X API credits, OAuth developer apps, API keys, bearer tokens, access tokens, refresh tokens, or `api.x.com`.

## X Website Pane

The desktop host creates a second native WebView2 for X. It navigates by default to:

```text
https://x.com/home
```

The user controls the website directly, including sign-in and choosing For You or Following. Geopolitical Monitor must not automate interaction, inspect the DOM, extract cookies/tokens, inject scripts, scrape posts, or intercept X network responses for data extraction. X posts are never imported automatically from the browser pane.

The X WebView2 has no native application bridge and no host objects. Password autosave and general autofill are disabled where supported. Normal WebView2 TLS, certificate, reputation, and browser security behavior remains enabled.

Top-level X-owned HTTPS pages are allowed inside the pane:

- `https://x.com/*`
- `https://www.x.com/*`

External non-X HTTPS links open in the system browser. Other schemes are blocked. X webpage subresources are not parsed or scraped by the application.

The X browser profile is stored under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`. Session cookies may persist there as normal browser state. SQLite is not used for X browser state. SQLite stores only app-owned preferences such as validated `XPaneWidth` and `XPaneSide` used to restore the native pane size and side.

Copy Post Link is user-driven and URL-only. It accepts only HTTPS public X status URLs of the form `https://x.com/<account>/status/<numeric-id>` or the `www.x.com` equivalent, normalizes to `https://x.com/<account>/status/<numeric-id>`, and writes only that URL to the clipboard. Copying does not inspect DOM, execute JavaScript, scrape text, create SourceItems, or validate existence with X.

The selected Event's privileged Sources UI accepts a manually pasted URL through a narrow typed bridge command. The application validates and normalizes the URL again, derives `PublishedAtUtc` locally from the public numeric status ID using the X/Twitter Snowflake epoch, then creates or reuses one URL-only `XPost` SourceItem and its Event association. It makes no X network request and does not obtain post text, author, media, engagement, cookies, tokens, or browser storage.

If X blocks embedded WebView2 login or browsing, keep the Open External fallback and report the blocker. Do not bypass the restriction.

## Future Integration Boundaries

Watchlists and Alert controls are not provider triggers. Creating, selecting, renaming, deleting, changing Event membership, enabling or disabling monitoring, opening Alert history, and marking an Alert read cause no GDACS refresh, ReliefWeb search/attachment, X navigation, or other outbound request. Names, memberships, rules, and history remain local and are never sent to providers. Alert evaluation piggybacks only on an explicit manual GDACS refresh; there is no automatic matching, startup provider work, polling, Windows notification, or app-closed monitoring.

Future geopolitical/news source providers remain behind provider-specific mockable application/infrastructure interfaces.

Automatic post ingestion remains deferred and must not be inferred from the explicit manual URL attachment workflow.

Automated tests must not depend on:

- live X credentials;
- live X website access;
- live GDACS, GDELT, or other source-provider access;
- personal credentials;
- unstable network behavior.

Remote pages, X pages, news websites, and remote scripts must not load directly inside the privileged application WebView.

Right-click Copy X Post Link is limited to normal WebView2 context-menu link metadata. If that metadata is a valid public status URL, the command may be shown and copies the normalized URL. If the post/card body does not expose such metadata, the app must not use JavaScript, DOM inspection, accessibility-tree scraping, DevTools Protocol, network interception, or X API calls to infer the URL.

The app does not style or inject into the X page to hide page-owned scrollbars. X remains scrollable according to normal WebView2/X behavior, and app-owned splitter/header chrome must remain outside X content bounds.

AdBlock is an integration boundary, not an X API. The production X WebView uses `CreateWithOptionsAsync` with `AreBrowserExtensionsEnabled = true` against the existing `XBrowser` user-data folder only after interrupted activation recovery and startup trust reconciliation. A trusted managed payload with no binding may enable the host solely so Reconcile can disable/remove any unbound same-ID instance and complete a fresh verified add transaction; it is not bound by ID alone. Otherwise X starts with extensions disabled and reports that reviewed re-enrollment is required. The privileged application WebView remains separate and extension-disabled.

Official browser-store enrollment is separate from candidate selection. The compiled initial authority approves only Chrome Web Store AdBlock `6.47.0`, official store ID `gighmmpiobklfepjocnamgkkbiglidom`, and exact full-runtime digest `9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae`. Production authenticates all regular runtime files except `_metadata/**`, enforcing 1,024 files, 512 MiB aggregate, 32 MiB per file, 256 directories, depth 16, and 512 strict-UTF-8 path bytes. It streams a bounded staging copy and recomputes the same digest for staging and final `Current` before WebView2 load; deterministic `staging`, `previous`, and `rejected` paths support startup recovery, and backup-cleanup failure never rolls back a verified `Current`. The binding is scoped to `XBrowser` and carries trust schema, product/store/version, official store ID, digest format/value, managed source, and runtime ID, and is written only after a verified WebView2 add transaction. Store ID establishes reviewed provenance; the returned/enumerated WebView2 ID identifies only the managed installed instance. The separate `ExtensionCapabilityDiagnostic` flow cannot authorize production. No payload is bundled, downloaded, silently installed, or automatically updated.
