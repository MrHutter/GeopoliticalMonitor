# Geopolitical Monitor - Repository Instructions

## Product Authority

Geopolitical Monitor v0.1 is a Windows desktop application.

The normal production runtime must not depend on:

- an ASP.NET or other localhost HTTP server;
- Vite;
- Docker;
- PostgreSQL;
- production inbound listening ports.

React is the embedded UI technology. WinUI 3, Windows App SDK, and WebView2 are the current desktop shell authority.

The current no-cost X integration uses the published X website in an isolated, user-operated WebView2 surface. Do not add scraping, DOM extraction, injected automation, cookie/token extraction, private X API calls, OAuth developer-app requirements, or paid X API dependency without explicit design authority. The X WebView must not expose the privileged native application bridge.

## Architecture

Retain these backend layers:

- `GeopoliticalMonitor.Domain`
- `GeopoliticalMonitor.Application`
- `GeopoliticalMonitor.Infrastructure`
- `GeopoliticalMonitor.Desktop`

Dependency direction:

```text
Domain
  ^
Application
  ^
Infrastructure

Application
  ^
Desktop

Infrastructure
  ^
Desktop
```

The desktop project is the composition root. Domain must not depend on UI, infrastructure, databases, HTTP clients, X, GDELT, React, WinUI, or WebView2.

## Desktop Runtime Rules

The application starts only after explicit user action. Closing the app ends normal application execution.

Do not create Windows Services, scheduled tasks, startup entries, registry autorun entries, login startup shortcuts, tray daemons that survive app exit, background updaters, PM2/NSSM configurations, or hidden process managers without explicit task authority.

The normal production desktop runtime must not expose an HTTP server or open inbound listening ports. The isolated X website pane is outbound HTTPS browser traffic only.

## Embedded Web UI

The React UI is built with Vite and packaged as static assets.

The privileged application WebView2 loads application-owned bundled/local content. It must not become a general web browser.

The isolated X WebView2 loads `https://x.com/home` and normal user-operated X-owned HTTPS pages. External non-X links open in the system browser instead of replacing the X pane.

The native/web bridge must be narrow and validated. Use typed, schema-controlled messages. Reject unknown message types, malformed payloads, unexpected fields, and unsupported operations.

Do not expose arbitrary native objects, shell execution, filesystem access, process execution, X cookies, X page content, or a generic native-command bridge to JavaScript.

## Persistence

SQLite is the current v0.1 embedded persistence foundation.

The v0.1 database lives at `%LOCALAPPDATA%\GeopoliticalMonitor\Data\geopolitical-monitor.db` and is created/migrated by the desktop composition root at startup.

Persisted v0.1 data currently includes the `AccentTheme`, `XPaneWidth`, and `XPaneSide` application preferences; `Event` records with embedded `Location` data, severity fields (`Severity`, `SeverityRule`, `SeverityInputs`), and provider identity fields (`Provider`, `ProviderKey`, `ProviderLastSeenRelease`); `SourceItem` and `EventSourceAssociation` records; Watchlists and their Event memberships; and Alert rules and Alert occurrences. Local demo Events and SourceItems are still seeded at startup until the demo-data-removal slice. Do not add accounts, credentials, live feeds, automatic ingestion, or automatic normalization/correlation without explicit task authority.

Narrow exception: the approved design `docs/design/event-automation-design.md` is the authority for timer-based provider refresh while the application is open, implemented by the in-process native `ProviderRefreshScheduler` that starts after the UI is ready and stops when the main window closes. Nothing runs when the application is closed. This does not authorize any Windows Service, scheduled task, startup registration, hidden process, inbound port, or app-closed activity, and it does not authorize providers beyond those the design names (GDACS, UCDP Candidate, GDELT), each only in its approved slice.

The X website pane uses normal WebView2 browser profile state under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`. Session cookies may persist there as browser state only. Do not read those cookies for application logic.

Do not place runtime databases or WebView2 profiles in the repository root, frontend assets, installation directory, or OneDrive by design.

## Domain Rules

Keep `SourceItem` and `Event` distinct.

A `SourceItem` is individual source material such as an X post, article, official statement, structured dataset record, or future OSINT source.

An `Event` is a normalized real-world geopolitical occurrence.

Multiple SourceItems may support one Event. Do not treat an X post as an authoritative Event merely because it exists. The current X website pane must not import X posts into SourceItem.

## Layout Rules

The geopolitical map must remain visually between the Events panel and the isolated X pane.

When X is Right:

```text
Events | Map | X
```

When X is Left:

```text
X | Map | Events
```

The selected-event detail panel remains below the Map + Events region and spans the main application width excluding X. Do not change this layout contract without explicit design authority.

## X Integration Rules

The current v0.1 X integration is a user-operated website pane, not an API integration.

Do not add or require:

- X Developer access;
- paid X API credits;
- X API SDKs;
- `api.x.com` calls;
- OAuth developer apps;
- client IDs or client secrets;
- bearer, access, or refresh tokens.

Do not inspect the X DOM, inject JavaScript or CSS, automate clicks, auto-select Following, automate scrolling, intercept X network responses, extract cookies, extract tokens, or scrape posts.

If X blocks WebView2 login or embedded browsing, retain the Open External/system-browser fallback and report the blocker. Do not spoof user agents, bypass restrictions, or copy cookies from another browser.

X post link handling is user-driven and URL-only. Permission to copy a public X status URL does not authorize scraping post content, metadata, engagement, media, DOM, cookies, tokens, or page source. The isolated X WebView must not gain privileged application bridge access.

## Visual Direction

The application is dark-native.

Use near-black/neutral-charcoal surfaces, restrained silver-gray typography, orange as the default accent with blue, red, green, and yellow alternates, semantic event colors independent from accent choice, flat dense panels, restrained borders, and minimal shadows. Do not reintroduce a strongly blue/navy application base next to the isolated X website pane without explicit design authority.

Avoid white-heavy UI, cyberpunk effects, scanlines, fake radar sweeps, glowing HUD borders, terminal-green styling, generic SaaS dashboards, and videogame strategy-map styling.

## Git Discipline

Do not commit or push unless explicitly requested.

Before completing a task, inspect `git status` and `git diff`. Do not discard unrelated user work.

Desktop responsiveness must preserve the horizontal intelligence workspace. Events and Map remain side-by-side regardless of whether X is Left or Right; do not solve desktop width pressure by vertically stacking those core panels. Clamp the auxiliary X pane before the main application drops below its minimum width, and compact or reflow secondary top-bar controls instead.

X page appearance and content must not be modified with injected scripts or styles merely to change scrollbar appearance. X post-link handling remains user-driven and URL-only; do not infer permission to scrape post content, metadata, DOM, page source, cookies, tokens, accessibility trees, DevTools data, or network responses.

The isolated X WebView may use WebView2 environment options for Fluent overlay scrollbars and user-selected browser extensions. These capabilities belong only to the X profile. The privileged application WebView must remain extension-disabled and must not expose host objects or native bridge access to X pages or extensions.

Geopolitical Monitor may host a user-selected unpacked browser extension in the isolated X profile, but it must not bundle, download, audit, or silently install an ad blocker. Extension behavior is governed by the extension manifest and WebView2 browser-extension permissions; native application code still must not inject into, scrape, or inspect X content.
