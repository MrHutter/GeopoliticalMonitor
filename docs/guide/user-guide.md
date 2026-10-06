# Geopolitical Monitor user guide

Geopolitical Monitor shows geopolitical Events from public data sources in a list and on a world map, with the normal X website in a pane beside them. This guide describes what you see and how to use it. Building and configuration are in [setup and configuration](setup-and-configuration.md); the sources themselves are described in [data sources](data-sources.md).

## Screen layout

- **Top bar:** the app name with `LOCAL DESKTOP`, the search box (`Search events, locations, keywords...`), the time range buttons `24H`, `7D`, `30D`, `90D` and `ALL`, the current UTC time, `Alerts` with the unread count, and the Settings button (gear).
- **Events and Map:** the Events panel and the map sit side by side. With the X pane on the right the order is Events, Map, X; with the X pane on the left it is X, Map, Events.
- **X pane:** the X website at one outer edge, with its own toolbar. Drag the splitter to resize it.
- **Details:** the selected Event's details sit below the Events and Map row. Scroll the page down to reach them.

The main area never gets narrower than 940 px. When the window is too narrow, the X pane gives up width first.

## Events list and filters

The list header shows how many Events are listed (for example `128 events`), a `Manage` button for Watchlists, and the active time range. The number of Sources is not shown in the header; it is available only to screen readers. Below it are two selectors: the scope (`All Events` or one Watchlist) and the minimum severity (`All severities`, `Medium and above`, `High and above`, `Critical and above`).

Every launch starts with these filters:

- time range `30D`;
- minimum severity `High and above`;
- an empty search and the `All Events` scope.

These filters are not saved; the next launch starts from them again. UCDP conflict data is about 20 to 50 days old, so at the `30D` startup range most UCDP Events do not appear; choose `90D` or `ALL` to see them. To see everything, choose `ALL` in the top bar and `All severities` in the severity selector. Search, time range, scope and severity combine: an Event must match all of them.

The list shows the newest Events first, by last update. It loads 100 rows at a time as you scroll. At most the newest 2,000 matching Events are listed and shown on the map. When more match, a notice such as `Newest 2,000 of 3,100 shown.` appears. The full sentence, `Newest 2,000 of 3,100 shown. Narrow range or search for others.`, is in its hover text and its accessible name. Search still finds older Events.

When nothing matches, the list says which filters are narrowing it, for example `No Events at High and above in the last 30 days. Widen the range or severity to see more.` The message `No Events yet. Automatic refresh is running.` appears only when the database has no Events at all and the filters are `ALL`, all severities, `All Events` and no search.

Each row shows the time of the last update, the place, the title, badges, the number of sources and the category.

- **Severity badge:** `Low`, `Medium`, `High`, `Critical` or `Catastrophic`. Severity is derived automatically from provider data: from the GDACS alert level and magnitude for earthquakes, and from the best fatality estimate for UCDP Events. It is not an independent assessment.
- **`Unverified` badge:** the Event comes from a provider record that the app has not independently verified.
- **Provider label:** `GDACS` or `UCDP Candidate` names the source that created the Event.

## Event details and sources

Select an Event in the list or on the map to show its details below. The tabs are:

- **Overview:** category, status, location, start and update times, coordinates, and the severity with the rule it was derived from. It also has the Watchlist checkboxes, and for UCDP Events the UCDP source note, the release the Event was last seen in, and the citation.
- **Timeline:** when the Event was created locally, when it started, and when it was last updated.
- **Sources:** the evidence linked to the Event. Dataset records show their canonical source URL.
- **X:** X posts attached to the Event.
- **Related:** reserved for later use; it shows no Event data yet.
- **Location:** location, region, country and map point.

## Attaching an X post to an Event

Only posts you choose are attached, and only their URL is stored. The app does not read post text, author, media or engagement.

1. Open the post in the X pane.
2. Copy its link: open the `...` menu (More X actions) and choose `Copy Post Link`, or right-click a post link and choose `Copy X Post Link`. `Copy Post Link` is available only when the X pane shows a single post.
3. Select the Event, open its `Sources` tab and choose `Attach X post`.
4. Paste the link into `X post URL` and choose `Attach`.

## Watchlists

A Watchlist is a local, named collection of Events.

- Choose `Manage` in the Events panel to create, rename or delete Watchlists.
- Add or remove the selected Event with the Watchlist checkboxes in its Overview tab.
- Choose a Watchlist in the scope selector to list only its Events. Time range, search and severity still apply.
- Deleting a Watchlist removes its monitoring and Alert history. The Events and Sources stay.

## Alerts

Alerts are in-app only; there are no Windows notifications.

