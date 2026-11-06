import { useEffect, useMemo, useRef } from 'react';
import * as maplibregl from 'maplibre-gl';
import type { GeoJSONSource, MapLayerMouseEvent, Map as MapLibreMapInstance, Popup } from 'maplibre-gl';
import 'maplibre-gl/dist/maplibre-gl.css';
import mapLibreWorkerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url';
import { Accent, MapLayer } from '../../types/mock';
import type { FirmsObservation, FirmsViewport } from '../../nativeBridge';
import { NativeEvent } from '../../types/events';
import {
  conflictTensionEntries,
  conflictTensionPopupHtml,
  conflictZoneFillLayerId,
  conflictZoneFillPaint,
  conflictZoneOutlineLayerId,
  conflictZoneOutlinePaint,
  conflictZoneSourceId,
  conflictZonesToFeatureCollection,
  entryIdsFromFeatureProperties,
  tensionCountriesToFeatureCollection,
  tensionFillLayerId,
  tensionFillPaint,
  tensionSourceId,
} from './conflictTensionLayer';
import { addExternalPointLayer } from './externalPointLayer';
import {
  firmsCirclePaint,
  firmsLayerId,
  firmsObservationsToFeatureCollection,
  firmsPopupHtml,
  firmsSourceId,
} from './firmsMap';
import {
  categoryColorExpression,
  severityRadiusExpression,
  severityStrokeColorExpression,
  severityStrokeWidthExpression,
  defaultMapView,
  eventLayerId,
  eventSourceId,
  eventsToFeatureCollection,
  eventTooltipContent,
  getEventCoordinates,
  locateMapView,
  selectedEventLayerId,
  selectedMarkerPaint,
} from './mapEvents';
import { createLocalMapStyle } from './mapStyle';

maplibregl.setWorkerUrl(mapLibreWorkerUrl);
maplibregl.setWorkerCount(1);

type LocateRequest = {
  eventId: string;
  requestId: number;
};

type MapLibreWorldMapProps = {
  accent: Accent;
  events: NativeEvent[];
  layers: MapLayer[];
  selectedEventId: string;
  locateRequest: LocateRequest | null;
  firmsObservations: FirmsObservation[];
  firmsRefreshedAtUtc: string | null;
  onSelectEvent: (eventId: string) => void;
  onViewportChange: (viewport: FirmsViewport) => void;
};

function isLayerEnabled(layers: MapLayer[], layerId: string) {
  return layers.find((layer) => layer.id === layerId)?.enabled ?? false;
}

function setLayerVisibility(map: MapLibreMapInstance, layerIds: string[], visible: boolean) {
  const visibility = visible ? 'visible' : 'none';
  layerIds.forEach((layerId) => {
    if (map.getLayer(layerId)) {
      map.setLayoutProperty(layerId, 'visibility', visibility);
    }
  });
}

function getMapFeatureId(event: MapLayerMouseEvent) {
  const id = event.features?.[0]?.properties?.id;
  return typeof id === 'string' ? id : undefined;
}

