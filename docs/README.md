# Documentation index

Every document in the repository, who it is for and what it covers.

The app's display name is "Northern Monitor", decided on 2026-10-06. It is centralized in `ProductIdentity`. The code, the data folder (`%LOCALAPPDATA%\GeopoliticalMonitor`), the executable and the repository keep the identifier `GeopoliticalMonitor` on purpose. The trademark screening result is yellow: the name is not cleared.

## User-facing documents

These must match the current code. Their "verified against" commit names the commit they were last checked against. The rule for keeping them current is in [AGENTS.md](../AGENTS.md), under Documentation maintenance.

| Document | Audience | Covers | Verified against commit |
|---|---|---|---|
| [README.md](../README.md) | User, operator, developer | What the app is, status, requirements, quick start, data sources, privacy, top troubleshooting | `49ac411` |
| [guide/user-guide.md](guide/user-guide.md) | User | The screen, Events and filters, details, X post attachment, Watchlists, Alerts, map and layers, FIRMS, Settings, the social pane (X and Bluesky) | `f4df3bd` |
| [guide/setup-and-configuration.md](guide/setup-and-configuration.md) | Operator, developer | Prerequisites, building and running, first launch, FIRMS key, X and Bluesky login, AdBlock for X and Bluesky, data locations, backups, troubleshooting | `f4df3bd` |
| [guide/data-sources.md](guide/data-sources.md) | User, operator | Each data source: content, freshness, keys, what it creates, attribution, limits; sources not used | `49ac411` |

## Repository policies

| Document | Audience | Covers |
|---|---|---|
| [SECURITY.md](../SECURITY.md) | Anyone reporting a vulnerability | Supported versions and how to report |
| [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) | Developer, operator | Third-party packages, map data and datasets with their licenses |

## Instructions for coding tools

| Document | Audience | Covers |
|---|---|---|
| [AGENTS.md](../AGENTS.md) | Developer, coding tools | Permanent product, architecture, security, layout and Git rules |
| [CLAUDE.md](../CLAUDE.md) | Claude Code | Claude Code specific rules and a current-status summary |
| [scripts/README.md](../scripts/README.md) | Developer | Rules for optional convenience scripts |
| [tasks/README.md](tasks/README.md) | Developer, coding tools | The bounded task workflow |

## Internal working documents

These record design work and history. They may describe plans that are not implemented, decisions that were later changed, or states that no longer apply. Check them against the code before relying on them.

| Document | Audience | Covers |
|---|---|---|
| [Geopolitical_Monitor_Migration_b4c2617_2026-09-29.md](Geopolitical_Monitor_Migration_b4c2617_2026-09-29.md) | Internal | Migration handoff: history, domain model, decisions and roadmap; later changes are marked as superseded notes |
| [architecture/system-overview.md](architecture/system-overview.md) | Developer | Desktop runtime, layers and hosting model |
| [architecture/data-model.md](architecture/data-model.md) | Developer | Event, Source and related data model notes |
| [architecture/integrations.md](architecture/integrations.md) | Developer | Provider integrations in technical detail |
| [architecture/security.md](architecture/security.md) | Developer | Security boundaries of the desktop runtime, bridge and social pane (X and Bluesky) |
| [product/v0.1-product-spec.md](product/v0.1-product-spec.md) | Developer | The v0.1 product specification |
| [design/interaction-v0.1.md](design/interaction-v0.1.md) | Developer | Interaction direction for v0.1 |
| [design/ui-v0.1.md](design/ui-v0.1.md) | Developer | Visual and layout direction for v0.1 |
| [design/local-basemap-data.md](design/local-basemap-data.md) | Developer | The bundled basemap data |
| [design/event-automation-design.md](design/event-automation-design.md) | Internal | Event automation design, slice plan and owner decisions |
| [design/conflict-tension-layer-design.md](design/conflict-tension-layer-design.md) | Internal | Conflict and tension reference layer design |
| [design/x-pane-design.md](design/x-pane-design.md) | Internal | Social pane design: Bluesky option, narrow-window fix, ad blocker indication |
| [design/social-pane-adblock-design.md](design/social-pane-adblock-design.md) | Internal | The ad blocker for the Bluesky pane |
| [decisions/README.md](decisions/README.md) | Developer | Where architecture decision records go and their suggested shape |
