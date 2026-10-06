# Social Pane Design: Bluesky Option, Narrow-Window Fix, Ad Blocker Indication

Implementation status: slice 1 (ad blocker indication) implemented and committed (`8e9e9d4`, documentation `7af60cc`); slices 2 to 5 not implemented.

Status of slices 2 to 5: design proposal, not implemented. Written 2026-10-02 against `main` at `5823a94`. Every proposal is marked PROPOSED; nothing here changes code, instruction files or owner decisions.

## Owner decisions (2026-10-02)

- **Narrow-window fix: DEFERRED by the owner.** The clipping below 1,270 px does not bother the owner and nothing is planned; section 6 and slice 2 are kept as a record only.
- **Bluesky switcher and ad blocker indication:** approved candidates, not yet scheduled.
- **Answers to the open questions (section 10):**
  - Q1 yes: two separate folders; the Bluesky control is created lazily on first use.
  - Q2 yes: the hidden control stays loaded, muted, with lowered memory.
  - Q3 yes: a neutral `SOCIAL FEED` label from one native constant.
  - Q4: only if measured as necessary.
  - Q5 moot: it belonged to the deferred narrow-window fix.
  - Q6 yes: the post time comes from the Bluesky identifier, labelled as such, with the 2023 lower bound.
  - Q7 yes: show "Ad blocker not set up", muted.
  - Q8 no: the single startup hash stays out of slice 1; revisit only after a measured startup gain.
  - Q9: decided after the slice 3 login check (default: `bsky.app` only).
  - Q10 yes: rename the Details `X` tab to `Posts`, in slice 4.

## 1. Summary and scope

Three related changes to the native pane that today shows only the X website:

1. **Bluesky option.** The pane can switch between the X website and the Bluesky website. Each feed keeps its own isolated browser profile and login. The user switches in the pane header; the choice is remembered like the pane's side and width.
2. **Narrow-window fix (DEFERRED by owner decision, kept as a record).** Below 1,270 px of client width the pane becomes narrower than its own header and its right-hand controls are cut off. The pane should instead hide itself temporarily while the window is too narrow and come back when there is room, without touching any saved preference or any browser session.
3. **Ad blocker indication.** On 2026-10-02 the production AdBlock instance disappeared from the X profile and nothing in the pane said so. The pane should show one muted line when the ad blocker is not active, pointing to the existing `Ad Blocker...` dialog.

What the user sees: a compact `X | Bluesky` switcher in the pane header; Bluesky logs in once and stays logged in; on a narrow window the pane would step aside instead of being clipped (deferred: the pane stays clipped below 1,270 px for now); and a quiet "Ad blocker inactive" line in the X feed when filtering is not running.

Out of scope: any change to the X profile, its session, its AdBlock trust authority (Chrome Web Store AdBlock `6.47.0` only), the privileged app WebView, the React layout contract (X right: Events | Map | X; X left: X | Map | Events; app view never below 940 px), and any use of the X or Bluesky APIs for the pane.

## 2. Findings

### 2.1 Code (evidence base)

**A. Pane structure and persistence**

- Grid columns: `AppColumn` `Width="*" MinWidth="940"`, `SplitterColumn` `Width="10"`, `XPaneColumn` `Width="420" MinWidth="320"` (`MainWindow.xaml:30-32`). The physical columns swap roles when the pane moves left (`MainWindow.xaml.cs:1915-1923`, `ApplyXPaneSide` at `:1925-1951`).
- Header (`MainWindow.xaml:90-161`): row 0 has the accent bar, eyebrow `MY X FEED`, title `X Website`, `Move Left`/`Move Right` and `Collapse`; row 1 has `Back`, `Forward`, `Home`, `Refresh`, a status text and the `...` overflow button; the overflow panel holds `Open External`, `Copy Post Link`, `Clear Session` and `Ad Blocker...`. An error banner with `Retry X` sits below (`:163-184`); the X WebView is row 2 (`:186`). The collapsed state shows a `Show X` button over the app (`:37-50`).
- Collapse (`MainWindow.xaml.cs:1712-1726`) sets the pane and splitter to `Visibility.Collapsed` and their columns to width 0; it does not close or navigate the WebView, so the page and session stay loaded. Reopen (`:1728-1738`) restores the remembered width through `XPaneLayout.Reopen`.
- Width math (`XPaneLayout.cs`): maximum = `max(0, min(0.55 × width, width − 940 − 10))`; minimum = `min(320, maximum)`; the requested width is clamped between them (`:17-39`). Defaults in `XBrowserDefaults.cs` (420 px, ratio 0.27, minimum 320, main pane 940, splitter 10).
- `ApplyXPaneWidth` (`MainWindow.xaml.cs:1821-1834`) sets the pane column's `MinWidth` to **0** and the app column's to 940 every time it runs. The XAML `MinWidth="320"` therefore only applies until the saved width is first restored (`RestoreXPaneWidthAsync`, `:1856-1876`, called on `Loaded` and from `InitializeWebViewsAsync`). `OnRootGridSizeChanged` (`:1745-1753`) re-applies the remembered width on every resize while the pane is expanded.
- Persisted: `XPaneWidth` and `XPaneSide` in `ApplicationPreferences` (`XPaneWidthPreferenceService.cs:8`, `XPaneSidePreferenceService.cs:5`). Collapse is not persisted (`XPaneLayout.Collapse` returns `ShouldPersist: false`). `XPaneSide` is sent read-only to React through `app.info` and `xPane.side.value` (`DesktopBridgeProtocol.cs:53`, `:192`, `:229`); the bridge accepts only `AccentTheme` as a preference key (`AccentThemePreferenceService.TryParsePreferenceKey`).

