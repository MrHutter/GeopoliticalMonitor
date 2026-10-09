# Social Pane Ad Blocker Design: AdBlock for the Bluesky Pane

Implementation status, 2026-10-06: option (c) implemented together with the Bluesky switcher, pending owner acceptance.

Status: design proposal, not implemented. Written 2026-10-06 against `main` at `855dbaa`. Every proposal is marked PROPOSED; nothing here changes code, instruction files or owner decisions.

State found: the Bluesky switcher (slice 3 of `docs/design/x-pane-design.md`) is **not implemented**. The source tree contains no Bluesky or `bsky` code. This design holds either way. Its pure Application parts can be built before the switcher, but everything that touches a Bluesky WebView needs the switcher first (section 5).

## 1. Summary

The owner wants the production ad blocker to cover the Bluesky feed as well as X. Today the design keeps the Bluesky profile extension-disabled because Bluesky shows no ads.

What the owner would get (option (c), PROPOSED as the ready path): the same reviewed AdBlock 6.47.0 runs in the Bluesky pane. It has its own verified copy of the payload, its own binding and its own status line. X stays exactly as it is.

What it costs:

- a second 326 MB payload copy on disk;
- a second full payload hash whenever the Bluesky control is created;
- a second `Install AdBlock...` step for Bluesky;
- a change to an AGENTS.md rule that today says extensions belong only to the X profile;
- one more extension with access to pages where the owner is logged in.

The benefit is small today. Bluesky's own site shows no ads (section 2.2), so AdBlock would mostly apply its default filter lists to a site that has nothing for them to remove.

**Recommendation (PROPOSED):** keep option (a), with no extension on Bluesky, until Bluesky shows ads. Keep option (c) designed and ready, and build it if the owner still wants coverage now. Option (b), sharing one payload folder between both profiles, is **blocked**: under Microsoft's documented behavior it would change how X's install and remove work (section 3).

## 2. Findings

### 2.1 Code

**A. Scoping**

- The binding is `XDiagnosticAdBlockBinding(SchemaVersion, ProfileScope, RuntimeExtensionId, SourceFolderPath, Manifest, Payload, TrustRecord)` (`XDiagnosticAdBlock.cs:67-74`). The scopes are plain strings: `ExtensionCapabilityDiagnostic` and `XBrowser` (`:77-78`).
- `Matches` compares every field, including `SourceFolderPath` and the trust record. For the production scope it also requires a trusted payload (`:92-108`).
- `IsStructurallyValid` accepts only those two scope strings. For `XBrowser` it also requires a 64-hex payload digest and an approved trust record (`:110-132`).
- Production behaviour is gated by `profileScope == ProductionXProfileScope` in several places: the display names (`:461-465`), the unmanaged-instance disabling in `SecureForAuthenticatedNavigationAsync` (`:473-501`), `Matches` and `IsStructurallyValid`. The startup trust policy hard-codes the X scope (`XAdBlockPayloadAuthentication.cs:234`).
- **Paths:** the binding file comes from `GetProductionAdBlockBindingPath()` (`LocalApplicationDataPathProvider.cs:50`, `Data\x-adblock-binding.json`). The managed payload comes from `GetProductionAdBlockManagedSourceDirectory()` (`:55`, `Extensions\AdBlock\Current`). Replacements go through `Current.staging`, `Current.previous` and `Current.rejected` (`FileXAdBlockManagedPayloadStore.cs:131-133`); the final move is at `:205`; recovery is at `:255`.
- **One payload for two profiles:** the binding names its source folder, so a second binding could point at the same `Current`. However, the X dialog replaces that folder on `Install AdBlock...` (`MainWindow.xaml.cs:1348`, `PrepareAsync`) and deletes it after `Remove` (`:1398-1409`). Microsoft documents that the extension content is not copied into the profile and that changing it removes the installed extension (section 2.2). A shared folder would therefore let every X install or removal also remove the Bluesky instance.
- **Re-authentication:** every inspection hashes the complete runtime tree (about 326 MB, `x-pane-design.md` section 2.1 D). A second profile needs its own trust evaluation whether or not the folder is shared.

**B. Startup**

