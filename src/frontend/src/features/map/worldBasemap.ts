import type { Feature, FeatureCollection, Geometry, MultiPolygon, Polygon, Position } from 'geojson';
import { feature, merge } from 'topojson-client';
import worldTopology from 'world-atlas/countries-110m.json';

type TopologyPolygon = {
  type: 'Polygon';
  arcs: number[][];
  id?: string | number;
};

type TopologyMultiPolygon = {
  type: 'MultiPolygon';
  arcs: number[][][];
  id?: string | number;
};

type TopologyCountryGeometry = TopologyPolygon | TopologyMultiPolygon;

const topology = worldTopology as unknown as {
  objects: {
    countries: {
      geometries: TopologyCountryGeometry[];
    };
    land: unknown;
  };
};

const russiaCountryId = '643';
const ukraineCountryId = '804';
export const crimeaRepresentativeCoordinate = [34.1, 44.95] as const satisfies Position;

function geometryPolygons(geometry: Geometry): Position[][][] {
  if (geometry.type === 'Polygon') {
    return [geometry.coordinates];
  }

  if (geometry.type === 'MultiPolygon') {
    return geometry.coordinates;
  }

  return [];
}

function ringContainsCoordinate(ring: Position[], [longitude, latitude]: Position) {
  let inside = false;
  for (let index = 0, previous = ring.length - 1; index < ring.length; previous = index, index += 1) {
    const [currentLongitude, currentLatitude] = ring[index];
    const [previousLongitude, previousLatitude] = ring[previous];
    if ((currentLatitude > latitude) !== (previousLatitude > latitude)
      && longitude < ((previousLongitude - currentLongitude) * (latitude - currentLatitude))
        / (previousLatitude - currentLatitude) + currentLongitude) {
      inside = !inside;
    }
  }
  return inside;
}

function polygonContainsCoordinate(polygon: Position[][], coordinate: Position) {
  return ringContainsCoordinate(polygon[0], coordinate)
    && !polygon.slice(1).some((hole) => ringContainsCoordinate(hole, coordinate));
}

export function countryNamesAtCoordinate(
  featureCollection: FeatureCollection<Geometry>,
  coordinate: Position,
) {
  return featureCollection.features
    .filter((country) => geometryPolygons(country.geometry)
      .some((polygon) => polygonContainsCoordinate(polygon, coordinate)))
    .map((country) => String(country.properties?.name ?? ''))
    .filter(Boolean);
}

function topologyPolygons(geometry: TopologyCountryGeometry) {
  return geometry.type === 'Polygon' ? [geometry.arcs] : geometry.arcs;
}

function applyInternationalSovereigntyOverrides(featureCollection: FeatureCollection<Geometry>) {
  const russiaFeature = featureCollection.features.find((country) => String(country.id) === russiaCountryId);
  const ukraineFeature = featureCollection.features.find((country) => String(country.id) === ukraineCountryId);
  const russiaTopology = topology.objects.countries.geometries
    .find((country) => String(country.id) === russiaCountryId);
  const ukraineTopology = topology.objects.countries.geometries
    .find((country) => String(country.id) === ukraineCountryId);
  if (!russiaFeature || !ukraineFeature || !russiaTopology || !ukraineTopology) {
    throw new Error('Expected Russia and Ukraine topology in the bundled sovereign basemap.');
  }

  const russiaPolygons = topologyPolygons(russiaTopology);
  const crimeaCandidates = russiaPolygons
    .map((arcs) => ({
      arcs,
      geometry: (feature(
        worldTopology as never,
        { type: 'Polygon', arcs } as never,
      ) as unknown as Feature<Polygon>).geometry,
    }))
    .filter((candidate) => polygonContainsCoordinate(
      candidate.geometry.coordinates,
      crimeaRepresentativeCoordinate,
    ));
  if (crimeaCandidates.length !== 1) {
    throw new Error('Expected one Crimea polygon in the bundled Russia topology.');
  }

  const crimeaTopology: TopologyPolygon = {
    type: 'Polygon',
    arcs: crimeaCandidates[0].arcs,
  };
  const correctedRussiaTopology: TopologyMultiPolygon = {
    type: 'MultiPolygon',
    arcs: russiaPolygons.filter((arcs) => arcs !== crimeaTopology.arcs),
  };
  const correctedRussia: Feature<Geometry> = {
    ...russiaFeature,
    geometry: (feature(
      worldTopology as never,
      correctedRussiaTopology as never,
    ) as unknown as Feature<MultiPolygon>).geometry,
  };
  const correctedUkraine: Feature<Geometry> = {
    ...ukraineFeature,
    // TopoJSON merge dissolves the former shared Ukraine/Crimea arc while
    // retaining the complete external coastline for the rendered line layer.
    geometry: merge(
      worldTopology as never,
      [ukraineTopology, crimeaTopology] as never,
    ),
  };

  return {
    ...featureCollection,
    features: featureCollection.features.map((country) => {
      if (country === russiaFeature) return correctedRussia;
      if (country === ukraineFeature) return correctedUkraine;
      return country;
    }),
  } satisfies FeatureCollection<Geometry>;
}

function hasAntimeridianJump(ring: Position[]) {
  return ring.some(([longitude], index) => index > 0 && Math.abs(longitude - ring[index - 1][0]) > 180);
}

function collapseDatelineEndpoint(position: Position, targetLongitude: 180 | -180) {
  return [targetLongitude, position[1], ...position.slice(2)] satisfies Position;
}

function normalizeAntimeridianRing(ring: Position[]) {
  if (!hasAntimeridianJump(ring)) {
    return ring;
  }

  const normalized = ring.map((position) => [...position] satisfies Position);

  for (let index = 1; index < normalized.length; index += 1) {
    const previous = normalized[index - 1];
    const current = normalized[index];
    if (Math.abs(current[0] - previous[0]) > 180) {
      normalized[index] = collapseDatelineEndpoint(current, previous[0] >= 0 ? 180 : -180);
    }
  }

  return normalized;
}

function normalizeAntimeridianGeometry(geometry: Geometry): Geometry {
  if (geometry.type === 'Polygon') {
    return {
      ...geometry,
      coordinates: geometry.coordinates.map(normalizeAntimeridianRing),
    } satisfies Polygon;
  }

  if (geometry.type === 'MultiPolygon') {
    return {
      ...geometry,
      coordinates: geometry.coordinates.map((polygon) => polygon.map(normalizeAntimeridianRing)),
    } satisfies MultiPolygon;
  }

  return geometry;
}

function normalizeAntimeridianFeature(feature: Feature<Geometry>) {
  return {
    ...feature,
    geometry: normalizeAntimeridianGeometry(feature.geometry),
  } satisfies Feature<Geometry>;
}

function normalizeAntimeridianFeatureCollection(featureCollection: FeatureCollection<Geometry>) {
  return {
    ...featureCollection,
    features: featureCollection.features.map(normalizeAntimeridianFeature),
  } satisfies FeatureCollection<Geometry>;
}

const rawNaturalEarthCountries = feature(
  worldTopology as never,
  topology.objects.countries as never,
) as unknown as FeatureCollection<Geometry>;

const rawNaturalEarthLand = feature(
  worldTopology as never,
  topology.objects.land as never,
) as unknown as FeatureCollection<Geometry>;

export const naturalEarthCountries = normalizeAntimeridianFeatureCollection(
  applyInternationalSovereigntyOverrides(rawNaturalEarthCountries),
);
export const naturalEarthLand = normalizeAntimeridianFeatureCollection(rawNaturalEarthLand);