**B. WebView and profile mechanics**

- The X WebView uses its own user-data folder `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser` (`LocalApplicationDataPathProvider.cs:69-72`). At startup the native host evaluates the AdBlock trust gate and either creates an extension-enabled environment with `CreateWithOptionsAsync` and `AreBrowserExtensionsEnabled = true` (`MainWindow.xaml.cs:240-268`) or falls back to a default environment (`:289-…`, used at `:189` and `:206`). It then calls `EnsureCoreWebView2Async`, wires handlers (`:270-287`) and secures the extension state before the first authenticated navigation (`:210-223`).
- Settings: no host objects, no web messages, no password autosave or general autofill, DevTools off in release (`XBrowserSecurityConfiguration.IsolatedDefault`, applied at `MainWindow.xaml.cs:478-488`). No user agent override. `PermissionRequested`, `IsMuted`, `MemoryUsageTargetLevel` and `ProcessFailed` are not handled today.
- Navigation policy (`XBrowserNavigationPolicy.cs`): top-level HTTPS to `x.com` or `www.x.com` stays in the pane; other HTTPS opens in the system browser; anything else is blocked (`:12-44`, applied at `MainWindow.xaml.cs:603-619`). New-window requests follow the same rule (`:650-662`); downloads are cancelled (`:664-667`). `Open External` opens the current X URL or X home (`:1121-1126`).

**C. Copy Post Link and manual attachment**

- The pane never reads the DOM: `Copy Post Link` uses `XWebView.Source` (`MainWindow.xaml.cs:1128-1145`), and the context-menu item uses WebView2's link metadata (`:669-679`). `SourceChanged` only refreshes button state (`:638-643`).
- `XPostUrl` accepts `https://x.com` or `www.x.com` status URLs with a 1–15 character account and a numeric status id (`XPostUrl.cs:7-34`). `XPostSnowflakeTimestamp` decodes the publication time from the status id (`>> 22` plus the X epoch, with a 5-minute future tolerance; `:7-39`).
- Attachment (`EventCatalogService.cs:71-120`): validate the URL, decode the timestamp, build a URL-only `XPost` SourceItem whose id is `src-x-` plus SHA-256 of `XPost\n{normalized URL}` (`:126-130`), then attach. The same URL is reused (`ExistingSourceAttached`), the same URL on the same Event is `AlreadyAttached`.
- Storage: `SourceType` is an enum (`Domain/Sources/SourceType.cs`: `XPost`, `NewsArticle`, `OfficialStatement`, `DatasetRecord`, `Other`) stored as a string column of at most 64 characters with no check constraint (`GeopoliticalMonitorDbContext.cs:57`) and read back with `Enum.Parse<SourceType>` (`SqliteEventStore.cs:968`). **A new enum value needs no migration**, but an older build cannot parse it.
- XPost assumptions: bridge messages `events.sources.attachXPost*` (`DesktopBridgeProtocol.cs:32-34`, `:159`, `:310-340`, `:1089-1139`); the frontend source-type list and union (`nativeBridge.ts:388`, `types/events.ts:18`, `:145`); the Sources tab's `Attach X post` form and the Details `X` tab, which lists `XPost` sources (`EventDetailsPanel.tsx:9`, `:74`, `:170-242`).

**D. AdBlock status**