- **X sequence** (`MainWindow.xaml.cs:162-241`):
  1. recover an interrupted activation (`:169`);
  2. read the binding (`:170`);
  3. inspect the managed payload (`:175`);
  4. decide with `XProductionAdBlockStartupTrustPolicy.Evaluate` (`:178-182`);
  5. create either the extension-enabled environment (`CreateWithOptionsAsync` with `AreBrowserExtensionsEnabled = true`, `:243-271`) or the default environment (`:292-307`), falling back to the default if creation throws (`:198-207`);
  6. call `EnsureCoreWebView2Async`, build the service with the X scope and run `SecureForAuthenticatedNavigationAsync`, keeping its snapshot (`:210-222`).
- **Clean state:** with no payload and no binding, the X gate allows the extension environment (`XAdBlockPayloadAuthentication.cs:213-216`), so a first install needs no restart.
- **A second environment** needs its own user-data folder. Microsoft documents that `AreBrowserExtensionsEnabled` must match only when connecting to an already running environment (section 2.2). The planned `BlueskyBrowser` folder is separate from `XBrowser` (`x-pane-design.md` 3.1 (a)), so its option is independent of X's.
- **Sharing the hash:** within one startup, before any payload write, one inspection result could serve two gates. With a separate payload per profile there is nothing to share; each folder is authenticated by its own gate.

**C. Install and management**

- **One dialog, one profile:** `ShowXProductionAdBlockDialogAsync` (`MainWindow.xaml.cs:1196-1434`) builds its stores from the X paths (`:1198-1202`) and acts through the single field `xProductionAdBlockService` (`:63`, `:1281`). Its title is "X Ad Blocker" (`:1247`), and its explanatory text says it manages "only that trusted production X-profile instance" (`:1237`).
- **Restart path:** an enrolment made while the extension host is unavailable needs a restart, then `Reconcile` (`:1357-1363`).
- **Diagnostic:** opens from the same dialog and uses a separate profile (`:1436`).
- **Single-profile assumptions:**
  - the fields `xProductionAdBlockService`, `xProductionExtensionHostState`, `xProductionExtensionStartupFailure` and `xProductionRequiresTrustedReenrollment` (`:63-64`, `:150-154`);
  - the dialog's stores (`:1198-1202`);
  - the profile adapter built on `XWebView` (`:216`);
  - the indication fields (`MainWindow.AdBlockIndication.cs`);
  - the scope constants above.

**D. Status**

- The status texts (`XAdBlockIndicationPolicy.cs:29-31`) are computed from the host state, the snapshot and a live check.
- The live check is X-only: it uses `xProductionExtensionHostState`, the X bound runtime id and a profile adapter on `XWebView` (`MainWindow.AdBlockIndication.cs:41-70`). It is triggered by X navigation completion (`MainWindow.xaml.cs:627-631`) and by the X dialog closing (`:1428-1429`).
- A second feed needs its own copy of this state and its own trigger. The pure policy can be reused unchanged.

**E. Trust surface**

- The manifest identity the code records holds the version, name, locale, author, key presence, manifest hash and expected runtime id (`XDiagnosticAdBlock.cs:20-29`), but **not** the extension's permissions.
- This document therefore cannot state the host permissions of AdBlock 6.47.0. The safe assumption is that it can read and change the content of every Bluesky page, as it can on X today. That includes the logged-in timeline, direct messages shown in the page and the login form.
- An extension with page access can, in principle, see what the page sees. Using it on Bluesky means trusting the reviewed payload with the Bluesky session as well as the X session.
- **What stays the same:** no native host objects, no bridge, no filesystem or database APIs (`security.md`, "Browser extensions are enabled only …"). The payload stays pinned by the compiled trust record.

**F. Tests and rules**

- Tests that know the X scope: `AdBlockTrustSecurityTests.cs`, `XAdBlockIndicationPolicyTests.cs`, `XDiagnosticAdBlockServiceTests.cs` (Application) and `LocalApplicationDataPathProviderTests.cs` (Infrastructure).
- AGENTS.md line 152 says the extension capabilities "belong only to the X profile". Line 154 says an extension may be hosted "in the isolated X profile". Both need a reviewed change before option (c) can be built (section 4).

### 2.2 Research (read 2026-10-06)

- **Bluesky ads.**
  - TechCrunch (2024-12-05, "Bluesky CEO Jay Graber isn't ruling out advertising") quotes CEO Jay Graber as not ruling out advertising. Any ads would be "much more user intent-driven", for example in search results. Subscriptions are mentioned as a hinted revenue stream.
  - Silicon Florist (2025-01-15) reports a third-party sponsored-post test by Graze on one custom feed starting 2025-01-07. Bluesky itself did not run ads.
  - **Not confirmed:** no source from 2026 says that bsky.app shows ads, and the site itself was not inspected. A PhoneArena page and a Search Engine Land page returned HTTP 403 and were not read.
