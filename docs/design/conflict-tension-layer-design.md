# Conflict and Tension Reference Layer Design

Status: design, decided with the owner; implemented as drafts in `1eaee7e` and updated in `9476579` (see sections 10 and 11); every entry awaits owner review. Written 2026-10-02.

This document records the owner's decisions for a map reference layer that shows where armed conflicts and elevated tensions are, as assessed by the owner. Entries are reference material, not Events.

## 1. Data file

- A static, owner-curated, versioned reference file in the repository.
- The owner is the sole curator.
- Each entry has these fields:

| Field | Meaning |
| :- | :- |
| `id` | Stable identifier of the entry. |
| `name` | Plain display name. |
| `class` | `armed_conflict` or `elevated_tension`. |
| `parties` | The parties involved. |
| `geometry` | The area shown on the map (see section 2). |
| `basis` | The sources the assessment rests on, each with the date it was checked. |
| `asOf` | The date the entry was last assessed. |

- When an elevated tension escalates into armed conflict, the owner moves the entry by editing its `class` and `asOf`.

## 2. Geometry

- `armed_conflict` entries use hand-drawn **approximate** polygons, drafted from the bundled Natural Earth geometry for the owner to review. Each such polygon is labelled "approximate" in the file.
- `elevated_tension` entries shade whole countries, referenced by country code against the bundled Natural Earth geometry.

## 3. Precedence

Conflict overrides tension where both apply: a country covered by an `armed_conflict` entry gets no tension tint. The tension entry may stay in the file.

## 4. Presentation

- Two visually distinct styles. Conflict keeps the existing hot styling; tension uses a lighter tint that cannot be mistaken for it.
- Popups and the legend show `asOf` and the label "Assessed, owner-curated".
- No entry is presented as ground truth.

## 5. Starting set

### 5.1 Armed conflict

- Russia-Ukraine
- Iran with the Gulf states, Jordan and the US
- Yemen with Saudi Arabia
- Israel with Lebanon and Hamas
- Myanmar
- Sudan

### 5.2 Elevated tension

- **Russian attacks on Europe:** EU and NATO members plus Moldova. A country is listed if any agreed source (section 6) lists it; official attribution is not required. Ukraine belongs to the conflict class, not to this entry. The exact country list is to be confirmed against the sources when the file is created, and the entry's `basis` names each source and the date it was checked. Any tier shown (for example officially attributed, or fitting the pattern) comes from the source tracker's own classification, never from what governments have said.
- India-Pakistan
- Pakistan-Afghanistan
- Israel-Iran (suppressed while a conflict entry covers it)
- China-Taiwan
- North and South Korea

## 6. Sources for "Russian attacks on Europe"

Agreed sources:

- The Everywhere War tracker (Sahaidachnyi Security Center)
- ICCT/GLOBSEC
- The AP investigation
- IISS
- The ISW map

Sources are cited in `basis`. No tracker's incident data is copied into the repository or the app (licensing is unverified). ACLED-derived data is excluded.

## 7. Wording rule

Every label, legend line and popup uses a plain description of what happened. One umbrella term for this kind of activity is banned by the owner; it is deliberately not written in this document, and it must not appear anywhere in the app.

## 8. Open items

- The exact country list for the "Russian attacks on Europe" entry, per source.
- The approximate conflict polygons, for owner review.
- The legend entries.

## 9. Not part of this layer

The Aircraft, Internet outages and Naval toggles are hidden behind `showPlaceholderMapLayers` (false) and are not part of this layer.

## 10. As built (2026-10-02, `1eaee7e`)

**Data file:** `src/frontend/src/data/conflictTensionLayer.json`, bundled with the frontend. Top level: `schemaVersion` (1), `fileVersion` (1), `updated` (ISO date) and `entries`. Each entry has the fields in section 1; `parties` is an array of plain strings, `basis` is one plain sentence without URLs, and `asOf` is an ISO date. `armed_conflict` geometry is a GeoJSON Polygon (counter-clockwise exterior ring, `[lon, lat]` rounded to 2 decimals, at most 30 vertices, no antimeridian crossing). `elevated_tension` geometry is an array of ISO 3166-1 alpha-3 country codes, resolved to the bundled Natural Earth countries through one mapping in `src/frontend/src/features/map/conflictTensionLayer.ts`. A test validates the whole file.

