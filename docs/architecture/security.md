# Security

## Desktop Runtime

Northern Monitor v0.1 is a Windows desktop application. The normal runtime does not require an ASP.NET server, Vite server, Docker, PostgreSQL, X Developer account, paid X API access, or production inbound ports.

The isolated X website pane and the isolated Bluesky website pane are intentionally online and may make outbound HTTPS connections to X-owned or Bluesky-owned web infrastructure. It does not create an inbound OAuth callback listener.

GDACS refresh is a separate native outbound HTTPS boundary. It runs on a native in-process timer only while the app window is open and on the manual action, is fixed to `www.gdacs.org`, credential-free, limited to the earthquake query, capped at 100 records and 1 MiB, and timed out after 15 seconds. The privileged React WebView receives only a parameterless typed refresh command, a fieldless status request, and a typed status push with fixed values; it cannot supply an endpoint, interval, or schedule, or make provider requests itself. The status push is never sent to the X WebView. The Event list request accepts only optional typed criteria (fixed range and severity values, search text of at most 200 characters, a validated Watchlist id, a validated keep Event id, and a request number); React never supplies query text or SQL, and native code binds every value as a parameter. Provider text is untrusted, bounded plain text; HTML and unsupported schemas are rejected.

UCDP Candidate refresh is another native outbound HTTPS boundary on the same in-process timer and manual action. It is fixed to `ucdp.uu.se` and the `/downloads/candidateged/` path, credential-free, rejects redirects, has no retries, times out after 60 seconds and caps responses at 32 MiB. The file name is derived from an internal release value; React sends only the fieldless `providers.ucdp.refresh` request and cannot supply a URL, release or schedule. CSV text is untrusted: rows are bounded, malformed rows are skipped, text is normalized and truncated, and article text, headlines and geometry are never stored. The User-Agent contains only the product name and version.

ReliefWeb discovery is a separate manual-only outbound HTTPS boundary fixed to `api.reliefweb.int`. React may supply only Event ID, bounded query/date/optional ISO3 search inputs or Event ID plus numeric report ID; it cannot supply a host, URL, HTTP method, result limit, filter JSON, appname, or persisted SourceItem metadata. Search is capped at 20 candidates and responses at 1 MiB with a 15-second timeout, caller cancellation, no redirects, retries, polling, or startup request. Strict parsing bounds metadata and ignores report bodies/files. The `appname` authority is native-only. ReliefWeb is dormant by owner decision: no `appname` will be requested, so every search and attachment fails with `ReliefWeb integration is not configured.` before HTTP dispatch.

NASA FIRMS is a separate manual-only outbound HTTPS boundary fixed to `firms.modaps.eosdis.nasa.gov`. React supplies exactly four finite viewport coordinates; native code owns the MAP_KEY, products, endpoint, `DAY_RANGE=2`, request splitting, and limits. The key is entered only in a native WinUI dialog and stored in Windows Credential Manager for the owner's Windows account, or acquired from `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` as a fallback; the app-stored key wins. It must not appear in bridge messages, frontend state, browser storage, SQLite, source records, logs, result/error payloads, or documentation examples. Missing configuration and invalid/oversized viewports issue no HTTP request. Redirects, retries, partial snapshots, startup requests, automatic viewport requests, polling, and background work are disabled.

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

Release frontend content uses this Content Security Policy, set in `src/frontend/index.html` and kept unchanged in the built `index.html`:

- `default-src 'self'`: any resource type without its own directive loads only from the app origin;
- `script-src 'self'`: scripts only from the app origin;
- `style-src 'self'`: styles only from the app origin;
- `img-src 'self' data:`: images from the app origin or data URLs;
- `connect-src 'none'`: no fetch, XHR, WebSocket or similar connections from page script;
- `worker-src 'self'`: workers only from the app origin;
- `object-src 'none'`: no plugins or embedded objects;
- `base-uri 'none'`: no `<base>` element can change the document base URL;
- `frame-ancestors 'none'`: present in the policy, but not enforced (see Limitations);
- `form-action 'none'`: forms cannot submit anywhere.

Limitations:

- The policy is delivered by a `<meta>` element, not a response header. The native host serves the bundled files through a virtual-host folder mapping and adds no CSP header.
- The CSP specification does not support `report-uri`, `frame-ancestors` and `sandbox` in a `<meta>` policy, so they are not enforced. `frame-ancestors 'none'` is therefore ignored, and this document makes no claim of protection against framing.
- There is no `frame-src` (and no `child-src`), so frames fall back to `default-src 'self'` and same-origin frames are allowed.
- The control that applies is the navigation policy: the privileged WebView allows top-level navigation only to the exact app origin (see WebView Boundary above).
- Enforcing the ignored directives would need a response-header CSP from the native host.

Do not broaden the privileged app WebView CSP or navigation allow-list because the isolated X or Bluesky WebView needs internet access. The privileged and the isolated WebViews have different trust roles.

## Persistence

SQLite is the embedded v0.1 persistence baseline. The desktop host creates and migrates the database at `%LOCALAPPDATA%\GeopoliticalMonitor\Data\geopolitical-monitor.db`.

The current schema stores ordinary application preferences (including the native-only `DemoDataCleanupCompleted` flag, which the bridge does not accept as a preference key), imported GDACS and UCDP Candidate Event/SourceItem records (including the UCDP release ordinal), explicitly attached metadata-only ReliefWeb SourceItems (ReliefWeb is dormant by owner decision), local Watchlist names plus Event-ID memberships, Watchlist-level GDACS Alert rules, and unread/read Alert history. `AccentTheme` supports `Orange`, `Blue`, `Red`, `Green`, and `Yellow`; `XPaneWidth` stores the validated local X pane width; `XPaneSide` stores `Left` or `Right`; browser localStorage is not the preference authority. Active Watchlist selection is not persisted. These tables do not store credentials, OAuth tokens, refresh tokens, passwords, API secrets, appnames, cookies, raw provider payloads, report bodies/files, provider account state, or cloud/account identity.

