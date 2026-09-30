import type { FeatureCollection, Point, Polygon } from 'geojson';
import type { Accent } from '../../types/mock';
import type { NativeEvent, NativeEventCategory, NativeEventSeverity, NativeEventStatus } from '../../types/events';

export const eventSourceId = 'mock-events';
export const eventLayerId = 'mock-events-circle';
export const selectedEventLayerId = 'mock-events-selected-ring';
export const conflictZoneSourceId = 'mock-conflict-zones';
export const conflictZoneLayerId = 'mock-conflict-zones-fill';

export const defaultMapView = { center: [22, 24] as [number, number], zoom: 1.05, pitch: 0, bearing: 0 };
export const locateMapView = { zoom: 3.15, pitch: 0, bearing: 0 };

export const categoryColors: Record<NativeEventCategory, string> = {
  MilitaryConflict: '#d95d62',
  Security: '#6da3d9',
  PoliticalDiplomatic: '#caa75d',
  CivilUnrest: '#9f76d1',
  Infrastructure: '#55b5a8',
  Humanitarian: '#d9858e',
  NaturalDisaster: '#79a86b',
  Economic: '#d3b85f',
  Other: '#9aa6b5',
};

export function getCategoryColor(category: NativeEventCategory) {
  return categoryColors[category];
}

const selectionColors: Record<Accent, string> = {
  orange: '#ff8a1f',
  blue: '#22d3ff',
  red: '#ff4d5e',
  green: '#3dff87',
  yellow: '#ffe45c',
};

export function getSelectionColor(accent: Accent) {
  return selectionColors[accent];
}

export const selectedMarkerRadius = 14;

export const severityZoomStops = [0, 4, 7] as const;

export const severityMarkerRadii: Record<NativeEventSeverity, readonly [number, number, number]> = {
  Low: [3.5, 5, 6],
  Medium: [4.5, 6.5, 8],
  High: [5.5, 7.5, 9],
  Critical: [6.5, 8.5, 10],
  Catastrophic: [7.5, 9.5, 11],
};

export const severityRingSeverities: readonly NativeEventSeverity[] = ['Critical', 'Catastrophic'];
export const severityRingColor = 'rgba(226, 228, 232, 0.82)';
export const severityRingWidth = 2;
export const defaultMarkerStrokeColor = '#0b1016';
export const defaultMarkerStrokeWidth = 1.4;

function severityRadiusAtStop(stopIndex: 0 | 1 | 2) {
  return [
    'match', ['get', 'severity'],
    'Low', severityMarkerRadii.Low[stopIndex],
    'Medium', severityMarkerRadii.Medium[stopIndex],
    'High', severityMarkerRadii.High[stopIndex],
    'Critical', severityMarkerRadii.Critical[stopIndex],
    'Catastrophic', severityMarkerRadii.Catastrophic[stopIndex],
    severityMarkerRadii.Medium[stopIndex],
  ];
}

export const severityRadiusExpression = [
  'interpolate', ['linear'], ['zoom'],
  severityZoomStops[0], severityRadiusAtStop(0),
  severityZoomStops[1], severityRadiusAtStop(1),
  severityZoomStops[2], severityRadiusAtStop(2),
];

export const severityStrokeColorExpression = [
  'match', ['get', 'severity'], [...severityRingSeverities], severityRingColor, defaultMarkerStrokeColor,
];

export const severityStrokeWidthExpression = [
  'match', ['get', 'severity'], [...severityRingSeverities], severityRingWidth, defaultMarkerStrokeWidth,
];

export function selectedMarkerPaint(accent: Accent) {
  return {
    'circle-color': getSelectionColor(accent),
    'circle-radius': selectedMarkerRadius,
    'circle-opacity': 0.3,
    'circle-stroke-color': getSelectionColor(accent),
    'circle-stroke-width': 2.4,
    'circle-stroke-opacity': 0.96,
  };
}

export const categoryColorExpression = [
  'match', ['get', 'category'],
  'MilitaryConflict', categoryColors.MilitaryConflict,
  'Security', categoryColors.Security,
  'PoliticalDiplomatic', categoryColors.PoliticalDiplomatic,
  'CivilUnrest', categoryColors.CivilUnrest,
  'Infrastructure', categoryColors.Infrastructure,
  'Humanitarian', categoryColors.Humanitarian,
  'NaturalDisaster', categoryColors.NaturalDisaster,
  'Economic', categoryColors.Economic,
  'Other', categoryColors.Other,
  '#9aa6b5',
];

export type EventFeatureProperties = {
  id: string;
  category: NativeEventCategory;
  status: NativeEventStatus;
  severity: NativeEventSeverity;
  title: string;
  region: string;
  location: string;
  updatedAtUtc: string;
  sourceCount: number;
};

export function getEventCoordinates(event: NativeEvent): [number, number] {
  return [event.location.longitude, event.location.latitude];
}

export function eventsToFeatureCollection(events: NativeEvent[]): FeatureCollection<Point, EventFeatureProperties> {
  return {
    type: 'FeatureCollection',
    features: events.map((event) => ({
      type: 'Feature',
      id: event.id,
      properties: {
        id: event.id,
        category: event.category,
        status: event.status,
        severity: event.severity,
        title: event.title,
        region: event.location.region ?? '',
        location: event.location.displayName,
        updatedAtUtc: event.lastUpdatedAtUtc,
        sourceCount: event.sourceCount,
      },
      geometry: { type: 'Point', coordinates: getEventCoordinates(event) },
    })),
  };
}

export const mockConflictZones: FeatureCollection<Polygon, { id: string; label: string }> = {
  type: 'FeatureCollection',
  features: [
    { type: 'Feature', properties: { id: 'demo-zone-red-sea', label: 'Demo maritime risk area' }, geometry: { type: 'Polygon', coordinates: [[[34, 11], [47, 11], [47, 21], [34, 21], [34, 11]]] } },
    { type: 'Feature', properties: { id: 'demo-zone-east-europe', label: 'Demo border watch area' }, geometry: { type: 'Polygon', coordinates: [[[18, 53], [32, 53], [32, 61], [18, 61], [18, 53]]] } },
  ],
};
