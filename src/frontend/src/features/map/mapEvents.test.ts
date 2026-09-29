import type { NativeEvent } from '../../types/events';
import type { Accent } from '../../types/mock';
import { eventsToFeatureCollection, getCategoryColor, getEventCoordinates, mockConflictZones, selectedMarkerPaint } from './mapEvents';

const nativeEvents: NativeEvent[] = [
  {
    id: 'evt-demo-red-sea-shipping',
    title: 'Commercial shipping disruption reported in Red Sea demo lane',
    summary: 'Demo summary',
    category: 'Infrastructure',
    status: 'Active',
    startedAtUtc: '2026-09-17T11:32:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:38:00Z',
    location: { latitude: 15.8, longitude: 41.9, displayName: 'Southern transit lane', region: 'Red Sea demo corridor' },
    createdAtUtc: '2026-09-17T06:00:00Z',
    updatedAtUtc: '2026-09-17T11:38:00Z',
    sourceCount: 3,
  },
  {
    id: 'evt-demo-baltic-security',
    title: 'Reported security activity near Baltic demo border area',
    summary: 'Demo summary',
    category: 'Security',
    status: 'Developing',
    startedAtUtc: '2026-09-17T11:48:00Z',
    lastUpdatedAtUtc: '2026-09-17T11:54:00Z',
    location: { latitude: 57.4, longitude: 24.8, displayName: 'Northern corridor', region: 'Baltic demo region' },
    createdAtUtc: '2026-09-17T06:00:00Z',
    updatedAtUtc: '2026-09-17T11:54:00Z',
    sourceCount: 3,
  },
];

describe('mapEvents', () => {
  it('adapts native event geographic data into a MapLibre GeoJSON source', () => {
    const collection = eventsToFeatureCollection(nativeEvents);
    const redSea = collection.features.find((feature) => feature.properties.id === 'evt-demo-red-sea-shipping');

    expect(collection.features).toHaveLength(nativeEvents.length);
    expect(redSea).toBeDefined();
    expect(redSea?.geometry.coordinates).toEqual([41.9, 15.8]);
    expect(redSea?.properties).toMatchObject({
      title: 'Commercial shipping disruption reported in Red Sea demo lane',
      category: 'Infrastructure',
      sourceCount: 3,
    });
  });

  it('resolves event coordinates as valid longitude and latitude pairs', () => {
    expect(getEventCoordinates(nativeEvents[0])).toEqual([41.9, 15.8]);

    eventsToFeatureCollection(nativeEvents).features.forEach((feature) => {
      const [longitude, latitude] = feature.geometry.coordinates;
      expect(longitude).toBeGreaterThanOrEqual(-180);
      expect(longitude).toBeLessThanOrEqual(180);
      expect(latitude).toBeGreaterThanOrEqual(-90);
      expect(latitude).toBeLessThanOrEqual(90);
    });
  });

  it('changes selected-marker accent treatment without changing semantic colors', () => {
    const expectedSelectionColors: Record<Accent, string> = {
      orange: '#ff8a1f',
      blue: '#22d3ff',
      red: '#ff4d5e',
      green: '#3dff87',
      yellow: '#ffe45c',
    };

    Object.entries(expectedSelectionColors).forEach(([accent, color]) => {
      expect(selectedMarkerPaint(accent as Accent)['circle-stroke-color']).toBe(color);
      expect(selectedMarkerPaint(accent as Accent)['circle-color']).toBe(color);
    });

    expect(getCategoryColor('MilitaryConflict')).toBe('#d95d62');
    expect(getCategoryColor('Security')).toBe('#6da3d9');
    expect(getCategoryColor('NaturalDisaster')).toBe('#79a86b');
  });

  it('keeps mock conflict zones as bounded regional polygons', () => {
    expect(mockConflictZones.features).toHaveLength(2);

    mockConflictZones.features.forEach((feature) => {
      const coordinates = feature.geometry.coordinates.flat();
      const longitudes = coordinates.map(([longitude]) => longitude);
      const latitudes = coordinates.map(([, latitude]) => latitude);

      expect(Math.max(...longitudes) - Math.min(...longitudes)).toBeLessThanOrEqual(20);
      expect(Math.max(...latitudes) - Math.min(...latitudes)).toBeLessThanOrEqual(12);
      longitudes.forEach((longitude) => {
        expect(longitude).toBeGreaterThanOrEqual(-180);
        expect(longitude).toBeLessThanOrEqual(180);
      });
      latitudes.forEach((latitude) => {
        expect(latitude).toBeGreaterThanOrEqual(-90);
        expect(latitude).toBeLessThanOrEqual(90);
      });
    });
  });
});
