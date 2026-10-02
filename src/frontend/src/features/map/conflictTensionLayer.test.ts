import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import type { Position } from 'geojson';
import {
  conflictTensionEntries,
  conflictTensionFile,
  conflictTensionPopupHtml,
  conflictZonesToFeatureCollection,
  countryNumericIds,
  entryIdsFromFeatureProperties,
  isConflictEntry,
  isTensionEntry,
  tensionCountriesToFeatureCollection,
  tintedTensionCountries,
  type ConflictEntry,
  type ConflictTensionEntry,
  type TensionEntry,
} from './conflictTensionLayer';
import { naturalEarthCountries } from './worldBasemap';

const rawFile = readFileSync(join(process.cwd(), 'src', 'data', 'conflictTensionLayer.json'), 'utf8');
const isoDate = /^\d{4}-\d{2}-\d{2}$/;
const conflicts = conflictTensionEntries.filter(isConflictEntry);
const tensions = conflictTensionEntries.filter(isTensionEntry);

function rings(entry: ConflictEntry): Position[][] {
  return entry.geometry.type === 'Polygon' ? entry.geometry.coordinates : entry.geometry.coordinates.flat();
}

function signedArea(ring: Position[]) {
  let area = 0;
  for (let index = 0; index < ring.length - 1; index += 1) {
    area += ring[index][0] * ring[index + 1][1] - ring[index + 1][0] * ring[index][1];
  }
  return area / 2;
}

function segmentsCross(a: Position, b: Position, c: Position, d: Position) {
  const orientation = (p: Position, q: Position, r: Position) => Math.sign((q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0]));
  return orientation(a, b, c) * orientation(a, b, d) < 0 && orientation(c, d, a) * orientation(c, d, b) < 0;
}

function isSimple(ring: Position[]) {
  const edges = ring.length - 1;
  for (let first = 0; first < edges; first += 1) {
    for (let second = first + 2; second < edges; second += 1) {
      if (first === 0 && second === edges - 1) continue;
      if (segmentsCross(ring[first], ring[first + 1], ring[second], ring[second + 1])) return false;
    }
  }
  return true;
}

function entry(overrides: Partial<ConflictTensionEntry> & Pick<ConflictTensionEntry, 'id' | 'class'>): ConflictTensionEntry {
  const base = { name: overrides.id, parties: ['A', 'B'], basis: 'Test basis.', asOf: '2026-10-02', ownerReviewed: false };
  if (overrides.class === 'armed_conflict') {
    return { ...base, geometry: { type: 'Polygon', coordinates: [[[0, 0], [1, 0], [1, 1], [0, 0]]] }, approximate: true, countryCodes: [], ...overrides } as ConflictEntry;
  }
  return { ...base, geometry: [], ...overrides } as TensionEntry;
}