**Implementation details added beyond section 1:**

- `ownerReviewed` (boolean): popups show "Assessed, owner-curated" when true and "Draft, not yet reviewed by the owner" when false.
- `countryCodes` on every `armed_conflict` entry: the countries whose territory its polygons cover, used only for the precedence rule in section 3 (not the parties).
- `approximate: true` on every polygon entry.

**Status of every entry:** all twelve entries are drafts (`ownerReviewed: false`, `asOf` 2026-10-02). The six conflict polygons are rough hulls drawn by an AI assistant for owner review from general geographic knowledge; no third-party dataset or tracker geometry was used.

**Precedence result:** Israel-Iran is fully suppressed (Israel and Iran are covered by conflict entries). The tinted countries are the 22 countries of "Russian attacks on Europe", plus India, Pakistan, Afghanistan, China, Taiwan, North Korea and South Korea.

**"Russian attacks on Europe" country check (2 October 2026):** a country is listed when at least one agreed source that could be read lists it. Read: the Sahaidachnyi Security Center article on its tracker (the tracker page itself did not show its data), the ICCT and GLOBSEC update, the AP map report, and the IISS paper (country labels in its incident chart). The ISW map could not be read directly and was checked through Newsweek's reproduction of it. Listed: Albania, Bulgaria, Czechia, Denmark, Estonia, Finland, France, Germany, Greece, Italy, Latvia, Lithuania, Moldova, Netherlands, Norway, Poland, Romania, Slovakia, Spain, Sweden, United Kingdom and the United States. Moldova rests only on the ISW map as reproduced by Newsweek. Countries the sources name outside the EU and NATO (and Ukraine, Russia and Belarus) are not listed.

**Remaining open items:** owner review of every polygon and every entry, including the country list above; the sources' tiers (officially attributed or fitting the pattern) are not shown in this version.

## 11. Update 2026-10-02 (`9476579`)

These changes follow owner decisions of 2026-10-02; the decisions recorded above are unchanged.

- **Russia-Ukraine war:** still one `armed_conflict` entry with one approximate polygon (30 vertices, about 27.9 to 61.6 degrees east and 41.9 to 69.6 degrees north), now covering the front area, Russia's border regions and European Russia east to the Urals. Its basis says plainly that the area covers heavy armed clashes and recorded long-range Ukrainian strikes, not ground fighting everywhere in it, citing Kyiv Post (drone strikes on Yekaterinburg and Chelyabinsk, April 2026) and The War Zone (the deeper Novy Urengoy strike, September 2026), read on 2 October 2026. `countryCodes` stays UKR and RUS.
- **Belarus with the EU and NATO:** new `elevated_tension` entry for Belarus, Poland, Lithuania and Latvia, based on Euronews (Lithuania's border closure over smuggling balloons, November 2025; migrant-crossing pressure at Latvia's border, August 2026) and United24 Media (balloons from Belarus entering Polish airspace, February 2026).
- **Gulf states and Jordan with Iran and the Houthis:** new `elevated_tension` entry for Saudi Arabia, the United Arab Emirates, Kuwait, Qatar, Oman and Jordan, based on Al Jazeera and Euronews (Iranian attacks on Gulf states and Jordan, July 2026) and Al Jazeera (Houthi attacks on southern Saudi cities, September 2026). Bahrain is named in the entry but not shaded, because the bundled 110m basemap has no Bahrain shape (owner decision).
- **Saudi Arabia:** removed from the Yemen entry's `countryCodes` so the rest of Saudi Arabia is tinted by the Gulf entry; the Yemen polygon is unchanged.
- **Display:** the map toolbar's column gap is 6 px (was 8 px), and the conflict legend swatch reads "Armed conflict (approx.)".
- **Tinted countries now:** the 22 countries of "Russian attacks on Europe", Belarus, Saudi Arabia, the United Arab Emirates, Kuwait, Qatar, Oman, Jordan, India, Pakistan, Afghanistan, China, Taiwan, North Korea and South Korea. Israel-Iran stays suppressed; Russia and Ukraine are never tinted.

**Remaining open items:** owner review of every entry and polygon; whether the conflict area should cover all of Ukraine; whether the Iran and Myanmar extents are right.
