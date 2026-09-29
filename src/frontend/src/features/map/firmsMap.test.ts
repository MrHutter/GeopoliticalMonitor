import type { FirmsObservation } from '../../nativeBridge';
import {
  classifyFirmsAge,
  filterFirmsObservations,
  firmsObservationsToFeatureCollection,
  firmsPopupHtml,
  type FirmsDisplayMode,
} from './firmsMap';

const observation: FirmsObservation = {
  id: 'firms-0123456789abcdef0123456789abcdef',
  latitude: 60.1234,
  longitude: 24.5678,
  acquiredAtUtc: '2026-09-28T14:00:00Z',
  source: 'VIIRS_NOAA20_NRT',
  satellite: '1',
  confidence: 'Nominal',
  frp: 18.6,
  brightnessTemperatureI4: 331.2,
  brightnessTemperatureI5: 290.4,
  scan: 0.42,
  track: 0.51,
  processingVersion: '2.0NRT',
  dayNight: 'Day',
};

describe('firmsMap', () => {
  it.each([
    ['significant', 'Nominal', 10, true],
    ['significant', 'High', 10, true],
    ['significant', 'Nominal', 9.99, false],
    ['significant', 'High', 9.99, false],
    ['significant', 'Low', 500, false],
    ['strong', 'Nominal', 50, true],
    ['strong', 'High', 50, true],
    ['strong', 'Nominal', 49.99, false],
    ['strong', 'Low', 500, false],
  ] as const)('%s filter handles %s confidence at %s MW', (mode, confidence, frp, included) => {
    const candidate = { ...observation, confidence, frp };
    expect(filterFirmsObservations([candidate], mode)).toHaveLength(included ? 1 : 0);
  });

  it('returns all detections without mutating the complete snapshot', () => {
    const snapshot = Object.freeze([
      Object.freeze({ ...observation, id: 'firms-11111111111111111111111111111111', confidence: 'Low' as const, frp: 1 }),
      Object.freeze({ ...observation, id: 'firms-22222222222222222222222222222222', confidence: 'High' as const, frp: 60 }),
    ]);

    const result = filterFirmsObservations(snapshot, 'all');

    expect(result).toEqual(snapshot);
    expect(result).not.toBe(snapshot);
    expect(snapshot).toHaveLength(2);
  });

  it.each([
    ['significant', 1],
    ['strong', 0],
    ['all', 2],
  ] as Array<[FirmsDisplayMode, number]>)('derives the %s subset from the same snapshot', (mode, expected) => {
    const snapshot = [observation, { ...observation, id: 'firms-33333333333333333333333333333333', confidence: 'Low' as const, frp: 100 }];
    expect(filterFirmsObservations(snapshot, mode)).toHaveLength(expected);
    expect(snapshot).toHaveLength(2);
  });

  it.each([
    ['2026-09-28T14:00:01Z', 'under-3h'],
    ['2026-09-28T12:00:00Z', '3-6h'],
    ['2026-09-28T09:00:00Z', '6-12h'],
    ['2026-09-28T03:00:00Z', '12-24h'],
  ] as const)('classifies %s into %s', (acquiredAtUtc, expected) => {
    expect(classifyFirmsAge(acquiredAtUtc, '2026-09-28T15:00:00Z')).toBe(expected);
  });

  it('adapts observations into an independent bounded point collection', () => {
    const result = firmsObservationsToFeatureCollection([observation], '2026-09-28T15:00:00Z');
    expect(result.features).toEqual([
      expect.objectContaining({
        id: observation.id,
        geometry: { type: 'Point', coordinates: [24.5678, 60.1234] },
        properties: expect.objectContaining({ source: 'VIIRS_NOAA20_NRT', ageBucket: 'under-3h' }),
      }),
    ]);
    expect(result.features[0].properties).not.toHaveProperty('latitude');
    expect(result.features[0].properties).not.toHaveProperty('longitude');
  });

  it('formats a bounded attributed popup without HTML injection', () => {
    const html = firmsPopupHtml({ ...observation, confidence: '<script>' }, observation.longitude, observation.latitude);
    expect(html).toContain('NASA FIRMS — Fire / Thermal Anomaly');
    expect(html).toContain('NOAA-20');
    expect(html).toContain('&lt;script&gt;');
    expect(html).toContain('FRP 18.6 MW');
    expect(html).toContain('NASA FIRMS / LANCE');
    expect(html).not.toContain('<script>');
  });
});
