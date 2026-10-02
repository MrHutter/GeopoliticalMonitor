# Security

## Desktop Runtime

Geopolitical Monitor v0.1 is a Windows desktop application. The normal runtime does not require an ASP.NET server, Vite server, Docker, PostgreSQL, X Developer account, paid X API access, or production inbound ports.

The isolated X website pane is intentionally online and may make outbound HTTPS connections to X-owned web infrastructure. It does not create an inbound OAuth callback listener.

GDACS refresh is a separate native outbound HTTPS boundary. It runs on a native in-process timer only while the app window is open and on the manual action, is fixed to `www.gdacs.org`, credential-free, limited to the earthquake query, capped at 100 records and 1 MiB, and timed out after 15 seconds. The privileged React WebView receives only a parameterless typed refresh command, a fieldless status request, and a typed status push with fixed values; it cannot supply an endpoint, interval, or schedule, or make provider requests itself. The status push is never sent to the X WebView. The Event list request accepts only optional typed criteria (fixed range and severity values, search text of at most 200 characters, a validated Watchlist id, a validated keep Event id, and a request number); React never supplies query text or SQL, and native code binds every value as a parameter. Provider text is untrusted, bounded plain text; HTML and unsupported schemas are rejected.

ReliefWeb discovery is a separate manual-only outbound HTTPS boundary fixed to `api.reliefweb.int`. React may supply only Event ID, bounded query/date/optional ISO3 search inputs or Event ID plus numeric report ID; it cannot supply a host, URL, HTTP method, result limit, filter JSON, appname, or persisted SourceItem metadata. Search is capped at 20 candidates and responses at 1 MiB with a 15-second timeout, caller cancellation, no redirects, retries, polling, or startup request. Strict parsing bounds metadata and ignores report bodies/files. The approved `appname` authority is native-only and currently unconfigured; missing configuration fails before HTTP dispatch.

NASA FIRMS is a separate manual-only outbound HTTPS boundary fixed to `firms.modaps.eosdis.nasa.gov`. React supplies exactly four finite viewport coordinates; native code owns the MAP_KEY, products, endpoint, `DAY_RANGE=2`, request splitting, and limits. The key is acquired from `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` and must not appear in bridge messages, frontend state, browser storage, SQLite, source records, logs, result/error payloads, or documentation examples. Missing configuration and invalid/oversized viewports issue no HTTP request. Redirects, retries, partial snapshots, startup requests, automatic viewport requests, polling, and background work are disabled.

## Manual Execution

No application component starts automatically with Windows, login, Docker startup, or background service managers.

Do not create Windows services, scheduled tasks, startup entries, registry autorun entries, startup shortcuts, tray daemons that survive app exit, PM2/NSSM configurations, background daemons, or automatic launchers without explicit authorization.

Closing the app ends normal application execution.

## WebView Boundary

React/WebView content and native C# code are separate trust boundaries.

The privileged application WebView accepts bridge messages only when WebView2 reports that the sending document has the exact bundled-application origin. Trusted messages must also use the explicit typed protocol; unknown message types, malformed payloads, unexpected fields, and unsupported operations are rejected.

Watchlist commands use that same origin check and strict typed parsing. They expose only bounded list, create, rename, delete, membership-query, and membership-set operations with validated IDs, names, booleans, dispositions, and array sizes. Errors use fixed user-safe codes and messages; exceptions, EF/SQL details, paths, and connection data are not serialized. The X WebView cannot issue Watchlist commands.

Alert commands use the same exact bundled-origin gate and strict typed protocol. They expose only unread summary, bounded rule listing, one fixed Watchlist-level trigger toggle, the newest 200 history rows, and mark-read. Alert, rule, Watchlist, and Event IDs are validated; unknown fields, trigger types, operations, malformed timestamps, duplicate IDs, and oversized arrays are rejected. The isolated X WebView and its extensions receive no Alert bridge access.

Do not expose arbitrary native objects, filesystem access, shell execution, process execution, or a generic native command bridge to JavaScript.

The main WebView permits navigation only when the parsed scheme, host, and effective port exactly match the configured application origin. Same-origin routes remain available; malformed URLs and textual prefix lookalikes are cancelled.

The X WebView is a separate, unprivileged browser surface for the normal X website. It must not expose host objects, native bridge messaging, app commands, DOM extraction, script injection, cookie extraction, token extraction, or DevTools Protocol extraction.

## Content Security

Release frontend content uses a restrictive Content Security Policy:

