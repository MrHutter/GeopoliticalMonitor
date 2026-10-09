# PROJECT MIGRATION HANDOFF — GEOPOLITICAL MONITOR

### Updates since this handoff (2026-10-02)

- Repository `C:\Dev\GeopoliticalMonitor-github` has a private GitHub remote (`origin`, MrHutter/GeopoliticalMonitor). Its fresh history begins at the single initial commit `1222d52`; `b4c2617` and earlier history exist only in the owner's local archive repository.
- Event automation Slice 1 `46db700` (Event severity Low/Medium/High/Critical/Catastrophic; `Provider` and `ProviderKey`), Slice 2 `0dba536` (in-process GDACS refresh scheduler, every 30 min while the app is open; provider status strip) and Slice 2B `ee5d89d` (native Event list windowing, cap 2,000) are implemented.
- Design, slice plan and owner decisions: `docs/design/event-automation-design.md`.
- Slice 3 `49c008c` (UCDP Candidate provider: monthly release, checked every 6 hours while the app is open, first check 2 minutes after `app.ready`, `Refresh UCDP` action, `ProviderLastSeenRelease` column, no Alerts for UCDP) is implemented.
- Dashboard layout: layout fix `3e94b4a` (bounded Events list, newest first, 100 rows rendered at a time), density pass `c24f6d0` (Details below the first screen; the Aircraft, Internet outages and Naval placeholder toggles hidden behind `showPlaceholderMapLayers`, Infrastructure kept; smaller markers) and scaling pass `e98a51f` (info bar removed, one-row top bar down to 940 px, Events column 300-320 px, single-line Events header text, full-width map toolbar rows).
- GDELT as an Event creator is blocked by the owner's 2026-10-02 probe of GDELT 2.0 exports (too few rows passed a strict kinetic bar, and most sampled rows were not geopolitical incidents). It is replaced by a planned explicit "Find recent reports" action on a selected Event; see `docs/design/event-automation-design.md`.
- AdBlock re-enrollment after the bound instance disappears from the X profile: see the README section "Re-enrolling AdBlock".
- KNOWN OPEN ISSUE: below about 1,270 px of client width the X pane is clipped at the right edge, because the native `XPaneColumn` has `MinWidth` 320 while the layout clamp can request less. Deliberately deferred by the owner. [Superseded 2026-10-06: the cause is the width clamp in `XPaneLayout`, which below about 1,270 px allows widths under the 320 px the header needs; the XAML `MinWidth` cannot prevent it because `ApplyXPaneWidth` resets it to 0. See `docs/design/x-pane-design.md` section 2.1 E.]
- Alert scroll to Details is implemented behind the `alertScrollsToDetails` flag, which ships off (`f68d424`); the README "Re-enrolling AdBlock" section was added in `acc8375`, and the documentation sync in `2e15c46`.
- Slice 4 demo data removal (no seeding; one-time cleanup of `evt-demo-` and `src-demo-` rows with a one-time notice; empty state) and the draft conflict and tension reference layer are implemented in `1eaee7e`, documented in `3c55296`. Every layer entry is a draft (`ownerReviewed: false`).
- Conflict and tension data update `9476579`: the Russia-Ukraine area now extends over Russia to the Urals as one entry; new Belarus and Gulf tension entries; Saudi Arabia is tinted by the Gulf entry; Bahrain is named but not shaded because it is absent from the 110m basemap.
- ReliefWeb is DORMANT BY OWNER DECISION: no appname will be requested; the code stays in place and makes zero network requests.
- On 2026-10-06 the owner began choosing a new display name. "Arctic Monitor" was considered and rejected because the owner's screening found an active public product using that exact name in the same field. "Northern Monitor" was screened (an AI-run, unverified preliminary search): no exact registered mark was found in the indexed Finnish, EU, WIPO, US or UK records, but a close name in the same field exists ("Nordic Monitor", a Stockholm-based intelligence and security publication), the words are weak as a trademark, and a UK security-monitoring business uses "Northern Monitoring Services"; rated yellow, not cleared. Alternative names are being screened. No rename has been made; the display name is still provisional and branding stays centralized in `ProductIdentity`. A professional clearance is advised before any public release or commercial use. [Superseded 2026-10-06: the display name is now "Northern Monitor", changed in `ProductIdentity` only; the identifiers keep "GeopoliticalMonitor". The screening result above (yellow, not cleared) and the advice for a professional clearance before any public release or commercial use stand.]
- Statements below that these changes contradict carry a "[Superseded 2026-10-02: ...]" note. The original text is kept.

Generated after local checkpoint commit:

`b4c2617`
`Add FIRMS external layers and harden AdBlock trust`

This handoff is intended for continuing the project in a fresh ChatGPT conversation or with another AI coding tool without relying on prior chat history.

---

## 0. CURRENT ACCEPTED CHECKPOINT

Repository:

`C:\Dev\GeopoliticalMonitor-github`

This repository's history begins at commit 1222d52. Earlier history, including b4c2617, exists only in the owner's local archive repository.

Branch:

`main`

Current accepted local commit:

`b4c2617` [Superseded 2026-10-02: HEAD has moved on; see "Updates since this handoff" at the top.]

Subject:

`Add FIRMS external layers and harden AdBlock trust`

This commit includes the previously uncommitted work for:

- External Data Layers foundation;
- NASA FIRMS;
- FIRMS rolling 24-hour behavior;
- FIRMS Significant / Strong only / All detections filtering;
- Crimea sovereign-basemap correction;
- full-payload AdBlock trust hardening;
- associated tests/documentation.

The exact full SHA was not supplied in chat, only short SHA `b4c2617`.

IMPORTANT:

