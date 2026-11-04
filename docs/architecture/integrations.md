# Integrations

## GDACS Earthquakes

The first live data provider is the official Global Disaster Awareness and Coordination System, GDACS. A refresh calls `https://www.gdacs.org/gdacsapi/api/events/geteventlist/SEARCH` with `eventlist=EQ`, preceding-four-day `fromdate`/`todate`, `pagesize=100`, and `pagenumber=1`. Refreshes run on the native in-process `ProviderRefreshScheduler` while the app is open (every 30 minutes, first 60 seconds after the UI sends `app.ready`) and on the manual `Refresh GDACS` action. Both paths use one shared `GdacsRefreshService`, so at most one GDACS refresh is in flight; a timer tick that finds it busy is skipped, not queued, and a successful manual refresh resets the next due time. After a failure the next refresh waits `max(30 min, 5 min × 2^(n−1))`, capped at 6 hours with ±10 % jitter; cancellation is not a failure. The native adapter allows no user URL, credential, paging, or retry inside a refresh. Requests time out after 15 seconds and responses are size- and record-bounded. The scheduler stops when the window closes; no service, scheduled task, startup registration, or background process exists.

The scheduler pushes a typed `providers.refresh.status` message to the privileged WebView on each state transition (refreshing, finished, failed, unconfigured, stale), and React may request the same payload with the fieldless `providers.refresh.status.get`. Each entry carries only a fixed provider ID, display name, state (`Idle`, `Refreshing`, `Failed`, `Stale`, `Unconfigured`), last success and next refresh times, one message from a fixed set, and the number of Alerts created by the latest refresh.

Strict GeoJSON parsing accepts point earthquake features only. Individual malformed features are skipped and counted; a malformed response or a nonempty response with no valid records fails without writes. Provider event and episode IDs produce deterministic Event and `DatasetRecord` SourceItem identities. Newer provider timestamps may update only provider-owned fields, and attached canonical GDACS provenance is required before an existing deterministic Event can be changed.

An enabled Watchlist Alert rule is evaluated during every accepted GDACS refresh, whether manual or timer-driven; the rules are unchanged. A strictly newer GDACS revision triggers only when an existing watched Event materially changes in title, summary, start time, or stored location fields. New Events, source-only/new-episode changes, revision-only updates, stale or repeated data, equal-timestamp conflicts, membership changes, X attachment, and ReliefWeb attachment do not trigger. The Event update and deduplicated Alert occurrence commit atomically in the same SQLite transaction.

Imported information is attributed to the Global Disaster Awareness and Coordination System, GDACS. GDACS automated alerts and impact information may be uncertain and should be confirmed with competent authorities before critical decisions. Other GDACS hazards, polygons, tracks, paging, GDELT, and a generic provider registry remain deferred.

## UCDP Candidate Events

The second scheduled provider is the Uppsala Conflict Data Program's UCDP Candidate Events Dataset, released monthly as CSV. `UcdpCandidateFeed` requests only `https://ucdp.uu.se/downloads/candidateged/GEDEvent_v{YY}_0_{M}.csv`, with the file name built from an internal release value, never from web input. It uses its own `HttpClient` with redirects disabled, no retries, a 60-second timeout linked to the caller's token, a streamed 32 MiB cap, a host, scheme and path check before each request, and a `GeopoliticalMonitor/<version>` User-Agent. A 404 means the release is not published yet; any other non-200 status, a redirect, a timeout, or an oversized body is a typed failure with a fixed message. No token or credential is used.

Refreshes run on the shared `ProviderRefreshScheduler` every 6 hours, first 2 minutes after `app.ready`, and on the fieldless `providers.ucdp.refresh` request; both use one `UcdpRefreshService`, so at most one UCDP check is in flight. The first import probes up to four release names counting down from the current month, takes the newest published one and also imports the release before it; later checks request only the release after the newest imported one (the maximum stored `ProviderLastSeenRelease`, combined with an in-memory value for the session). If that release is missing although it is already two or more months old, the four-name probe runs again. `No new release` is a successful check. The provider status entry for UCDP also carries the fixed label `monthly release` and `dataThrough` (`yyyy-MM`); UCDP is stale when the newest imported release was extracted more than 45 days ago.

The RFC 4180 parser resolves required columns by header name, skips and counts malformed rows, limits fields to 8,192 characters, rows to 65,536 characters and files to 20,000 rows, and never reads article, headline, source text or geometry columns. Rows are included only with valid non-zero coordinates, `where_prec` 1 to 4, `best` at least 1 and `date_start` inside the release month, at most 5,000 per release ranked by `best`. Text is normalized to NFC, stripped of control characters and truncated to the database limits before one SQLite transaction per release. Severity uses rule `ucdp-candidate-v1` on `best`. UCDP data is licensed under CC BY 4.0, and the required citation and DEMSCORE acknowledgement appear in Details and Settings. UCDP updates never create Alerts in this version.

