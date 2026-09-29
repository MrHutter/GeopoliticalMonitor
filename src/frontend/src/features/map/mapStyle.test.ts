import type { FeatureCollection, Geometry, Position } from 'geojson';
import {
  countryBoundaryLayerId,
  countryFillLayerId,
  countrySourceId,
  createLocalMapStyle,
  mapStyleSources,
} from './mapStyle';
import { naturalEarthCountries, naturalEarthLand } from './worldBasemap';

function geometryRings(geometry: Geometry): Position[][] {
  if (geometry.type === 'Polygon') {
    return geometry.coordinates;
  }

  if (geometry.type === 'MultiPolygon') {
    return geometry.coordinates.flat();
  }

  return [];
}

function allRings(featureCollection: FeatureCollection<Geometry>) {
  return featureCollection.features.flatMap((feature) => geometryRings(feature.geometry));
}

function expectValidCoordinateRanges(featureCollection: FeatureCollection<Geometry>) {
  allRings(featureCollection).forEach((ring) => {
    ring.forEach(([longitude, latitude]) => {
      expect(longitude).toBeGreaterThanOrEqual(-180);
      expect(longitude).toBeLessThanOrEqual(180);
      expect(latitude).toBeGreaterThanOrEqual(-90);
      expect(latitude).toBeLessThanOrEqual(90);
    });
  });
}

function expectNoUnhandledLongitudeJumps(featureCollection: FeatureCollection<Geometry>) {
  allRings(featureCollection).forEach((ring) => {
    ring.forEach(([longitude], index) => {
      if (index === 0) {
        return;
      }

      const [previousLongitude] = ring[index - 1];
      expect(Math.abs(longitude - previousLongitude)).toBeLessThanOrEqual(180);
    });
  });
}

describe('mapStyle', () => {
  it('builds a local non-empty Natural Earth basemap style with artifact-free country layers', () => {
    const style = createLocalMapStyle();

    expect(naturalEarthLand.features).toHaveLength(1);
    expect(mapStyleSources.countries.features).toHaveLength(177);
    expect(style.sources[countrySourceId]).toMatchObject({ type: 'geojson', data: mapStyleSources.countries });
    expect(style.layers.map((layer) => layer.id)).toEqual([
      'ocean-background',
      countryFillLayerId,
      countryBoundaryLayerId,
    ]);

    const oceanLayer = style.layers.find((layer) => layer.id === 'ocean-background');
    expect(oceanLayer).toMatchObject({ type: 'background', paint: { 'background-color': '#000000' } });

    const fillLayer = style.layers.find((layer) => layer.id === countryFillLayerId);
    expect(fillLayer).toMatchObject({ type: 'fill', source: countrySourceId, paint: { 'fill-color': '#191a1c', 'fill-opacity': 0.94 } });

    const boundaryLayer = style.layers.find((layer) => layer.id === countryBoundaryLayerId);
    expect(boundaryLayer).toMatchObject({ type: 'line', source: countrySourceId, paint: { 'line-color': '#686d75', 'line-opacity': 0.58 } });
    expect(style.sources[countrySourceId]).toMatchObject({ data: naturalEarthCountries });
  });

  it('removes the former Ukraine-Crimea shared arc from the rendered boundary input', () => {
    const formerBoundaryInterior = [34.41094410944109, 46.00431922049941];
    const hasFormerBoundaryCoordinate = allRings(mapStyleSources.countries).some((ring) => (
      ring.some(([longitude, latitude]) => (
        Math.abs(longitude - formerBoundaryInterior[0]) < 1e-9
        && Math.abs(latitude - formerBoundaryInterior[1]) < 1e-9
      ))
    ));

    expect(hasFormerBoundaryCoordinate).toBe(false);
    const style = createLocalMapStyle();
    expect(style.layers.find((layer) => layer.id === countryBoundaryLayerId))
      .toMatchObject({ type: 'line', source: countrySourceId });
    expect(style.sources[countrySourceId]).toMatchObject({ data: mapStyleSources.countries });
  });

  it('keeps rendered world coordinates valid and removes unhandled antimeridian jumps', () => {
    expectValidCoordinateRanges(mapStyleSources.countries);
    expectNoUnhandledLongitudeJumps(mapStyleSources.countries);
  });
});
