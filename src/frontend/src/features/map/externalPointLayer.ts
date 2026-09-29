import type { FeatureCollection, Point } from 'geojson';
import type { CircleLayerSpecification, Map as MapLibreMapInstance } from 'maplibre-gl';

export type ExternalPointLayerDefinition = {
  sourceId: string;
  layer: CircleLayerSpecification;
  data: FeatureCollection<Point>;
};

export function addExternalPointLayer(map: MapLibreMapInstance, definition: ExternalPointLayerDefinition) {
  map.addSource(definition.sourceId, { type: 'geojson', data: definition.data, cluster: false, generateId: false });
  map.addLayer(definition.layer);
}
