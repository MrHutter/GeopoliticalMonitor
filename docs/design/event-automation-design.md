# Event Automation Design

Status: design approved by the owner on 2026-09-30, with the changes recorded in section 14; slices 1 to 4 (including 2B) are implemented, Slice 5 (GDELT provider) is blocked, the Slice 5 replacement (find recent reports) is parked, and slices 6 and 7 are not started (see the implementation status table). Baseline: `main` at `e30d955` ("Add third-party notices and security policy"). Written 2026-09-30.

Values marked **PROPOSED** are approved as starting values and will be revisited after about two weeks of real data. File and line references are to the baseline commit.

## Implementation status (2026-10-02)

| Slice | Status | Commit |
| :- | :- | :- |
| Slice 1: Severity field and display | Implemented | `46db700` |
| Slice 2: Shared refresh scheduler, with GDACS on the timer | Implemented | `0dba536` |
| Slice 2B: Event list windowing | Implemented | `ee5d89d` |
| Slice 3: UCDP Candidate provider | Implemented | `49c008c` |
| Slice 4: Demo data removal | Implemented | `1eaee7e` (docs `3c55296`) |
| Slice 5: GDELT provider under strict rules | **Blocked** (owner probe 2026-10-02, see 5.2) | — |
| Slice 5 (replacement): Find recent reports | **Parked** (owner test 2026-10-02, see the slice) | — |
| Slice 6: Cross-provider deduplication | Not started; matters only if a second conflict source is added, so reassess before starting | — |
| Slice 7: Alert integration | Not started | — |

The baseline named in the status line above and the findings in section 2 describe the code before implementation began. Line numbers in section 2 refer to `e30d955` and have since moved, so use the file names. **As built** notes mark where the implementation differs from the text. Owner decisions, thresholds and later slices are unchanged, except that GDELT Event creation (section 5.2 and Slice 5) was blocked on 2026-10-02, which makes Q3 and Q7 moot. ReliefWeb is dormant by owner decision: no appname will be requested, and its code stays in place with zero network requests.

---

## 1. Summary and scope

Geopolitical Monitor currently fills its Event list from local demo seeds plus a manual "Refresh GDACS" button. This design makes Event ingestion automatic **while the app is open**, adds a severity level to every Event, and adds two new Event routes. (**As built:** since Slice 2, GDACS also refreshes on the in-process timer; the manual "Refresh GDACS" button remains, in the provider status strip.)

What changes for the user:

- Events appear and update on their own while the app is running. Nothing runs when the app is closed.
- Every Event shows a severity level: low, medium, high, critical or catastrophic. Severity is separate from lifecycle status and from any provider's own alert level, and Details explains how it was derived.
- Automatically created Events are visibly marked as unverified and carry a provider label (GDACS, UCDP Candidate, GDELT machine-coded).
- New routes: UCDP Candidate Events (curated conflict events, monthly release) and GDELT 2.0 (live, machine-coded, noisy; strict creation rules). ReliefWeb stays attach-only. ACLED is out of scope.
- UCDP Candidate data is released monthly and is up to about a month old. The 24H and 7D time filters will therefore show few UCDP Events, and the status strip labels UCDP as "monthly release".
- Each provider shows its last successful update, its next scheduled refresh, and a stale or failed state. Manual refresh buttons remain.
- Demo Events are removed from normal startup once real feeds exist.

Out of scope: any service, scheduled task, startup registration or hidden process; a candidate or review queue; model-assisted matching; map clustering; dynamic Watchlist criteria; Windows notifications; ACLED.

---

## 2. Findings

### 2.1 Current code (Part A)

**Event and SourceItem model**

- `Event` holds Id, Title, Summary, Category, Status, StartedAtUtc, LastUpdatedAtUtc, Location, CreatedAtUtc and UpdatedAtUtc (`src/backend/GeopoliticalMonitor.Domain/Events/Event.cs:31-40`). There is no severity, provider or provider-key field.
- `EventStatus` is `Developing`, `Active`, `Resolved` or `Unverified` (`Domain/Events/EventStatus.cs:3-8`).
- `EventCategory` has nine values: MilitaryConflict, Security, PoliticalDiplomatic, CivilUnrest, Infrastructure, Humanitarian, NaturalDisaster, Economic and Other (`Domain/Events/EventCategory.cs:3-13`).
- `SourceItem` has SourceType, ExternalId, Title, Text, Url, author fields, Published/Retrieved timestamps and an optional Location (`Domain/Sources/SourceItem.cs:35-46`).
- Persistence mirrors the domain. Status and Category are stored as strings (`Infrastructure/Persistence/Entities/EventEntity.cs:5-21`). Length limits are Title 256, Summary 2000, SourceItem Text 4000, Url 1024 and ExternalId 256 (`Infrastructure/Persistence/GeopoliticalMonitorDbContext.cs:32-60`).
- Deleting an Event cascades to its associations, Watchlist memberships and alert occurrences (`GeopoliticalMonitorDbContext.cs:73-80, 104-107, 142-145`).
- There are four EF migrations, the latest being `20260927164002_AddAlertsFoundation`. The database is migrated at startup (`Infrastructure/Persistence/LocalDatabaseInitializer.cs:16`).

**Where "Unverified" is set and shown**

- GDACS sets it (`Application/Gdacs/GdacsRefreshService.cs:104`), and one demo seed uses it (`Infrastructure/Persistence/DemoDataSeeder.cs:36`).
- Details shows the status as text (`src/frontend/src/features/event-details/EventDetailsPanel.tsx:80, 110, 118`).
- The Event list row shows status **only as a colored dot marked `aria-hidden`** (`src/frontend/src/features/events/EventPanel.tsx:71`). There is no text, so the list does not tell a user in words that an Event is unverified.
- The list is sorted by status rank before anything else (`EventPanel.tsx:27, 31`).

**Existing severity**

- No Event severity field exists.
- The GDACS alert level and magnitude are parsed (`Application/Gdacs/GdacsModels.cs:13-14`; alert level limited to Green/Orange/Red at `Infrastructure/Gdacs/GdacsGeoJsonParser.cs:256-258`). They are persisted **only inside the SourceItem provenance text** (`GdacsRefreshService.cs:138-139`), not as structured data.
- Status dots already use severity-like tokens: Unverified uses `--critical` red, Active `--high`, Developing `--medium` and Resolved `--low` (`src/frontend/src/styles.css:24-27, 992-1017`). Status therefore already reads visually like a severity. That conflicts with a real severity scale and must change.

**GDACS path end to end**

- UI: the "Refresh GDACS" button (`EventPanel.tsx:50-52`) calls `refreshGdacs` (`src/frontend/src/App.tsx:708-714`), which sends `providers.gdacs.refresh`.
- Native side: dispatch (`src/backend/GeopoliticalMonitor.Desktop/MainWindow.xaml.cs:658-659`) → `RefreshGdacsAsync` (`MainWindow.xaml.cs:806-822`) → `GdacsRefreshBridgeHandler.RefreshAsync` (`Application/DesktopBridge/GdacsRefreshBridgeHandler.cs:7-13`) → `GdacsRefreshService.RefreshAsync` (`GdacsRefreshService.cs:30-89`).
- On success the UI re-requests the Event list and, if new Alerts were created, the Alert summary (`App.tsx:375-393`).
- Networking is limited to the fixed host `www.gdacs.org`, a 1 MiB response cap, a 15 s timeout linked to the caller's token, and 100 records with no paging (`Infrastructure/Gdacs/GdacsEventFeed.cs:9-11, 30-31, 91`; `GdacsGeoJsonParser.cs:9`). It requests a four-day window (`GdacsRefreshService.cs:44`).
- Provider identity is encoded in deterministic IDs: `evt-gdacs-eq-{eventId}`, `src-gdacs-eq-{eventId}-{episodeId}` and `GDACS:EQ:{eventId}:{episodeId}` (`GdacsRefreshService.cs:11-13, 95-97`). There are no provider columns.
- Ownership check: an existing Event with a GDACS-shaped ID is updated only if one of its sources is a GDACS DatasetRecord with a matching ExternalId prefix and an official `https://www.gdacs.org/report.aspx` URL. Anything else counts as a Conflict (`Infrastructure/Persistence/SqliteEventStore.cs:277-307, 457-475, 492-498`).
- Stale and idempotency rules: a newer `LastUpdatedAtUtc` updates the record, an older one leaves it unchanged, and an equal one is unchanged if the data matches or a Conflict if it differs (`SqliteEventStore.cs:417-455`). Records missing from the feed are never deleted.
- Provider updates overwrite Title, Summary, times and location, but never Status or Category (`SqliteEventStore.cs:526-538`).
- Alerts: rules for `GdacsEventMateriallyUpdated` are joined with Watchlist memberships (`SqliteEventStore.cs:265-275`). An occurrence is added only for a strictly newer, materially changed update (`SqliteEventStore.cs:333-364`; material fields at `500-510`). Everything, alerts included, is saved in one transaction (`SqliteEventStore.cs:237, 404-405`).
- **Timer hook point:** `GdacsRefreshService.RefreshAsync`. A scheduler calls the same service instance the bridge handler uses, so the existing gate (`GdacsRefreshService.cs:15, 32-35`) enforces a single in-flight refresh across manual and timer calls.
- Nothing in the store checks whether a refresh was manual. The "Alerts only on manual refresh" rule is enforced purely by there being no other caller. (**As built:** Slice 2 added `ProviderRefreshScheduler`, through `GdacsScheduledProvider`, as a second caller of the same `GdacsRefreshService` instance, so Alerts now also evaluate on timer refreshes.)

