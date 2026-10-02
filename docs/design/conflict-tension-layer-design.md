# Conflict and Tension Reference Layer Design

Status: design, decided with the owner, not implemented. Written 2026-10-02.

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