export function MapLibreWorldMap({
  accent,
  events,
  layers,
  selectedEventId,
  locateRequest,
  firmsObservations,
  firmsRefreshedAtUtc,
  onSelectEvent,
  onViewportChange,
}: MapLibreWorldMapProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const mapRef = useRef<MapLibreMapInstance | null>(null);
  const popupRef = useRef<Popup | null>(null);
  const firmsPopupRef = useRef<Popup | null>(null);
  const referencePopupRef = useRef<Popup | null>(null);
  const onSelectEventRef = useRef(onSelectEvent);
  const eventsRef = useRef(events);
  const selectedEventIdRef = useRef(selectedEventId);
  const firmsObservationsRef = useRef(firmsObservations);
  const firmsRefreshedAtUtcRef = useRef(firmsRefreshedAtUtc);
  const onViewportChangeRef = useRef(onViewportChange);
  const mapStyle = useMemo(() => createLocalMapStyle(), []);

  useEffect(() => {
    onSelectEventRef.current = onSelectEvent;
  }, [onSelectEvent]);

  useEffect(() => {
    eventsRef.current = events;
  }, [events]);

  useEffect(() => {
    selectedEventIdRef.current = selectedEventId;
  }, [selectedEventId]);

  useEffect(() => {
    firmsObservationsRef.current = firmsObservations;
    firmsRefreshedAtUtcRef.current = firmsRefreshedAtUtc;
  }, [firmsObservations, firmsRefreshedAtUtc]);

  useEffect(() => {
    onViewportChangeRef.current = onViewportChange;
  }, [onViewportChange]);

  useEffect(() => {
    if (!containerRef.current || mapRef.current) {
      return;
    }

    const container = containerRef.current;
    const map = new maplibregl.Map({
      container,
      style: mapStyle,
      center: defaultMapView.center,
      zoom: defaultMapView.zoom,
      pitch: defaultMapView.pitch,
      bearing: defaultMapView.bearing,
      renderWorldCopies: false,
      attributionControl: false,
      dragRotate: false,
      pitchWithRotate: false,
    });

    mapRef.current = map;
    popupRef.current = new maplibregl.Popup({ closeButton: false, closeOnClick: false, offset: 12, className: 'event-map-tooltip' });
    firmsPopupRef.current = new maplibregl.Popup({ closeButton: true, closeOnClick: true, offset: 8, className: 'firms-map-popup' });
    referencePopupRef.current = new maplibregl.Popup({ closeButton: true, closeOnClick: true, offset: 8, className: 'conflict-tension-popup' });

    const reportViewport = () => {
      const bounds = map.getBounds();
      const rawWest = bounds.getWest();
      const rawEast = bounds.getEast();
      if (rawEast - rawWest >= 360) {
        onViewportChangeRef.current({ west: -180, south: bounds.getSouth(), east: 180, north: bounds.getNorth() });
        return;
      }
      const normalizeLongitude = (value: number) => ((value + 180) % 360 + 360) % 360 - 180;
      onViewportChangeRef.current({
        west: normalizeLongitude(rawWest),
        south: bounds.getSouth(),
        east: normalizeLongitude(rawEast),
        north: bounds.getNorth(),
      });
    };


    const handleLoad = () => {
      // Reference layers sit above the basemap and below FIRMS and Event markers: tension, then conflict fill and outline.
      map.addSource(tensionSourceId, { type: 'geojson', data: tensionCountriesToFeatureCollection(conflictTensionEntries) });
      map.addLayer({ id: tensionFillLayerId, type: 'fill', source: tensionSourceId, paint: tensionFillPaint });
      map.addSource(conflictZoneSourceId, { type: 'geojson', data: conflictZonesToFeatureCollection(conflictTensionEntries) });
      map.addLayer({ id: conflictZoneFillLayerId, type: 'fill', source: conflictZoneSourceId, paint: conflictZoneFillPaint });
      map.addLayer({ id: conflictZoneOutlineLayerId, type: 'line', source: conflictZoneSourceId, paint: conflictZoneOutlinePaint });

      addExternalPointLayer(map, {
        sourceId: firmsSourceId,
        data: firmsObservationsToFeatureCollection(firmsObservationsRef.current, firmsRefreshedAtUtcRef.current),
        layer: {
          id: firmsLayerId,
          type: 'circle',
          source: firmsSourceId,
          paint: firmsCirclePaint as never,
        },
      });

      map.addSource(eventSourceId, {
        type: 'geojson',
        data: eventsToFeatureCollection(eventsRef.current),
        cluster: false,
        generateId: false,
      });
      map.addLayer({
        id: eventLayerId,
        type: 'circle',
        source: eventSourceId,
        paint: {
          'circle-color': categoryColorExpression as never,
          'circle-radius': severityRadiusExpression as never,
          'circle-opacity': 0.88,
          'circle-stroke-color': severityStrokeColorExpression as never,
          'circle-stroke-width': severityStrokeWidthExpression as never,
        },
      });
      map.addLayer({
        id: selectedEventLayerId,
        type: 'circle',
        source: eventSourceId,
        filter: ['==', ['get', 'id'], selectedEventIdRef.current],
        paint: selectedMarkerPaint(accent),
      });
      setLayerVisibility(map, [eventLayerId, selectedEventLayerId], isLayerEnabled(layers, 'events'));
      setLayerVisibility(map, [conflictZoneFillLayerId, conflictZoneOutlineLayerId], isLayerEnabled(layers, 'conflict-zones'));
      setLayerVisibility(map, [tensionFillLayerId], isLayerEnabled(layers, 'tension'));
      setLayerVisibility(map, [firmsLayerId], isLayerEnabled(layers, 'firms'));
      reportViewport();
      requestAnimationFrame(() => map.resize());
    };

    const handleClick = (event: MapLayerMouseEvent) => {
      const eventId = getMapFeatureId(event);
      if (eventId) {
        onSelectEventRef.current(eventId);
      }
    };

    const handleMouseEnter = (event: MapLayerMouseEvent) => {
      map.getCanvas().style.cursor = 'pointer';
      const feature = event.features?.[0];
      if (feature) {
        popupRef.current?.setLngLat(event.lngLat).setDOMContent(eventTooltipContent(feature.properties)).addTo(map);
      }
    };

    const handleMouseMove = (event: MapLayerMouseEvent) => {
      if (event.features?.[0]) {
        popupRef.current?.setLngLat(event.lngLat).setDOMContent(eventTooltipContent(event.features[0].properties));
      }
    };

    const handleMouseLeave = () => {
      map.getCanvas().style.cursor = '';
      popupRef.current?.remove();
    };

    const handleFirmsClick = (event: MapLayerMouseEvent) => {
      const feature = event.features?.[0];
      if (!feature) return;
      firmsPopupRef.current
        ?.setLngLat(event.lngLat)
        .setHTML(firmsPopupHtml(feature.properties ?? {}, event.lngLat.lng, event.lngLat.lat))
        .addTo(map);
    };

    // Conflict and tension popups open only when no Event marker or FIRMS point is under the pointer; hidden layers
    // return no features, so a toggled-off layer never opens a popup.
    const handleReferenceClick = (event: MapLayerMouseEvent) => {
      const renderedLayers = (ids: string[]) => ids.filter((id) => map.getLayer(id));
      if (map.queryRenderedFeatures(event.point, { layers: renderedLayers([eventLayerId, selectedEventLayerId, firmsLayerId]) }).length > 0) return;
      const hits = map.queryRenderedFeatures(event.point, { layers: renderedLayers([conflictZoneFillLayerId, tensionFillLayerId]) });
      const entryIds = entryIdsFromFeatureProperties(hits.map((hit) => hit.properties));
      const entries = entryIds
        .map((id) => conflictTensionEntries.find((entry) => entry.id === id))
        .filter((entry) => entry !== undefined);
      if (entries.length === 0) return;
      referencePopupRef.current?.setLngLat(event.lngLat).setHTML(conflictTensionPopupHtml(entries)).addTo(map);
    };

    map.on('load', handleLoad);
    map.on('click', handleReferenceClick);
    map.on('click', eventLayerId, handleClick);
    map.on('mouseenter', eventLayerId, handleMouseEnter);
    map.on('mousemove', eventLayerId, handleMouseMove);
    map.on('mouseleave', eventLayerId, handleMouseLeave);
    map.on('click', firmsLayerId, handleFirmsClick);
    map.on('moveend', reportViewport);

    const handleResize = () => map.resize();
    let resizeObserver: ResizeObserver | null = null;
    const ResizeObserverConstructor = globalThis.ResizeObserver;
    if (typeof ResizeObserverConstructor === 'function') {
      resizeObserver = new ResizeObserverConstructor(handleResize);
      resizeObserver.observe(containerRef.current);
    } else {
      globalThis.addEventListener('resize', handleResize);
    }

    return () => {
      resizeObserver?.disconnect();
      globalThis.removeEventListener('resize', handleResize);
      popupRef.current?.remove();
      popupRef.current = null;
      firmsPopupRef.current?.remove();
      firmsPopupRef.current = null;
      referencePopupRef.current?.remove();
      referencePopupRef.current = null;
      map.off('load', handleLoad);
      map.off('click', handleReferenceClick);
      map.off('click', eventLayerId, handleClick);
      map.off('mouseenter', eventLayerId, handleMouseEnter);
      map.off('mousemove', eventLayerId, handleMouseMove);
      map.off('mouseleave', eventLayerId, handleMouseLeave);
      map.off('click', firmsLayerId, handleFirmsClick);
      map.off('moveend', reportViewport);
      map.remove();
      mapRef.current = null;
    };
  }, [mapStyle]);

  useEffect(() => {
    const map = mapRef.current;
    const source = map?.getSource(eventSourceId) as GeoJSONSource | undefined;
    source?.setData(eventsToFeatureCollection(events));
  }, [events]);

  useEffect(() => {
    const map = mapRef.current;
    const source = map?.getSource(firmsSourceId) as GeoJSONSource | undefined;
    source?.setData(firmsObservationsToFeatureCollection(firmsObservations, firmsRefreshedAtUtc));
  }, [firmsObservations, firmsRefreshedAtUtc]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map?.getLayer(selectedEventLayerId)) {
      return;
    }

    map.setFilter(selectedEventLayerId, ['==', ['get', 'id'], selectedEventId]);
  }, [selectedEventId]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map?.getLayer(selectedEventLayerId)) {
      return;
    }

    const paint = selectedMarkerPaint(accent);
    Object.entries(paint).forEach(([property, value]) => {
      map.setPaintProperty(selectedEventLayerId, property as never, value as never);
    });
  }, [accent]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map) {
      return;
    }

    setLayerVisibility(map, [eventLayerId, selectedEventLayerId], isLayerEnabled(layers, 'events'));
    setLayerVisibility(map, [conflictZoneFillLayerId, conflictZoneOutlineLayerId], isLayerEnabled(layers, 'conflict-zones'));
    setLayerVisibility(map, [tensionFillLayerId], isLayerEnabled(layers, 'tension'));
    setLayerVisibility(map, [firmsLayerId], isLayerEnabled(layers, 'firms'));
    requestAnimationFrame(() => map.resize());
  }, [layers]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !locateRequest) {
      return;
    }

    const event = events.find((item) => item.id === locateRequest.eventId);
    if (!event) {
      return;
    }

    map.easeTo({
      center: getEventCoordinates(event),
      zoom: locateMapView.zoom,
      pitch: locateMapView.pitch,
      bearing: locateMapView.bearing,
      duration: 650,
      essential: true,
    });
  }, [events, locateRequest]);

  const zoomIn = () => mapRef.current?.zoomIn({ duration: 180 });
  const zoomOut = () => mapRef.current?.zoomOut({ duration: 180 });
  const resetWorldView = () => {
    mapRef.current?.easeTo({ ...defaultMapView, duration: 500, essential: true });
  };

  return (
    <div className="map-canvas-wrap">
      <div ref={containerRef} className="map-canvas" aria-label="local MapLibre world map" />
      <div className="map-control-stack" aria-label="map controls">
        <button type="button" onClick={zoomIn} aria-label="Zoom in">+</button>
        <button type="button" onClick={zoomOut} aria-label="Zoom out">-</button>
        <button type="button" onClick={resetWorldView} aria-label="Reset world view">World</button>
      </div>
    </div>
  );
}