- No GitHub remote has been created yet. [Superseded 2026-10-02: a private GitHub remote exists (`origin`, MrHutter/GeopoliticalMonitor).]
- Nothing has been pushed. [Superseded 2026-10-02: the fresh history has been pushed to the private remote.]
- The repository has not yet completed publication-hygiene remediation.
- The repository is not yet intended to be public.
- The next immediate phase is repository/publication hygiene and re-audit, NOT another major feature. [Superseded 2026-10-02: event automation is in progress; next planned slice is Slice 3 (UCDP Candidate) per `docs/design/event-automation-design.md`.] [Superseded 2026-10-02 (later): Slice 3 is implemented (`49c008c`); the next planned work is Slice 4 (demo data removal) and the conflict/tension reference layer (`docs/design/conflict-tension-layer-design.md`).]

Before any new coding task, recover fresh repository truth.

---

## 1. PERMANENT PROJECT WORKFLOW

For EVERY coding task:

1. Read repository-root `AGENTS.md` BEFORE doing any other project work.
2. Follow it throughout the task.
3. Check for nested `AGENTS.md` files.
4. Read applicable nested instructions before working inside their scope.

The last verified state had:

- root `AGENTS.md`;
- no nested `AGENTS.md`.

Fresh repository truth always wins.

Never allow an AI coding tool to:

- reset owner work;
- stash owner work;
- clean owner work;
- discard owner work;
- commit unless the owner explicitly requests it;
- push unless the owner explicitly requests it.

When a Codex task is revised or expanded:

- provide the ENTIRE revised task as one complete self-contained paste-ready block;
- do not provide partial addenda.

When a Codex task itself contains triple-backtick code blocks:

- use FOUR-backtick outer fencing.

---

## 2. MANUAL TESTING RULE

Whenever asking the owner to manually test Geopolitical Monitor:

ALWAYS provide the exact current PowerShell launch command.

Last verified command:

```powershell
cd C:\Dev\GeopoliticalMonitor-github
& ".\src\backend\GeopoliticalMonitor.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GeopoliticalMonitor.Desktop.exe"
```

If build output changes, recover the actual current command rather than blindly reusing this path.

For FIRMS configured testing:

```powershell
cd C:\Dev\GeopoliticalMonitor-github
$env:GEOPOLITICALMONITOR_FIRMS_MAP_KEY = "<your MAP_KEY>"
& ".\src\backend\GeopoliticalMonitor.Desktop\bin\Debug\net10.0-windows10.0.19041.0\win-x64\GeopoliticalMonitor.Desktop.exe"
```

Do not ask the owner to paste the real FIRMS MAP_KEY into chat.

---

## 3. PROJECT PURPOSE

Geopolitical Monitor is a real Windows desktop geopolitical / OSINT situational-awareness application.

Long-term direction includes:

- live geopolitical world map;
- normalized geopolitical Events;
- multiple supporting SourceItems per Event;
- personal X browsing/following feed;
- future Bluesky feed option;
- external geopolitical/news/OSINT providers;
- transient/live external map layers;
- static/versioned reference layers;
- source inspection;
- timelines;
- locations;
- Watchlists;
- Alerts;
- dossiers;
- broader analysis tools;
- eventual automation of Event/source ingestion and association.

The application should feel like a serious geopolitical situational-awareness workstation.

Avoid turning it into:

- generic news website;
- social-media clone;
- military videogame;
- fake hacker terminal;
- generic SaaS dashboard.

The product name “Geopolitical Monitor” remains provisional.

Branding should stay centralized so the product can be renamed later without invasive code changes.

The repository is intended to become portfolio/public-source material later, but not yet.

---

## 4. PUBLIC / PORTFOLIO RELEASE BAR

The owner does NOT intend to make the repository public immediately.

Before public release, the project should at minimum be PRESENTABLE, not necessarily finished.

Required presentation bar before public release:

- automated Event ingestion/monitoring fully working;
- demo data removed from normal product use; [Superseded 2026-10-02: done in `1eaee7e`; startup no longer seeds demo data and formerly seeded demo rows are removed once.]
- product starts from real provider-driven data or a clean empty state;
- unused placeholder layers hidden from the UI until implemented;
- unfinished controls should not visibly advertise non-working features;
- README/screenshots/architecture should present a coherent real product;
- security/privacy/publication audit must be clean;
- licensing/attribution/public history must be addressed.

Unused UI layers that should be hidden until real functionality exists include placeholders such as:

- aircraft;
- naval;
- infrastructure;
- internet outages;
- other not-yet-implemented layer entries.

The goal is:

**presentable, coherent, real, under active development**

—not “finished product.”

---

## 5. GITHUB / PUBLICATION INTENT

The eventual GitHub repository is primarily intended as a portfolio artifact for job applications.

Recommended strategy:

1. keep current private development repository/history local;
2. finish publication hygiene;
3. eventually create a clean public-history root / publication snapshot;
4. initially GitHub can be private during final verification;
5. later make it public when automated Events and presentation cleanup are complete.

Do NOT mirror all refs/history blindly.

The previous publication audit recommended a clean/squashed public-history root rather than exposing the entire current history.

Reason:

- old reachable history contains personal email metadata;
- one tracked historical test path contains owner-specific Windows/OneDrive path;
- obsolete/private development refs do not need publication.

---

## 6. CURRENT APPLICATION ARCHITECTURE

Primary runtime:

```text
WinUI 3 / Windows App SDK desktop host
    ↓
privileged application WebView2
    ↓
bundled React + TypeScript frontend
    ↓
MapLibre
```

Separately:

```text
WinUI 3 desktop host
    ↓
isolated X WebView2
    ↓
normal x.com website
```

Native layers:

- `GeopoliticalMonitor.Domain`
- `GeopoliticalMonitor.Application`
- `GeopoliticalMonitor.Infrastructure`
- `GeopoliticalMonitor.Desktop`

Persistence:

- SQLite
- Entity Framework Core

Database:

`%LOCALAPPDATA%\GeopoliticalMonitor\Data\geopolitical-monitor.db`

Normal runtime does NOT run:

- ASP.NET/Kestrel;
- localhost HTTP API;
- Docker;
- PostgreSQL;
- Windows Service;
- scheduled startup;
- registry autorun;
- hidden persistent background process.

The application runs only when explicitly launched and ends when closed.

Desktop deployment remains unpackaged/self-contained.