**Lifetime and shutdown today**

- `MainWindow` owns `lifetimeCancellation` (`MainWindow.xaml.cs:36`) and passes it to provider operations (for example line 817).
- `OnClosed` cancels it, disposes the HTTP clients, disposes the token source and closes both WebViews (`MainWindow.xaml.cs:1925-1934`).
- Persistence and GDACS are composed in `InitializePersistenceAsync` (`MainWindow.xaml.cs:481-510`).
- In-flight transactions that never reach `CommitAsync` are rolled back when disposed or when the process ends, and SQLite's journal handles an abrupt exit.
- Minor: `refreshGate.WaitAsync(0, cancellationToken)` sits outside the try block in GDACS (`GdacsRefreshService.cs:32`), so an already-cancelled token throws instead of returning `Cancelled`. FIRMS handles this case (`Application/Firms/FirmsRefreshService.cs:31-44`). A scheduler must tolerate both. (**As built:** fixed in Slice 2. The gate wait now sits inside a try block in `GdacsRefreshService.cs`, and an already-cancelled token returns `Cancelled`.)

**Seeded demo data**

- `DemoDataSeeder` inserts 6 Events, 14 SourceItems and their links on **every** startup (`DemoDataSeeder.cs:12-28`; called at `MainWindow.xaml.cs:510`).
- The inserts are insert-if-missing (`SqliteEventStore.cs:63-105`), so a deleted demo Event comes back on the next launch.
- Demo records are marked only by their ID prefixes `evt-demo-` and `src-demo-` and by "demo" wording in their text.
- Tests that run the seeder: `tests/GeopoliticalMonitor.Infrastructure.Tests/SqliteEventStoreTests.cs:186, 217` and `WatchlistPersistenceTests.cs:172`.
- Several tests use demo-style IDs as plain fixtures: `DesktopBridgeProtocolTests.cs`, `EventSourceBridgeHandlerTests.cs`, `ReliefWeb*Tests.cs`, `src/frontend/src/nativeBridge.test.ts`, `App.test.tsx` and `mapEvents.test.ts`.
- The frontend also imports `initialMapLayers` and `mockXPosts` from `src/frontend/src/mock/dashboardData.ts` (`App.tsx:13`). That is separate from Event seeding.

**UI touchpoints**

- List rows show time, region, title, source count and category, with a left border in the category color (`EventPanel.tsx:70-81`).
- Map markers are circles filled by category color with a dark stroke (`src/frontend/src/features/map/MapLibreWorldMap.tsx:199-203`; palette at `src/frontend/src/features/map/mapEvents.ts:14-24`).
- FIRMS uses a warm orange-to-brown age ramp (`src/frontend/src/features/map/firmsMap.ts:82-97`).
- Accent themes redefine `--accent*` only (`styles.css:18-70`). Semantic tokens stay independent of the accent.
- (**As built, 2026-10-02:** the legend now lists exactly the nine Event categories in the marker colors and has no "Maritime" entry.) The map legend lists "Maritime", which is not an `EventCategory`, and omits PoliticalDiplomatic, Economic and Other (`src/frontend/src/features/map/MapLegend.tsx:1-9`).
- Search matches status text (`src/frontend/src/features/events/eventFilters.ts:30`).
- Source URLs in Details are shown as text, not links (`EventDetailsPanel.tsx:169-171`).
- The ReliefWeb candidate uses an `<a target="_blank">` (`src/frontend/src/features/event-details/ReliefWebDiscovery.tsx:128`), while the app WebView registers no `NewWindowRequested` handler. What WebView2 then does with the click should be verified separately (see section 14).

**Bridge surface**

- There are request/response types for GDACS and FIRMS refresh (`Application/DesktopBridge/DesktopBridgeProtocol.cs:40-45`), plus Events, Watchlists, Alerts, ReliefWeb and preferences (`DesktopBridgeProtocol.cs:17-72`).
- The native side already pushes one message nobody requested: `xPane.side.value` (`MainWindow.xaml.cs:729-732`), validated on the web side (`src/frontend/src/nativeBridge.ts:453`). That is the precedent for a typed status push.

**FIRMS and GDACS as patterns**

- Both use a `SemaphoreSlim(1,1)` gate with a zero-wait "AlreadyInProgress" result, a `TimeProvider` for testability, typed failure kinds mapped to fixed user messages, and a caller-owned cancellation token.
- GDACS persists through a store in one transaction. FIRMS keeps an in-memory snapshot and never persists.
- What a scheduler can reuse: the gate, the status enums, the fixed-host feeds with caps and timeouts, and the result-to-message mapping.
- What stays provider-specific: request windows, parsing, normalization, identity, and the persistence or snapshot model.

**Other observations**

- `GetEventsAsync` loads every Event without paging and sorts in memory (`SqliteEventStore.cs:15-23`). The frontend renders all of them. This matters for volume (section 11). (**As built:** since Slice 2B, `events.list` uses the windowed native query (`EventWindowService` and `SqliteEventStore.QueryEventWindowAsync`), capped at 2,000 Events, and the selected Event is kept in the window when it matches the criteria. `GetEventsAsync` remains, but only tests call it.)
- The FIRMS key is read from the process environment variable `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` (`MainWindow.xaml.cs:111`).

### 2.2 Differences between the handoff doc and the code

The code is treated as the authority. References are to sections of `docs/Geopolitical_Monitor_Migration_b4c2617_2026-09-29.md`.

| Handoff says | Code shows |
| :- | :- |
| §14 lists categories with Economic before NaturalDisaster | Enum order is NaturalDisaster, Economic (`EventCategory.cs:11-12`). Stored as strings, so no functional impact. |
| §18 and §21 imply provider identity is a modelled concept | Identity exists only as ID and ExternalId string prefixes; there are no provider columns. |
| §21: Alerts evaluate only during explicit manual GDACS refresh | Alert evaluation is inside the store and applies to any caller. "Manual only" holds only because no other caller exists. **As built:** since Slice 2 the timer is a second caller, so Alerts evaluate on manual and timer refreshes. |
| §21: Alert state is unread/read only | Matches (`Domain/Alerts/AlertOccurrence.cs:23-29`). |
| §8 lists bridge capabilities | Also includes the unsolicited native push `xPane.side.value`, which is not listed. |
| §48: demo data should be removed or disabled later | The seeder re-inserts demo rows on every launch, so deleting them by hand is not durable. |
| §14: provider-ingested Unverified means not independently verified | Matches for GDACS. However, the list UI shows Unverified only as a red dot with no text. |
| Not mentioned | Status dots use the `--critical`/`--high`/`--medium`/`--low` tokens, so status already looks like severity. |
| Not mentioned | The map legend includes "Maritime" and omits three real categories. |
| AGENTS.md "Persistence" lists preferences and demo Event data only | Watchlists and Alerts are also persisted (migrations `AddWatchlistFoundation`, `AddAlertsFoundation`). AGENTS.md is out of date here; it is not edited in this task. **As built:** AGENTS.md was updated in the 2026-10-02 docs sync. |