- Startup trust gate: `XProductionAdBlockStartupTrustPolicy.Evaluate` (`XAdBlockPayloadAuthentication.cs:205-244`) decides between the extension environment and the fallback; host state is `Available`, `FallbackUnavailable` or `XUnavailable` (`XProductionAdBlockPolicy.cs:3-8`).
- After the X WebView exists, `SecureForAuthenticatedNavigationAsync` (`XDiagnosticAdBlock.cs:473-503`) takes a snapshot and disables any enabled, unmanaged AdBlock-like extension. The returned snapshot is **discarded** (`MainWindow.xaml.cs:220`). Snapshot states: `NoManagedInstance`, `InstalledManagedEnabled`, `InstalledManagedDisabled`, `InstalledUnmanaged`, `BindingStale`, `InvalidFolder`, `InstallationFailed`, `OperationFailed` (`XDiagnosticAdBlock.cs:7-18`). The "bound instance is no longer installed" case is `BindingStale` (`:762-766`).
- The `Ad Blocker...` dialog (`MainWindow.xaml.cs:1192-1428`) builds its own snapshot when opened and on Refresh (`:1312`, `:1417`) and formats it with `XProductionAdBlockPolicy.Format` (`XProductionAdBlockPolicy.cs:50-71`).
- **Double hash at startup.** The managed payload (about 326 MB) is authenticated twice in one startup: once by `sourceInspector.InspectAsync(managedSourcePath)` for the trust gate (`MainWindow.xaml.cs:175`), and again inside `CreateSnapshotAsync`, which inspects `binding.SourceFolderPath` (the same managed `Current` folder) when a binding exists (`XDiagnosticAdBlock.cs:752`) or the unmanaged candidate path when it does not (`:714`). The startup cost was not measured (the app was not launched).
- A cheap, side-effect-free check exists in principle: `IXBrowserExtensionProfile.GetInstalledExtensionsAsync` lists the installed extensions with their ids and enabled state without touching the payload; combined with the runtime id from the last trusted snapshot it can tell whether the bound instance is present and enabled.

**E. Clipping (computed from the code, not observed)**

| Client width | Maximum pane | Pane width | Splitter | App column | Result (X right or left) |
| :- | :- | :- | :- | :- | :- |
| 1,030 px | min(566.5, 80) = 80 | 80 | 10 | 940 | Pane is 80 px wide; header controls (about 300 px of fixed-width buttons) overflow and are cut off at the pane's right edge; the X page is unusable. |
| 1,270 px | min(698.5, 320) = 320 | 320 | 10 | 940 | Pane is exactly 320 px; the header just fits (estimated, not measured). |
| 1,400 px | min(770, 450) = 450 | clamp(saved, 320, 450), default 378 | 10 | 1,012 at the default | No clipping. |

The sides do not change the arithmetic; with the pane on the left the overflow is cut at the pane's inner edge next to the splitter. The cause is the clamp: below 1,270 px it allows widths under the 320 px the header needs, and the XAML `MinWidth="320"` cannot prevent it because `ApplyXPaneWidth` resets it to 0. A second, transient case: until the saved width is restored at startup, the XAML values apply (pane 420 px, minimum 320), so a window narrower than 1,370 px briefly lays out wider than itself and the pane is cut off at the window's right edge.

**F. Tests**

There is no Desktop (WinUI) test project. Existing Application tests cover the pane only through pure policies: `XPaneWidthPreferenceServiceTests`, `XPaneSidePreferenceServiceTests` (including `XPaneLayout.Collapse`/`Reopen`), `XToolbarPolicyTests`, `XToolbarCommandStatePolicyTests`, `XBrowserNavigationPolicyTests`, `XPostLinkCopyServiceTests`, `XPostSnowflakeTimestampTests`, `XProductionAdBlockPolicyTests`, `XDiagnosticAdBlockServiceTests`, `AdBlockTrustSecurityTests` and others. New behaviour should therefore live in pure Application policies (auto-collapse decision, indicator state mapping, feed preference, Bluesky URL and TID parsing, Bluesky navigation policy, per-feed command rules) with native code kept thin.

### 2.2 Research (read 2026-10-02)