---

## 7. SECURITY MODEL — PERMANENT

Permanent rules:

- no inbound application ports during normal runtime;
- no Windows Service;
- no scheduled startup;
- no registry autorun;
- no hidden persistent background process;
- no committed secrets;
- no credentials in frontend/localStorage/sessionStorage;
- no provider credentials in SQLite preferences;
- no provider credentials in Events or Sources;
- no secret-bearing bridge messages;
- no secret-bearing logs;
- no native X cookie/token extraction;
- no X DOM scraping;
- no application-injected X JavaScript/CSS;
- no private/internal X API interception;
- no active X Developer API/OAuth dependency;
- no generic arbitrary HTTP bridge;
- no arbitrary native execute bridge;
- no arbitrary database/query bridge.

Trusted application origin:

`https://app.geopoliticalmonitor.local`

Trust uses exact URI-origin semantics.

Security authority remains centralized in:

`PrivilegedWebViewSecurityPolicy`

The X WebView remains outside the privileged bridge.

---

## 8. PRIVILEGED BRIDGE

React uses a narrow typed privileged bridge.

Capabilities now include:

- `app.ready` / `app.info`;
- preferences;
- Event list/get/source operations;
- manual X source attachment;
- GDACS refresh;
- ReliefWeb search/attach;
- Watchlist operations;
- Alert operations;
- FIRMS refresh.

Do NOT introduce:

- generic `database.query`;
- generic `native.execute`;
- arbitrary `http.request`;
- arbitrary filesystem commands.

Accepted regression seam:

`PrivilegedBridgeMessageProcessor`

Production path:

trusted source
→ origin/security validation
→ strict payload parsing
→ typed dispatch

X WebView does not use this bridge.

---

## 9. X INTEGRATION

Current X architecture:

- isolated WebView2;
- persistent user-operated session;
- no privileged bridge;
- no scraping;
- no cookie/token extraction;
- no DOM inspection;
- no application JS/CSS injection;
- extension support enabled;
- production AdBlock active.

X profile:

`%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser`

Accepted X behavior:

- `x.com/home`;
- real login/session;
- Following / For You;
- Back;
- Forward;
- Home;
- Refresh;
- Open External;
- Clear Session;
- Collapse/Reopen;
- Move Left/Move Right;
- horizontal resizing;
- persisted X side;
- persisted X width;
- Copy Post Link for valid public X status URLs.

Copying is URL-only.

Not authorized:

- post-text extraction;
- author extraction;
- media extraction;
- engagement extraction;
- DOM inspection.

---

## 10. FUTURE BLUESKY OPTION

Roadmap item:

allow the social-feed pane to use Bluesky instead of X.

Potential future UI concept:

`X | Bluesky`

Only one social pane active at a time.

Do NOT prematurely genericize the existing X integration.

Bluesky requires its own design pass.

Possible future approaches:

- isolated user-operated Bluesky WebView;
- direct AT Protocol integration if appropriate.

This is deferred until later.

---

## 11. X LAYOUT CONTRACT

X RIGHT:

`Events | Map | X`

Details below Events + Map.

X LEFT:

`X | Map | Events`

Details below Map + Events.

Permanent rules:

- Map remains visually between X and Events.
- Events + Map stay side-by-side.
- Do not stack Events above/below Map to solve width pressure.
- X width clamps before dashboard breaks.
- Details remain below the full Map + Events region.
- Map remains dominant/flexible.
- Events remains bounded.

---

## 12. MAP BASELINE

MapLibre GL JS implemented.

Accepted map behavior:

- fully local/bundled world basemap;
- no remote normal-map tile dependency;
- Natural Earth/world-atlas-derived 110m geography;
- dark neutral styling;
- pan/zoom;
- World reset;
- Event markers;
- selected Event ring;
- semantic Event colors;
- mock conflict-zone foundation; [Superseded 2026-10-02: the mock zones were replaced in `1eaee7e` by the owner-curated draft conflict and tension reference layer.]
- antimeridian correction.

Critical setting:

`renderWorldCopies: false`

Do not remove this.

Map remains the visual center of the product.

---

## 13. CRIMEA SOVEREIGN BASEMAP CORRECTION

The base map follows internationally recognized sovereign geography.

Current accepted result:

Crimea visually belongs to Ukraine’s sovereign basemap geometry.

The original bundled `world-atlas/countries-110m.json` classified Crimea inside Russia geometry.

Final accepted correction:

- identify the Crimea topology polygon under Russia;
- remove it from Russia;
- merge it into Ukraine;
- use `topojson-client` merge/dissolve behavior;
- dissolve the obsolete Ukraine–Crimea shared sovereign arc;
- derive final GeoJSON after correction;
- preserve coastline;
- preserve Russia/Ukraine geometry elsewhere;
- preserve antimeridian normalization.

Important semantic separation:

**sovereign reference geography != operational control**

Future occupation/frontline/control-of-terrain layers must be separate, sourced, timestamped overlays.

Do not mix them into base sovereignty.

---

## 14. DOMAIN MODEL

Core rule:

`SourceItem != Event`

Event:

one normalized real-world geopolitical occurrence.

SourceItem:

one piece of evidence/source material.

One Event may have many SourceItems.

One SourceItem may support multiple Events through `EventSourceAssociation`.

Event categories:

- MilitaryConflict
- Security
- PoliticalDiplomatic
- CivilUnrest
- Infrastructure
- Humanitarian
- Economic
- NaturalDisaster
- Other

Statuses: [Superseded 2026-10-02: status values are unchanged; Slice 1 added a separate `Severity` (Low, Medium, High, Critical, Catastrophic) with `SeverityRule` and `SeverityInputs`, plus provider identity fields `Provider` and `ProviderKey`.]

- Developing
- Active
- Resolved
- Unverified

Provider-ingested `Unverified` means:

not independently verified by Geopolitical Monitor.

Do not map provider severity into Event lifecycle status unless explicitly designed.

SourceTypes:

- XPost
- NewsArticle
- OfficialStatement
- DatasetRecord
- Other