- default content from self;
- scripts from self;
- styles from self;
- images from self or data URLs;
- no objects;
- no framing;
- no forms.

Do not broaden the privileged app WebView CSP or navigation allow-list because the isolated X WebView needs internet access. The two WebViews have different trust roles.

## Persistence

SQLite is the embedded v0.1 persistence baseline. The desktop host creates and migrates the database at `%LOCALAPPDATA%\GeopoliticalMonitor\Data\geopolitical-monitor.db`.

The current schema stores ordinary application preferences, fictional local demo records, manually imported GDACS Event/SourceItem records, explicitly attached metadata-only ReliefWeb SourceItems, local Watchlist names plus Event-ID memberships, Watchlist-level GDACS Alert rules, and unread/read Alert history. `AccentTheme` supports `Orange`, `Blue`, `Red`, `Green`, and `Yellow`; `XPaneWidth` stores the validated local X pane width; `XPaneSide` stores `Left` or `Right`; browser localStorage is not the preference authority. Active Watchlist selection is not persisted. These tables do not store credentials, OAuth tokens, refresh tokens, passwords, API secrets, appnames, cookies, raw provider payloads, report bodies/files, provider account state, or cloud/account identity.

Alert evaluation runs inside every accepted GDACS refresh, manual or timer-driven. The Event update and Alert insertion share one SQLite transaction, and the uniqueness constraint on rule, Event, and provider revision prevents duplicate history; a cancelled or failed refresh rolls back and the next successful refresh creates the Alert once. No Alert action starts a network request. The only timer is the in-process GDACS refresh scheduler, which runs while the app window is open and stops on close. There is no Windows service, background worker process, Windows notification/toast, scheduled task, startup registration, or monitoring after the desktop process exits.

The X browser profile lives under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`. Session cookies may persist there solely as normal browser state controlled by X and WebView2. Geopolitical Monitor must not read those cookies for application logic.

Do not place runtime databases or WebView2 profile data in the repository root, frontend assets, installation directory, or OneDrive by design.

## Secrets

Secrets never enter Git.

The current X website-pane design requires no X API secret, client ID, client secret, bearer token, access token, or refresh token. Copy Post Link writes only a validated public status URL to the Windows clipboard and must not read unrelated clipboard contents, cookies, tokens, page HTML, post text, or X network responses.

Frontend variables, browser localStorage, SQLite preference rows, Event/SourceItem rows, logs, bridge messages, and source files are not secret storage. FIRMS MAP_KEY acquisition is process-native only; authenticated URLs are not included in public errors or results.

## External Links

X-owned HTTPS top-level navigation may remain in the isolated X pane. Non-X HTTPS links from X open in the user's system browser. Non-HTTPS schemes are blocked.

## Future Deployment

Installer/MSIX packaging, auto-update, public networking, and remote deployment require separate explicit task authority and security review.

X scrollbar handling is constrained by the same isolation model as all other X behavior. The application may adjust only app-owned chrome and layout. It must not inject CSS/JavaScript, inspect the DOM, use accessibility scraping, or intercept X network responses to alter or infer page content.

Context-menu X post copying is URL-only and metadata-only. WebView2-provided link metadata may be validated and normalized; absence of that metadata is not authority to scrape. Copying a URL does not create SourceItem, Event, or EventSourceAssociation records. Creation requires a separate explicit paste-and-submit action in the privileged Event Sources UI.

Browser extensions are enabled only for the isolated production X profile through the supported WebView2 environment option. The privileged app WebView remains extension-disabled. Production authenticity comes only from immutable reviewed records plus the canonical complete-runtime digest; manifest branding, folder selection, Store ID alone, diagnostic success, and the managed WebView2 runtime ID cannot self-authorize a payload. The candidate, staging tree, final `Current`, schema-2 binding, and installed runtime ID are independently reconciled. A production binding is persisted only after the exact add/enumeration transaction; Reconcile removes an unbound same-ID instance and performs a fresh add instead of adopting it. Browser-generated `_metadata/**` is excluded from the managed runtime. Unknown versions, resource-limit violations, links/reparse points, tree mutations, old bindings, interrupted activation, and final mismatches fail closed before authenticated X navigation. Exception diagnostics retain bounded type/HRESULT/category information while redacting private paths and URLs. No extension receives native host objects, the native bridge, filesystem or database APIs, cookie/token APIs, or SourceItem/Event creation authority.

The X environment uses WebView2's default scrollbar style. FluentOverlay remains deferred, and the application must not inject CSS or JavaScript into X to hide or restyle scrollbars.
