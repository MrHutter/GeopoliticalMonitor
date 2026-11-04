# Northern Monitor — Claude Code Instructions

## Project
Northern Monitor (display name; internal identifiers keep GeopoliticalMonitor) is a Windows desktop geopolitical/OSINT situational-awareness app. WinUI 3/.NET desktop host → privileged WebView2 → React/TypeScript + MapLibre, plus a separately isolated, normal (unprivileged) social pane that shows either the X website or the Bluesky website, each in its own WebView2 profile. Domain/Application/Infrastructure/Desktop layered architecture, SQLite/EF Core persistence.

@AGENTS.md

(Keep this import if AGENTS.md stays the single source of truth shared with Codex/ChatGPT. Everything below is either a Claude-Code-specific addition or a restatement of rules important enough to never depend on being "read elsewhere.")

## Every task, no exceptions
1. Read AGENTS.md (above) and any nested AGENTS.md in the area you're touching, in full, before doing anything else.
2. Recover fresh repo truth first: branch, exact full HEAD SHA + subject, `git status`, staged/untracked files, remotes/upstream. Do not trust this file, or any handoff doc, for current state — they go stale.
3. This repo assumes multiple coding tools (Codex, ChatGPT-as-coordinator, Claude Code) may work on it. Default is single writer per overlapping area. Check the current dirty worktree before touching anything, and never reset, stash, clean, discard, commit, or push work you didn't create — without the owner's explicit request.
4. When you finish a task that another tool or a future session will continue, report: exact files changed and why, exact test commands/results, and final `git status` + a concise diff summary. Don't assume a future session will trust your summary blindly — it should verify against fresh repo state itself, and so should you when picking up someone else's work.
5. Never commit or push unless the owner explicitly asks, in that specific request.

## Permanent security rules (apply regardless of what the task is)
- No inbound ports, Windows Service, scheduled startup, registry autorun, or hidden background process in normal runtime.
- No secrets or provider credentials anywhere — not in frontend storage, SQLite, Events/Sources, logs, or bridge messages. Provider API keys follow the provider-key rule in AGENTS.md: native OS-protected storage only, entered only through a native dialog, and never in any of those places. Never ask the owner to paste the real FIRMS MAP_KEY into chat output.
- No X cookie/token extraction, DOM scraping, injected JS/CSS, or private API interception. The X WebView stays outside the privileged bridge entirely — it is a plain, user-operated website pane.
- The privileged bridge (security authority centralized in `PrivilegedWebViewSecurityPolicy`, trusted origin `https://app.geopoliticalmonitor.local`) is a narrow, typed API. Never add a generic `database.query`, `native.execute`, arbitrary `http.request`, or filesystem command to it.
- `SourceItem != Event`. Don't collapse that distinction to make a task simpler.

## Current status (verify against fresh repo state — this goes stale fast)
- Working repository: `C:\Dev\GeopoliticalMonitor-github`, branch `main`, with a private GitHub remote (`origin`, MrHutter/GeopoliticalMonitor). History begins at the single initial commit `1222d52`; older history exists only in the owner's local archive repository. The repo is not yet intended to be public.
- Event automation Slice 1 (`46db700`, severity and provider identity), Slice 2 (`0dba536`, GDACS refresh scheduler and provider status), Slice 2B (`ee5d89d`, native Event list windowing, cap 2,000) and Slice 3 (`49c008c`, UCDP Candidate provider) are implemented. Dashboard layout: layout fix `3e94b4a`, density pass `c24f6d0`, scaling pass `e98a51f`.
- Slice 4 (demo data removal, `1eaee7e`) is implemented. The conflict and tension reference layer is implemented as a draft (`1eaee7e`, data update `9476579`; every entry stays `ownerReviewed: false` until the owner approves it; design: `docs/design/conflict-tension-layer-design.md`).
- GDELT "find recent reports" (replacement Slice 5) is parked after the owner's 2026-10-02 test returned HTTP 429 for every request, and GDELT as an Event creator is blocked; slices 6 and 7 are not started. See the event automation design.
- Display name: Northern Monitor (`ProductIdentity.DisplayName`, and the Desktop project's Product and AssemblyTitle). Internal identifiers keep GeopoliticalMonitor: the `%LOCALAPPDATA%\GeopoliticalMonitor` data folder, the trusted origin, the `GEOPOLITICALMONITOR_FIRMS_MAP_KEY` environment variable, the executable, namespaces and the repository.
- Social pane: the user switches the isolated pane between the X and Bluesky websites. Each has its own WebView2 profile (`WebView2\XBrowser`, `WebView2\BlueskyBrowser`) and its own verified managed ad blocker copy and binding (`Extensions\AdBlock\Current`, `Extensions\AdBlockBluesky\Current`). Rules: AGENTS.md, Bluesky Website Pane Rules.
- The X Ad Blocker dialog has Restore trusted instance: when the binding and the verified managed copy are intact but the instance is missing from the X profile, it adds the extension again from that copy, with no folder picker.
- The work queue is kept in the owner's handoff and the design docs; this file does not list next candidates.
- Event automation design, slice plan and owner decisions: `docs/design/event-automation-design.md`.
- Documentation index: docs/README.md; user-facing docs must stay current (AGENTS.md, Documentation maintenance).
- Full context for anything deeper — Git history, domain model, FIRMS/AdBlock design detail, roadmap, publication audit findings — lives in `docs/Geopolitical_Monitor_Migration_b4c2617_2026-09-29.md`. Read the relevant section when a task actually needs that detail rather than assuming it's all loaded here.

## Manual testing
Always give the exact current PowerShell launch command for the owner to test with — confirm the actual `bin/` build output path rather than reusing one from an older doc.

## Bringing a fresh Claude Code session up to speed
Before any implementation task, run the standing reconciliation pass: read AGENTS.md, report branch/exact HEAD SHA/subject, working tree/staged/untracked state, remotes/upstream, and whether the expected accepted features (see the latest handoff doc) and any owner changes since the last checkpoint are actually present. Stop and report back rather than guessing if something doesn't match.