### 2.3 External sources (Part B)

Both routes are permitted for the planned use, provided the attribution below is shown. Neither is blocked.

**UCDP Candidate Events**

- Downloads page: <https://ucdp.uu.se/downloads/>. API docs: <https://ucdp.uu.se/apidocs/>. Codebook v1.5: <https://ucdp.uu.se/downloads/candidateged/ucdp-candidate-codebook1.5.pdf>.
- **License:** the downloads page says all datasets are "free of charge and licensed under CC BY 4.0".
- **Required citation** (codebook, "When using this data, please always cite"): Hegre, Håvard, Mihai Croicu, Kristine Eck, and Stina Högbladh (July 2020). "Introducing the UCDP Candidate Events Dataset." *Research & Politics*, doi:10.1177/2053168020935257. The downloads page also asks to acknowledge that UCDP is part of and funded by DEMSCORE.
- **Cadence:** monthly releases, "Data extracted from UCDP systems on the 20th of every month" (codebook). The page listed v26.0.8 (August 2026), v26.0.7 (July 2026) and a January–June 2026 release v26.01.26.06.
- **File URLs as listed on the page:** `https://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv` (plus `.xlsx`). The pattern is `GEDEvent_v{YY}_0_{M}.csv` for monthly releases, but the half-year release uses a different form, so **the naming is not fully predictable**. Those exact URLs came from a summarized page fetch and must be re-checked before implementation.
- **Token:** the page presents direct file links, and nothing fetched indicated a login. **Unconfirmed until the first real request:** that the CSV downloads need no token, the file size, and the CSV delimiter and header.
- **API:** requires a token sent as the HTTP header `x-ucdp-access-token`, requested by email to the API maintainer listed on the apidocs page. The quota is 5,000 requests per day, and errors count. Base URL `https://ucdpapi.pcr.uu.se/api/`, resource `gedevents/<version>` with `pagesize`/`page` and `StartDate`/`EndDate`/`Country`/`Geography` filters.
- **Fields (codebook):** `id` ("A unique numeric id identifying each event"; "Users should rely exclusively on id for persistent event identification across versions"; `relid` can change), `date_start`, `date_end`, `date_prec` (1 exact … 5 up to a year), `latitude`, `longitude`, `where_prec` (1 exact; 2 within ca. 25 km; 3 admin-2 centroid; 4 admin-1 centroid; 5 linear or fuzzy feature; 6 country only; 7 international waters or airspace), `best`, `low`, `high` (fatality estimates), `type_of_violence` (1 state-based, 2 non-state, 3 one-sided), `side_a`, `side_b`, `dyad_name`, `country`, `where_coordinates`, `source_article`, `number_of_sources`, `event_clarity`, and `code_status`.
- **`code_status`** is `Clear` or one of `Check Type of violence`, `Check Dyad`, `Check Deaths`, `Check Incompatibility`, `Check Vague/biased source`, `Check Geography` or `Check Other`, flagging higher uncertainty.
- **Revisions:** "Not all events in UCDP Candidate will make it into the final UCDP GED and new events will be added." Monthly releases include every event with `date_start` or `date_end` in the exported month, so **the same event can appear in two consecutive releases**.

**GDELT 2.0**

- About and terms: <https://www.gdeltproject.org/about.html>. Data page: <https://www.gdeltproject.org/data.html>. GDELT 2.0 announcement: <https://blog.gdeltproject.org/gdelt-2-0-our-global-world-in-realtime/>. Event codebook: <https://data.gdeltproject.org/documentation/GDELT-Event_Codebook-V2.0.pdf>.
- **Terms:** all datasets are "available for unlimited and unrestricted use for any academic, commercial, or governmental use of any kind without fee", and "any use or redistribution of the data must include a citation to the GDELT Project and a link to this website".
- **Update list:** `https://data.gdeltproject.org/gdeltv2/lastupdate.txt`, updated every 15 minutes. It has three lines, each `<bytes> <md5> <url>`, for `.export.CSV.zip`, `.mentions.CSV.zip` and `.gkg.csv.zip`. A fetch on 2026-09-30 showed an export file of 64,413 bytes compressed.
  - The listed file URLs use **`http://`**. The design requires HTTPS on the fixed host `data.gdeltproject.org`, so the client must rewrite the scheme and validate host and path.
  - **Unconfirmed:** that the zip files are served over HTTPS at the same path.
- **Event file:** tab-delimited, one record per line (codebook). Fields useful for filtering: `GlobalEventID`, `SQLDATE`, `DATEADDED` (15-minute resolution), `IsRootEvent`, `EventCode`, `EventBaseCode`, `EventRootCode`, `QuadClass` (4 = Material Conflict), `GoldsteinScale` (−10 to +10, fixed per event code), `NumMentions`, `NumSources`, `NumArticles`, `AvgTone`, `ActionGeo_Type`, `ActionGeo_FullName`, `ActionGeo_CountryCode`, `ActionGeo_Lat`, `ActionGeo_Long`, `ActionGeo_FeatureID` and `SOURCEURL`.
- **`ActionGeo_Type` values:** 1 COUNTRY, 2 USSTATE, 3 USCITY, 4 WORLDCITY, 5 WORLDSTATE.
- **Important:** `NumMentions` and `NumSources` count only "the 15 minute update in which it was first seen". They do not grow in later export files; later mentions go to the Mentions table.
- `GlobalEventID` is globally unique but not guaranteed sequential.
- **Rate, robots and polling guidance:** none found on the pages read. The design polls at most once per 15-minute publish cycle and downloads only the newest export file.
- **Unconfirmed:** the exact column count and order of the 2.0 export (to be pinned against the codebook table in the slice that builds the parser), and typical rows per file.

---

## 3. Refresh scheduler

### 3.1 Where it lives

**Recommendation: native.** A `ProviderRefreshScheduler` in the Application layer, composed and started by `MainWindow` after persistence initialization and stopped by `lifetimeCancellation`.

| Option | Assessment |
| :- | :- |
| Native (Application class, started by the Desktop composition root) | Services, gates, `TimeProvider` and the lifetime token already live here. It keeps working if the web UI reloads or errors. It is testable with a fake `TimeProvider` and fake feeds. It does not need a React tab to be open or healthy. |
| React-driven (`setInterval` sending refresh messages) | Duplicates timing logic across reloads. Timers are throttled when the WebView is hidden. It moves a scheduling decision into the less-trusted surface and increases bridge traffic. The only advantage is that it needs no push message. |

Design:

- The scheduler holds one `ProviderSchedule` per provider, each with an interval, the next due time, a consecutive failure count and the last outcome.
- It uses `TimeProvider.CreateTimer`, or one `PeriodicTimer` ticking every 30 s that checks due times. The tick loop is a single `Task` started from the UI thread and awaited on shutdown.
- It calls the **same service instance** as the bridge handler, so the existing `SemaphoreSlim` gate gives exactly one in-flight refresh per provider whether the trigger is manual or the timer.
- A timer tick that finds the gate busy is **skipped, not queued**.
- A successful manual refresh resets that provider's next due time to now plus the interval.
- Providers are refreshed one at a time, never in parallel, which caps concurrent outbound requests at one.

### 3.2 Intervals (PROPOSED)

| Provider | Interval | First automatic run after launch | Rationale |
| :- | :- | :- | :- |
| GDACS | 30 min | 60 s after the UI sends `app.ready` | The four-day window is capped at 100 records, and the feed changes slowly. |
| UCDP Candidate | 6 h (checks for a new release; downloads only when one appears) | 2 min after `app.ready` | Monthly release, extracted around the 20th. |
| GDELT | 15 min | 90 s after `app.ready` | Matches the publish cycle. Only the newest export is fetched, with no backfill of cycles missed while the app was closed. |

### 3.3 Shutdown mid-refresh

`OnClosed` already cancels `lifetimeCancellation`. The scheduler loop and every provider call observe that token.

- Cancellation before `CommitAsync` means the transaction is disposed without commit, so SQLite rolls back and no Event or Alert is partly written.
- Cancellation during `CommitAsync` means SQLite commits atomically or not at all.
- If the process ends mid-transaction, the rollback journal restores the previous state on the next open.

