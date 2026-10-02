# Data Model Notes

The fundamental rule is:

```text
SourceItem != Event
```

An `Event` is one normalized real-world geopolitical occurrence. A `SourceItem` is one individual source or evidence item, such as an X post, article, official statement, structured dataset record, or other source.

Multiple SourceItems may support one Event, and one SourceItem may support multiple Events. `EventSourceAssociation` is the explicit many-to-many relationship, unique by `EventId` + `SourceItemId`. Multiple reports of the same occurrence must not automatically become multiple Events.

`Location` is embedded in Event and optional SourceItem persistence as latitude, longitude, display name, optional country code, and optional region.

## Current Values

Event categories:

- `MilitaryConflict`
- `Security`
- `PoliticalDiplomatic`
- `CivilUnrest`
- `Infrastructure`
- `Humanitarian`
- `NaturalDisaster`
- `Economic`
- `Other`

Event statuses:

- `Developing`
- `Active`
- `Resolved`
- `Unverified`

For provider-ingested Events, `Unverified` means that Geopolitical Monitor has not independently verified the occurrence. Provider alert levels, magnitude, and severity metadata do not determine Event lifecycle status.

Source types:

- `XPost`
- `NewsArticle`
- `OfficialStatement`
- `DatasetRecord`
- `Other`

## Persistence

The current SQLite schema contains:

- `ApplicationPreferences`
- `Events`
- `SourceItems`
- `EventSourceAssociations`
- `Watchlists`
- `WatchlistEvents`
- `AlertRules`
- `AlertOccurrences`

NASA FIRMS observations deliberately have no Domain entity, EF entity, table, migration, SourceItem, or EventSourceAssociation. A successful refresh returns a complete bounded rolling-24-hour Application snapshot that lives only in React memory until replaced or the application exits; the MapLibre source receives a locally derived Significant, Strong only, or All detections subset. The snapshot, display filter, and layer visibility are session-only. This preserves the distinction between normalized Events, evidence SourceItems, transient/live map observations, and bundled static reference geometry.

Persisted application preference authorities are:

- `AccentTheme`: `Orange`, `Blue`, `Red`, `Green`, or `Yellow`.
- `XPaneWidth`: the validated preferred width of the isolated X pane.
- `XPaneSide`: `Left` or `Right`.

Runtime data is not limited to the deterministic demo seed:

- Startup idempotently ensures 6 fictional demo Events, 14 demo SourceItems, and 14 EventSourceAssociations. Existing provider- and user-created records coexist with this seed.
- Manual X attachment validates a public X status URL, derives its publication timestamp locally from the numeric status Snowflake, and creates or reuses a URL-only `XPost` SourceItem. The X WebView is user-operated and is not scraped; attachment does not obtain post text, author, media, engagement, cookies, or tokens.
- GDACS refresh imports earthquake `EQ` point records only. It creates or updates provider-owned `NaturalDisaster` / `Unverified` Events and linked `DatasetRecord` SourceItems. GDACS refreshes on an in-process timer only while the app is open (every 30 minutes, first 60 seconds after the UI is ready) and on the manual `Refresh GDACS` action; both share one in-flight refresh. Nothing runs after the app closes, and scheduler state is not persisted.
- UCDP Candidate import creates or updates provider-owned `Unverified` Events (`MilitaryConflict` for state-based and non-state violence, `Security` for one-sided violence) with `Provider = UcdpCandidate`, `ProviderKey` = the UCDP event id, Event id `evt-ucdp-cand-{id}`, and one linked `DatasetRecord` SourceItem `src-ucdp-cand-{id}` / `UCDP:CAND:{id}`. `LastUpdatedAtUtc` is the end of the event's last day. The nullable `ProviderLastSeenRelease` (year times 100 plus month, for example 202608) records the newest release that contained the Event and is the UCDP revision: a newer release updates provider-owned fields, an equal or older one is ignored, and records absent from a release are never changed or deleted. UCDP imports never create Alerts. It refreshes on the same in-process timer only while the app is open (a release check every 6 hours, first 2 minutes after the UI is ready) and on the manual `Refresh UCDP` action.
- ReliefWeb searches reports for an already selected Event only after an explicit user action. Candidates remain transient until the user explicitly attaches a report. Attachment creates or reuses one `Other` SourceItem identified by `src-reliefweb-report-{reportId}` and `ReliefWeb:Report:{reportId}`, then creates the EventSourceAssociation. Reattaching the same report to the same Event is idempotent; the same SourceItem may support another Event.

ReliefWeb SourceItems retain bounded metadata, contributing-organization attribution, original publication time, and the canonical report URL. They do not store report bodies, HTML, PDFs, files, images, attachments, or arbitrary provider payloads. Existing first-slice ReliefWeb metadata is not automatically synchronized.

Production ReliefWeb remains intentionally unconfigured until an owner-approved V2 `appname` is supplied through native client composition. Missing configuration produces no ReliefWeb network request. ReliefWeb does not create Events, match reports automatically, attach automatically, poll, or run at startup.

`Watchlist` is a locally persisted, user-named collection of references to existing Event IDs. IDs use the opaque `wl-` plus 32-lowercase-hex format. Names are Form-C normalized, whitespace-normalized, limited to 80 UTF-16 code units, and unique by an uppercase-invariant comparison key. Slice 1 permits at most 100 Watchlists and 2000 Events per Watchlist.

`WatchlistEvents` is the explicit many-to-many membership relation, unique by `WatchlistId` + `EventId`; it copies no Event fields. A future Event deletion removes dangling memberships, while Events, SourceItems, and EventSourceAssociations are never deleted by Watchlist deletion. Startup creates no seeded Watchlists, so the deterministic 6 / 14 / 14 demo seed remains unchanged.

An `AlertRule` belongs to exactly one Watchlist and has the single supported trigger `GdacsEventMateriallyUpdated`. IDs use `ar-` plus 32 lowercase hexadecimal characters. A rule is enabled or disabled, retains its creation time, and records the most recent disabled-to-enabled transition. Enabling does not evaluate or backfill existing Event state. Rules do not contain dynamic criteria, thresholds, expressions, provider configuration, or new-Event matching.

An `AlertOccurrence` belongs to one AlertRule and one existing Event. IDs use `al-` plus 32 lowercase hexadecimal characters. It records the authoritative GDACS provider revision and the local trigger time, and has only unread/read state through nullable `ReadAtUtc`. Persistent history is ordered newest first and is deduplicated by `AlertRuleId` + `EventId` + `ProviderRevisionAtUtc`.

Alert creation is part of the existing GDACS SQLite transaction. A strictly newer provider revision creates one occurrence per enabled rule only when the watched Event is already a member of that rule's Watchlist and at least one existing material Event field changes: title, summary, start time, or location latitude, longitude, display name, country code, or region. New Events, source-only/new-episode changes, revision-only changes, stale or repeated data, equal-timestamp conflicts, manual X attachment, ReliefWeb attachment, and membership changes do not create Alerts.

Deleting a Watchlist cascades its memberships, AlertRule, and Alert history while preserving Events, SourceItems, and EventSourceAssociations. Deleting an Event cascades occurrences for that Event; deleting a rule cascades its occurrences. Disabling a rule or removing membership preserves existing history. Watchlists and Alerts have no account ownership, cloud state, or background execution.