- **WebView2 extensions** (Microsoft Learn, `CoreWebView2Profile.AddBrowserExtensionAsync`):
  - The extension is added "to the current user profile" and persists in it.
  - "During installation, the content of the extension is not copied to the user data folder."
  - "changing the content of the extension will cause the extension to be removed from the installed profile."
  - Adding from the same folder path reinstalls the extension.
  - With extensions disabled, the call fails with `ERROR_NOT_SUPPORTED`.
- **Environment options** (Microsoft Learn, `CoreWebView2EnvironmentOptions.AreBrowserExtensionsEnabled`): the option defaults to false. "If connecting to an already running environment with a different value … it will fail with `ERROR_INVALID_STATE`." Separate user-data folders are separate environments.
- **AdBlock behaviour** (AdBlock Help Center, "Introduction to filter lists", undated): EasyList and Acceptable Ads are enabled by default. Nothing is documented about behaviour or resource use on sites without ads, so the cost-benefit cannot rely on a documented "idle" cost.

## 3. Options (PROPOSED)

| Option | How | Verdict |
| :- | :- | :- |
| (a) No extension on Bluesky | Bluesky profile extension-disabled, as in `x-pane-design.md` section 4 | **Recommended now.** No new trust surface, no disk or startup cost, no AGENTS.md change. Revisit if Bluesky starts showing ads. |
| (b) One shared payload, second binding | The Bluesky binding points at `Extensions\AdBlock\Current` | **Blocked.** X's Install replaces and X's Remove deletes that folder, so either action would remove the Bluesky instance. Preventing that would change X's Install and Remove behaviour, which the owner forbids. |
| (c) Separate verified payload per profile | The Bluesky profile gets its own managed copy, binding and trust gate | **Recommended if the owner wants coverage.** Full independence from X; costs disk, one hash and one install step. |

### 3.1 Option (c) in detail (PROPOSED)

**Locations and binding**

| Item | X (unchanged) | Bluesky (new) |
| :- | :- | :- |
| User-data folder | `WebView2\XBrowser` | `WebView2\BlueskyBrowser` |
| Managed payload | `Extensions\AdBlock\Current` | `Extensions\AdBlockBluesky\Current` (with its own `.staging`, `.previous`, `.rejected`) |
| Binding file | `Data\x-adblock-binding.json` | `Data\bluesky-adblock-binding.json` |
| `ProfileScope` | `XBrowser` | `BlueskyBrowser` |

- **Schema:** the Bluesky binding uses the same schema 2 with no new fields.
- **Production scopes:** in the Application layer, `ProductionXProfileScope` stays exactly as is, and a new `ProductionBlueskyProfileScope = "BlueskyBrowser"` is added. A helper `IsProductionScope(scope)` returns true for both; `IsStructurallyValid` and `Matches` apply the production checks for either scope.
- **No cross-matching:** a binding matches only its own expected scope, so an X binding never satisfies the Bluesky gate and vice versa. `XProductionAdBlockStartupTrustPolicy.Evaluate` gains a scope parameter, and the X call keeps passing `XBrowser`.
- **Same trust records:** both scopes use the one compiled record (AdBlock 6.47.0, digest `9022cbf4…a822ae`); no new record.

**Startup of the Bluesky control** (lazy, when the switcher first creates it, or at startup when the saved feed is Bluesky)

1. The X sequence runs first and unchanged.
2. Recover any interrupted activation of the Bluesky payload, read the Bluesky binding and inspect the Bluesky payload. This is a full hash, done once per Bluesky creation.
3. Evaluate the gate with the Bluesky scope. Unlike X, a clean state (no payload, no binding) gives an **extension-disabled** environment, so nothing changes for an owner who never opts in.
4. If the gate passes, create the `BlueskyBrowser` environment with `AreBrowserExtensionsEnabled = true`. Otherwise, or if creation throws, create the default extension-disabled environment.
5. Build a second `XDiagnosticAdBlockService` with the Bluesky scope, the Bluesky binding store and the Bluesky payload path. Run `SecureForAuthenticatedNavigationAsync` before the first Bluesky navigation and keep its snapshot.

**Hashing:** there is no sharing. Each folder is authenticated by its own gate, and every later snapshot (dialog open, Refresh, install, reconcile) re-authenticates as today. No check is skipped or reused across folders.