Keep these information categories distinct:

1. Normalized Events
2. Transient/live map layers
3. Static/versioned reference layers
4. Evidence / SourceItems

Do not force every map datum into Event.

---

## 15. EVENT SEARCH / FILTERING

Implemented:

- Event search;
- case-insensitive matching;
- search across title/summary/category/status/location fields; [Superseded 2026-10-02: search also matches severity; a minimum-severity filter (All, Medium+, High+, Critical+) combines with AND.]
- time ranges:
  - ALL
  - 24H
  - 7D
  - 30D
  - 90D
- ALL startup default; [Superseded 2026-10-02: ALL stays the startup default, but the list is now queried natively and capped at the 2,000 newest Events by last update (the selected Event is kept when it matches), with a notice when more match.] [Superseded 2026-10-06: ALL is no longer the startup default. The Events filters start at `30D` and `High and above` on every launch (`29c3677`); ALL still means no time limit, and Alert activation still resets to ALL, all severities, All Events and no search.]
- search + time range use AND semantics;
- Event rows + Map markers use same visible collection;
- selection clears when filtered out;
- clearing filters does not reselect old Event.

---

## 16. MANUAL X SOURCE ATTACHMENT

Workflow:

user browses X
→ Copy Post Link
→ manually paste URL into selected Event Sources
→ application validates
→ creates/reuses XPost SourceItem
→ associates to Event

No automatic import.

No scraping.

No X API.

No clipboard monitoring.

Same URL + same Event:

`AlreadyAttached`

Same URL + different Event:

reuse SourceItem + add association.

X timestamp derived locally from Snowflake ID.

---

## 17. SETTINGS

Accepted Settings includes:

- compact Settings entry;
- right-side panel/modal;
- blocking backdrop;
- Appearance;
- Accent themes:
  - Orange
  - Blue
  - Red
  - Green
  - Yellow
- preference persistence;
- application information;
- Escape close;
- keyboard focus containment;
- focus restoration.

Orange default.

Accent Theme is separate from Event semantic colors.

---

## 18. GDACS

Accepted GDACS behavior:

- earthquakes only;
- event type EQ;
- manual refresh; [Superseded 2026-10-02: GDACS also refreshes on an in-process timer every 30 min while the app is open; manual refresh remains.]
- preceding four-day window;
- max 100;
- no paging;
- no startup refresh; [Superseded 2026-10-02: the first automatic refresh runs 60 s after the UI signals `app.ready`.]
- no polling; [Superseded 2026-10-02: timer refresh every 30 min while the app is open, with backoff after failures.]
- no background task; [Superseded 2026-10-02: an in-process scheduler task runs while the main window is open and stops when it closes; nothing runs when the app is closed.]
- point epicenter only.

GDACS Events:

`EventStatus.Unverified`

Networking:

- native;
- fixed official HTTPS host;
- 15-second timeout;
- cancellation;
- 1 MiB response cap;
- no retries;
- no startup networking. [Superseded 2026-10-02: GDACS networking starts 60 s after `app.ready`, never before the window exists.]

Persistence:

- idempotent;
- episodes reuse Event;
- newer provider update can update provider-owned fields;
- stale data cannot overwrite newer;
- missing records do not delete/resolve existing Events;
- ownership collision protection.

---

## 19. RELIEFWEB

ReliefWeb does NOT create Events.

Workflow:

selected Event
→ Sources
→ Find ReliefWeb reports
→ explicit search
→ transient candidates
→ explicit Attach
→ persistent reusable SourceItem
→ EventSourceAssociation

No automatic matching.

No ReliefWeb map markers.

Production remains intentionally unconfigured because no approved V2 appname has been supplied. [Superseded 2026-10-02: ReliefWeb is dormant by owner decision; no appname will be requested, and the code stays in place with zero network requests.]

Without appname:

- zero network requests;
- bounded configuration unavailable state;
- app remains usable.

Do not invent a placeholder appname.

---

## 20. WATCHLISTS

Watchlist:

locally persisted named collection of Event IDs.

Behavior:

- list;
- create;
- rename;
- delete;
- add/remove membership;
- same Event can belong to multiple lists;
- All Events scope;
- Watchlist scope;
- search/time filters apply within scope;
- one visible Event collection drives rows + map;
- persists across restart;
- active Watchlist selection session-only;
- startup = All Events.

Limits:

- 100 Watchlists;
- 2000 Events per Watchlist;
- name max 80 UTF-16 code units.

No dynamic Watchlist criteria yet.

---

## 21. ALERTS

Accepted Alerts foundation:

Rule trigger:

`GdacsEventMateriallyUpdated`

Each rule belongs to one Watchlist.

Rules:

- created enabled;
- enable/disable;
- no historical backfill;
- no request on enable;
- history persists while disabled.

Alert created only when:

1. Event already exists.
2. Event is in monitored Watchlist.
3. enabled GDACS alert rule exists.
4. provider revision strictly newer.
5. material Event field changed.
6. provider update accepted.
7. Event + Alert commit atomically.

Material fields:

- title;
- summary;
- StartedAtUtc;
- latitude;
- longitude;
- location display;
- country code;
- region.

No Alert for:

- new Event;
- revision-only update;
- source-only update;
- stale/equal data;
- disabled rule;
- Event outside Watchlist;
- removed membership;
- X attachment;
- ReliefWeb attachment;
- membership change itself.

Alert state:

- unread/read only.

No:

- acknowledgement;
- dismissal;
- clear-all;
- mark-unread.

No Windows notifications.

No app-closed monitoring.

No polling/background worker. [Superseded 2026-10-02: Alerts evaluate inside timer-driven GDACS refreshes run by the in-process scheduler while the app is open.]

Alerts evaluate only during explicit manual GDACS refresh. [Superseded 2026-10-02: Alerts evaluate on every accepted GDACS refresh, manual or timer.]

---

## 22. NASA FIRMS — ACCEPTED IMPLEMENTATION

NASA FIRMS is a transient live map layer.

