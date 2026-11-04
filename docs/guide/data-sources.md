# Data sources

This page lists every source the app uses: what it provides, how fresh it is, whether it needs a key, what it creates in the app, its attribution and its limits. Component and data licenses are also listed in [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md).

The app keeps two kinds of records apart. An **Event** is a real-world occurrence. A **Source** is one piece of evidence about it, such as a dataset record or an X post. Several Sources can support one Event, and a Source on its own never becomes an Event.

## GDACS

- **Provides:** earthquakes only, from the Global Disaster Awareness and Coordination System (GDACS) official HTTPS API.
- **Freshness:** while the app is open it refreshes every 30 minutes, first 60 seconds after the window is ready, and backs off after failures. The `Refresh` button in the GDACS status row starts the same refresh by hand. Each refresh asks for at most 100 earthquake records from the preceding four days.
- **Key:** none.
- **Creates:** one `Natural Disaster` Event per earthquake at its epicenter, marked `Unverified` and labelled `GDACS`. Each GDACS episode is kept as a dataset-record Source with its canonical GDACS report URL. Severity is derived from the GDACS alert level and magnitude.
- **Attribution:** "Global Disaster Awareness and Coordination System, GDACS".
- **Limits:** GDACS alerts and automated impact estimates may be uncertain and are not a substitute for information from competent authorities. Other hazards, polygon or track geometry, and records beyond the 100-record page are not imported.
- **Alerts:** a material change to an Event in a monitored Watchlist creates an Alert. GDACS is the only source that creates Alerts.

## UCDP Candidate

- **Provides:** conflict events from the Uppsala Conflict Data Program's monthly UCDP Candidate Events release. The app downloads one monthly CSV file from `https://ucdp.uu.se/downloads/candidateged/`.
- **Freshness:** a release is published about 20 days after its month ends, so the data is about 20 to 50 days old. Most UCDP Events therefore fall outside the `30D` startup range; choose `90D` or `ALL` to see them. While the app is open it checks every 6 hours, first 2 minutes after the window is ready. The `Refresh` button in the UCDP Candidate status row runs the same check by hand. The first import takes the newest release and the one before it; later checks ask only for the next release, unless that release is missing although it is two or more months old, when they look for the newest published release instead. When that release is not published yet, the status row shows `No new release`.
- **Key:** none.
- **Creates:** one Event per record that has a fatality estimate of at least 1 and a place precise to province level or better. The Event is `Military conflict` or `Security`, marked `Unverified` and labelled `UCDP Candidate`, with a dataset-record Source. Severity is derived from the best fatality estimate. Details show the release the Event was last seen in. Article text, headlines and geometry from the file are not stored.
- **Caveat shown in the app:** "Candidate data from the Uppsala Conflict Data Program; it may be revised in later releases." Candidate records can also be removed in later releases.
- **License:** CC BY 4.0.
- **Required citation, as the app shows it:** Hegre, Håvard, Mihai Croicu, Kristine Eck, and Stina Högbladh (July 2020). Introducing the UCDP Candidate Events Dataset. Research & Politics. doi:10.1177/2053168020935257
- **Acknowledgement, as the app shows it:** UCDP is part of and funded by DEMSCORE, national research infrastructure grant 2021-00162 from the Swedish Research Council.
- **Where the app shows them:** the citation and acknowledgement appear in Settings under Data sources and in the Details of each UCDP Event.

## NASA FIRMS

- **Provides:** satellite fire and thermal anomaly detections (VIIRS NOAA-20 and NOAA-21 near-real-time) from the NASA FIRMS Area API.
- **Freshness:** a rolling window of the latest 24 hours for the visible map area. Fetched only when you choose `Refresh FIRMS`.
- **Key:** a free MAP_KEY from NASA, set in the app (Settings, Data sources, `Set key...`; stored in Windows Credential Manager) or in the `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` environment variable; the key set in the app wins. See [setup and configuration](setup-and-configuration.md#nasa-firms). Without a key, no FIRMS request is made. The app has no key-status check, because NASA's documentation describes no endpoint for one.
- **Creates:** a transient map layer only. Detections are never stored, and they are not Events, Sources, Watchlist members or Alerts.
- **Limits:**
  - Area: at most 90 degrees of longitude by 60 degrees of latitude per refresh.
  - Size: each response may be at most 4 MiB, and a refresh keeps at most 20,000 detections.
  - Display filters: `Significant`, `Strong only` and `All detections` are the app's own filters, not NASA severity categories.
  - NASA's own limit: 5000 transactions per 10-minute interval for a MAP_KEY.
- **Attribution:** the app shows "NASA FIRMS / LANCE". This credits the data source; it does not mean NASA endorses the app.

## X

- **Provides:** the normal X website in the X pane, operated by you. The app uses no X API, developer account or token.
- **Freshness:** whatever the X website shows.
- **Key:** none. You sign in to X on the website itself if you want your own feed.
- **Creates:** a Source on an Event, and only when you paste a post's URL into that Event's Sources tab. The app stores the canonical URL, the post's numeric id, a neutral label, and the post's creation time decoded locally from that id. It does not store post text, author, media or engagement.
- **Limits:** no scraping, no reading of X pages, cookies or tokens, and no injected scripts or styles. X posts never become Events.

## Bluesky

- **Provides:** the normal Bluesky website (`bsky.app`) in the social pane, when you switch the pane from X to Bluesky. The app uses no Bluesky API or token.
- **Freshness:** whatever the Bluesky website shows.
- **Key:** none. You sign in to Bluesky on the website itself if you want your own feed.
- **Creates:** nothing. Bluesky posts cannot be attached to Events, and Copy Post Link is not offered for Bluesky.
- **Limits:** only pages on `bsky.app` open in the pane; other HTTPS links open in the system browser. No scraping, no reading of Bluesky pages, cookies or tokens, and no injected scripts or styles.

## Conflict and tension layer

- **Provides:** a reference layer on the map with two kinds of entries. Armed-conflict entries are drawn as approximate polygons. Elevated-tension entries shade whole countries.
- **Freshness:** static. It changes only when the bundled file is edited; each entry names its basis and an as-of date.
- **Key:** none. Bundled with the app; no network use.
- **Creates:** map shading and popups only. It never creates or selects Events.
- **Status:** the entries are assessed and owner-curated. Every current entry has been reviewed by the owner, and its popup says `Assessed, owner-curated`. The armed-conflict polygons are rough outlines drawn by an AI assistant from general geographic knowledge; no third-party dataset or tracker geometry was used. The owner reviewed them on 2026-11-04. Treat the layer as orientation, not verified fact.

## Basemap

- **Provides:** the world map's country boundaries and land: Natural Earth Admin 0, scale 1:110m, distributed through the `world-atlas` 2.0.2 package.
- **Freshness:** static, bundled; no remote map tiles.
- **License:** Natural Earth data is in the public domain; `world-atlas` is ISC licensed.
- **Note:** the neutral country layer follows internationally recognized sovereignty, for example Crimea within Ukraine.

## ReliefWeb

Not active and not configured by default. The code for searching and attaching ReliefWeb reports to an Event remains. It needs an approved ReliefWeb `appname` that will not be requested, so it stays unconfigured and makes no requests. Search and attachment report `ReliefWeb integration is not configured.`

## Not used

- **GDELT as an Event source:** blocked. The owner's testing of GDELT 2.0 exports found too few usable records under strict rules.
- **GDELT search for related reports:** parked. Requests were rejected as rate-limited during testing.
- **ACLED:** out of scope, because of its access and license terms.