**Dialog:** `Ad Blocker...` opens the dialog for the active feed.

- The X dialog is unchanged in title, text, buttons and behaviour.
- The Bluesky dialog is titled "Bluesky Ad Blocker" and has the same buttons except `Test Extension Support`; there is only one diagnostic, reached from the X dialog.
- Install source: the folder picker as for X, plus `Use verified X copy`, which takes `Extensions\AdBlock\Current` as the *source* of a fresh staging copy. The copy is authenticated again in staging and in its final location, so trust is not weakened.
- Bluesky's `Remove` deletes only the Bluesky payload and binding.
- In code, the dialog takes a per-feed context (paths, stores, service, host state and labels). The X context is built from exactly today's values.

**Status line per feed:**

- The Bluesky feed gets its own indication state: snapshot, bound runtime id, live check and in-flight flag. Its triggers are Bluesky navigation completion and the Bluesky dialog closing. The texts are the same three lines.
- If the Bluesky ad blocker was never set up, the Bluesky feed shows **no** line. It is opt-in, unlike X's "Ad blocker not set up".
- The X line keeps its current state and triggers.

**Failure isolation (required):**

- Bluesky uses separate fields, paths, binding, payload, service and environment.
- Every Bluesky AdBlock exception is caught on the Bluesky path, sets only Bluesky state and leaves Bluesky extension-disabled for that run.
- No Bluesky code reads or writes X's fields, X's binding or X's payload. The only exception is the read-only `Use verified X copy` source, which only reads and hashes.
- An X failure behaves exactly as today.

**What the user does:**

1. Switch to Bluesky and open `Ad Blocker...`.
2. Choose `Install AdBlock...` or `Use verified X copy`.
3. If the Bluesky environment was extension-disabled (always on the first opt-in, because a clean state starts disabled), restart the app, then choose `Reconcile` in the Bluesky dialog. This is the same restart path X uses today (`MainWindow.xaml.cs:1357-1363`).

## 4. Security review (PROPOSED)

**Access on Bluesky pages:** the reviewed extension would run on bsky.app with whatever host access its manifest grants. Assume full read and modify access to page content, including the logged-in session as rendered in the page and the login form.

**Changes to the threat check** in `x-pane-design.md` section 4:

- "browser extensions disabled" becomes "browser extensions disabled unless the owner installs the reviewed AdBlock for Bluesky".
- With the extension installed, the reviewed payload is trusted with Bluesky page content.
- Everything else is unchanged: the Bluesky pane still cannot reach the X profile, the privileged app WebView, its bridge, SQLite, the filesystem or native code.

**Trust is not weakened:**

- The only accepted payload is the same compiled AdBlock 6.47.0 record with its fixed digest.
- There is no auto-following and no diagnostic route.
- The privileged app WebView stays extension-disabled, and the X profile, session and AdBlock path are unchanged.
- The Bluesky profile is extension-disabled by default.

**Proposed AGENTS.md wording** (proposal only; instruction-file edits are a separate reviewed task):

- Line 152: replace "These capabilities belong only to the X profile." with "These capabilities belong only to the isolated X profile and, when the owner installs the reviewed ad blocker for it, the isolated Bluesky profile."
- Line 154: replace "in the isolated X profile" with "in the isolated X profile and the isolated Bluesky profile, each with its own verified managed copy and binding".
- Add the sentence: "The Bluesky profile is extension-disabled until the owner installs the reviewed ad blocker for it, and no Bluesky ad blocker action may read or change the X profile's binding, payload or extension state."

**Matching sentences** for `docs/architecture/security.md` ("Browser extensions are enabled only …"), `integrations.md` (the AdBlock paragraphs) and `system-overview.md` (payload and binding locations).

## 5. Implementation slices (PROPOSED)

**Order:** the Bluesky switcher (`x-pane-design.md` slice 3) first, ideally used for a while with Bluesky extension-disabled. Slices 1 and 2 below are pure and could be built before the switcher; slices 3 to 5 need it.

**Backups:** none of these slices writes to SQLite. The new binding is a file in the Data folder, so back up the whole Data folder before each manual test.

0. **Instruction change.**
   - Scope: the AGENTS.md wording in section 4. It must land before any code.
   - Separate reviewed task. Risk: none.