## NASA FIRMS Fire / Thermal Anomalies

NASA FIRMS is a transient live map boundary, not Event ingestion. An explicit `Refresh FIRMS` command sends only the current viewport's west, south, east, and north values through the privileged bridge. Native code validates a maximum normalized span of 90 degrees longitude and 60 degrees latitude, splits valid antimeridian coverage, and constructs fixed-host HTTPS requests to `firms.modaps.eosdis.nasa.gov`. React cannot select the host, URL, method, product, day range, response limits, or MAP_KEY.

Every logical refresh retrieves exactly `VIIRS_NOAA20_NRT` and `VIIRS_NOAA21_NRT` using `DAY_RANGE=2`. Each HTTP operation has a 15-second timeout, caller/lifetime cancellation, redirects disabled, no retry, and a 4 MiB response limit. Strict CSV parsing validates the known VIIRS fields. Native orchestration retains observations from the rolling latest 24 hours, rejects clearly future data, collapses only exact deterministic identities, and rejects rather than truncates snapshots above 20,000 observations. All normal or antimeridian requests must succeed before the frontend replaces its prior snapshot.

The provider's two-day request is a calendar-day acquisition mechanism, not the user-facing time range; the only first-slice view is the native-filtered rolling latest 24 hours. React retains the complete normalized snapshot and derives the visible subset locally. `Significant` is the default and requires nominal/high confidence plus FRP >= 10 MW; `Strong only` requires nominal/high confidence plus FRP >= 50 MW; `All detections` applies no additional display filter. These are Northern Monitor operational-display presets rather than NASA severity definitions, and changing them issues no bridge or provider request.

The MAP_KEY is resolved natively for each HTTP operation by `FirmsKeyResolver`: a key entered in the native WinUI key dialog and stored by `WindowsCredentialFirmsKeyStore` (Windows Credential Manager, generic credential, fixed target name, persisted for this user on this computer only) wins; otherwise the process environment variable `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` is used. Both are normalized the same way (surrounding whitespace and one pair of matching quotes removed). The key is held as a `FirmsMapKey`, whose text form is redacted, and no options object keeps it between refreshes, so setting or removing the in-app key applies to the next refresh without a restart. A key that is not 32 hexadecimal characters is saved with a warning, never blocked. Missing configuration fails locally before HTTP dispatch.

Failed responses are classified into bounded categories from observed behavior: HTTP 400 whose body contains `Invalid MAP_KEY` is `KeyRejected`, HTTP 400 whose body contains `Invalid source` is `ProductUnavailable` (both observed from the official host on 2026-11-01; NASA does not document them), HTTP 429 is `RateLimited` by its HTTP meaning (NASA does not document its over-limit response), the per-operation timeout is `TimedOut`, and everything else stays `RequestFailed`. At most 1 KiB of an error body is read, only for this decision, and it is never returned. The privileged bridge carries only key status through three zero-field requests (`providers.firms.key.status.get`, `providers.firms.key.set`, `providers.firms.key.remove`) and one `providers.firms.key.status` result with `configured`, `source`, `formatWarning`, `storeUnavailable` and `outcome`; `set` and `remove` make the native host show its own dialogs. NASA's documentation describes no key-status endpoint, so the app has no key check request. FIRMS never runs at startup, on pan/zoom, or from layer visibility changes, and it has no polling, timer, persistence, Event/Source conversion, Watchlist integration, or Alert behavior. Visible provider attribution is NASA FIRMS / LANCE.

## ReliefWeb Reports

ReliefWeb is a report-discovery boundary for an already selected Event. It is dormant by owner decision: the code stays in place, but no `appname` will be requested, so it makes no requests. An explicit native POST to `https://api.reliefweb.int/v2/reports` searches title, disaster name, and country name with a `date.original` range, an optional exact ISO3 filter, explicit metadata fields, `limit = 20`, and `offset = 0`. Candidates are transient and never create Events, SourceItems, associations, or map points by themselves.

Attachment accepts only Event ID and ReliefWeb report ID, then retrieves `GET /v2/reports/{id}` and revalidates the authoritative metadata. It persists one immutable `Other` SourceItem identified by `src-reliefweb-report-{reportId}` / `ReliefWeb:Report:{reportId}` and reuses it across Events. Text is a bounded metadata summary; canonical URL, original publication date, and contributing source organizations are retained. Bodies, HTML, PDFs, files, images, and arbitrary provider JSON are excluded.

