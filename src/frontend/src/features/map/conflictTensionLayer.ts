import type { Feature, FeatureCollection, Geometry, MultiPolygon, Polygon } from 'geojson';
import type { FillLayerSpecification, LineLayerSpecification } from 'maplibre-gl';
import conflictTensionData from '../../data/conflictTensionLayer.json';
import { naturalEarthCountries } from './worldBasemap';

// Owner-curated reference layer (docs/design/conflict-tension-layer-design.md). Entries are assessments, never Events
// and never ground truth; every popup states the review status.

export type ConflictEntry = {
  id: string;
  name: string;
  class: 'armed_conflict';
  parties: string[];
  geometry: Polygon | MultiPolygon;
  approximate: true;
  /** Countries whose territory the polygons cover; used only for precedence over tension tints. */
  countryCodes: string[];
  basis: string;
  asOf: string;
  ownerReviewed: boolean;
};

export type TensionEntry = {
  id: string;
  name: string;
  class: 'elevated_tension';
  parties: string[];
  /** ISO 3166-1 alpha-3 codes of the countries to shade. */
  geometry: string[];
  basis: string;
  asOf: string;
  ownerReviewed: boolean;
};

export type ConflictTensionEntry = ConflictEntry | TensionEntry;

export type ConflictTensionFile = {
  schemaVersion: number;
  fileVersion: number;
  updated: string;
  entries: ConflictTensionEntry[];
};

/** The bundled file; its shape is enforced by conflictTensionLayer.test.ts. */
export const conflictTensionFile = conflictTensionData as unknown as ConflictTensionFile;
export const conflictTensionEntries = conflictTensionFile.entries;

/** ISO 3166-1 alpha-3 to the numeric ids the bundled Natural Earth (world-atlas) countries carry. */
export const countryNumericIds: Readonly<Record<string, string>> = {
  AFG: '004', ALB: '008', BGR: '100', CHN: '156', CZE: '203', DEU: '276', DNK: '208', ESP: '724', EST: '233',
  FIN: '246', FRA: '250', GBR: '826', GRC: '300', IND: '356', IRN: '364', ISR: '376', ITA: '380', KOR: '410',
  LBN: '422', LTU: '440', LVA: '428', MDA: '498', MMR: '104', NLD: '528', NOR: '578', PAK: '586', POL: '616',
  PRK: '408', PSE: '275', ROU: '642', RUS: '643', SAU: '682', SDN: '729', SVK: '703', SWE: '752', TWN: '158',
  UKR: '804', USA: '840', YEM: '887',
};

export const conflictZoneSourceId = 'conflict-zones';
export const conflictZoneFillLayerId = 'conflict-zones-fill';
export const conflictZoneOutlineLayerId = 'conflict-zones-outline';
export const tensionSourceId = 'tension-countries';
export const tensionFillLayerId = 'tension-countries-fill';

// Conflict keeps the former conflict-zone styling; tension is a lighter, desaturated stone tint. Both are independent
// of the accent theme and of the Event category palette.
export const conflictZoneColors = { fill: '#9b5f64', outline: '#b0787c' } as const;
export const tensionColors = { fill: '#a89f8e' } as const;

export const conflictZoneFillPaint: FillLayerSpecification['paint'] = {
  'fill-color': conflictZoneColors.fill,
  'fill-opacity': 0.16,
};

export const conflictZoneOutlinePaint: LineLayerSpecification['paint'] = {
  'line-color': conflictZoneColors.outline,
  'line-opacity': 0.7,
  'line-width': 1,
};

export const tensionFillPaint: FillLayerSpecification['paint'] = {
  'fill-color': tensionColors.fill,
  'fill-opacity': 0.12,
};

export const conflictLegendLabel = 'Armed conflict (approximate area)';
export const tensionLegendLabel = 'Elevated tension';

export function isConflictEntry(entry: ConflictTensionEntry): entry is ConflictEntry {
  return entry.class === 'armed_conflict';
}

export function isTensionEntry(entry: ConflictTensionEntry): entry is TensionEntry {
  return entry.class === 'elevated_tension';
}

/**
 * Precedence: a country is tinted only if an elevated_tension entry lists it and no armed_conflict entry covers it.
 * Returns each tinted country (alpha-3) with the ids of the tension entries that list it, in file order.
 */
export function tintedTensionCountries(entries: readonly ConflictTensionEntry[]) {
  const coveredByConflict = new Set(entries.filter(isConflictEntry).flatMap((entry) => entry.countryCodes));
  const tinted = new Map<string, string[]>();
  for (const entry of entries.filter(isTensionEntry)) {
    for (const code of entry.geometry) {
      if (coveredByConflict.has(code)) continue;
      tinted.set(code, [...(tinted.get(code) ?? []), entry.id]);
    }
  }
  return tinted;
}

export function conflictZonesToFeatureCollection(entries: readonly ConflictTensionEntry[]): FeatureCollection<Polygon | MultiPolygon, { entryId: string }> {
  return {
    type: 'FeatureCollection',
    features: entries.filter(isConflictEntry).map((entry) => ({
      type: 'Feature',
      properties: { entryId: entry.id },
      geometry: entry.geometry,
    })),
  };
}

export function tensionCountriesToFeatureCollection(
  entries: readonly ConflictTensionEntry[],
  countries: FeatureCollection<Geometry> = naturalEarthCountries,
): FeatureCollection<Geometry, { countryCode: string; entryIds: string }> {
  const features: Feature<Geometry, { countryCode: string; entryIds: string }>[] = [];
  for (const [code, entryIds] of tintedTensionCountries(entries)) {
    const country = countries.features.find((item) => String(item.id) === countryNumericIds[code]);
    if (!country) continue;
    // MapLibre flattens feature properties, so the entry ids travel as one comma-separated string.
    features.push({ type: 'Feature', properties: { countryCode: code, entryIds: entryIds.join(',') }, geometry: country.geometry });
  }
  return { type: 'FeatureCollection', features };
}

/** Entry ids referenced by the clicked conflict or tension features, de-duplicated in order. */
export function entryIdsFromFeatureProperties(properties: ReadonlyArray<Record<string, unknown> | null | undefined>) {
  const ids: string[] = [];
  for (const item of properties) {
    const raw = [item?.entryId, ...(typeof item?.entryIds === 'string' ? item.entryIds.split(',') : [])];
    for (const id of raw) {
      if (typeof id === 'string' && id && !ids.includes(id)) ids.push(id);
    }
  }
  return ids;
}

function escapeHtml(value: unknown) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function conflictTensionStatusLine(entry: ConflictTensionEntry) {
  return entry.ownerReviewed ? 'Assessed, owner-curated' : 'Draft, not yet reviewed by the owner';
}

/** Bounded popup listing every entry under the pointer; it never selects or creates an Event. */
export function conflictTensionPopupHtml(entries: readonly ConflictTensionEntry[]) {
  return entries.map((entry) => [
    '<section>',
    `<strong>${escapeHtml(entry.name)}</strong>`,
    `<span>${isConflictEntry(entry) ? 'Armed conflict' : 'Elevated tension'}${isConflictEntry(entry) ? ' · approximate' : ''}</span>`,
    `<span>Parties: ${escapeHtml(entry.parties.join(', '))}</span>`,
    `<span>${escapeHtml(entry.basis)}</span>`,
    `<span>As of ${escapeHtml(entry.asOf)}</span>`,
    `<small>${conflictTensionStatusLine(entry)}</small>`,
    '</section>',
  ].join('')).join('');
}
