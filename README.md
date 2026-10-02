# Geopolitical Monitor

Geopolitical Monitor v0.1 is a Windows desktop application for geopolitical situational awareness and OSINT research.

The current repository is a local desktop foundation. It includes a dashboard shell, a bundled local MapLibre world map, SQLite-backed Events and SourceItems, persisted preferences, Watchlists, in-app Alerts for material GDACS updates, explicit manual X status-URL attachment, a no-cost isolated X website pane, GDACS earthquake ingestion that refreshes on an in-app timer while the app is open (plus a manual refresh action), UCDP Candidate conflict Events from monthly releases checked on the same in-app timer (plus a manual check), explicit ReliefWeb report discovery/attachment, and a transient manually refreshed NASA FIRMS thermal-observation layer. It does not implement X API access, OAuth developer-app login, real X timeline ingestion, provider refresh while the app is closed, AI, or installer packaging.

## Architecture

- `GeopoliticalMonitor.Desktop` is the WinUI 3 / Windows App SDK desktop entry point.
- `src/frontend` contains the React, TypeScript, and Vite UI embedded inside the privileged application WebView2.
- `GeopoliticalMonitor.Application` contains application-level contracts such as the narrow native/web message protocol and testable X browser policy helpers.
- `GeopoliticalMonitor.Infrastructure` contains infrastructure adapters, including EF Core SQLite persistence under local application data.
- `GeopoliticalMonitor.Domain` remains independent from UI and infrastructure.

There is no ASP.NET runtime API project in this baseline. Kestrel is not required.

## Runtime Model

The released application is launched through the desktop executable. It does not require the user to start Vite, run Docker, start PostgreSQL, obtain X Developer access, or configure X API credentials.

Vite is a development/build tool only. Packaged operation builds static frontend assets and includes them with the desktop application. WebView2 loads those bundled assets through an application-owned virtual host origin.

Normal application runtime opens no inbound listening ports. The bundled MapLibre map uses local Natural Earth-derived geometry and no remote tile provider. Its neutral sovereign-country basemap follows internationally recognized sovereignty, including Crimea within Ukraine; any future operational-control or occupation assessment belongs in a separately sourced and timestamped overlay.

The X pane is intentionally online. It is a second native WebView2 that loads the normal X website at `https://x.com/home`. The user signs into X directly inside that website if X permits WebView2 login. Geopolitical Monitor does not receive the password, cookies, tokens, post DOM, or feed data.

## X Website Pane

The X pane:

- uses the published X website, not `api.x.com`;
- requires no X Developer account, paid API credits, OAuth app, client ID, client secret, bearer token, access token, or refresh token;
- is user-operated, with no automatic Following selection, clicking, scrolling, scraping, or DOM extraction;
- has no native bridge or host objects exposed to X content;
- disables WebView2 password autosave and general autofill where supported;
- keeps X-owned HTTPS top-level navigation in the pane;
- opens non-X HTTPS links in the system browser;
- provides Home, Back, Forward, Refresh, Open External, Move Left/Right, Collapse, Copy Post Link, Clear Session, and a native horizontal splitter for resizing the pane.

The X browser profile is application-controlled local browser state under:

```text
%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser
```

Session cookies may persist in that profile according to X and WebView2 behavior. They are not copied to SQLite or read by application logic. The user-selected X pane width and side are stored separately as local `XPaneWidth` and `XPaneSide` application preferences and are validated/clamped on restore. The permanent layout contract is `Events | Map | X` when X is on the right and `X | Map | Events` when X is on the left; selected-event details stay below Map + Events.

## Persistence

SQLite is the embedded v0.1 persistence baseline. The desktop host creates and migrates the database on startup with EF Core.

Runtime data lives under the current user's local application data directory:

```text
%LOCALAPPDATA%\GeopoliticalMonitor\Data\geopolitical-monitor.db
```

The current schema persists the `AccentTheme`, `XPaneWidth`, and `XPaneSide` application preferences plus Events, SourceItems, explicit EventSourceAssociations, Watchlists and memberships, Alert rules, and Alert occurrences. Seeded records are fictional demonstration data. A user may explicitly paste a copied public X status URL into the selected Event's Sources view; the app stores only the canonical URL, numeric status ID, a neutral technical label, and the locally decoded Snowflake creation timestamp. GDACS earthquake records are normalized as `NaturalDisaster` Events with attached `DatasetRecord` provenance, and UCDP Candidate records as `MilitaryConflict` or `Security` Events with a `DatasetRecord` SourceItem and the release they were last seen in. Explicitly attached ReliefWeb reports reuse `Other` SourceItems containing bounded metadata, original-source attribution, and the canonical report URL; report bodies, HTML, PDFs, images, and attachments are not retained. FIRMS observations and layer visibility are never written to SQLite. No accounts, tokens, automatic polling, or cross-provider event matching exist in SQLite.