Required fixes:

- The scheduler must catch `OperationCanceledException` and `ObjectDisposedException`, because `OnClosed` disposes the token source immediately (`MainWindow.xaml.cs`).
- The scheduler must not post to a closed WebView.
- `OnClosed` should stop the scheduler (cancel, then stop its timer) before disposing the HTTP clients.

Nothing waits on shutdown and nothing outlives the window.

### 3.4 Failure, backoff and stale display

- **No retries inside a refresh.** The existing "no retries" feed rule stays.
- **After a failure** (ProviderUnavailable, TimedOut, InvalidResponse or PersistenceFailure) the next due time is `now + max(interval, 5 min × 2^(n−1))`, capped at 6 h and with ±10 % jitter, where n is the consecutive failure count. The counter resets on success.
- Configuration problems (for example a missing UCDP token when the API route is chosen) mark the provider `Unconfigured`. It stops being scheduled until restart, making zero requests, as ReliefWeb does without an appname.
- There are no retry storms: at most one attempt per provider per due time, one provider at a time, and no queued retries.
- **Stale rule (PROPOSED):** a provider is stale when its last success is older than 3 × its interval, or for UCDP when the newest imported release is more than 45 days old.
- **What the UI shows:** a compact provider status strip in the Events panel header. Each provider shows its name, "Updated 12 min ago", the next refresh time, and a state of refreshing, stale, failed with a fixed short message, or not configured. UCDP carries the label "monthly release", because its data is released monthly and is up to about a month old; the 24H and 7D filters will show few UCDP Events. The existing "Refresh GDACS" button becomes one Refresh action per provider, disabled while that provider is refreshing. (**As built:** the strip is `ProviderStatusStrip.tsx`, and with GDACS as the only scheduled provider it holds the "Refresh GDACS" action.)

### 3.5 Permanent rules versus relaxed slice rules

None of this violates the permanent rules (handoff §7, AGENTS.md). There is no inbound port, service, scheduled task, startup registration, registry autorun or hidden process. The timer is an in-process task owned by the main window and cancelled when it closes. Networking stays native, uses fixed hosts, and applies timeouts, response caps and cancellation.

The following **slice-level** rules from the accepted GDACS and Alerts slices are deliberately relaxed. They were acceptance criteria for manual-first slices, not permanent security rules:

| Slice rule (handoff) | Relaxed to |
| :- | :- |
| GDACS: "no startup refresh", "no startup networking" | The first automatic refresh starts shortly after the UI signals ready, never before the window exists. |
| GDACS: "no polling", "no background task" | Interval refresh by an in-process timer, only while the window is open. |
| Alerts: "No polling/background worker"; "Alerts evaluate only during explicit manual GDACS refresh" | Alerts evaluate on any accepted provider refresh, manual or timer (section 8). |

"No app-closed monitoring" and "No Windows notifications" remain in force.

---

## 4. Severity model

### 4.1 Levels and storage

The levels are Low, Medium, High, Critical and Catastrophic, stored as a new non-null `Severity` enum on `Event` and as a string column.

Severity stays separate from `Status` (lifecycle) and from provider alert levels. Two additional bounded, non-null fields make the level explainable:

- `SeverityRule`: a string of at most 64 characters, for example `gdacs-eq-v1`, `ucdp-candidate-v1` or `gdelt-v1`, and `manual` for demo or future hand-entered Events.
- `SeverityInputs`: a string of at most 256 characters, for example `alert=Orange; magnitude=6.4`.

Severity is recomputed by the owning provider on each accepted update. A rule change bumps the rule version, so old and new results stay distinguishable.

### 4.2 Per-provider mapping (all thresholds PROPOSED)

**GDACS earthquakes.** Inputs are the GDACS alert level (Green, Orange or Red, which reflects estimated exposure and vulnerability) and the magnitude.

| Condition | Severity |
| :- | :- |
| Green, magnitude < 6.0 or not provided | Low |
| Green, magnitude ≥ 6.0 | Medium |
| Orange | High |
| Red, magnitude < 7.5 or not provided | Critical |
| Red, magnitude ≥ 7.5 | Catastrophic |

**UCDP Candidate.** The input is `best`, the best fatality estimate. `low`, `high` and `code_status` are shown as context but do not change the level.

| `best` | Severity |
| :- | :- |
| 0 | Low (unused while Q5 skips zero-fatality records; kept for rule completeness) |
| 1–9 | Medium |
| 10–99 | High |
| 100–999 | Critical |
| ≥ 1000 | Catastrophic |

**GDELT.** There is no casualty data, and codes are machine-assigned. Severity is **capped at High**.

| Condition | Severity |
| :- | :- |
| Event meets the creation bar (section 5.2), including the root-18 subset | Medium |
| … and `EventRootCode` is 19 or 20, and `NumSources` ≥ 5 | High |

Root-18 subset Events (bombings and assassinations) stay Medium in this rule version.

**Demo and manual Events:** set explicitly, with rule `manual`.

**The Catastrophic bar** is reserved. It is reached only by a GDACS Red alert with magnitude ≥ 7.5, or a UCDP best estimate ≥ 1000. Machine-coded data never reaches Critical or Catastrophic on its own.

### 4.3 Display, filters, Watchlists

- **Row:** a text badge with the severity word (for example "High"), with color as a secondary cue. The badge sits beside an "Unverified" text badge and the provider label (section 5.3). The list is sorted by severity (descending), then by last update, replacing the current status-rank sort (`EventPanel.tsx`).
- **Map:** keep the category fill. Show severity through circle radius (five steps) and, for Critical and Catastrophic, a light outer ring. Severity is **not** mapped to fill color: the category palette (`mapEvents.ts`), the FIRMS ramp and the red accent theme leave no room for five more distinct fills.
- **Color scale:** reuse `--low`, `--medium`, `--high` and `--critical` (`styles.css`), and add a `--catastrophic` token. The new token must be distinct from `--critical`, the accent red `#ff3b4f`, MilitaryConflict `#d95d62` and the FIRMS oranges; a dark magenta is suggested and needs design sign-off. Like the other semantic colors, it must not change with the accent theme.
- **Status dots must stop using severity tokens.** Move status to neutral styling or text only, so red means severity alone.
- **Details:** a "Severity" block showing the level, the rule in plain words (for example "GDACS Orange alert → High"), the inputs, and the sentence "Severity is derived automatically from provider data and is not an independent assessment."
- **Filters:** add a minimum-severity filter (All, Medium+, High+, Critical+) combined with AND alongside search and time range. Add severity words to search.
- **Watchlists:** unchanged. Membership is by Event ID; the filters apply within scope.

---

## 5. Event creation rules

### 5.1 UCDP Candidate → Event (PROPOSED)

Records are **included** only if all of the following hold:

- `latitude` and `longitude` are valid.
- `where_prec` ≤ 4.
- `date_start` falls within the import window, which is the release month; the first import also takes the previous release.
- `best` ≥ 1, to limit volume (open question Q5).

`where_prec` 5–7 and `best` = 0 records are skipped and counted.

| Event field | Source |
| :- | :- |
| Id | `evt-ucdp-cand-{id}` |
| Title | Type label plus place, for example "State-based violence — {where_coordinates}", sanitized and truncated to 256 characters |
| Summary | Dyad or sides, the date range, "Fatalities (best/low/high): b/l/h", code status, and the sentence "Candidate data may be revised or removed in later UCDP releases." Truncated to 2000 characters. |
| Category | `type_of_violence` 1 or 2 → MilitaryConflict; 3 (one-sided) → Security (open question Q6) |
| Status | Unverified |
| StartedAtUtc | `date_start` at 00:00 UTC |
| LastUpdatedAtUtc | The release's publication timestamp, used as the provider revision (see 6.1) |
| Location | latitude, longitude, `where_coordinates`, `country`, `region` |
| Severity | Section 4.2 |

Each record also creates a SourceItem `src-ucdp-cand-{id}` with SourceType DatasetRecord, ExternalId `UCDP:CAND:{id}`, a URL pointing to the UCDP downloads page, and provenance text containing the citation and the `code_status`.

### 5.2 GDELT strict creation rules (PROPOSED) — BLOCKED

