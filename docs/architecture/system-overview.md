# System Overview

Geopolitical Monitor v0.1 is a Windows desktop application.

Runtime composition:

```text
WinUI 3 desktop executable
  -> privileged application WebView2
      -> bundled React/Vite static assets
      -> bundled MapLibre GL JS world map assets

WinUI 3 desktop executable
  -> isolated X website WebView2
      -> https://x.com/home
      -> dedicated X browser profile under LocalAppData
      -> no native bridge

WinUI 3 desktop executable
  -> Application layer
  -> Domain layer

WinUI 3 desktop executable
  -> Infrastructure layer
  -> EF Core SQLite database under per-user local app data
  -> explicit manual HTTPS request to official GDACS earthquake API
  -> explicit selected-Event ReliefWeb report search and attachment boundary
  -> explicit transient NASA FIRMS viewport refresh boundary
```

Backend projects:

- `GeopoliticalMonitor.Domain` contains core domain concepts and rules.
- `GeopoliticalMonitor.Application` contains use-case/application behavior, the typed desktop bridge protocol, and testable X browser navigation/security policy helpers.
- `GeopoliticalMonitor.Infrastructure` contains adapters for local EF Core SQLite storage and LocalAppData path resolution.
- `GeopoliticalMonitor.Desktop` composes Application and Infrastructure in a WinUI 3 shell with two WebView2 controls.

There is no production localhost API server. ASP.NET/Kestrel is not part of the v0.1 desktop runtime.

The Task 003 map uses MapLibre GL JS with bundled Natural Earth-derived world-atlas geometry. Runtime basemap rendering does not require external tiles or a local tile server. A deterministic local sovereignty normalization transfers the bundled Crimea topology from Russia to Ukraine and dissolves their former shared arc before deriving the GeoJSON used by both country fill and boundary layers. The neutral sovereign basemap follows internationally recognized borders; any future representation of operational control or occupation must be a separate, sourced, timestamped overlay. Event markers are derived from persisted Event coordinates returned through the typed desktop bridge, including manually imported GDACS earthquake epicenters. The separate NASA FIRMS source/layer renders transient point observations without entering the Event collection.

SQLite persistence is initialized by the desktop host at startup. The v0.1 database path is `%LOCALAPPDATA%\GeopoliticalMonitor\Data\geopolitical-monitor.db`. The current schema stores `AccentTheme`, `XPaneWidth`, and `XPaneSide` preferences plus `Events`, `SourceItems`, explicit `EventSourceAssociations`, locally created `Watchlists`, explicit `WatchlistEvents` memberships, Watchlist-level `AlertRules`, and persistent `AlertOccurrences`. A deterministic, idempotent demo seed coexists with manually attached X sources, manually refreshed GDACS data, and explicitly attached ReliefWeb sources; it creates no Watchlists or Alerts.

The desktop composition root wires the focused Watchlist application service to a short-lived EF/SQLite store and typed bridge handler. Watchlist membership is manual and Event-ID-only. The active Watchlist is React session state that starts at `All Events`; it is not persisted. Scope is applied before the existing search and time filters, and the resulting single visible Event collection drives both rows and Map markers. Watchlist operations do not invoke providers, and deleting one preserves Events and all source data.

The desktop composition root also wires the focused Alert application service and typed bridge handler. A rule monitors one Watchlist for material updates to already watched GDACS Events. During explicit manual GDACS refresh, the persistence path classifies each existing Event as materially updated, revision-only updated, unchanged, or conflicting; Event updates and deduplicated Alert insertion commit in the same SQLite transaction. The React app requests only the unread summary at startup and loads rules and the newest 200 history rows when the user opens Alerts. There is no provider request, polling, notification subsystem, or app-closed execution in this flow.

The X website pane uses WebView2 browser state under `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`. It can be placed left or right without recreating the X WebView. It does not use X API/OAuth or automatically import X posts. Copying a post link writes only a validated public status URL to the clipboard; a separate explicit paste-and-submit action in the privileged Sources UI may persist that URL as an XPost association.