1. **Scope generalisation (Application only).**
   - Scope: `ProductionBlueskyProfileScope`, `IsProductionScope`, and the scope parameter on the trust policy.
   - Non-goals: no Desktop change.
   - Files: `XDiagnosticAdBlock.cs`, `XAdBlockPayloadAuthentication.cs`.
   - Tests: every existing test unchanged and passing; new tests for Bluesky-scope validity and matching; X bindings never matching the Bluesky scope and the reverse; the clean-state decision per scope.
   - Risk to X: low; X call sites pass the same values.
2. **Bluesky paths.**
   - Scope: `GetBlueskyAdBlockBindingPath`, `GetBlueskyAdBlockManagedSourceDirectory`.
   - Files: `LocalApplicationDataPathProvider.cs` and its tests (all paths distinct from X's).
   - Risk: none.
3. **Bluesky environment and trust gate.**
   - Scope: the startup sequence in section 3.1 on the Bluesky control; extension-disabled by default; fallback handling.
   - Non-goals: dialog and status line.
   - Files: `MainWindow.xaml.cs`, or a new partial for the Bluesky feed.
   - Tests: Application tests for the per-scope gate.
   - Manual check: X starts and filters exactly as before, and a clean Bluesky starts extension-disabled.
   - Risk to X: medium if shared code is touched, so the X sequence must stay byte-for-byte equivalent.
4. **Bluesky dialog.**
   - Scope: per-feed dialog context; Install, `Use verified X copy`, Reconcile, Enable, Disable and Remove for Bluesky only.
   - Files: `MainWindow.xaml.cs`; tests for the per-feed action policy.
   - Manual check: install for Bluesky, restart, reconcile; ads on X unaffected; Bluesky `Remove` leaves X working.
   - Risk to X: medium; review that the X context is unchanged.
5. **Bluesky status line.**
   - Scope: the per-feed indication state and triggers.
   - Files: `MainWindow.AdBlockIndication.cs`, and `XAdBlockIndicationPolicy.cs` only if a feed parameter is needed.
   - Tests: no line for a never-set-up Bluesky; the off and inactive lines.
   - Risk: low.
6. **Documentation.**
   - Scope: the guides, the architecture documents and this design's "As built".
   - Risk: none.

## 6. Open questions for the owner

| # | Question | Recommendation |
| :- | :- | :- |
| Q1 | Option (a) now, or option (c)? | (a) until Bluesky shows ads; (c) stays ready. |
| Q2 | If (c): accept a second 326 MB payload copy and a second hash at each Bluesky creation? | Yes; it is the price of keeping X untouched. |
| Q3 | Keep the Bluesky profile extension-disabled until AdBlock is installed for it? | Yes. |
| Q4 | Offer `Use verified X copy` as an install source next to the folder picker? | Yes; the copy is re-authenticated. |
| Q5 | No status line for a never-set-up Bluesky ad blocker? | Yes, no line. |
| Q6 | One dialog per feed, opened from the active feed? | Yes; "X Ad Blocker" unchanged, plus "Bluesky Ad Blocker". |
| Q7 | Keep `Test Extension Support` only in the X dialog? | Yes. |
| Q8 | Build (c) only after the switcher has been in use? | Yes. |

## 7. Risks and what is deliberately not designed

- The extension's exact permissions are not recorded in code, so its access on Bluesky is assumed to be full page access.
- If (c) is built while Bluesky has no ads, it adds trust surface and cost for little benefit.
- Microsoft documents that changing an installed extension's folder content removes the extension. This is a possible, **unconfirmed**, lead for the 2026-10-02 incident in which the X instance disappeared while the payload and binding stayed valid. It needs its own investigation and is not assumed here.
- A future reviewed AdBlock version needs re-enrolment in both profiles separately.
- Two extension-enabled profiles double the places where the extension can fail; per-feed status lines make that visible.
- **Not designed:**
  - another ad blocker or any unreviewed payload;
  - auto-following new versions;
  - sharing one payload between profiles;
  - extensions in the privileged app WebView;
  - any change to X's dialog, binding, payload or startup;
  - Bluesky API use, scraping or injection.

## As built

Differences between the implementation and this design:

- A separate "Bluesky Ad Blocker" dialog was built instead of one dialog with a per-feed context, so the X dialog stays unchanged.
- After the restart, the user finishes with `Install AdBlock...` or `Use verified X copy` (which reuses the existing verified Bluesky copy), or with `Reconcile` when it is offered.
- Creating the Bluesky control hashes the Bluesky payload twice (the trust gate plus the first snapshot), mirroring X.
- AGENTS.md has an exception: the `Use verified X copy` source may read and re-authenticate the X payload, but never writes to it.