- Open `Alerts` in the top bar. Under Monitoring, turn on `Monitor GDACS updates` for a Watchlist.
- An Alert is created when a GDACS refresh materially changes an Event that is in a monitored Watchlist. Monitoring starts with the next qualifying refresh and does not look back.
- No other source creates Alerts. New Events, UCDP updates and FIRMS points do not.
- History shows the newest 200 Alerts, marked read or unread.
- Selecting an Alert marks it read and selects its Event. It also resets the filters to `ALL`, all severities, `All Events` and an empty search, so the Event is never hidden.

## Map

Hover over an Event marker to see its title, place and source count; click it to select the Event. Marker color shows the category, and marker size shows the severity. Critical and Catastrophic markers have a light ring.

Layer toggles:

- **`Events`:** the Event markers.
- **`Conflict zones`:** approximate polygons for armed conflicts.
- **`Tension`:** shades whole countries in an elevated-tension situation. A country covered by a conflict zone gets no tension tint.
- **`NASA FIRMS — Fire / Thermal Anomalies`:** fire and thermal detection points. Off by default; see the FIRMS section below.
- **`Infrastructure`:** marked `Pending` and has no data yet.

The conflict and tension entries are assessed, owner-curated reference material, not verified facts. Click an area where no marker or point is to open a popup with the entry's name, class, parties, basis and as-of date. While an entry is unreviewed, the popup says `Draft, not yet reviewed by the owner`; currently every entry is a draft. The layer never creates or selects Events.

The legend lists the Event categories and the severity size scale. While the matching layers are on, it also shows `Armed conflict (approx.)`, `Elevated tension` and the FIRMS detection ages.

### NASA FIRMS

1. Turn on the FIRMS layer and move the map to the area you want.
2. Choose `Refresh FIRMS`. The app requests the detections for the visible area once, and the layer shows the latest 24 hours.

- **Display filter:** `Significant` (the default) shows nominal or high confidence detections with a fire radiative power of at least 10 MW. `Strong only` raises that to 50 MW. `All detections` shows everything received. Changing the filter sends no request.
- **Area limit:** the visible area may span at most 90 degrees of longitude and 60 degrees of latitude. Otherwise the app asks you to `Zoom in to a smaller area before refreshing NASA FIRMS.`
- **Point popup:** click a point to see its acquisition time, satellite, confidence, fire radiative power, day or night, and coordinates.
- **Not stored:** FIRMS points are never saved, and they are not Events.
- **No key:** without a MAP_KEY, the refresh shows `NASA FIRMS is not configured.`

## Provider status and manual refresh

Below the Events header, one row per provider shows its last update, its next scheduled refresh and its state. The UCDP Candidate row also shows `monthly release` and the month its data runs through.

- **States:** shown when not idle, as `Refreshing…`, `Stale`, `Not configured`, a failure message, or `No new release` for UCDP.
- **`Refresh` buttons:** in the GDACS row and the UCDP Candidate row. They start the same refresh the timer runs; only one runs at a time per provider.

A finished refresh reloads the list without changing your selection, filters or open details.

## Settings

Open Settings with the gear button. It has three sections:

- **Accent theme:** `Orange` (default), `Blue`, `Red`, `Green` or `Yellow`. The choice applies at once and is saved.
- **Application:** the app's name, shell, UI host, persistence, and whether inbound ports are required (`None required`).
- **Data sources:** the UCDP attribution: source, citation, funding acknowledgement and license.

## X pane

The pane header reads `MY X FEED` and `X Website`. Sign in on the X website inside the pane if you want your own feed; the app never sees your password.

- **Header:** `Move Left` or `Move Right` switches the pane side. `Collapse` hides the pane; `Show X` brings it back. The side and width are saved.
- **Toolbar:** `Back`, `Forward`, `Home` (opens `https://x.com/home`) and `Refresh`.
- **`...` menu:**
  - `Open External` opens the current page in your system browser.
  - `Copy Post Link` copies the current post's link.
  - `Clear Session` asks for confirmation, then deletes all cookies and clears all browsing data of the X pane's browser profile and opens X home again. This signs you out of X. Events and preferences are not affected. Clear Session clears the X pane's browsing data; check the ad blocker status afterwards.
  - `Ad Blocker...` opens the ad blocker dialog.
- **Links:** links to sites other than X open in your system browser.

When the optional ad blocker is not active, one muted line appears under the toolbar:

- `Ad blocker inactive: open Ad Blocker... for details`: it was set up but is not running in the X pane, for example because its installed instance has disappeared. The dialog shows the reason.
- `Ad blocker off: open Ad Blocker... to turn it on`: it is installed but turned off; use `Enable` in the dialog.
- `Ad blocker not set up`: no ad blocker is installed. It was never installed or was fully removed.

No line means it is active, or its state is unknown. The line only reports what the app can observe. It cannot tell whether an enabled ad blocker is actually filtering. Setup is in [setup and configuration](setup-and-configuration.md#adblock-optional).

## What needs a restart

- A new or changed FIRMS key: the app reads it only at startup.
- Some ad blocker operations: the dialog's message says so when a restart is needed.

Accent theme, X pane side and X pane width apply immediately.