Docker and PostgreSQL/PostGIS are not required for the v0.1 desktop baseline.

GDACS is composed lazily as a native `IGdacsEventFeed` Infrastructure adapter and a provider-specific Application refresh service. Dependency construction performs no network activity. Only `providers.gdacs.refresh` starts a fixed-host, four-day, 100-record earthquake request; normalization and one SQLite transaction then create or update provider-owned Events, episode SourceItems, and associations.

ReliefWeb is composed as a native fixed-host Infrastructure client and a provider-specific Application discovery service. It searches reports only for an already selected Event after an explicit user action; candidates are transient, and a separate explicit attachment creates or reuses a bounded metadata-only `Other` SourceItem and EventSourceAssociation. ReliefWeb never creates Events. Production is intentionally configured with no `appname` until the owner supplies an approved ReliefWeb V2 identifier through native composition, so search and attachment return a bounded not-configured result without sending a network request. There is no startup request, automatic matching, automatic attachment, paging, polling, or provider metadata synchronization.

NASA FIRMS is composed as a provider-specific native Infrastructure client/parser and Application refresh service. The client obtains its MAP_KEY only from `GEOPOLITICALMONITOR_FIRMS_MAP_KEY`; no key enters React, the bridge, SQLite, browser storage, or logs. Only the typed `providers.firms.refresh` operation can request the bounded visible viewport. NOAA-20 and NOAA-21 VIIRS NRT requests use two provider calendar days to form one atomic rolling-24-hour snapshot. React derives the default Significant, Strong only, or All detections visible subset locally without another provider request. The resulting observations remain React session state and an independent MapLibre point source; they do not create or update Events, Sources, Watchlists, or Alerts.

Main layout rule:

```text
Events | Map | X   when XPaneSide is Right
X | Map | Events   when XPaneSide is Left
```

The selected-event details panel remains below the Map + Events region.

Desktop width policy: `XPaneWidth` is rendered through a shared side-independent clamp. Effective maximum X width is `min(windowWidth * MaximumPaneWidthRatio, windowWidth - MinimumMainPaneWidth - SplitterWidth)`, with `MinimumMainPaneWidth` currently 940 px. The main app owns the intelligence workspace; X yields before Events and Map can stack.

The React workspace uses two columns in all supported desktop states. On the right, Events is the bounded column and Map is flexible; on the left, Map is flexible and Events is bounded. Details remain below the complete Map + Events region. MapLibre resize remains connected through container/window resize handling; side changes and clamping must not recreate the X WebView.

The desktop host keeps separate persistent data folders for both WebView2 surfaces. The privileged application WebView uses the default extension-disabled environment. Before enabling browser extensions for the isolated X WebView, startup recovers any interrupted deterministic payload activation and revalidates persisted production authority against the complete managed AdBlock payload and compiled trust record. A clean profile or a trusted `Current` awaiting its fresh install transaction may start extension-capable; invalid, old, incomplete, or mutated saved state uses the extension-disabled X environment and requires explicit re-enrollment. An extension-environment creation failure also falls back once to the default X environment for that run. The diagnostic profile remains user-invoked and separate.

Production AdBlock payloads live at `%LOCALAPPDATA%\GeopoliticalMonitor\Extensions\AdBlock\Current`, outside both the repository and WebView user-data folder. The Application layer contains one immutable initial trust record for the reviewed Chrome Web Store `6.47.0` runtime payload. Candidate, staging, and final `Current` are independently bounded and hashed with `gpm-extension-payload-v1`; `_metadata/**` is enrollment evidence and is neither authenticated as runtime content nor copied. A schema-2 binding under `%LOCALAPPDATA%\GeopoliticalMonitor\Data` associates the exact trust-record identity and full-payload digest with the production profile and WebView2 runtime ID. The runtime ID identifies the installed managed instance but is not authenticity authority. A separate `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\ExtensionCapabilityDiagnostic` profile and diagnostic binding are created or read only after the user explicitly requests the capability test; they cannot authorize production management.