Alert evaluation runs inside every accepted GDACS refresh, manual or timer-driven. The Event update and Alert insertion share one SQLite transaction, and the uniqueness constraint on rule, Event, and provider revision prevents duplicate history; a cancelled or failed refresh rolls back and the next successful refresh creates the Alert once. No Alert action starts a network request. The only timer is the in-process provider refresh scheduler, which runs GDACS every 30 minutes (first run 60 seconds after the interface is ready) and the UCDP Candidate release check every 6 hours (first run 2 minutes after ready) while the app window is open, and stops on close. There is no Windows service, background worker process, Windows notification/toast, scheduled task, startup registration, or monitoring after the desktop process exits.

The X browser profile lives under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`. Session cookies may persist there solely as normal browser state controlled by X and WebView2. The app must not read those cookies for application logic.

The Bluesky browser profile lives under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\BlueskyBrowser`, a separate user-data folder with its own browser process group, created only when the user first shows Bluesky. The same rule applies: its cookies are browser state only and are never read; Clear Session only deletes them. The native-only `SocialPaneFeed` preference holds only `X` or `Bluesky`.

Do not place runtime databases or WebView2 profile data in the repository root, frontend assets, installation directory, or OneDrive by design.

## Secrets

Secrets never enter Git.

The current X website-pane design requires no X API secret, client ID, client secret, bearer token, access token, or refresh token. Copy Post Link writes only a validated public status URL to the Windows clipboard and must not read unrelated clipboard contents, cookies, tokens, page HTML, post text, or X network responses.

Frontend variables, browser localStorage, SQLite preference rows, Event/SourceItem rows, logs, bridge messages, and source files are not secret storage. FIRMS MAP_KEY acquisition is process-native only; authenticated URLs are not included in public errors or results. The only key store is Windows Credential Manager, written and read only by native code through advapi32 with one fixed target name; it protects the key with the Windows account, but other programs running as the same Windows user can read it. The bridge carries key status only (configured, source, format warning, store availability, last outcome), never the key, and React has no key input.

## External Links

X-owned HTTPS top-level navigation may remain in the isolated X pane. Non-X HTTPS links from X open in the user's system browser. Non-HTTPS schemes are blocked.

In the isolated Bluesky pane only top-level HTTPS navigation to the exact host `bsky.app` stays in the pane; other HTTPS links, including other Bluesky hosts, open in the system browser, and other schemes are blocked. The Bluesky WebView is never wired to the privileged message handler, has no host objects or web messages, cancels downloads, and denies every permission request (camera, microphone, location, notifications, clipboard and all others). A Bluesky page can reach its own origin's storage in the Bluesky folder and the network; it cannot reach the X profile, the privileged app WebView or its bridge, SQLite, the filesystem or native code.

## Future Deployment

Installer/MSIX packaging, auto-update, public networking, and remote deployment require separate explicit task authority and security review.

X scrollbar handling is constrained by the same isolation model as all other X behavior. The application may adjust only app-owned chrome and layout. It must not inject CSS/JavaScript, inspect the DOM, use accessibility scraping, or intercept X network responses to alter or infer page content.

Context-menu X post copying is URL-only and metadata-only. WebView2-provided link metadata may be validated and normalized; absence of that metadata is not authority to scrape. Copying a URL does not create SourceItem, Event, or EventSourceAssociation records. Creation requires a separate explicit paste-and-submit action in the privileged Event Sources UI.

Browser extensions are enabled only for the isolated production X profile and, only when the owner installs the reviewed ad blocker for it, the isolated Bluesky profile, through the supported WebView2 environment option. The Bluesky profile is extension-disabled by default; it uses its own verified managed copy, binding and first-class `BlueskyBrowser` scope under the same compiled trust record, and no Bluesky ad blocker action changes the X profile's binding, payload or extension state (the `Use verified X copy` source only reads and re-authenticates the X copy). With the extension installed, the reviewed payload is trusted with Bluesky page content as well as X page content. `Restore trusted instance` in the X dialog adds no authority: it is user-invoked, accepts no folder, authenticates the configured managed X payload against the compiled record again and uses the existing install transaction. The privileged app WebView remains extension-disabled. Production authenticity comes only from immutable reviewed records plus the canonical complete-runtime digest; manifest branding, folder selection, Store ID alone, diagnostic success, and the managed WebView2 runtime ID cannot self-authorize a payload. The candidate, staging tree, final `Current`, schema-2 binding, and installed runtime ID are independently reconciled. A production binding is persisted only after the exact add/enumeration transaction; Reconcile removes an unbound same-ID instance and performs a fresh add instead of adopting it. Browser-generated `_metadata/**` is excluded from the managed runtime. Unknown versions, resource-limit violations, links/reparse points, tree mutations, old bindings, interrupted activation, and final mismatches fail closed before authenticated X navigation. Exception diagnostics retain bounded type/HRESULT/category information while redacting private paths and URLs. No extension receives native host objects, the native bridge, filesystem or database APIs, cookie/token APIs, or SourceItem/Event creation authority.

The X environment uses WebView2's default scrollbar style. FluentOverlay remains deferred, and the application must not inject CSS or JavaScript into X to hide or restyle scrollbars.