> **Blocked (2026-10-02).** GDELT does not create Events. The owner's probe over 24 hours of GDELT 2.0 exports on 2026-10-02 applied a strict kinetic-code bar (CAMEO codes 193 to 196, the 20x codes, the 183x bombing codes, 185 and 186; `QuadClass` 4; city-level locations; one row per source URL). Only about 16 rows a day passed at `NumSources` 2 and `NumArticles` 3, and about 8 at the proposed 3 and 5. The sampled rows were mostly not geopolitical incidents: a court case, a wildlife-photo contest coded as small-arms fire, and one article producing several rows pinned on a capital. Slice 5 is replaced by an explicit "Find recent reports" action (section 13, Slice 5 replacement). Access facts kept for that work: the `http://` links listed for the bulk files can be rewritten to `https://`, and `lastupdate.txt` carries each file's size and md5. The rules below are kept as the record of what was proposed.

A record **creates an Event** only if all of the following hold:

1. `QuadClass` = 4, and either `EventRootCode` is 19 (fight) or 20 (unconventional mass violence), or `EventCode` is in the root-18 subset: 183, 1831, 1832 and 1833 (non-military, suicide, car and roadside bombing), 185 (attempt to assassinate) and 186 (assassinate). Ordinary assault codes (180, 181, 182, 1821, 1822, 1823 and 184) are excluded. Codes verified against the GDELT CAMEO event code lookup, <https://www.gdeltproject.org/data/lookups/CAMEO.eventcodes.txt>, on 2026-09-30.
2. `IsRootEvent` = 1.
3. `ActionGeo_Type` is 3 or 4 (city-level), and the coordinates are valid and not (0, 0).
4. `NumSources` ≥ 3 and `NumArticles` ≥ 5. Because these count only the first 15-minute window, this is a deliberately high bar.
5. `SQLDATE` is within 2 days of `DATEADDED`.
6. `SOURCEURL` is an absolute http or https URL of at most 1024 characters.
7. No existing Event matches under the deduplication rules (section 7).

**Exclusions:** records with both Actor1 and Actor2 empty, and records whose `ActionGeo_FeatureID` equals a country or state centroid.

**Per-refresh cap:** at most 25 new GDELT Events. Beyond that, records are ranked by `NumSources` and the rest are dropped and counted.

Records below the bar are **not stored**. Once deduplication exists (slice 6), a below-bar record that matches an existing Event may be attached to it as a SourceItem, with at most 3 GDELT SourceItems per Event. The record itself never becomes an Event.

Mapping:

- Id `evt-gdelt-{GlobalEventID}`.
- Title from `EventCode` description plus `ActionGeo_FullName`.
- Category: root 19 → MilitaryConflict; root 20 and the root-18 subset → Security.
- Status Unverified.
- The SourceItem has SourceType NewsArticle, ExternalId `GDELT:EVT:{GlobalEventID}`, and the URL set to `SOURCEURL`, displayed as text.

### 5.3 Marking automatically created Events

- Status is Unverified.
- Add a `Provider` field on Event: `Gdacs`, `UcdpCandidate`, `Gdelt`, `Demo` or `Manual`.
- The row shows two text badges, "Unverified" and a provider label ("GDACS", "UCDP Candidate", "GDELT · machine-coded"), because status is currently invisible as text in rows.
- Details repeats the provider-specific caveat.
- UCDP-created and GDELT-created Events are never labelled verified, confirmed or official.

---

## 6. Identity and idempotency

### 6.1 Keys and updates

Add `Provider` and a nullable `ProviderKey` to `EventEntity`, with a unique index on (`Provider`, `ProviderKey`). Keep the deterministic ID prefixes as a second guard.

| Provider | ProviderKey | Revision used for stale checks |
| :- | :- | :- |
| GDACS | EQ event id | GDACS `ProviderUpdatedAtUtc` (unchanged) |
| UCDP Candidate | `id` | Release ordinal derived from the version (for example 26.0.8 → 2026-08). A later release may update an earlier one; the same or an older release is ignored. |
| GDELT | GlobalEventID | `DATEADDED`. GDELT events are effectively immutable, so a re-seen ID is left unchanged. |

For each new provider, follow the GDACS store pattern exactly:

- One transaction per refresh.
- Newer revision → update provider-owned fields and recompute severity.
- Older revision → unchanged.
- Equal revision with different data → Conflict.
- An existing Event whose ID has the right shape but lacks a matching provider-owned SourceItem, or has a different `Provider`, counts as an **ownership collision** and is never overwritten.

### 6.2 Disappearance from feeds

Never delete. A missing record never resolves or deletes an Event. That keeps the current GDACS rule and protects Watchlist memberships and Alert history, which would otherwise cascade-delete.

UCDP records absent from a later release that covers their dates are marked in Details as "not present in the latest UCDP Candidate release". They are not deleted and their status is not changed. This needs a nullable `ProviderLastSeenRelease` field.

---

## 7. Cross-provider deduplication

The approach is rule-based and deterministic. GDACS is excluded, because natural disasters do not overlap with UCDP or GDELT conflict categories.

**Match rule (PROPOSED).** Two conflict Events match when:

- Their categories match or are both MilitaryConflict or Security.
- Their date ranges overlap within ±1 day. UCDP uses `date_start`–`date_end` and requires `date_prec` ≤ 2.
- Their distance is ≤ 25 km when both are city-level or better (UCDP `where_prec` ≤ 2; GDELT `ActionGeo_Type` 3 or 4), and ≤ 50 km when one is admin-2 level (`where_prec` 3). Anything coarser never matches automatically.
- Exactly one candidate matches. Two or more candidates means no action, and the case is counted as ambiguous.

| Risk | Effect | Mitigation |
| :- | :- | :- |
| False merge (two real incidents in the same city on the same day) | One incident disappears from view, and Alerts go to the wrong Event | Link rather than merge, and do nothing when ambiguous |
| False split (same incident, coordinates more than 25 km apart) | Duplicate markers | Accepted; visible duplicates are safer than silent merges |

**GDELT-created Event, then a matching UCDP record (recommendation: attach and supersede, never overwrite):**

- UCDP always creates or updates its own Event, because the curated identity wins.
- The matching GDELT SourceItem is also associated with the UCDP Event. The many-to-many `EventSourceAssociation` already allows this.
- The GDELT Event gets `SupersededByEventId` and is hidden from default lists and the map, but stays reachable from the UCDP Event's Details ("Earlier machine-coded report"). This is reversible.
- Field ownership stays with each Event's own provider; no provider writes another provider's fields.
- **Alerts:** Watchlist memberships of the superseded Event are **not** moved automatically (open question Q8). Details shows "This Event superseded a watched Event", and no Alert fires for supersession itself.

**A GDELT record matching an existing UCDP or GDELT Event:** attach it as a SourceItem, within the per-Event cap, and never create a new Event.

**Model-assisted matching** (text similarity, embeddings) is out of scope. It could later be added as an opt-in layer that only suggests links, never merges.

---

## 8. Alerts

**Recommendation: keep the Slice 1 semantics and extend them minimally.**

1. `GdacsEventMateriallyUpdated` fires on timer refreshes as well as manual ones. The code already does this once there is a second caller; the handoff wording changes.
2. Add `UcdpEventMateriallyUpdated`, using the same material-field rule with the newer-release revision.
3. Add `SeverityIncreased`, for watchlisted Events whose recomputed severity rises from any curated provider (GDACS or UCDP). It is keyed on (rule, Event, provider revision) through the existing unique index (`GeopoliticalMonitorDbContext.cs:146`).
4. **GDELT never creates Alerts** in this design; the data is too noisy.
5. Still no Alerts for new Events, supersession, source-only updates, membership changes, or disabled rules. Alerts for new Events in a watched area would need dynamic Watchlist criteria, which are out of scope.

**Atomicity:** Alert occurrences are written in the same transaction as the Event update (unchanged pattern, `SqliteEventStore.cs`).

**Partial failure:** any exception rolls back the whole refresh batch, so no Events and no Alerts are written. Because the stored revision did not advance, the next successful refresh sees the same update as newer and creates the Alert then, exactly once, guaranteed by the unique index. A refresh cancelled on app close behaves the same way.

**UI:** after any refresh that created Alerts, the status push (section 3.4) includes `newAlerts`, and the UI refreshes the Alert summary as it does today (`App.tsx`).