The Event list and map do not load every Event. React sends the active scope, time range (`ALL` by default), search, and minimum severity to a native windowed query, which returns at most the newest 2,000 matching Events by last update together with the total match count; the panel says when more Events match and suggests narrowing the time range or search. Search still finds older Events, and the selected Event stays visible while it matches. The list shows the newest Events first (last update, then id), scrolls inside a bounded Events panel, and renders 100 rows at a time as you scroll; neither the panel nor the map grows with the number of Events. The top bar, info bar and Map + Events row fill the window height; the selected Event's Details sit below and are reached by scrolling.

## GDACS Earthquakes

While the app is open, a native in-process scheduler refreshes GDACS every 30 minutes, first 60 seconds after the UI is ready, with backoff after failures; nothing runs when the app is closed, and there is no service, scheduled task, startup registration, or background process. The Events panel shows each provider's last update, next refresh, and state, and `Refresh GDACS` remains available as a manual action that shares the same single in-flight refresh. Each refresh requests at most 100 earthquake records for the preceding four days from the official GDACS HTTPS API. Native Infrastructure performs the 15-second, size-bounded request; React has no provider network authority. Valid point epicenters become idempotent `NaturalDisaster` Events with `Unverified` meaning that Geopolitical Monitor has not independently verified the occurrence. Each episode remains separate evidence as a linked `DatasetRecord` SourceItem.

Imported data is attributed to the Global Disaster Awareness and Coordination System, GDACS. GDACS alerts and automated impact estimates may be uncertain and are not a substitute for competent-authority information. The canonical GDACS report URL remains visible with each imported source.

The app does not contact GDACS before the UI is ready, and refreshes only on the in-app timer or the manual action. Other hazards, paging beyond 100 records, polygon/track geometry, GDELT, generic provider frameworks, dossiers, and automatic X ingestion remain deferred.

## UCDP Candidate Events

While the app is open, the same in-process scheduler checks the Uppsala Conflict Data Program's monthly UCDP Candidate Events release every 6 hours, first 2 minutes after the UI is ready; `Refresh UCDP` runs the same single in-flight check manually. Native Infrastructure downloads one monthly CSV (for example `GEDEvent_v26_0_8.csv`) from the fixed host `https://ucdp.uu.se/downloads/candidateged/` with a 60-second timeout, a 32 MiB cap, no redirects, no retries, and no credentials. The first import takes the newest published release and the one before it; later checks request only the next release, and a missing release is reported as `No new release`.

A release is published about 20 days after its month ends, so UCDP data is up to about 50 days old and the 24H and 7D ranges show few UCDP Events. Records with a fatality estimate of at least 1 and a place precise to province level or better become `Unverified` Events labelled `UCDP Candidate`, with severity from the best fatality estimate. Candidate data may be revised or removed in later UCDP releases. Article text, headlines and geometry from the CSV are not stored. UCDP data is licensed under CC BY 4.0; the required citation is shown in Details and in Settings under Data sources.

## ReliefWeb Reports

The selected Event's Sources view can open a compact ReliefWeb report search. Search is explicit, capped at 20 transient candidates, and uses native fixed-host HTTPS; opening the form, switching Events, and application startup do not contact ReliefWeb. Attachment is a second explicit operation that retrieves the authoritative report by ID and transactionally creates or reuses `src-reliefweb-report-{reportId}` plus the Event association. ReliefWeb does not create Events or map markers.

Live access requires a pre-approved ReliefWeb V2 `appname` supplied in the native desktop composition root. Production is currently explicitly unconfigured because no approved value has been supplied. In that state, search and attachment return `ReliefWeb integration is not configured.` before any HTTP request. The identifier is not accepted from React, Settings, localStorage, SQLite, or bridge messages.

Automatic matching, paging, polling, startup search, ReliefWeb-created Events, report-body/PDF storage, mutable provider-metadata synchronization, and a generic provider framework remain deferred.

## NASA FIRMS — Fire / Thermal Anomalies

`Refresh FIRMS` explicitly requests the current bounded MapLibre viewport from the native NASA FIRMS Area CSV API. The desktop host reads `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` once for native client composition; the key is never persisted or sent to React. Each logical refresh retrieves `VIIRS_NOAA20_NRT` and `VIIRS_NOAA21_NRT` with `DAY_RANGE=2`, retains the rolling latest 24 hours, and publishes an all-or-nothing transient snapshot only after every required request succeeds.

The viewport is limited to 90 degrees longitude by 60 degrees latitude. Antimeridian viewports are split natively. Each response is limited to 4 MiB and the complete normalized snapshot to 20,000 exact-deduplicated observations. FIRMS is manual-only: startup, map movement, Event selection, layer visibility, Settings, Watchlists, and Alerts do not fetch. Observations are small independent map points with age styling and inspection metadata; they are not Events, SourceItems, Watchlist members, or Alerts. Attribution is NASA FIRMS / LANCE.

The displayed data window is always the rolling latest 24 hours. `DAY_RANGE=2` intentionally fetches two provider calendar days so native code can construct that true rolling window across UTC midnight. The default Geopolitical Monitor display preset is `Significant` (nominal/high confidence and FRP >= 10 MW); `Strong only` raises the FRP threshold to 50 MW, while `All detections` shows the complete valid snapshot. These are application display filters, not NASA severity categories. Switching presets filters the in-memory snapshot and performs no provider request.