- **Post URLs** (Bluesky docs "Posts" and community references): `https://bsky.app/profile/{handle-or-DID}/post/{rkey}`. Handle-based links break when a handle changes; DID-based links are stable.
- **Handles** (atproto.com, Handle spec): DNS hostnames, at most 253 ASCII characters, at least two segments of letters, digits and hyphens, case-insensitive (normalize to lowercase), custom domains allowed, some TLDs disallowed.
- **DIDs** (atproto.com, DID spec): `did:plc` and hostname-level `did:web`; syntax `^did:[a-z]+:[a-zA-Z0-9._:%-]*[a-zA-Z0-9._-]$`, hard limit 2,048 characters.
- **Record keys** (atproto.com, Record Key spec; post Lexicon in the atproto repository): posts use key type `tid`. **TID** (atproto.com, TID spec): always 13 characters, base32-sortable alphabet `234567abcdefghijklmnopqrstuvwxyz`, first character from `234567abcdefghij`; top bit 0, next 53 bits are microseconds since the Unix epoch, last 10 bits a random clock id. The time can be decoded, **but** the spec warns that TIDs are not guaranteed unique and can be fabricated, so the decoded time is the time the posting client claims, not a server-assigned time (unlike X's Snowflake). The post record also carries a client-declared `createdAt`.
- **Hosts** (Bluesky docs "API Hosts and Auth", hostname listings): the web client is `bsky.app`; services include `bsky.social` (entryway and OAuth authorization), `api.bsky.app`, `cdn.bsky.app`, `video.bsky.app` and others. Media and API calls are subresources, not top-level navigations.
- **Single-page behaviour**: WebView2 raises `SourceChanged` for navigations to a different URL, including fragment navigations, but not for `history.pushState` with the same URL (Microsoft Learn, `CoreWebView2.SourceChanged`). That bsky.app updates the URL as you move between posts is consistent with its use as a single-page app but was **not confirmed** from a Bluesky page; `Copy Post Link` reads `Source` on click, so it does not depend on the event.
- **Terms** (Bluesky Terms of Service; Community Guidelines): the terms govern Bluesky Social itself and say other applications on the protocol have their own terms; they contain nothing on automated access, scraping or embedding. The guidelines forbid trying to "compromise, exploit, bypass, abuse, or disrupt" Bluesky's systems. **Not confirmed**: any explicit statement about showing the website in an app WebView. The design uses ordinary user-operated browsing only.
- **Ads** (Hootsuite, The Keyword and others reporting company statements): Bluesky shows no ads today; leadership has not ruled out future, non-intrusive ads. No extension is needed for Bluesky now.
- **WebView2** (Microsoft Learn): the profile is chosen when a control is created (`CoreWebView2ControllerOptions.ProfileName`) and a control cannot switch profiles afterwards; each user-data folder runs its own browser process group with renderer, GPU and helper processes; environments sharing a user-data folder must use identical options or creation fails; `AreBrowserExtensionsEnabled` is an environment option and connecting to a running environment with a different value fails; hidden controls can lower memory with `MemoryUsageTargetLevel.Low` and `TrySuspendAsync` (best effort).
- **Not readable / not confirmed**: whether the bsky.app login ever performs a top-level navigation to another host (for example an OAuth page on `bsky.social`); this needs a manual check in slice 3.

## 3. Switcher (PROPOSED)

### 3.1 Options

| Option | How | Verdict |
| :- | :- | :- |
| (a) Two WebView2 controls, separate user-data folders | Keep the X control exactly as it is. Add a second control with its own folder `%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\BlueskyBrowser`, a default (extension-disabled) environment, created lazily on first switch; show one control, hide the other. | **Recommended.** Full isolation; the X environment, profile, session and AdBlock path are untouched. Cost: a second browser process group while Bluesky exists. |
| (a′) Second profile inside the X user-data folder | Same environment, `ProfileName = "Bluesky"`. | Rejected: the environment is extension-enabled and shared with X (same browser process); environment options must match, so Bluesky could not be extension-disabled at the environment level, and any environment-creation change would touch X's AdBlock path. |
| (b) One control, navigate between sites | Navigate the X control to bsky.app. | Rejected: one profile for both sites (shared cookies and storage), the X navigation policy would have to allow Bluesky, every switch reloads and loses page state, and the AdBlock profile would run on Bluesky. |
| (c) Create and close on each switch | Close the Bluesky control when switching back to X. | Not recommended: every switch to Bluesky starts a cold process group and reloads the page; it saves memory only when Bluesky is rarely used. Can be offered later as a setting. |

### 3.2 Header

- Row 0: the accent bar, then a compact two-segment switcher `X | Bluesky` in place of the static title, then `Move Left`/`Move Right` and `Collapse` as today. The eyebrow becomes a neutral label (`SOCIAL FEED`) taken from one native constant, and every pane label (eyebrow, segment names, `Show …` button, dialog titles) comes from one small native label class, next to `ProductIdentity`, instead of the current hard-coded `MY X FEED`, `X Website` and `Show X`.
- Width check: row 0 at 320 px is tight. If slice 3 measures that the switcher does not fit at 320 px, the fallback is to move `Move Left`/`Move Right` into the overflow menu (an owner decision, question Q4). Because the narrow-window fix is deferred, the pane can still be narrower than 320 px below 1,270 px of client width; the header is then clipped as today, with or without the switcher, and the 320 px check applies only from 1,270 px up.
- Per feed:

| Control | X feed (unchanged) | Bluesky feed |
| :- | :- | :- |
| Back / Forward / Refresh | X control | Bluesky control |
| Home | `https://x.com/home` | `https://bsky.app/` |
| Open External | current X URL or X home | current Bluesky URL or Bluesky home |
| Copy Post Link | X status URL | Bluesky post URL (section 5) |
| Clear Session | "Clear X session?" clears only the X profile | "Clear Bluesky session?" clears only the Bluesky profile |
| Ad Blocker... | shown | hidden |
| Ad blocker line | shown when inactive (section 7) | never shown |
| Error banner and Retry | `Retry X` | `Retry Bluesky` |

### 3.3 Hidden control, persistence, startup

- The hidden control stays loaded (no close, no navigation), so its session and page survive a switch. While hidden: `IsMuted = true` (so a video cannot keep playing audio), `MemoryUsageTargetLevel = Low`; restore both when shown. `TrySuspendAsync` is optional and best effort; not used in the first version because resuming has to be handled on every switch.
- Preference: a native-only key `SocialPaneFeed` with values `X` or `Bluesky`, default `X`, stored in `ApplicationPreferences` like `XPaneSide`, written when the user switches, read at startup. Unknown values fall back to `X`.
- Startup: the X control initializes exactly as today, including the AdBlock trust sequence, whatever feed is selected. The Bluesky control is created at startup only when the saved feed is `Bluesky`; otherwise on the first switch. No second WebView exists for users who never open Bluesky.
- First Bluesky use: the Bluesky control opens `https://bsky.app/`; the user logs in on Bluesky's own page; the login persists in the Bluesky profile folder as browser state only.
- Collapsed state: collapse and reopen act on the pane as today; the active feed is kept; `Show …` reads `Show X` or `Show Bluesky`.
- Keyboard and accessibility: the switcher is two toggle buttons in one group, with accessible names "Show X feed" and "Show Bluesky feed" and the selected state exposed; both are in the tab order before `Move` and `Collapse`; switching keeps focus on the switcher.

## 4. Security and rules (PROPOSED)

**Boundary.** The Bluesky pane is a plain, user-operated website pane with the same boundary as X: no privileged bridge, no host objects, no web messages, no injected JavaScript or CSS, no DOM inspection or scraping, no cookie or token extraction, no use of the Bluesky or X APIs for the pane, no automation of clicks or scrolling, and URL-only copying of validated post links. It uses its own isolated profile with browser extensions disabled (no ads today, so no extension is needed). Settings: `IsolatedDefault` (no host objects, no web messages, no password autosave, no general autofill, no DevTools in release). Navigation: top-level HTTPS to `bsky.app` stays in the pane; other HTTPS opens in the system browser; other schemes are blocked; new windows follow the same rule; downloads are cancelled. Add a `PermissionRequested` handler that denies camera, microphone, geolocation, notifications and clipboard-read requests for the Bluesky control (X keeps its current behaviour).

**Proposed wording for AGENTS.md** (a proposal only; instruction-file edits are a separate, reviewed task): "The isolated social pane may show either the published X website or the published Bluesky website, switched by the user. Each site uses its own WebView2 user-data folder (`%LOCALAPPDATA%\GeopoliticalMonitor\WebView2\XBrowser` and `…\BlueskyBrowser`). The Bluesky WebView follows the X rules: no native bridge or host objects, no DOM inspection, injected scripts or styles, scraping, cookie or token extraction, automation or Bluesky API calls; bsky.app top-level HTTPS navigation stays in the pane and other HTTPS links open in the system browser. Browser extensions are enabled only for the X profile. Bluesky post links are handled URL-only, exactly like X post links." Matching sentences would go into `docs/architecture/integrations.md` (Bluesky Website Pane section), `docs/architecture/security.md` (boundary and profile location) and `docs/architecture/system-overview.md` (a second isolated WebView).

**Threat check.** A Bluesky page can reach its own origin's storage and cookies in the Bluesky folder, the network, and the clipboard through the browser's normal permission rules. It cannot reach the X profile (separate folder and browser process), the privileged app WebView or its bridge, SQLite, the filesystem (downloads cancelled) or native code (no host objects or web messages). A malicious link can at most open an HTTPS page in the system browser. The new native-only preference holds only `X` or `Bluesky`.

## 5. Source attachment (PROPOSED)

- **URL validation** (`BlueskyPostUrl`): HTTPS; host exactly `bsky.app`; path exactly `/profile/{actor}/post/{rkey}`; `actor` is a handle per the Handle spec (normalized to lowercase) or a DID per the DID spec; `rkey` is a TID (13 characters from `234567abcdefghijklmnopqrstuvwxyz`, first from `234567abcdefghij`); query and fragment removed; normalized form `https://bsky.app/profile/{actor}/post/{rkey}`.
- **SourceType** `BlueskyPost`. No migration: the column is a 64-character string without a constraint. Consequence: a database with a `BlueskyPost` row cannot be read by an older build (`Enum.Parse` would throw), so take a backup before the first manual test and do not roll back the build afterwards without restoring it.
- **Identity and duplicates**: SourceItem id `src-bsky-` plus SHA-256 of `BlueskyPost\n{normalized URL}`; `ExternalId` `{actor}/{rkey}`. Same URL and same Event: `AlreadyAttached`; same URL and another Event: the existing source is attached (`ExistingSourceAttached`), as for X. Limitation: a handle-form and a DID-form URL of the same post are different URLs and become two sources, because resolving a handle to a DID would need a network call.
- **Timestamp**: decode the TID (top 53 bits, microseconds since the Unix epoch); accept only times from 2023-01-01 to now plus 5 minutes, otherwise refuse like an invalid X timestamp. Because the TID is chosen by the posting client, show and document it as "time from the post identifier", not as a verified time (question Q6).
- **Copy Post Link** in the Bluesky feed copies the current `Source` if it validates; the context menu offers "Copy Bluesky Post Link" for a link target that validates. Status texts as for X ("Post link copied", "Open a post first").
- **Bridge and UI**: one new typed request `events.sources.attachBlueskyPost` with `eventId` and `url` and its result and error messages, validated like `attachXPost`. The Sources tab gets a choice of link type or a second `Attach Bluesky post` action; the Details `X` tab becomes a neutral `Posts` tab listing `XPost` and `BlueskyPost` sources with a small provider label. The frontend `NativeSourceType` union and validator gain `BlueskyPost`.

## 6. Narrow-window fix (PROPOSED) — DEFERRED by owner decision, kept as a record

| Option | What it does | At 1,030 px | Verdict |
| :- | :- | :- | :- |
| (a) Transient auto-collapse, drop the XAML minimum | When the space for the pane (client width − 940 − 10) is under 320 px, hide the pane exactly like Collapse but without recording a user collapse; show it again with the remembered width when the space returns. Set the XAML `XPaneColumn` to `MinWidth="0"` so the pre-restore layout cannot overflow. | Pane hidden, app 1,030 px wide, `Show …` button disabled with the tooltip "Widen the window to show the pane". | **Recommended.** |
| (b) Window minimum width of 1,270 px | Stop the window from shrinking below 1,270 px. | Not reachable. | Owner decision; conflicts with the wish that the UI scales down to 940 px. |
| (c) Narrower pane header below 320 px | Collapse header buttons into the overflow menu. | Pane 80 px wide; content still unusable. | Not recommended. |

**Rule** (a pure function in Application, for example `XPaneAutoCollapsePolicy.Evaluate`):

- Inputs: client width, whether the user collapsed the pane.
- Output: `Visible`, `AutoCollapsed` or `UserCollapsed`.
- `UserCollapsed` when the user collapsed it (the user's choice wins); otherwise `AutoCollapsed` when `clientWidth − 950 < 320`; otherwise `Visible`.
- Boundaries, identical for the left and right side: 1,269 px gives 319 px, so `AutoCollapsed`; 1,270 px gives 320 px, so `Visible`; 1,271 px gives 321 px, so `Visible`. Non-finite or non-positive widths give `AutoCollapsed`.

**Guarantees**: no write to `XPaneWidth`, `XPaneSide` or `SocialPaneFeed`; no WebView unload, reload, navigation or session change (the pane is only hidden); the remembered width stays as it was; the user's Collapse, Reopen, Move and drag-resize keep their accepted behaviour. Evaluate on every `SizeChanged`; leaving `AutoCollapsed` reapplies `XPaneLayout.Reopen(remembered width)`.

**Tests**: the boundary values above for both sides; user collapse beats auto-collapse; leaving auto-collapse restores the remembered width; no persistence call is made (preference services spied).

## 7. Ad blocker indication (PROPOSED)

**Texts** (one muted line in the native header, X feed only, never for Bluesky, no ids, paths or exception text):

| Condition | Text |
| :- | :- |
| Host `Available` and snapshot `InstalledManagedEnabled` | (no line) |
| Snapshot `InstalledManagedDisabled` | "Ad blocker off: open Ad Blocker... to turn it on" |
| No binding and no managed payload (never set up) | "Ad blocker not set up" (question Q7) |
| Host `FallbackUnavailable` (trust gate or environment failure); snapshot `BindingStale` (including the removed instance of 2026-10-02), `InstalledUnmanaged`, `InvalidFolder`, `InstallationFailed`, `OperationFailed`; the bound instance missing at a later check | "Ad blocker inactive: open Ad Blocker... for details" |
| Host `XUnavailable` (pane failed) | (no line; the existing error banner covers it) |

**Computation**: a pure mapping in Application from (host state, last snapshot state, live extension presence) to one of the texts. The native side keeps the snapshot returned at startup by `SecureForAuthenticatedNavigationAsync` (today discarded at `MainWindow.xaml.cs:220`) and the last snapshot built by the Ad Blocker dialog. The live check is `GetInstalledExtensionsAsync` plus a comparison with the bound runtime id; it never hashes the payload, never enables, disables, installs or re-enrolls anything, and never writes a binding.

**Refresh**: when the X pane finishes initializing; after the Ad Blocker dialog closes (using its last snapshot); after each completed X navigation (live check only, at most once per navigation).

**Limits**: it can only report whether the trusted instance is installed and enabled; it cannot detect an enabled extension that is not filtering, and the line never claims that ads are blocked.

**Single startup hash**: pass the startup trust-gate validation (`MainWindow.xaml.cs:175`) into the first snapshot so `CreateSnapshotAsync` reuses it instead of re-inspecting the same managed folder (`XDiagnosticAdBlock.cs:752`). Conditions: same canonical path, the same startup sequence, no install, recovery or payload write in between, and only for that one call; every later snapshot (dialog open, Refresh, install, reconcile) still re-authenticates. Benefit: about 326 MB less reading and hashing at each startup. Risk: a change to the payload between the two points of one startup would be noticed one snapshot later; the trust gate itself is unchanged, so no production check is weakened. Ship it with the indication only if a measured startup gain justifies it (question Q8).

## 8. Persistence and bridge (PROPOSED)

| Key | Values | Default | Visible to the bridge |
| :- | :- | :- | :- |
| `SocialPaneFeed` | `X`, `Bluesky` | `X` | No (native-only; the bridge accepts only `AccentTheme`) |

No key for auto-collapse (it is transient) and none for the ad blocker line (computed). Nothing new reaches the React page for the switcher, the clipping fix or the indication. Only slice 4 adds one typed bridge request, `events.sources.attachBlueskyPost`, plus its result and error messages, because attaching a source is a React action today.

## 9. Implementation slices (PROPOSED)

1. **Ad blocker indication.**
   - Scope: the state mapping, keeping the startup snapshot, the live check, the header line; optionally the single startup hash.
   - Non-goals: any trust, install or enable behaviour.
   - Files: `XProductionAdBlockPolicy.cs` (or a new indication policy), `MainWindow.xaml`, `MainWindow.xaml.cs`; tests in `XProductionAdBlockPolicyTests` and `XDiagnosticAdBlockServiceTests`.
   - Migration: no. Backup: not needed.
   - Risk to X and AdBlock: low (read-only), medium if the single hash is included.
   - Manual acceptance: no line with AdBlock enabled; "off" after disabling in the dialog; "inactive" after removing the instance; nothing in Bluesky (once it exists).
2. **Narrow-window fix — DEFERRED by owner decision, kept as a record.**
   - Note: not planned; the slice number is kept so later slices keep their numbers.
   - Scope: `XPaneAutoCollapsePolicy`, `MinWidth="0"` in XAML, the disabled `Show …` state.
   - Files: `XPaneLayout.cs` or a new policy, `MainWindow.xaml`, `MainWindow.xaml.cs`; new policy tests.
   - Migration: no. Backup: no.
   - Risk: low; the X session stays loaded.
   - Manual acceptance: shrink below 1,270 px on both sides, the pane hides; widen, it returns at its width; the saved width is unchanged after restart; user collapse is kept.
3. **Bluesky switcher.**
   - Scope: the second control and folder, lazy creation, the Bluesky navigation policy and permission handler, the header switcher and labels, the `SocialPaneFeed` preference, per-feed commands, mute and memory level when hidden.
   - Non-goals: source attachment.
   - Files: `MainWindow.xaml`, `MainWindow.xaml.cs`, `LocalApplicationDataPathProvider.cs`, new `Bluesky*` policies and a preference service in Application, `XToolbarPolicy` per feed; tests for policies, preference and navigation.
   - Migration: no (a new preference row). Backup: recommended (a new preference row in the database).
   - Risk: medium for X if the shared code is touched carelessly, so X's startup path must stay byte-for-byte equivalent.
   - Manual acceptance: X unchanged with AdBlock; first switch logs in to Bluesky; the switch back keeps both pages; restart restores the feed; Clear Session per feed; external links open in the browser; check the login flow for top-level hosts. Test with a client width of at least 1,270 px: the narrow-window fix is deferred, so there is no auto-collapse and a narrower window clips the header as it does today.
4. **Bluesky source attachment and Copy Post Link.**
   - Scope: `BlueskyPostUrl`, the TID timestamp, `SourceType.BlueskyPost`, the attachment service, the typed bridge request, the Sources and `Posts` tab UI, Copy Post Link and the context menu in the Bluesky feed.
   - Files: Domain `SourceType.cs`, Application (URL, TID, catalog service, bridge protocol), Desktop handlers, frontend `nativeBridge.ts`, `types/events.ts`, `EventDetailsPanel.tsx`; tests on every side.
   - Migration: no. **Backup: required** (older builds cannot read `BlueskyPost` rows).
   - Risk to X: low.
   - Manual acceptance: attach a handle URL and a DID URL; a duplicate on the same Event; the same URL on another Event; the `Posts` tab lists both providers.
5. **Instruction and architecture documentation.**
   - Scope: AGENTS.md (the section 4 wording), README, `docs/architecture/*`, the migration handoff note, this document's "As built".
   - Migration and backup: no. Risk: none.
   - Reviewed separately, like every instruction-file change.

## 10. Open questions for the owner

| # | Question | Recommendation |
| :- | :- | :- |
| Q1 | Switcher option (a), with two separate folders and the Bluesky control created lazily? | Yes. |
| Q2 | Keep the hidden control loaded (muted, low memory) rather than closing it? | Keep it loaded. |
| Q3 | Neutral eyebrow text? | `SOCIAL FEED`, from one native constant. |
| Q4 | If the switcher does not fit at 320 px, move `Move Left`/`Move Right` into the overflow menu? | Yes, only if measured necessary. |
| Q5 | Narrow-window option (a) with a disabled `Show …` button while too narrow? | Yes. **Moot:** the narrow-window fix is deferred. |
| Q6 | Use the TID time as the Bluesky post time, labelled as coming from the identifier? | Yes, with the 2023 lower bound. |
| Q7 | Show "Ad blocker not set up" when AdBlock was never enrolled? | Yes, muted. |
| Q8 | Include the single startup hash in slice 1? | Only after measuring the startup gain. |
| Q9 | Allow `bsky.social` top-level pages in the Bluesky pane if login needs them? | Decide after the slice 3 manual check; default is `bsky.app` only. |
| Q10 | Rename the Details `X` tab to `Posts`? | Yes, in slice 4. |

## 11. Risks and things deliberately not designed

- Memory: a second WebView2 process group adds a noticeable amount of memory while Bluesky exists (not measured); lazy creation limits it to users who open Bluesky.
- Bluesky could change its login flow, add ads or change URL forms; the allowlist, the "no extension" decision and the URL validator would then need review.
- The TID time is client-chosen and can be wrong or fabricated.
- Handle and DID URLs of the same post are not deduplicated.
- A database written by slice 4 cannot be opened by an older build.
- The indication cannot see whether an enabled extension actually filters.
- Not designed: embedding Bluesky through its API, feed import into SourceItems, notifications, more than two feeds, per-feed pane widths, and any change to AdBlock trust, version pinning or the diagnostic route.

## 12. Review notes (reviewer comments, not owner decisions)

(a) The proposed AGENTS.md wording in section 4 should also state that the Bluesky control is never wired to the privileged message handler. The wording must be revised if the slice 3 login check shows that top-level `bsky.social` pages are needed (Q9).

(b) The Bluesky permission-deny handler proposed in section 4 is stricter than X's current default behaviour, since X handles no permission requests today. Applying the same handler to X is a possible, separate hardening task.

(c) A hidden but loaded feed keeps running its scripts and polling, as the collapsed X pane does today. Memory is lowered and audio is muted, but background network use has not been measured, and `TrySuspendAsync` is deferred.

(d) Slice 4 should make source-type reading tolerant, so that an unknown stored `SourceType` reads back as `Other`. An older build would then degrade instead of failing to load Events. This is in addition to the required backup.

(e) Before slice 1 relies on the ad blocker live check (`GetInstalledExtensionsAsync`), the code must be verified to show that it has no side effects. The check must never call `SecureForAuthenticatedNavigationAsync`.

(f) The post time derived from a Bluesky identifier is claimed by the posting client, and it must be labelled as such wherever the date appears.

(g) The single startup hash (Q8) stays out of slice 1.