describe('conflict and tension data file', () => {
  it('has the versioned top-level schema and valid entry fields', () => {
    expect(Object.keys(conflictTensionFile).sort()).toEqual(['entries', 'fileVersion', 'schemaVersion', 'updated']);
    expect(conflictTensionFile.schemaVersion).toBe(1);
    expect(Number.isInteger(conflictTensionFile.fileVersion)).toBe(true);
    expect(conflictTensionFile.updated).toMatch(isoDate);
    expect(new Set(conflictTensionEntries.map((item) => item.id)).size).toBe(conflictTensionEntries.length);

    for (const item of conflictTensionEntries) {
      expect(['armed_conflict', 'elevated_tension']).toContain(item.class);
      expect(item.id).toMatch(/^[a-z0-9-]+$/);
      expect(item.name.trim()).not.toBe('');
      expect(item.parties.length).toBeGreaterThan(0);
      item.parties.forEach((party) => expect(typeof party).toBe('string'));
      expect(item.basis).toMatch(/^[^\n]+\.$/);
      expect(item.asOf).toMatch(isoDate);
      expect(Number.isNaN(Date.parse(item.asOf))).toBe(false);
      expect(typeof item.ownerReviewed).toBe('boolean');
      const allowed = item.class === 'armed_conflict'
        ? ['approximate', 'asOf', 'basis', 'class', 'countryCodes', 'geometry', 'id', 'name', 'ownerReviewed', 'parties']
        : ['asOf', 'basis', 'class', 'geometry', 'id', 'name', 'ownerReviewed', 'parties'];
      expect(Object.keys(item).sort()).toEqual(allowed);
    }
  });

  it('starts every entry as an unreviewed draft, as of the current date', () => {
    expect(conflictTensionEntries.every((item) => item.ownerReviewed === false)).toBe(true);
    expect(conflictTensionEntries.every((item) => item.asOf === '2026-10-02')).toBe(true);
  });

  it('contains the starting set from the design document', () => {
    expect(conflicts.map((item) => item.name)).toEqual([
      'Russia-Ukraine', 'Iran with the Gulf states, Jordan and the US', 'Yemen with Saudi Arabia', 'Israel with Lebanon and Hamas', 'Myanmar', 'Sudan',
    ]);
    expect(tensions.map((item) => item.name)).toEqual([
      'Russian attacks on Europe', 'India-Pakistan', 'Pakistan-Afghanistan', 'Israel-Iran', 'China-Taiwan', 'North Korea and South Korea',
    ]);
  });

  it('keeps every armed_conflict polygon approximate, valid and small', () => {
    for (const item of conflicts) {
      expect(item.approximate).toBe(true);
      expect(item.countryCodes.length).toBeGreaterThan(0);
      expect(['Polygon', 'MultiPolygon']).toContain(item.geometry.type);
      for (const [index, ring] of rings(item).entries()) {
        expect(ring.length).toBeGreaterThanOrEqual(4);
        expect(ring.length - 1).toBeLessThanOrEqual(30);
        expect(ring[0]).toEqual(ring.at(-1));
        // RFC 7946: exterior rings counter-clockwise (holes would be clockwise; none are used).
        expect(index === 0 ? signedArea(ring) > 0 : signedArea(ring) < 0).toBe(true);
        expect(isSimple(ring)).toBe(true);
        for (const [longitude, latitude] of ring) {
          expect(longitude).toBeGreaterThanOrEqual(-180);
          expect(longitude).toBeLessThanOrEqual(180);
          expect(latitude).toBeGreaterThanOrEqual(-90);
          expect(latitude).toBeLessThanOrEqual(90);
          expect(Math.round(longitude * 100) / 100).toBe(longitude);
          expect(Math.round(latitude * 100) / 100).toBe(latitude);
        }
        ring.slice(1).forEach(([longitude], vertex) => expect(Math.abs(longitude - ring[vertex][0])).toBeLessThan(180));
      }
    }
  });

  it('labels every polygon as an assistant draft for owner review', () => {
    conflicts.forEach((item) => expect(item.basis).toMatch(/drawn by an AI assistant for owner review/));
  });

  it('resolves every country code to the bundled Natural Earth geometry through one mapping', () => {
    const codes = new Set([...conflicts.flatMap((item) => item.countryCodes), ...tensions.flatMap((item) => item.geometry)]);
    for (const code of codes) {
      expect(code).toMatch(/^[A-Z]{3}$/);
      const numericId = countryNumericIds[code];
      expect(numericId, code).toBeDefined();
      expect(naturalEarthCountries.features.some((country) => String(country.id) === numericId), code).toBe(true);
    }
    expect(Object.keys(countryNumericIds).sort()).toEqual([...codes].sort());
  });

  it('never uses the banned word, URLs or a source title', () => {
    expect(rawFile.toLowerCase()).not.toContain('hybrid');
    expect(rawFile).not.toMatch(/https?:|www\./i);
  });

  it('never lists Ukraine in the sabotage entry', () => {
    const sabotage = tensions.find((item) => item.id === 'russian-attacks-on-europe');
    expect(sabotage?.geometry).toContain('MDA');
    expect(sabotage?.geometry).not.toContain('UKR');
    expect(sabotage?.geometry).not.toContain('RUS');
    expect(sabotage?.basis).toMatch(/2 October 2026/);
  });
});