For a local configured launch, set the key only in the process environment before starting the desktop executable:

```powershell
$env:GEOPOLITICALMONITOR_FIRMS_MAP_KEY = "<your MAP_KEY>"
```

Do not place runtime databases or WebView2 profile data in the repository, frontend assets, installation directory, or OneDrive by design.

## Manual Execution

Geopolitical Monitor does not install or configure automatic startup. The app runs only when explicitly started by the user. Closing the app ends normal application execution.

## Development

Restore, build, and test:

```powershell
dotnet restore src/backend/GeopoliticalMonitor.sln
dotnet build src/backend/GeopoliticalMonitor.sln
dotnet test src/backend/GeopoliticalMonitor.sln
```

Frontend:

```powershell
cd src/frontend
npm install
npm test
npm run build
```

Build order: build the frontend before the desktop project. `GeopoliticalMonitor.Desktop.csproj` includes the existing `src/frontend/dist` files as content and copies them to `wwwroot\` in the desktop build output, which the app WebView serves. If `dist` is missing when the desktop project builds, `wwwroot\` is not populated and the app launches with a blank window. After rebuilding the frontend, rebuild the desktop project to refresh `wwwroot\`.

```powershell
cd src/frontend
npm ci
npm run build
cd ../..
dotnet build src/backend/GeopoliticalMonitor.sln
```

Optional frontend hot reload may use:

```powershell
cd src/frontend
npm run dev
```

The Vite dev server binds only to `127.0.0.1:5173` with strict port behavior. It is development tooling only and is not part of the normal desktop runtime.

## Security Notes

No secrets belong in source control, frontend code, browser localStorage, SQLite preference rows, Event/SourceItem records, bridge messages, or logs. The FIRMS MAP_KEY remains native-only and is acquired from `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` for the current process.

The WebView2 bridge accepts only explicit typed messages. It must not become a general native command surface. The isolated X WebView2 must never receive this bridge and must not be used for scraping, automation, cookie extraction, or token extraction.

Responsive behavior: the native shell preserves at least 940 px for the main application. Events are a bounded side panel, Map receives the remaining workspace width, and Events + Map never intentionally stack during desktop use. The top bar stays on one row from the 940 px minimum up, keeping branding, search, time range, UTC, Alerts (always labelled) and the Settings entry point. The Events column is 300-320 px (up to 340 px on very wide app views) and its layout adapts to the panel's own width through container queries. Settings opens as a modal side panel; it owns all five accent choices and reports the application/runtime information (shell, UI host, local SQLite persistence, no inbound ports) that the main screen no longer shows in a separate info bar. Settings does not change Event selection or filter state. If a stored X pane width is too large for the current window, the rendered width is clamped temporarily without treating the X pane as the primary surface.

`Geopolitical Monitor` remains the provisional visible product name. Frontend and native display consumers each use a bounded display-name authority so later branding work does not require changing stable identities. A visible rename must not automatically rename `%LOCALAPPDATA%\GeopoliticalMonitor`, the database filename, WebView2 profiles, namespaces, projects, assemblies, preference keys, or the trusted `app.geopoliticalmonitor.local` origin.

X scrollbar and link-copy boundaries: the app does not inject CSS or JavaScript into X to restyle its page scrollbar. App-owned splitter/header chrome is kept outside the WebView content bounds; if X exposes its own scrollbar, that remains page-owned. Right-click Copy X Post Link is offered only when WebView2's normal context-menu link metadata is already a valid public X status URL; whole-card support depends on X/WebView2 exposing that URL without scraping. Copying alone never imports data. Attachment requires the user to paste and submit the URL in the privileged Event Sources UI; no post text, author, media, or engagement data is obtained.

X pane polish: the isolated X WebView uses the browser's default scrollbar without page injection. The X toolbar keeps Back, Forward, Home, and Refresh visible and moves secondary actions into a native overflow menu: Open External, Copy Post Link, Clear Session, and Ad Blocker.

The production X WebView uses the extension-enabled WebView2 environment against its existing `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser` profile only when saved production AdBlock state is absent or revalidates under the compiled trust authority. The sole initial record approves Chrome Web Store AdBlock `6.47.0`, official ID `gighmmpiobklfepjocnamgkkbiglidom`, with `gpm-extension-payload-v1` digest `9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae`. Explicit installation hashes every runtime file except browser-generated `_metadata/**`, stages only that authenticated tree under `%LOCALAPPDATA%\GeopoliticalMonitor\Extensions\AdBlock`, and rehashes both staging and final `Current`. Deterministic staging/previous paths let startup recover an interrupted replacement without treating a partial tree as production. New versions require a separately reviewed compiled record; candidate selection cannot self-enroll a digest. The WebView2 runtime ID and exact install transaction remain management identity, not authenticity evidence. Invalid or old saved state starts X with extensions disabled until explicit re-enrollment; after restart, Reconcile removes any same-ID unbound instance and performs a fresh verified WebView2 add transaction before persisting a binding. The privileged app WebView remains extension-disabled, and `Test Extension Support` remains a separate capability diagnostic that cannot establish production trust.