---

## 9. Credentials

- **The UCDP CSV route needs no credential** (to be confirmed on first request). It is the recommended route.
- **If the CSV route proves insufficient** (for example the files require a login), use the API with a token read only from the process environment variable `GEOPOLITICALMONITOR_UCDP_ACCESS_TOKEN`, following the FIRMS pattern (`MainWindow.xaml.cs:111`). It is sent only as the `x-ucdp-access-token` header to the fixed host `ucdpapi.pcr.uu.se`. If it is absent, the provider is `Unconfigured` and makes zero requests.
- The token is never stored in SQLite, preferences, Events or SourceItems, never logged, never included in bridge messages, and never echoed in errors. Status messages say only "UCDP is not configured."
- The API quota (5,000 requests per day, errors included) is far above what a 6-hour check needs.
- GDELT and GDACS need no credentials.

---

## 10. Demo data plan

1. Stop calling `DemoDataSeeder` in normal startup (`MainWindow.xaml.cs`). Keep the class, because the three tests that run it (section 2.1) still use it.
2. Delete existing demo rows once, in one transaction: Events with ID prefix `evt-demo-` and SourceItems with `src-demo-`.
   - Cascades remove their associations, Watchlist memberships and Alert occurrences.
   - Removing demo Events therefore also deletes their Watchlist memberships and Alert history. A Watchlist that held only demo Events becomes empty but is not deleted.
   - The UI tells the user this once: "Demo Events were removed."
   - A preference flag records that cleanup ran, so it never repeats.
3. Leave demo-style IDs in unit-test fixtures as they are; they are plain strings and do not depend on the seeder.
4. Add an empty state: "No Events yet. Automatic refresh is running." with provider statuses.
5. `mock/dashboardData.ts` (`mockXPosts`, `initialMapLayers`) is a separate cleanup and is not part of this plan.

**As built (`1eaee7e`):** the plan above is implemented as written, with these details. The cleanup (`DemoDataCleanup`) deletes the rows and writes the native-only `DemoDataCleanupCompleted` preference in the same transaction; the prefix match is case-sensitive; a failure commits nothing and the next launch retries. The notice reaches the UI through the typed `app.info` field `demoEventsRemoved`, true only on the launch that removed at least one demo Event, and is dismissible. The empty state "No Events yet. Automatic refresh is running." appears only for the all-inclusive criteria (`ALL`, no search, all severities, All Events); other criteria keep the filter-empty messages. `DemoDataCleanupTests` also run the seeder directly.

**Timing:** after UCDP is live (see open question Q2), so the public view has both disaster and conflict data rather than earthquakes alone.

---

## 11. Volume and performance

| Provider | Expected records per refresh | Caps (PROPOSED) | New Events per refresh |
| :- | :- | :- | :- |
| GDACS | ≤ 100 (existing) | 1 MiB, 15 s (existing) | ≤ 100 |
| UCDP Candidate | One monthly file. Size and row count unconfirmed; the release covers all conflicts globally for one month. | 32 MiB response, 60 s timeout, 20,000 rows parsed | ≤ 5,000 on first import, then per release |
| GDELT | One 15-minute export file (about 64 KB compressed in the sample). Rows per file unconfirmed. | 5 MiB compressed, 32 MiB uncompressed, exactly one zip entry with a name matching `^\d{14}\.export\.CSV$`, 50,000 rows parsed, 20 s timeout | ≤ 25 |

**Database growth:** roughly 3 KB per Event with one SourceItem, so 20,000 Events is about 60 MB. That is acceptable for local SQLite.

**Known pressure point:** `GetEventsAsync` loads every Event (`SqliteEventStore.cs:15-23`), and the frontend renders every marker. Native windowing of the Event list is handled by Slice 2B, which is required before Slice 3 because the first UCDP import can create up to 5,000 Events. (**As built:** Slice 2B is implemented. The Event list is a native window capped at 2,000 Events, with the selected Event kept when it matches, so the list and the markers render at most 2,000 Events.)

**Marker density:** no clustering now. **Revisit trigger:** more than 2,000 markers visible in the default view, or more than 20,000 Events in the table.

---

## 12. Security and privacy review

**New attack surface**

- Two new outbound fixed hosts, `ucdp.uu.se` and `data.gdeltproject.org`, plus `ucdpapi.pcr.uu.se` only if the API route is used. Each gets its own `HttpClient` with a fixed base, `https` only, a host allowlist check before every request, a timeout, a streamed read with a byte cap, cancellation, no redirects to other hosts, and no retries.
- GDELT zip handling: `System.IO.Compression`, exactly one entry, no path segments in the entry name, and a streamed decompression cap (zip-bomb guard).
- CSV parsing with row and field-length limits. A malformed row is skipped and counted, never thrown.
- There is still no inbound port and no generic bridge operation.

**Untrusted text** (titles, place names, actor names, GDELT URLs)

- Before persistence: normalize to NFC, strip control characters (except newline in summaries), collapse whitespace, and truncate to the database limits (Title 256, Summary 2000, Text 4000, Url 1024).
- Display is React text only; there is currently no `dangerouslySetInnerHTML`, and it stays that way.

**URLs**

- Store only absolute `http` or `https` URLs, and display them as text (`EventDetailsPanel.tsx` pattern).
- If clickable links are wanted later, add one typed operation `links.openExternal` that accepts only an Event-attached SourceItem ID. The native side looks the URL up and opens it in the system browser; it never accepts a raw URL from JavaScript.
- Separate finding to verify: the ReliefWeb `<a target="_blank">` (`ReliefWebDiscovery.tsx`), given that the app WebView has no `NewWindowRequested` handler.

**Licensing and attribution UI**

- UCDP Events: the citation and the DEMSCORE acknowledgement in Details and in a "Data sources" section in Settings.
- GDELT Events: "Source: The GDELT Project" plus the link `https://www.gdeltproject.org/`, shown as text or opened through the typed operation above.
- Add both datasets to `THIRD_PARTY_NOTICES.md` in the slices that introduce them.

**Privacy:** no user data is sent to providers. Requests contain only dates and fixed paths. The app User-Agent (product name and version) contains no user identifiers.

---

## 13. Implementation slices

The suggested order is kept, with demo removal moved after UCDP and Slice 2B (Event list windowing) inserted before UCDP. Other slice numbers are unchanged. Each slice is independently testable and can be accepted manually.

### Slice 1: Severity field and display

- **Scope:**
  - Add the `EventSeverity` enum, plus `Severity`, `SeverityRule` and `SeverityInputs` fields on Event, with a migration.
  - Store `Provider` and `ProviderKey`.
  - Add a GDACS severity rule and backfill: parse the app-authored provenance lines "GDACS alert level:" and "Magnitude:" from existing GDACS SourceItems, falling back to Low with rule `legacy-default`.
  - Assign demo severities.
  - Add DTO and bridge fields with strict web-side validation.
  - UI: severity badge, Unverified badge and provider label on rows; a Severity block in Details; a minimum-severity filter; severity-first sort; status dots moved off severity colors; map radius by severity; a `--catastrophic` token.
- **Non-goals:** timers, new providers, Alerts.
- **Tests:** domain enum and rule mapping at the boundary values in 4.2; migration up on a copy of an existing database; backfill parsing; bridge schema that rejects unknown severity values; filter and sort unit tests; styles test that severity tokens do not follow the accent.
- **As built (later change, owner decision 2026-10-02):** the severity-first list sort was replaced by newest first (last update descending, then id ascending, the native window order). The severity badge and the minimum-severity filter are unchanged.
- **Manual acceptance:** launch on an existing database; GDACS Events show a sensible level with its explanation; demo Events show their levels; the filter works together with search and time range; switching the accent does not change severity colors; the list shows "Unverified" as text.

### Slice 2: Shared refresh scheduler, with GDACS on the timer

- **Scope:**
  - `ProviderRefreshScheduler` (Application) and its composition in `MainWindow`.
  - `providers.refresh.status` push and a `providers.refresh.status.get` request.
  - Provider status strip.
  - GDACS every 30 min, starting 60 s after `app.ready`.
  - Backoff and stale rules.
  - Alerts evaluated on timer refreshes.
  - Fix the GDACS cancelled-token gate.