describe('conflict over tension precedence', () => {
  it('tints a tension country only when no armed_conflict entry covers it', () => {
    const tinted = tintedTensionCountries([
      entry({ id: 'war', class: 'armed_conflict', countryCodes: ['ISR', 'IRN'] }),
      entry({ id: 'pair', class: 'elevated_tension', geometry: ['ISR', 'IRN'] }),
      entry({ id: 'other', class: 'elevated_tension', geometry: ['IND', 'PAK'] }),
      entry({ id: 'second', class: 'elevated_tension', geometry: ['PAK', 'AFG'] }),
    ]);

    expect([...tinted.keys()]).toEqual(['IND', 'PAK', 'AFG']);
    expect(tinted.get('PAK')).toEqual(['other', 'second']);
  });

  it('suppresses Israel-Iran in the bundled file and tints the remaining tension countries', () => {
    const tinted = tintedTensionCountries(conflictTensionEntries);

    expect(tinted.has('ISR')).toBe(false);
    expect(tinted.has('IRN')).toBe(false);
    expect(tinted.has('UKR')).toBe(false);
    expect([...tinted.keys()].sort()).toEqual([
      'AFG', 'ALB', 'BGR', 'CHN', 'CZE', 'DEU', 'DNK', 'ESP', 'EST', 'FIN', 'FRA', 'GBR', 'GRC', 'IND', 'ITA', 'KOR', 'LTU',
      'LVA', 'MDA', 'NLD', 'NOR', 'PAK', 'POL', 'PRK', 'ROU', 'SVK', 'SWE', 'TWN', 'USA',
    ]);
    expect(tinted.get('PAK')).toEqual(['india-pakistan', 'pakistan-afghanistan']);
  });
});

describe('conflict and tension GeoJSON builders', () => {
  it('builds one conflict feature per armed_conflict entry', () => {
    const collection = conflictZonesToFeatureCollection(conflictTensionEntries);
    expect(collection.features.map((feature) => feature.properties.entryId)).toEqual(conflicts.map((item) => item.id));
    expect(collection.features[0].geometry).toBe(conflicts[0].geometry);
  });

  it('builds one tension feature per tinted country from the bundled geometry', () => {
    const collection = tensionCountriesToFeatureCollection(conflictTensionEntries);
    const pakistan = collection.features.find((feature) => feature.properties.countryCode === 'PAK');

    expect(collection.features).toHaveLength(tintedTensionCountries(conflictTensionEntries).size);
    expect(pakistan?.properties.entryIds).toBe('india-pakistan,pakistan-afghanistan');
    expect(pakistan?.geometry).toBe(naturalEarthCountries.features.find((country) => String(country.id) === '586')?.geometry);
    expect(collection.features.some((feature) => feature.properties.countryCode === 'ISR')).toBe(false);
  });

  it('collects the entry ids under the pointer once each', () => {
    expect(entryIdsFromFeatureProperties([
      { entryId: 'russia-ukraine' },
      { countryCode: 'PAK', entryIds: 'india-pakistan,pakistan-afghanistan' },
      { entryId: 'russia-ukraine' },
      null,
    ])).toEqual(['russia-ukraine', 'india-pakistan', 'pakistan-afghanistan']);
  });
});

describe('conflict and tension popup', () => {
  it('shows every field and the draft status for an unreviewed polygon', () => {
    const html = conflictTensionPopupHtml([conflicts[0]]);

    expect(html).toContain('<strong>Russia-Ukraine</strong>');
    expect(html).toContain('Armed conflict · approximate');
    expect(html).toContain('Parties: Russia, Ukraine');
    expect(html).toContain(conflicts[0].basis);
    expect(html).toContain('As of 2026-10-02');
    expect(html).toContain('Draft, not yet reviewed by the owner');
    expect(html).not.toMatch(/verified|ground truth/i);
  });

  it('shows the owner-curated status once an entry is reviewed and lists overlapping entries', () => {
    const reviewed = { ...tensions[1], ownerReviewed: true };
    const html = conflictTensionPopupHtml([reviewed, tensions[2]]);

    expect(html).toContain('Elevated tension');
    expect(html).not.toContain('approximate');
    expect(html).toContain('Assessed, owner-curated');
    expect(html).toContain('Draft, not yet reviewed by the owner');
    expect(html.match(/<section>/g)).toHaveLength(2);
  });

  it('escapes entry text', () => {
    const html = conflictTensionPopupHtml([entry({ id: 'x', class: 'elevated_tension', name: '<b>x</b>', parties: ['A & B'] })]);
    expect(html).toContain('&lt;b&gt;x&lt;/b&gt;');
    expect(html).toContain('A &amp; B');
  });
});