It does NOT create:

- Event;
- SourceItem;
- Watchlist membership;
- Alert;
- SQLite rows.

Provider products:

- `VIIRS_NOAA20_NRT`
- `VIIRS_NOAA21_NRT`

Not used:

- Suomi-NPP;
- MODIS;
- LANDSAT;
- WMS/WFS.

Fixed host:

`firms.modaps.eosdis.nasa.gov`

Credential:

`GEOPOLITICALMONITOR_FIRMS_MAP_KEY`

Native-only.

Never in:

- React;
- bridge payload;
- localStorage/sessionStorage;
- SQLite;
- Event/Source;
- logs;
- authenticated URL output.

Missing/blank key => local `ConfigurationUnavailable` before HTTP.

---

## 23. FIRMS 24-HOUR WINDOW

User-facing FIRMS window is:

**rolling latest 24 hours**

Provider request uses:

`DAY_RANGE=2`

Reason:

FIRMS Area API day ranges are calendar-day based, so two days are needed to construct a true rolling 24h window across UTC midnight.

Native filter:

`AcquiredAtUtc >= nowUtc - 24 hours`

No additional time-window UI yet.

---

## 24. FIRMS GEOGRAPHIC / NETWORK LIMITS

Refresh uses current visible MapLibre viewport.

Bridge request:

- west;
- south;
- east;
- north.

Maximum viewport:

- 90° longitude span;
- 60° latitude span.

Too large:

- zero HTTP requests;
- typed `AreaTooLarge`;
- prior snapshot preserved;
- tell user to zoom in.

Antimeridian:

- split natively.

Logical requests:

- normal: 2;
- antimeridian: 4.

Networking:

- HTTPS only;
- fixed host;
- redirects disabled;
- no retries;
- 15-second timeout;
- cancellation;
- 4 MiB response cap per request;
- max 20,000 normalized observations per snapshot;
- no startup request;
- no polling.

NO `world` scope.

---

## 25. FIRMS REFRESH ATOMICITY

NOAA-20 + NOAA-21 together form one logical snapshot.

All-or-nothing:

any required request failure means:

- do not replace current snapshot;
- preserve previous successful snapshot;
- show bounded failure.

Successful refresh replaces previous snapshot.

No accumulation across refreshes.

---

## 26. FIRMS DISPLAY FILTERS

Three application-defined display modes:

### Significant — DEFAULT

- confidence nominal OR high
- FRP >= 10 MW

### Strong only

- confidence nominal OR high
- FRP >= 50 MW

### All detections

- complete valid 24h snapshot

These are Geopolitical Monitor display filters.

They are NOT NASA severity categories.

Do not label them:

- major fire;
- fire size;
- NASA significant;
- NASA strong.

Filter switching is local-only.

NO network request.

Status distinguishes:

- shown count;
- total count;
- 24h.

Example:

`312 shown · 1,842 detections · 24h`

---

## 27. FIRMS MAP RENDERING

FIRMS:

- independent GeoJSON point source/layer;
- session-only;
- default visibility off;
- no persistence;
- no browser storage.

Layer order:

1. basemap/reference
2. conflict zones
3. FIRMS
4. Event markers
5. selected Event ring

Age buckets:

- <3h
- 3–6h
- 6–12h
- 12–24h

Popup:

- NASA FIRMS — Fire / Thermal Anomaly
- acquisition UTC
- NOAA-20 / NOAA-21
- confidence
- FRP
- day/night
- coordinates
- processing version
- attribution

Click does not select/create Event.

Attribution:

`NASA FIRMS / LANCE`

---

## 28. ADBLOCK — OLD ACCEPTED BASELINE

Production managed location:

`%LOCALAPPDATA%\GeopoliticalMonitor\Extensions\AdBlock\Current`

Historically observed managed WebView2 runtime ID:

`ejmmfklccndmnifalleboolihmpaflb`

This is path-derived from the stable managed location.

It is NOT official store identity.

Diagnostic extension validation and production trust must remain separate.

---

## 29. ADBLOCK SECURITY AUDIT FINDING

Publication/security audit discovered a real production security weakness:

the old extension workflow trusted:

- self-asserted manifest identity;
- manifest-only SHA-256;
- WebView2 runtime/path identity.

This did NOT authenticate the complete extension payload.

Arbitrary scripts/rules/assets could theoretically differ while manifest still matched.

A subsequent trust-enrollment investigation established an independent official browser-store source.

---

## 30. ADBLOCK TRUST ENROLLMENT AUTHORITY

Initial approved record:

Product:

AdBlock

Store:

Chrome Web Store

Official Chrome extension ID:

`gighmmpiobklfepjocnamgkkbiglidom`

Version:

`6.47.0`

Canonical format:

`gpm-extension-payload-v1`

Canonical full-runtime-payload SHA-256:

`9022cbf484913e8c45cbeae3828c7072de51ffa1369013e7e0aa105172a822ae`

Runtime files:

443

Runtime bytes:

326,555,718

No Edge record is initially approved.

No other Chrome version is approved.

Future versions require explicit enrollment/review.

---

## 31. ADBLOCK CANONICAL PAYLOAD IDENTITY

Authenticate every regular runtime file except:

`_metadata/**`

Included examples:

- manifest;
- JS/service worker;
- HTML;
- CSS;
- source maps;
- rule data;
- locales;
- WASM/binaries;
- fonts;
- images/icons/assets;
- licenses/release notes.

Excluded:

- `_metadata/verified_contents.json`
- `_metadata/computed_hashes.json`
- `_metadata/generated_indexed_rulesets/**`
- all `_metadata/**`

Canonical digest format:

`gpm-extension-payload-v1`

Conceptual stream:

```text
ASCII "GPM-EXTENSION-PAYLOAD"
0x00
UInt32LE format-version = 1
UInt64LE file-count

for each sorted file:
  0x01
  UInt32LE UTF-8-path-byte-count
  UTF-8 normalized relative path
  UInt64LE file-byte-count
  32 raw bytes SHA-256(file contents)
```