- **Non-goals:** new providers, demo removal.
- **Tests:** a fake `TimeProvider` drives ticks; single in-flight across manual and timer; a skipped tick is not queued; backoff sequence and reset; cancellation mid-persist leaves the database unchanged; shutdown with an in-flight refresh raises no unhandled exception; the push schema rejects unexpected fields.
- **Manual acceptance:** launch, and GDACS refreshes about a minute later; the status strip shows the times; manual refresh during a timer refresh shows "already in progress"; disconnect the network and see the failed state with backoff; close the window mid-refresh and confirm in Task Manager that nothing remains running.

### Slice 2B: Event list windowing

Required before Slice 3. The first UCDP import can create up to 5,000 Events, but `GetEventsAsync` loads every Event (`SqliteEventStore.cs:15-23`) and the UI starts on the ALL time range.

**As built (`ee5d89d`):**

- The request text field is named `search`, not `query` (at most 200 characters). The request also carries `minimumSeverity`, `keepEventId` and a `requestId`. Unknown fields and values are still rejected.
- Ordering is newest by `LastUpdatedAtUtc`, then by Id, not newest `StartedAtUtc`. The time range still compares `StartedAtUtc`.
- `keepEventId`: the selected Event stays in the window when it matches the criteria. At the cap it replaces the oldest returned Event, so the window never exceeds 2,000.
- The native query reproduces the frontend filter semantics through the shared rule class `EventWindowRules` (`EventWindow.cs`). Search also matches severity. A shared parity fixture (`tests/fixtures/event-window-parity.json`) is checked by both the native tests and the frontend reference filter (`eventFilters.ts`).
- The frontend renders the returned window directly. It debounces typing and discards responses that are not for its latest `requestId`. The notice reads "Showing the newest 2,000 of N matching Events. Narrow the time range or search to see others." (**As built:** the app shows "Newest 2,000 of N shown."; the full sentence "Newest 2,000 of N shown. Narrow range or search for others." is in its hover text and accessible name.)

- **Scope (PROPOSED):**
  - `events.list` gains typed, validated request fields:
    - `range`: one of `24H`, `7D`, `30D`, `90D` or `ALL`.
    - `query`: an optional string of at most 200 characters.
    - `watchlistId`: optional.
    - Unknown fields and values are rejected, as today. This is a fixed Event query, not a generic query capability.
  - The native store applies scope, time range and search in SQL, with the **same semantics the frontend uses today** (`eventFilters.ts`):
    - The time range compares `StartedAtUtc` against now minus the range.
    - Search is a case-insensitive substring match on title, summary, category (raw and display form), status, location display name and region.
    - Search and time range combine with AND, within the selected scope (All Events or one Watchlist).
  - The native side returns at most **2,000** matching Events (PROPOSED cap), newest `StartedAtUtc` first with ties broken by Id, plus the total match count. Source counts are computed only for the returned Events.
  - The frontend keeps a single visible collection that drives both the list and the map markers. It requests a new window whenever range, search or scope changes (debounced for typing) and after every refresh. It still runs its existing client-side filter over the returned window, so behavior stays identical when fewer than 2,000 Events match.
  - Selection clears when the selected Event is not in the returned window, as it already does when an Event is filtered out. Clearing filters does not reselect it.
  - **ALL keeps its meaning** ("no time limit"), but it is bounded by the cap. It is no longer the startup default: since `29c3677` the Events filters start at `30D` and `High and above`. When more Events match than are returned, the panel shows "Newest 2,000 of N shown."; the full sentence "Newest 2,000 of N shown. Narrow range or search for others." is in its hover text and accessible name.
  - Older Events are never hidden from search. A search runs natively over all Events in scope, so any older Event can be found by searching or by choosing a narrower scope.
  - The selected Event's Details still load by ID through `events.get`, unaffected by the window.
- **Non-goals:** infinite scrolling or page-by-page browsing; map clustering; changes to Watchlist limits; fuzzy or ranked search.
- **Tests:**
  - Store tests showing that each range, search, scope and AND combination returns the same Events the current frontend filter would, over a seeded set larger than the cap.
  - Cap and total count, including a tie on `StartedAtUtc`.
  - Bridge schema tests that reject an unknown range, an over-long query, unknown fields and a malformed Watchlist id.
  - Frontend tests confirming list and map use the same collection, selection clears when the selected Event leaves the window, and the "Showing the newest …" notice appears only when the total exceeds the cap.
  - A performance test that listing with 20,000 Events in the database stays under an agreed time budget.
- **Manual acceptance:**
  - Launch; ALL is selected and the list and map match.
  - Seed more than 2,000 test Events in a scratch database; the notice shows the correct total.
  - Search for an old Event's title and find it.
  - Switch to 24H, and the list and map shrink together.
  - Select an Event, then pick a range that excludes it; the selection clears and does not come back when the range is cleared.
  - Choose a Watchlist scope, and search and range still apply within it.
- **As built (layout fix, 2026-10-02):** with about 2,000 Events the Events panel grew to the full list height and stretched the Map, because the application grid had only a minimum height and its `1fr` row sized to content. The grid now has a definite height, the Events panel is a bounded flex column whose list scrolls, and the list renders 100 rows at a time as the user scrolls (the selected row is always rendered and revealed). The 2,000-Event cap, its notice, the native query and the shared list/map collection are unchanged.

### Slice 3: UCDP Candidate provider

- **Prerequisite:** Slice 2B (Event list windowing).
- **Scope:**
  - Release discovery: try the expected monthly file name, then the previous month.
  - CSV client with caps, parser, normalization per 5.1.
  - Store method following the GDACS pattern, severity per 4.2, and `ProviderLastSeenRelease`.
  - Citation UI and a `THIRD_PARTY_NOTICES.md` entry.
- **Non-goals:** the API route unless the CSV route fails; deduplication.
- **Tests:** a parser fixture with representative rows (including `Check` statuses, `where_prec` 5–7 and `best` = 0); idempotent re-import of the same release; a later release updates the Event; an earlier release is ignored; ownership collision; size and row caps; release-name discovery.
- **Manual acceptance:** UCDP Events appear after a few minutes, marked Unverified and "UCDP Candidate"; Details shows fatalities, code status, citation and severity explanation; the status strip labels UCDP as "monthly release"; a restart re-imports nothing.

**As built (Slice 3, pending owner acceptance):**

- Verified file facts (owner probes, 2026-10-02): `https://ucdp.uu.se/downloads/candidateged/GEDEvent_v26_0_8.csv` returns 200 over HTTPS with no token, login or redirect (`text/csv`, about 1.4 MB); `GEDEvent_v26_0_9.csv` returns 404 until published. Monthly names are `GEDEvent_v{YY}_0_{M}.csv` with no zero padding on the month; the half-year release uses another pattern and is not used. The header has 49 columns, starting `id,relid,year,active_year,code_status,type_of_violence` and ending `best,high,low,gwnoa,gwnob`. A release appears about 20 days after its month ends, so the newest events are 20 to 50 days old.
- D1: `LastUpdatedAtUtc` for a UCDP Event is the end of its `date_end` day (UTC), not the release timestamp, so a release does not make thousands of Events tie as newest in the 2,000-Event window. The release ordinal (year times 100 plus month) is stored in the nullable `ProviderLastSeenRelease` (migration `AddProviderLastSeenRelease`) and is the UCDP revision: a greater ordinal updates provider-owned fields (title, summary, category, start, last update, location, severity, release); an equal or lower ordinal is ignored, with no equal-release Conflict. This runs in a separate UCDP store method; the GDACS method and its stale, Conflict and ownership rules are unchanged.
- D2: CSV route only. No UCDP API route and no credential code.
- D3: `source_headline`, `source_original`, `source_article`, `source_office`, `source_date` and `geom_wkt` are never read or stored.
- D4: a typed fieldless `providers.ucdp.refresh` request with a typed result, routed through the scheduler's manual tracking to the same `UcdpRefreshService` instance, so the gate allows one in-flight check.
- Release discovery: the first import probes up to four names counting down from the current month, then also imports the release before the newest found. Later checks probe only the next release after the newest imported one (maximum stored `ProviderLastSeenRelease`, plus an in-memory session value so a zero-row release is not downloaded again). Addition: if that next release is missing although it is two or more months old, the four-name probe runs again so the provider cannot stall behind a removed monthly file. All releases are fetched before anything is persisted; one transaction per release, oldest first.
- The status push for UCDP adds the optional fields `cadenceLabel` (`monthly release`) and `dataThrough` (`yyyy-MM`). `No new release` is a success. UCDP is stale when the newest imported release was extracted (the 20th of the following month) more than 45 days ago, or by the shared rule of no success for three intervals.
- The parser rejects more than 20,000 rows as an invalid response rather than importing part of a release. Rows longer than 65,536 characters are skipped. Location country code stays empty, because UCDP `country_id` is a Gleditsch-Ward number.
- Deferred: the Details note "not present in the latest UCDP Candidate release" (section 6.2).