Both operations require a pre-approved native `appname`, use fixed-host HTTPS with redirects disabled, a 15-second timeout, caller cancellation, and a 1 MiB response cap. Production composition passes no `appname`, so the client fails locally before sending a request and every search and attachment returns `ReliefWeb integration is not configured.`. Automatic matching, paging, polling, startup requests, provider metadata updates, report-created Events, and a generic provider registry remain deferred.

The current v0.1 X integration is a no-cost, user-operated website pane. It does not use X Developer access, paid X API credits, OAuth developer apps, API keys, bearer tokens, access tokens, refresh tokens, or `api.x.com`.

## X Website Pane

The desktop host creates a second native WebView2 for X. It navigates by default to:

```text
https://x.com/home
```

The user controls the website directly, including sign-in and choosing For You or Following. The app must not automate interaction, inspect the DOM, extract cookies/tokens, inject scripts, scrape posts, or intercept X network responses for data extraction. X posts are never imported automatically from the browser pane.

The X WebView2 has no native application bridge and no host objects. Password autosave and general autofill are disabled where supported. Normal WebView2 TLS, certificate, reputation, and browser security behavior remains enabled.

Top-level X-owned HTTPS pages are allowed inside the pane:

- `https://x.com/*`
- `https://www.x.com/*`

External non-X HTTPS links open in the system browser. Other schemes are blocked. X webpage subresources are not parsed or scraped by the application.

The X browser profile is stored under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`. Session cookies may persist there as normal browser state. SQLite is not used for X browser state. SQLite stores only app-owned preferences such as validated `XPaneWidth` and `XPaneSide` used to restore the native pane size and side.

Copy Post Link is user-driven and URL-only. It accepts only HTTPS public X status URLs of the form `https://x.com/<account>/status/<numeric-id>` or the `www.x.com` equivalent, normalizes to `https://x.com/<account>/status/<numeric-id>`, and writes only that URL to the clipboard. Copying does not inspect DOM, execute JavaScript, scrape text, create SourceItems, or validate existence with X.

The selected Event's privileged Sources UI accepts a manually pasted URL through a narrow typed bridge command. The application validates and normalizes the URL again, derives `PublishedAtUtc` locally from the public numeric status ID using the X/Twitter Snowflake epoch, then creates or reuses one URL-only `XPost` SourceItem and its Event association. It makes no X network request and does not obtain post text, author, media, engagement, cookies, tokens, or browser storage.

If X blocks embedded WebView2 login or browsing, keep the Open External fallback and report the blocker. Do not bypass the restriction.

## Bluesky Website Pane

The social pane can show the published Bluesky website instead of X, switched by the user in the pane header. The desktop host creates a separate isolated WebView2 control for it, lazily, with its own user-data folder `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\BlueskyBrowser` and its own environment, and navigates it to:

```text
https://bsky.app/
```

The Bluesky WebView follows the X rules: it is never wired to the privileged message handler, has no host objects or web messages, uses `XBrowserSecurityConfiguration.IsolatedDefault` (no password autosave, no general autofill, no DevTools in release builds), and the app does not inject scripts or styles, inspect the DOM, scrape, read or extract cookies or tokens, automate interaction, or call any Bluesky or AT Protocol API. `Clear Session` for Bluesky only deletes the Bluesky profile's cookies and browsing data.

Top-level HTTPS navigation to the exact host `bsky.app` stays in the pane; other HTTPS pages, including other Bluesky hosts, open in the system browser; every other scheme is blocked. New-window requests follow the same rule, downloads are cancelled, and a `PermissionRequested` handler denies every permission request. A failed Bluesky process shows a Bluesky error banner with `Retry Bluesky`; a browser-process exit closes the control so Retry creates a new one. The native-only `SocialPaneFeed` preference (`X` or `Bluesky`, default `X`) is stored in `ApplicationPreferences` and is not accepted by the bridge. Copy Post Link is X-only and hidden while Bluesky is shown; no Bluesky URL is attached to Events.

## Future Integration Boundaries

Watchlists and Alert controls are not provider triggers. Creating, selecting, renaming, deleting, changing Event membership, enabling or disabling monitoring, opening Alert history, and marking an Alert read cause no GDACS refresh, ReliefWeb search/attachment, X navigation, or other outbound request. Names, memberships, rules, and history remain local and are never sent to providers. Alert evaluation piggybacks only on GDACS refreshes, manual or timer-driven while the app is open; there is no automatic matching, Windows notification, or app-closed monitoring.

Future geopolitical/news source providers remain behind provider-specific mockable application/infrastructure interfaces.

Automatic post ingestion remains deferred and must not be inferred from the explicit manual URL attachment workflow.

Automated tests must not depend on:

- live X credentials;
- live X website access;
- live GDACS, UCDP, GDELT, or other source-provider access;
- personal credentials;
- unstable network behavior.

