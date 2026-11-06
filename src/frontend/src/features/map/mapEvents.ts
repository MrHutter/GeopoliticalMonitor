import type { FeatureCollection, Point } from 'geojson';
import type { Accent } from '../../types/mock';
import { formatCategory, type NativeEvent, type NativeEventCategory, type NativeEventSeverity, type NativeEventStatus } from '../../types/events';

export const eventSourceId = 'mock-events';
export const eventLayerId = 'mock-events-circle';
export const selectedEventLayerId = 'mock-events-selected-ring';

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

/** Legend order; every entry takes its color from categoryColors and its label from the Event list's formatCategory. */
export const legendCategoryOrder: readonly NativeEventCategory[] = [
  'MilitaryConflict', 'Security', 'PoliticalDiplomatic', 'CivilUnrest', 'Infrastructure', 'Humanitarian', 'Economic', 'NaturalDisaster', 'Other',
];

export const legendCategories = legendCategoryOrder.map((category) => ({
  category,
  label: formatCategory(category),
  color: categoryColors[category],
}));

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

export const selectedMarkerRadius = 11;

export const severityZoomStops = [0, 4, 7] as const;

/** Circle radius per severity at zoom 0, 4 and 7 (about 28% smaller than the original 3.5-11 px; never below 3 px). */
export const severityMarkerRadii: Record<NativeEventSeverity, readonly [number, number, number]> = {
  Low: [3, 3.6, 4.4],
  Medium: [3.6, 4.6, 5.8],
  High: [4.2, 5.4, 6.5],
  Critical: [4.8, 6.2, 7.2],
  Catastrophic: [5.4, 7, 8],
};

export const severityRingSeverities: readonly NativeEventSeverity[] = ['Critical', 'Catastrophic'];
export const severityRingColor = 'rgba(226, 228, 232, 0.82)';
export const severityRingWidth = 1.5;
export const defaultMarkerStrokeColor = '#0b1016';
export const defaultMarkerStrokeWidth = 1;

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

function tooltipLine(tagName: 'strong' | 'span', text: string) {
  const line = document.createElement(tagName);
  line.textContent = text;
  return line;
}

/** Hover tooltip for an Event marker. Event text is untrusted provider data, so it enters the DOM only as text nodes. */
export function eventTooltipContent(properties: Record<string, unknown> | null | undefined) {
  const content = document.createDocumentFragment();
  content.append(
    tooltipLine('strong', String(properties?.title ?? 'Local event')),
    tooltipLine('span', `${properties?.region ?? ''} / ${properties?.location ?? ''}`),
    tooltipLine('span', `${properties?.sourceCount ?? 0} sources`),
  );
  return content;
}