### Slice 4: Demo data removal

- **Scope:** stop seeding; one-time cleanup with notice; empty state.
- **Non-goals:** changes to the frontend mock data.
- **Tests:** the cleanup removes only `evt-demo-` and `src-demo-` rows and runs once; seeder tests still pass because they call the seeder directly.
- **Manual acceptance:** after launch no demo Events remain; the empty state shows before the first refresh completes; restarting does not bring the demo Events back.

### Slice 5: GDELT provider under strict rules — BLOCKED

> **Blocked (2026-10-02):** see the evidence in section 5.2. Replaced by the next slice; the plan below is kept as a record and is not to be implemented.

- **Depends on:** Slice 2B (Event list windowing), which is already required before Slice 3.
- **Scope:**
  - `lastupdate.txt` client that rewrites to https and validates host and path.
  - Zip and CSV parser with caps.
  - Creation rules per 5.2 with the per-refresh cap.
  - Severity capped at High.
  - Provider label "GDELT · machine-coded", attribution, and a notices entry.
  - A per-provider enable toggle stored as a preference (see Q3 for the default).
- **Non-goals:** attach-only association, deduplication, Alerts.
- **Tests:** fixture rows at every rule boundary; zip-bomb and wrong-entry rejection; http-to-https rewrite and host rejection; an unchanged `lastupdate.txt` means no download; the cap of 25.
- **Manual acceptance:** a small number of GDELT Events appear, clearly labelled; turning the toggle off stops requests (verify in the status strip); markers stay readable.

### Slice 5 (replacement): Find recent reports

> **Parked (2026-10-02).** On 2026-10-02 the owner's requests to the GDELT DOC API at `api.gdeltproject.org` all returned HTTP 429 with a message asking for at most one request every 5 seconds, including requests 30 seconds apart and after a waiting period, from the owner's network. The documented limit therefore does not match what was enforced. A parse of other responses showed that errors can also arrive as HTTP 200 `text/html`. Revisit only if that behavior changes. No decision or later slice changes.

- **Status:** not implemented.
- **Scope:** an explicit user action on a selected Event. The native host runs one GDELT DOC 2.0 search (`api.gdeltproject.org`, https only, free, no key, about a three-month window, at most one request per five seconds, up to 250 articles). Errors may arrive as HTTP 200 plain text and must be recognised as errors. Results are transient candidates (title, URL, domain, date, language) shown to the user; an explicit Attach creates a SourceItem and its association with the Event. Nothing is created automatically. Citation to the GDELT Project is required.
- **Pattern:** the same as the ReliefWeb search-and-attach flow. `SourceItem` != `Event` stays: a GDELT article never becomes an Event.
- **Non-goals:** Event creation, background or timer requests, Alerts.

### Slice 6: Cross-provider deduplication

> **Reassess (2026-10-02):** with GDELT creation blocked, deduplication only matters if a second conflict source is added.

- **Scope:** match rule per section 7; `SupersededByEventId`; attach below-bar GDELT records to matched Events (at most 3 per Event); a Details section listing linked and superseded Events; ambiguous-match counts in the refresh result.
- **Non-goals:** model-assisted matching and automatic Watchlist migration.
- **Tests:** distance, time and precision boundaries; ambiguous cases do nothing; supersede is reversible; no field of one provider is written by another.
- **Manual acceptance:** a GDELT Event later covered by UCDP disappears from the default list and is reachable from the UCDP Event.

### Slice 7: Alert integration

- **Scope:** `UcdpEventMateriallyUpdated` and `SeverityIncreased` triggers; rule UI to choose triggers per Watchlist; no GDELT Alerts.
- **Non-goals:** Alerts for new Events, and Windows notifications.
- **Tests:** atomic Event plus Alert commit; rollback leaves no Alert and the next refresh creates it exactly once; disabled rule; Event outside the Watchlist.
- **Manual acceptance:** watch a UCDP Event; import a newer release with changed fatalities; one unread Alert appears, including after a restart mid-refresh.

---

## 14. Owner decisions (2026-09-30)

| # | Question | Recommended answer | Decision |
| :- | :- | :- | :- |
| Q1 | Approve the severity thresholds in 4.2, including the Catastrophic bar? | Approve as written, and revisit after two weeks of real data. | Approved as written; revisit after about two weeks of real data. |
| Q2 | Remove demo data before or after UCDP is live? | After UCDP (slice order above). | After UCDP is live. |
| Q3 | GDELT automatic refresh on by default? | Off by default, with a clear toggle, until the thresholds are tuned on real data. | Off by default; the owner switches it on after the thresholds are tuned. **Moot (2026-10-02):** GDELT creation is blocked (section 5.2). |
| Q4 | Refresh intervals in 3.2? | Approve: GDACS 30 min, UCDP 6 h check, GDELT 15 min. | Approved. |
| Q5 | Skip UCDP records with `best` = 0? | Yes, for volume; revisit if coverage looks thin. | Yes, skip `best` = 0. |
| Q6 | Category for UCDP one-sided violence (type 3)? | Security. MilitaryConflict is kept for state-based and non-state fighting. | Security. |
| Q7 | GDELT creation bar (`NumSources` ≥ 3, `NumArticles` ≥ 5, roots 18–20, city-level)? | Approve as the starting bar. | Approved with a revision: root 18 is limited to the bombing and assassination subset (section 5.2 rule 1). **Moot (2026-10-02):** GDELT creation is blocked (section 5.2). |
| Q8 | When a GDELT Event is superseded, move its Watchlist memberships to the UCDP Event? | No automatic move; show a notice in Details and let the user decide. | No automatic move. |
| Q9 | Should severity changes from GDACS or UCDP create Alerts for watched Events? | Yes (`SeverityIncreased`, slice 7). | Yes. |
| Q10 | Clickable source links through a typed `links.openExternal`? | Defer. Keep text-only now, and review the ReliefWeb `target="_blank"` link separately. | Deferred. |
| Q11 | Approve a new `--catastrophic` color token? | Yes; a dark magenta, pending a visual check on the dark theme. | Yes; dark magenta, pending a visual check. |
| Q12 | Use the UCDP API route if CSV downloads need a login? | Only if needed, with a token from an environment variable, as with FIRMS. | Only if the CSV route fails. |

---

## 15. Risks and things deliberately not designed

**Risks**

- UCDP release file names are not fully predictable (the half-year release uses a different pattern), so release discovery may need a fallback. Rejected alternatives: parsing the downloads page HTML is brittle, and switching to the API needs a token.
- GDELT's first-window `NumSources` bar may be too strict (few Events) or too loose in busy news cycles. Tuning is expected.
- Machine-coded GDELT geolocation can be wrong: city centroids, and places named in the story rather than where the incident happened. The Unverified label and the High cap limit the damage but do not remove it.
- Rule-based deduplication will produce both false splits and occasional false links.
- The event list and map currently scale linearly with the number of Events. Slice 2B bounds what the list and map receive but does not change how the map renders dense areas.
- Timer refreshes make the UI change without user action. The status strip and unchanged selection behavior must keep that calm.
- Accuracy of provider data is outside the app's control. It must never be presented as verified.

**Deliberately not designed**

- Any background or app-closed execution.
- A candidate or review queue.
- ACLED.
- ReliefWeb Event creation.
- Model-assisted matching.
- Map clustering.
- Dynamic Watchlist criteria and new-Event Alerts.
- Windows notifications.
- Retention or pruning of old Events.
- Backfilling GDELT cycles missed while the app was closed.
- Changes to the X pane.