Remote pages, X pages, news websites, and remote scripts must not load directly inside the privileged application WebView.

Right-click Copy X Post Link is limited to normal WebView2 context-menu link metadata. If that metadata is a valid public status URL, the command may be shown and copies the normalized URL. If the post/card body does not expose such metadata, the app must not use JavaScript, DOM inspection, accessibility-tree scraping, DevTools Protocol, network interception, or X API calls to infer the URL.

The app does not style or inject into the X page to hide page-owned scrollbars. X remains scrollable according to normal WebView2/X behavior, and app-owned splitter/header chrome must remain outside X content bounds.

AdBlock is an integration boundary, not an X API. The production X WebView uses `CreateWithOptionsAsync` with `AreBrowserExtensionsEnabled = true` against the existing `XBrowser` user-data folder only after interrupted activation recovery and startup trust reconciliation. A trusted managed payload with no binding may enable the host solely so Reconcile can disable/remove any unbound same-ID instance and complete a fresh verified add transaction; it is not bound by ID alone. Otherwise X starts with extensions disabled and reports that reviewed re-enrollment is required. The privileged application WebView remains separate and extension-disabled. The X pane header indication ("Ad blocker inactive", "Ad blocker off" or "Ad blocker not set up") only observes: it reuses the startup snapshot that is already taken before the first authenticated navigation and the Ad Blocker dialog's last snapshot, and after each completed X navigation compares a read-only `GetBrowserExtensionsAsync` listing with the bound runtime instance (at most one check in flight, bounded wait); it never hashes the payload, never changes extension state and never writes a binding.

Official browser-store enrollment is separate from candidate selection. The compiled initial authority approves only Chrome Web Store AdBlock `6.47.0`, official store ID `gighmmpiobklfepjocnamgkkbiglidom`, and exact full-runtime digest `9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae`. Production authenticates all regular runtime files except `_metadata/**`, enforcing 1,024 files, 512 MiB aggregate, 32 MiB per file, 256 directories, depth 16, and 512 strict-UTF-8 path bytes. It streams a bounded staging copy and recomputes the same digest for staging and final `Current` before WebView2 load; deterministic `staging`, `previous`, and `rejected` paths support startup recovery, and backup-cleanup failure never rolls back a verified `Current`. The binding is scoped to `XBrowser` and carries trust schema, product/store/version, official store ID, digest format/value, managed source, and runtime ID, and is written only after a verified WebView2 add transaction. Store ID establishes reviewed provenance; the returned/enumerated WebView2 ID identifies only the managed installed instance. The separate `ExtensionCapabilityDiagnostic` flow cannot authorize production. No payload is bundled, downloaded, silently installed, or automatically updated.

AdBlock for the Bluesky pane is a separate verified copy (opt-in): payload `Extensions\AdBlockBluesky\Current` with its own `staging`, `previous` and `rejected` recovery paths, binding `Data\bluesky-adblock-binding.json` scoped to `BlueskyBrowser`, and the same compiled trust record and digest. The scope generalization keeps `XBrowser` byte-for-byte compatible: an existing X binding validates exactly as before, and a binding matches only its own scope. The Bluesky environment is extension-enabled only when the Bluesky gate trusts the Bluesky binding and payload, or a freshly enrolled trusted Bluesky payload awaits its install transaction; a clean state stays extension-disabled. The `Bluesky Ad Blocker` dialog installs from a chosen unpacked `6.47.0` folder (same checks as X) or from `Use verified X copy`, which re-authenticates the X managed copy in full and stages it into the Bluesky folder without writing to X's folder or reading or changing X's binding. The diagnostic route exists only in the X dialog. The X dialog also offers a user-invoked `Restore trusted instance` action for the state in which a valid X binding and an authenticated managed payload remain but the browser no longer lists the bound instance. The extension has been observed to be missing after the application is restarted; the lifecycle log shows it is not loaded when the browser starts, while the application's own copy and binding remain intact. The cause is not yet established. It takes no folder: it re-checks the binding, authenticates the configured managed `Current` again and re-adds and re-binds through the existing install transaction; it never writes into the payload, is X-only and never runs automatically. A temporary diagnostic log (`Diagnostics\adblock-lifecycle.log`, 128 KiB with one `.old` copy) records the X ad blocker state, extension counts and file-system metadata (existence, size or entry count, last-write time) of the X profile's extension folders and preference files at startup and close; it never reads file contents and redacts every value with the existing diagnostic formatter. It will be removed once the instance-loss cause is known. The Bluesky status line reuses the indication policy, shows nothing until the Bluesky ad blocker is set up, and is checked read-only after each completed Bluesky navigation.