Path rules:

- `\` -> `/`
- strict UTF-8
- ordinal ordering
- preserve case
- no Unicode normalization
- no absolute path
- no drive/UNC
- no `.`
- no `..`
- no NUL
- no duplicate normalized paths
- no reparse/special entries

Final identity:

lowercase outer SHA-256.

---

## 32. ADBLOCK FULL-PAYLOAD TRUST HARDENING — ACCEPTED

Production now uses a compiled immutable trust-record authority.

Candidate cannot self-enroll.

Trust requires exact:

- product;
- official store ID;
- exact version;
- digest format;
- full canonical digest.

Production flow:

candidate
→ bounded tree inspection
→ exact trust-record match
→ bounded staging
→ staged digest recheck
→ safe activation
→ final `Current` recheck
→ WebView2 load
→ managed runtime ID verification
→ strict binding persistence

Startup reconciliation also revalidates the managed payload.

Manifest identity remains compatibility/sanity only.

Runtime ID remains instance identity only.

Neither is authenticity authority.

---

## 33. ADBLOCK RESOURCE / FILESYSTEM LIMITS

Current limits:

- max runtime files: 1,024
- max aggregate runtime bytes: 512 MiB
- max single file: 32 MiB
- max directories: 256
- max directory depth: 16
- max normalized relative path: 512 UTF-8 bytes
- bounded manifest/locale read: 1 MiB

Files are:

- size-checked before allocation;
- streamed for hashing/copying;
- checked for size/timestamp changes.

Rejected:

- reparse points;
- junctions;
- symbolic links;
- malformed paths;
- duplicate normalized paths;
- non-regular entries.

---

## 34. ADBLOCK MANAGED ACTIVATION SAFETY

Managed transaction paths include:

- `Current.staging`
- `Current.previous`
- `Current.rejected`

Staging is fully authenticated before activation.

Final `Current` is reauthenticated afterward.

Failures preserve/recover prior trusted `Current`.

Startup recovers interrupted replacement states.

Production never loads candidate or staging path directly.

---

## 35. ADBLOCK BINDING / RECONCILIATION

Binding schema is now version 2.

Carries reviewed trust-record identity, canonical payload identity, managed path/profile/runtime identity.

Schema-1 or malformed/unknown bindings are not silently trusted.

Persisted invalid state causes extensions to start disabled.

Production reconcile:

- removes unbound same-ID instance;
- performs fresh verified WebView2 add;
- persists schema-2 authority only after verification.

Manual hardening acceptance passed.

The owner successfully found and selected the Chrome 6.47.0 official store-managed payload.

---

## 36. ADBLOCK DIAGNOSTIC / PRODUCTION DISTINCTION

The visible:

`Diagnostic AdBlock Validation`

dialog is intentionally diagnostic.

It uses an isolated diagnostic profile.

It does NOT mean production X is modified by diagnostic testing.

The owner had run extension-support testing, which is why the diagnostic dialog showed its extension state.

Do not confuse:

- diagnostic profile management;
- production X profile AdBlock authority.

Chrome AdBlock payload root used for approved source enrollment is conceptually under:

`%LOCALAPPDATA%\Google\Chrome\User Data\Default\Extensions\gighmmpiobklfepjocnamgkkbiglidom\<6.47.0-version-folder>`

Do not expose owner-specific absolute paths in publication.

---

## 37. AUTOMATED TEST BASELINE AFTER HARDENING

Latest accepted validation before commit:

Backend:

- Domain: 56 passed
- Application: 336 passed
- Infrastructure: 138 passed
- Total: 530 passed

Backend build:

- 0 warnings
- 0 errors

Frontend:

- 109 passed
- 9 files

Frontend production build:

passed

`git diff --check`:

passed

Vite still reports the existing advisory for chunks >500 kB.

No real extension payload, browser profile, secret, or session data entered repository.

---

## 38. PUBLIC-RELEASE SECURITY / PRIVACY AUDIT SUMMARY

A comprehensive read-only audit was completed before the AdBlock hardening.

At audit time:

- BLOCKER: 0
- HIGH: 4
- MEDIUM: 2
- LOW: 2

Important positive findings:

NO real:

- FIRMS MAP_KEY;
- provider credential;
- private key;
- X session DB;
- production SQLite DB;
- login data;
- browser cookies;
- private API key

was found in tracked or reachable Git content.

No credential rotation was indicated.

---

## 39. PUBLICATION FINDINGS STILL UNRESOLVED

These remain after commit `b4c2617`.

### HIGH / IMPORTANT

1. Tracked test contains owner-specific Windows/OneDrive path.

Historical location:

`XDiagnosticAdBlockIdentityTests.cs`

It exposes a personal path and should be replaced with a synthetic path before publication.

2. Existing Git history exposes personal email metadata.

At audit time:

- all 23 reachable commits used same personal Gmail address;
- no GitHub noreply commits.

Full old history should NOT be published unless owner explicitly accepts disclosure.

Recommended:

clean public-history root / squashed publication snapshot.

3. `.gitignore` lacks explicit defensive coverage for:

- `*.db`
- `*.sqlite`
- `*.sqlite3`
- WAL/SHM/journal/backups
- WebView2/Chromium profile markers/directories
- extension runtime data
- crash dumps
- source maps outside dist
- machine-local runtime configuration

4. A stale ignored legacy WebView2 profile exists beneath backend `bin`.

It is ignored and would not be pushed normally, but should be cleaned in a separate authorized cleanup before raw workspace/archive sharing.

### MEDIUM / PUBLIC HYGIENE

- project license still needs selection;
- NOTICE/attribution/redistribution review incomplete.

### LOW

- ensure no path-sensitive diagnostic output remains outside the hardened extension/WebView2 paths;
- never publish raw workspace archives.

---

## 40. COMMIT METADATA / PUBLIC HISTORY STRATEGY

Do NOT publish current development history as-is.

Recommended strategy:

**create a clean public-history root / squashed publication state**

Reason:

- no real secret requires rotation;
- old history includes personal email;
- owner-specific path exists historically;
- obsolete OAuth branch does not need publication;
- Codex internal refs do not need publication;
- easier to review one controlled public snapshot.

Do not mirror all refs.

Do not push:

- archive branches;
- Codex refs;
- old internal refs

unless deliberately approved.

---

## 41. GIT / REPOSITORY STATE AFTER CURRENT COMMIT

Current local accepted checkpoint:

`b4c2617`

Subject:

`Add FIRMS external layers and harden AdBlock trust`

No GitHub remote yet. [Superseded 2026-10-02: a private GitHub remote exists (`origin`, MrHutter/GeopoliticalMonitor).]

Nothing pushed. [Superseded 2026-10-02: the fresh history has been pushed to the private remote.]

Before next task:

recover:

- full HEAD SHA;
- working tree status;
- staged state;
- remotes/upstream;
- branches/refs.

Do not assume working tree is clean solely from this handoff.

---

## 42. NEXT IMMEDIATE PHASE

Next task should be:

**REPOSITORY / PUBLICATION HYGIENE REMEDIATION**

It should address:

1. synthetic replacement for owner-specific test path;
2. `.gitignore` hardening;
3. stale ignored WebView2 profile cleanup;
4. review remaining diagnostic path leaks;
5. publication-safe Git identity strategy;
6. clean public-root preparation;
7. LICENSE selection;
8. dependency/data attribution and NOTICE review;
9. re-run publication audit.

Do NOT create GitHub remote yet unless owner explicitly changes order. [Superseded 2026-10-02: the private remote has been created.]

---

## 43. PUBLICATION HYGIENE — EXPECTED CLEANUP AREAS

Potential future task should inspect/fix:

### Git tracked source

- personal paths
- machine-specific paths
- local identifiers

### .gitignore

Defensive patterns for:

- SQLite DBs and sidecars
- WebView2 profiles
- Chromium browser state
- extension runtime copies
- crash dumps
- machine-local config
- source maps
- logs/temp output

### Local build/runtime cleanup

- stale WebView2 profile under ignored backend `bin`

### Git identity

- set future author email to GitHub noreply or desired public-safe address before future public commits
- do not rewrite current private history unless specifically needed
- public root should have clean metadata

### Public history

- clean/squashed root from reviewed tree
- do not mirror historical refs

---

## 44. LICENSE / ATTRIBUTION STATUS

Current known public-hygiene facts:

README exists.

No project:

- LICENSE
- consolidated NOTICE/attribution file
- SECURITY.md
- CONTRIBUTING.md

yet.

Known dependencies/data mentioned in audit:

- Natural Earth data: public domain
- world-atlas: ISC
- MapLibre: BSD-3-Clause
- topojson-client: ISC

No AdBlock extension binaries/assets are bundled in repo.

Before public release:

- select project license;
- complete dependency/data attribution review;
- determine NOTICE requirements;
- verify redistribution terms for any bundled/runtime third-party material.

Do not conflate licensing with privacy/security.

---

## 45. KNOWN COLD FIRST-LAUNCH BEHAVIOR

Known recurring behavior:

- first/cold launch may be slow;
- immediate subsequent launches are essentially instant.

Previously investigated.

No clear repository-controlled root cause.

Do not confuse with earlier invalid desktop dependency manifest problem.

A prior separate incident occurred when sandbox-blocked NuGet restore generated a broken `.deps.json` missing Microsoft.WinUI.

That was fixed.

A regression test validates generated desktop `.deps.json` includes Microsoft.WinUI.

Treat normal cold-start slowness as non-blocking unless new evidence shows a materially different regression.

Do not make speculative startup changes.

---

## 46. EXTERNAL DATA ROADMAP

Current planned external-data areas:

1. NASA FIRMS — DONE first slice
2. static/reference layers:
   - airports
   - nuclear facilities
   - rail later after suitable offline import/versioning workflow
3. MET Norway contextual weather
4. USGS seismic observational layer

Deferred:

- Copernicus/Sentinel imagery
- ADS-B aircraft
- Ukraine air-alert feed
- GNSS/GPS interference

Frontline/control-of-terrain mapping remains deferred.

Do not automatically resume it.

---

## 47. EVENT AUTOMATION — NEXT MAJOR PRODUCT PRIORITY

After publication hygiene / private GitHub setup, the likely next MAJOR product area is:

**automated Event ingestion / monitoring architecture**

Do NOT implement automatically without a dedicated design pass. [Superseded 2026-10-02: the design pass is done and approved (`docs/design/event-automation-design.md`); Slices 1, 2 and 2B are implemented.] [Superseded 2026-10-02 (later): Slice 3 (`49c008c`) is implemented as well.]

Future design topics:

- automated provider refresh;
- ingestion lifecycle;
- Event normalization;
- update detection;
- provider identity;
- cross-provider Event deduplication;
- automated Event/source association;
- Watchlist interaction;
- Alert interaction;
- retry/failure semantics;
- scheduling/runtime lifecycle;
- behavior while app is closed;
- duplicate prevention across providers.

This work is also a prerequisite for making the eventual public portfolio build feel presentable and real.

---

## 48. DEMO DATA / PUBLIC PRESENTATION

Before public release:

- remove/disable demo-seeded Events from normal product startup; [Superseded 2026-10-02: done in `1eaee7e`.]
- app should use real automated ingestion or a clean empty state;
- demo fixtures may remain in tests where appropriate;
- public UI should not look like a development sandbox.

Do not remove demo/test infrastructure prematurely before automated Event ingestion is working.

---

## 49. UNUSED PLACEHOLDER LAYERS

Current map-layer UI historically includes placeholders such as:

- aircraft
- naval
- infrastructure [Superseded 2026-10-02: the owner decided to keep the Infrastructure toggle visible; only Aircraft, Internet outages and Naval are hidden.]
- internet outage

Before public presentation:

hide any layer from normal UI until it is backed by real functionality. [Superseded 2026-10-02: done in `c24f6d0` for Aircraft, Internet outages and Naval, which are hidden behind `showPlaceholderMapLayers` (false); Infrastructure stays visible by owner decision.]

Do not expose non-functional controls merely to show roadmap ideas.

Roadmap features belong in docs/issues, not unfinished visible UI.

---

## 50. FUTURE SOCIAL FEED PROVIDERS

Current:

X only.

Future roadmap:

Bluesky option.

Do not genericize immediately.

Design later around:

- provider-specific auth/security;
- WebView vs AT Protocol integration;
- consistent pane UX;
- preserved isolation.

Potential UI:

`X | Bluesky`

---

## 51. FRONTLINE / CONTROL-OF-TERRAIN PRINCIPLE

Future Ukraine/Russia frontline/control layer is interesting but deferred.

If revisited:

- source-specific assessed geometry;
- provenance always visible;
- `as of` timestamp;
- do not present one line as objective truth;
- consider independent source layers;
- distinguish:
  - frontline/control boundary;
  - contested/grey zone;
  - territorial-control polygons;
  - geolocated combat Events.

Do not use sovereign basemap to represent current military control.

---

## 52. VISUAL DESIGN

Base:

near-black / dark charcoal.

Avoid strong navy cast.

Typography:

muted silver / cool gray.

Accent themes:

- Orange
- Blue
- Red
- Green
- Yellow

Orange default.

Accents vivid/high-chroma but restrained.

Semantic Event colors independent from Accent Theme.

Desired:

- dark
- dense
- low-luminance
- map-first
- restrained
- professional
- OSINT/intelligence workstation

Avoid:

- glowing full panels
- scanlines
- radar sweeps
- green hacker terminal
- giant SaaS cards
- videogame HUD

---

## 53. ACCEPTED IMPORTANT COMMIT HISTORY

`0b95e49`
Complete isolated X browser desktop integration

`1ab776b`
Harden privileged WebView origin validation

`e733d7a`
Restore fast X startup and defer unsupported AdBlock host path

`f7a857a`
Validate WebView2 extension capability

`2d2761d`
Validate diagnostic AdBlock extension management

`0ab51c3`
Activate AdBlock for isolated X browser

`1665f1f`
Complete event filtering and manual X source attachment

`c37a10f`
Complete Settings foundation and product-name centralization

`beb4614`
Add GDACS earthquake ingestion

`c4331b5`
Add ReliefWeb report discovery and attachment

`abfad10`
Reconcile architecture documentation

`78f306d`
Add privileged bridge regression coverage

`82636d4`
Add watchlist foundation

`6e5d9a0`
Add alerts foundation

`b4c2617`
Add FIRMS external layers and harden AdBlock trust

---

## 54. CURRENT ACCEPTANCE SUMMARY

Through commit `b4c2617`:

### FUNCTIONALLY ACCEPTED

- isolated X browser
- production AdBlock
- hardened full-payload AdBlock trust
- Events
- Event filtering
- manual X source attachment
- Settings
- GDACS
- ReliefWeb
- Watchlists
- Alerts
- MapLibre local basemap
- Crimea sovereign-basemap correction
- NASA FIRMS
- FIRMS 24-hour behavior
- FIRMS filters
- FIRMS popup/status/legend

### AUTOMATED TEST BASELINE

Backend:

- 530 passed [Superseded 2026-10-02: last reported at `ee5d89d`: Domain 68, Application 459, Infrastructure 184.] [Superseded 2026-10-02 (later): last reported at `e98a51f`: Domain 77, Application 535, Infrastructure 233.]

Frontend:

- 109 passed across 9 files [Superseded 2026-10-02: last reported at `ee5d89d`: 169 passed across 11 files.] [Superseded 2026-10-02 (later): last reported at `e98a51f`: 202 passed across 15 files.]

### PUBLICATION STATUS

NOT READY FOR PUBLIC PUSH YET.

Remaining:

- publication hygiene
- public history cleanup strategy
- license
- attribution/NOTICE
- re-audit

---

## 55. FIRST TASK FOR A NEW CHAT / SECOND AI

Start with a READ-ONLY repository reconciliation from `b4c2617`.

Report:

- root/nested AGENTS
- branch
- exact full HEAD SHA
- HEAD subject
- working tree
- staged files
- untracked files
- remotes
- upstream
- branches/tags/refs relevant to publication
- whether commit `b4c2617` contains the expected accepted features
- whether any owner changes happened after the commit

Do not immediately implement new features.

Then proceed to the repository/publication hygiene remediation. [Superseded 2026-10-02: reconcile against current HEAD instead; the next planned work is Slice 3 (UCDP Candidate).] [Superseded 2026-10-02 (later): Slice 3 is implemented (`49c008c`); the next planned work is Slice 4 (demo data removal) and the conflict/tension reference layer (`docs/design/conflict-tension-layer-design.md`).]

---

## 56. AUTHORITY RULE

When this handoff conflicts with fresh repository truth:

1. read `AGENTS.md`;
2. inspect current Git state;
3. inspect current code/tests/docs;
4. reconcile deliberately;
5. do not guess.

This handoff is context, not permission to overwrite newer work.

---

## 57. IMMEDIATE NEXT DIRECTION

Current local checkpoint:

`b4c2617`
`Add FIRMS external layers and harden AdBlock trust`

NEXT:

**Repository/publication hygiene remediation** [Superseded 2026-10-02: the private repository with fresh history exists and event automation is in progress; next planned slice is Slice 3 (UCDP Candidate).] [Superseded 2026-10-02 (later): Slice 3 is implemented (`49c008c`); the next planned work is Slice 4 (demo data removal) and the conflict/tension reference layer (`docs/design/conflict-tension-layer-design.md`).]

Then:

**re-run publication audit**

Then likely:

**create a private GitHub repository / controlled clean public-history root**

Later major product work:

**automated Event ingestion / monitoring architecture**

Public release only once:

- automation is real;
- demo startup content is removed; [Superseded 2026-10-02: done in `1eaee7e`.]
- unused placeholder UI is hidden;
- repo is presentable and public-safe.
